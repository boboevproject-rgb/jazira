using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Jazira;

// «Живая» часть острова: музыка с текстом песни в такт и эквалайзером по настоящему звуку,
// индикаторы (громкость, яркость, раскладка, Caps Lock, камера и микрофон), страница «Сегодня»,
// уведомления об обновлении и подставные состояния для снимков.
public partial class IslandWindow
{
    static readonly TimeSpan LyricLead = TimeSpan.FromMilliseconds(250); // строка появляется чуть раньше — успеваешь прочитать
    static readonly double[] EqSpeed = { 9.1, 12.7, 7.3, 10.9 };
    static readonly SolidColorBrush MoonBrush = Theme.Brush(Color.FromRgb(0xFF, 0xE0, 0xA3));
    readonly double[] _eq = { 4, 4, 4, 4 };
    FrameworkElement[] _eqBars = Array.Empty<FrameworkElement>();
    int _lyricIndex = -1;
    DateTime[]? _prayers;
    string _prayerKey = "";
    DateTime _lastPrayerPeek;
    string? _fakeCam, _fakeMic; // для снимков

    string? CamApp => _sys?.CameraApp ?? _fakeCam;
    string? MicApp => _sys?.MicApp ?? _fakeMic;

    void WireLive()
    {
        _eqBars = new FrameworkElement[] { Eq1, Eq2, Eq3, Eq4 };
        _media.Changed += OnMediaChanged;
        _media.TrackChanged += OnTrackChanged;
        _power.Changed += RefreshIdle;
        _power.PlugChanged += OnPlugChanged;
        _power.Changed += CheckLowBattery;
        _focus.Finished += OnFocusFinished;
        _lyrics.Changed += OnLyricsChanged;
        _local.Changed += RefreshLocal;
        _cal.Changed += OnCalendarChanged;
        if (_audio != null) _audio.VolumeChanged += () => VolumePeek(_audio.Volume, _audio.Muted);
        if (_sys != null)
        {
            _sys.LayoutChanged += OnLayout;
            _sys.CapsChanged += OnCaps;
            _sys.PrivacyChanged += OnPrivacy;
            _sys.BrightnessChanged += v => ShowPeek("bright", Res("Amber"), SunGeo, Strings.Get("bright.title"), right: $"{v}%", seconds: 1.6, bar: v / 100.0);
        }
    }

    // ---- Музыка, текст песни, эквалайзер --------------------------------------------------------

    void OnMediaChanged()
    {
        if (!ReferenceEquals(_coverSeen, _media.Cover))
        {
            _coverSeen = _media.Cover;
            _coverColor = CoverColor.From(_media.Cover);
        }
        if (AppState.Current.Lyrics && HasTrack) _lyrics.Load(_media.Title, _media.Artist, _media.Duration);
        else if (!HasTrack) _lyrics.Clear();
        _lyricIndex = _lyrics.IndexAt(_media.Position + LyricLead);
        RefreshMusic();
        RefreshIdle();
        ApplyTint();
        if (_mode == Mode.Expanded && PageName == "music") AnimateTo(PageSize()); // плеер включили или закрыли
        else if (_mode == Mode.Idle) AnimateTo(IdleSize(), gentle: true);
    }

    void OnLyricsChanged()
    {
        _lyricIndex = _lyrics.IndexAt(_media.Position + LyricLead);
        RefreshMusic();
        RefreshIdle();
        if (_mode == Mode.Expanded && PageName == "music") AnimateTo(PageSize());
        else if (_mode == Mode.Idle) AnimateTo(IdleSize(), gentle: true);
    }

    string CurrentLyric() =>
        _lyrics.Lines is { } l && _lyricIndex >= 0 && _lyricIndex < l.Length ? l[_lyricIndex].Text : "";

    void RefreshLyricTexts()
    {
        var l = _lyrics.Lines;
        var now = CurrentLyric();
        int next = _lyricIndex + 1;
        LyricNow.Text = now.Length > 0 ? now : "♪";
        LyricNext.Text = l != null && next >= 0 && next < l.Length ? l[next].Text : "";
    }

    // 30 раз в секунду, только пока играет музыка и остров не спрятан: эквалайзер и смена строк текста
    void UpdateFast()
    {
        bool on = !_instant && _media.IsPlaying && !_tucked;
        if (on != _fast.IsEnabled) _fast.IsEnabled = on;
    }

    void OnFast()
    {
        if (!IsVisible) return;
        if (_mode == Mode.Idle && IdleEq.Visibility == Visibility.Visible) UpdateEq();
        if (_lyrics.Lines != null)
        {
            int i = _lyrics.IndexAt(_media.Position + LyricLead);
            if (i != _lyricIndex) { _lyricIndex = i; OnLyricLine(); }
        }
    }

    void OnLyricLine()
    {
        RefreshLyricTexts();
        if (_mode == Mode.Expanded && PageName == "music") Fade(LyricBlock);
        if (_mode != Mode.Idle) return;
        RefreshIdle();
        AnimateTo(IdleSize(), gentle: true);
        if (IdleLyric.Visibility == Visibility.Visible) Fade(IdleLyric);
    }

    void Fade(UIElement el)
    {
        if (!_instant) el.BeginAnimation(OpacityProperty, new DoubleAnimation(0.15, 1, TimeSpan.FromMilliseconds(280)));
    }

    // Столбики прыгают по уровню звука, который сейчас идёт в динамики; если его не узнать — просто «танцуют»
    void UpdateEq()
    {
        float peak = _audio?.Peak() ?? -1;
        double t = _clock.Elapsed.TotalSeconds;
        for (int i = 0; i < _eqBars.Length; i++)
        {
            double wobble = 0.5 + 0.5 * Math.Sin(t * EqSpeed[i] + i * 1.7);
            double target = peak < 0 ? 4 + 8 * wobble
                          : 3 + 13 * Math.Pow(Math.Min(1, peak * 1.4), 0.7) * (0.45 + 0.55 * wobble);
            _eq[i] += (target - _eq[i]) * (target > _eq[i] ? 0.55 : 0.2); // вверх быстро, вниз плавно
            _eqBars[i].Height = _eq[i];
        }
    }

    // ---- Индикаторы -----------------------------------------------------------------------------

    void VolumePeek(float volume, bool muted)
    {
        if (!AppState.Current.Indicators) return;
        if (muted) ShowPeek("volume", Res("Sky"), MuteGeo, Strings.Get("vol.muted"), seconds: 1.6);
        else ShowPeek("volume", Res("Sky"), SpeakerGeo, Strings.Get("vol.title"), right: $"{(int)Math.Round(volume * 100)}%",
                      seconds: 1.6, bar: volume);
    }

    void OnLayout(CultureInfo c)
    {
        if (c.Equals(CultureInfo.InvariantCulture)) return;
        var name = c.NativeName;
        int bracket = name.IndexOf(" (", StringComparison.Ordinal);
        if (bracket > 0) name = name[..bracket];
        if (name.Length > 0) name = char.ToUpper(name[0], c) + name[1..];
        ShowPeek("layout", Res("Lagoon"), KeyboardGeo, name, Strings.Get("layout.sub"), c.TwoLetterISOLanguageName.ToUpperInvariant(), seconds: 1.4);
    }

    void OnCaps(bool on) =>
        ShowPeek("caps", on ? Res("Coral") : Res("Muted"), CapsGeo, Strings.Get(on ? "caps.on" : "caps.off"), seconds: 1.4);

    // Камера или микрофон заняты: зелёная или оранжевая точка в острове, пока они работают
    void OnPrivacy(string? camStarted, string? micStarted)
    {
        RefreshIdle();
        if (_mode == Mode.Idle) AnimateTo(IdleSize(), gentle: true);
        if (camStarted != null) ShowPeek("cam", Res("Green"), CameraGeo, Strings.Get("cam.on"), camStarted, seconds: 3.5);
        else if (micStarted != null) ShowPeek("mic", Res("Amber"), MicGeo, Strings.Get("mic.on"), micStarted, seconds: 3.5);
    }

    // ---- Сегодня: погода, курс, намаз ------------------------------------------------------------

    void RefreshLocal()
    {
        var st = AppState.Current;
        var city = LocalInfo.CityOf(st.City);
        WxCity.Text = city.Name;
        if (_local.Temp is double temp && _local.WeatherCity == city.Id)
        {
            int t = (int)Math.Round(temp);
            WxTemp.Text = (t > 0 ? "+" : "") + t.ToString(CultureInfo.InvariantCulture) + "°";
            WxText.Text = Strings.Get(LocalInfo.WeatherKey(_local.Code));
            var (icon, fill) = WeatherIcon(_local.Code, _local.IsDay);
            WxIcon.Data = icon;
            WxIcon.Fill = fill;
        }
        else
        {
            WxTemp.Text = "—";
            WxText.Text = Strings.Get("local.loading");
            WxIcon.Data = CloudGeo;
            WxIcon.Fill = Res("Dim");
        }

        var tjs = Strings.Get("tjs");
        RateUsd.Text = _local.Usd is decimal usd ? $"$1 = {usd.ToString("0.00", Strings.Culture)} {tjs}" : "$1 = —";
        RateRub.Text = _local.Rub is decimal rub ? $"1000 ₽ = {(rub * 1000).ToString("0.00", Strings.Culture)}" : "1000 ₽ = —";
        RateNote.Text = _local.Usd != null ? $"{Strings.Get("rate.note")} · {_local.RatesDate:dd.MM}" : Strings.Get("local.loading");

        bool prayer = st.Prayer;
        PrayerBlock.Visibility = prayer ? Visibility.Visible : Visibility.Collapsed;
        PrayerCol.Width = prayer ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        if (!prayer) return;
        var (i, at) = NextPrayer();
        PrayerLabel.Text = Strings.Get("prayer.next");
        PrayerNext.Text = $"{Strings.Get("prayer." + PrayerTimes.Keys[i])} {at.ToString("HH:mm", CultureInfo.InvariantCulture)}";
        PrayerIn.Text = Strings.Format("in", Duration(at - DateTime.Now));
    }

    (Geometry Icon, Brush Fill) WeatherIcon(int code, bool day) => code switch
    {
        0 or 1 => day ? (SunGeo, Res("SunGradient")) : (MoonGeo, MoonBrush),
        2 or 3 => (CloudGeo, Res("Dim")),
        45 or 48 => (FogGeo, Res("Dim")),
        (>= 51 and <= 67) or (>= 80 and <= 82) => (RainGeo, Res("Sky")),
        (>= 71 and <= 77) or 85 or 86 => (SnowGeo, Res("Fg")),
        >= 95 => (StormGeo, Res("Amber")),
        _ => (CloudGeo, Res("Dim")),
    };

    static string Duration(TimeSpan t)
    {
        int m = Math.Max(0, (int)Math.Ceiling(t.TotalMinutes));
        return m >= 60 ? $"{m / 60} {Strings.Get("h")} {m % 60} {Strings.Get("min")}" : $"{m} {Strings.Get("min")}";
    }

    DateTime[] TodayPrayers()
    {
        var city = LocalInfo.CityOf(AppState.Current.City);
        var key = AppState.Today + city.Id;
        if (_prayers == null || key != _prayerKey)
        {
            _prayers = PrayerTimes.ForDate(DateTime.Today, city.Lat, city.Lon).Select(x => x.ToLocalTime()).ToArray();
            _prayerKey = key;
        }
        return _prayers;
    }

    // Ближайший намаз (восход — не намаз, пропускаем); после хуфтана — завтрашний бомдод
    (int Index, DateTime At) NextPrayer()
    {
        var now = DateTime.Now;
        var t = TodayPrayers();
        for (int i = 0; i < t.Length; i++)
            if (i != 1 && t[i] > now) return (i, t[i]);
        var city = LocalInfo.CityOf(AppState.Current.City);
        return (0, PrayerTimes.ForDate(DateTime.Today.AddDays(1), city.Lat, city.Lon)[0].ToLocalTime());
    }

    void CheckPrayer(DateTime now)
    {
        if (!AppState.Current.Prayer) return;
        var t = TodayPrayers();
        for (int i = 0; i < t.Length; i++)
        {
            if (i == 1 || now < t[i] || now - t[i] >= TimeSpan.FromMinutes(1) || _lastPrayerPeek == t[i]) continue;
            _lastPrayerPeek = t[i];
            PrayerPeek(i, t[i]);
        }
    }

    void PrayerPeek(int i, DateTime at) =>
        ShowPeek("prayer", Res("Violet"), MoonGeo, Strings.Get("prayer.now"),
                 right: $"{Strings.Get("prayer." + PrayerTimes.Keys[i])} {at.ToString("HH:mm", CultureInfo.InvariantCulture)}", seconds: 10);

    // ---- Обновления -------------------------------------------------------------------------------

    public void ShowUpdateReady(Version v, Action apply) =>
        ShowPeek("update", Res("Lagoon"), UpdateGeo, Strings.Format("update.ready", v), seconds: 12,
                 action: Strings.Get("update.btn"), onAction: apply);

    public void ShowInfo(string text) => ShowPeek("info", Res("Lagoon"), UpdateGeo, text, seconds: 3.5);

    // ---- Снимки всех состояний (Jazira.exe --snapshot) ------------------------------------------

    internal IEnumerable<(string Name, Action Setup)> SnapshotStates(BitmapSource cover, string[]? langs = null)
    {
        var st = AppState.Current;
        var lines = new LyricLine[]
        {
            new(TimeSpan.FromSeconds(60), "Lanterns drift across the bay"),
            new(TimeSpan.FromSeconds(76), "Morning tide will find our way"),
            new(TimeSpan.FromSeconds(92), "Hold the light and let it stay"),
        };

        void Base(string lang)
        {
            Reveal(); // прошлый снимок мог быть «спрятанным»
            Strings.Lang = lang;
            st.Theme = "night"; st.Shape = "pill"; st.Docked = true; st.CoverTint = true;
            st.Koi = true; st.Lyrics = true; st.Indicators = true; st.LocalPage = true; st.Prayer = false;
            st.City = "dushanbe"; st.WaterGoalMl = 2000;
            st.Shelf = new List<string>();
            st.DayPage = true; st.Tasks = new List<TaskItem>(); st.Timers = new List<TimerItem>();
            st.CalendarUrl = null; st.Ramadan = true; _fakeRamadan = false; _cal.Fake(Array.Empty<CalEvent>());
            TaskInput.Clear(); TaskInputRow.Visibility = Visibility.Collapsed;
            st.LastDrink = DateTime.Now.AddMinutes(-30);
            st.WaterLog[AppState.Today] = 750; // без сохранения на диск
            FakePet(9, 4, false, 2);
            _fakeCam = _fakeMic = null;
            _dropHover = false;
            _lyrics.Fake(null);
            _lyricIndex = -1;
            _media.FakeNone();
            _power.Fake(true, false, 78);
            _focus.Reset();
            ApplyLook();
        }
        void Track(bool playing)
        {
            _media.Fake("Morning Tide", "Sea Lanterns", "Spotify", cover, playing, TimeSpan.FromSeconds(84), TimeSpan.FromSeconds(212));
            _coverSeen = cover;
            _coverColor = CoverColor.From(cover);
            ApplyTint(force: true);
        }
        void Lyrics() { _lyrics.Fake(lines); _lyricIndex = 1; }
        void Water(int ml, double hoursAgo) { st.WaterLog[AppState.Today] = ml; st.LastDrink = DateTime.Now.AddHours(-hoursAgo); }
        void Pet(int age, int streak, bool today, int gap = 2) => FakePet(age, streak, today, gap);
        // Подставные тексты на языке снимка: ru, en (для ролика на сайте), tg
        static string L(string lang, string ru, string en, string tg) => lang == "tg" ? tg : lang == "en" ? en : ru;
        List<string> ShelfFiles(string lang) => new()
        {
            L(lang, @"C:\Docs\Отчёт за октябрь.pdf", @"C:\Docs\October report.pdf", @"C:\Docs\Ҳисоботи октябр.pdf"),
            @"C:\Docs\photo-dushanbe.jpg",
            L(lang, @"C:\Docs\Презентация.pptx", @"C:\Docs\Pitch deck.pptx", @"C:\Docs\Презентатсия.pptx"),
            @"C:\Docs\budget.xlsx",
        };
        void Tasks(string lang, int extra = 0)
        {
            var now = DateTime.Now;
            st.Tasks = new List<TaskItem>
            {
                new() { Text = L(lang, "Позвонить маме", "Call mom", "Ба модар занг задан"), Due = now.Date.AddHours(now.Hour + 2).AddMinutes(30) },
                new() { Text = L(lang, "Оплатить интернет", "Pay the internet bill", "Пардохти интернет"), Due = now.AddMinutes(-25), Reminded = true },
                new() { Text = L(lang, "Купить хлеб", "Buy bread", "Нон харидан") },
                new() { Text = L(lang, "Встреча в банке", "Meeting at the bank", "Вохӯрӣ дар бонк"), Due = now.Date.AddDays(1).AddHours(10) },
                new() { Text = L(lang, "Отправить отчёт", "Send the report", "Ҳисобот фиристодан"), Done = true, DoneAt = now.AddHours(-1) },
            };
            for (int i = 0; i < extra; i++) st.Tasks.Add(new() { Text = L(lang, "Задача ", "Task ", "Вазифа ") + (i + 1) });
        }
        void Calendar(string lang, bool soon = true)
        {
            st.CalendarUrl = "https://example.com/fake.ics"; // только в памяти: снимки не сохраняют настройки
            var now = DateTime.Now;
            var start = soon ? now.AddMinutes(12) : now.AddMinutes(-20);
            _cal.Fake(new[] { new CalEvent("s", L(lang, "Созвон с командой", "Team call", "Мулоқот бо даста"), start, start.AddMinutes(45), false, "https://meet.google.com/abc-defg-hij") });
        }
        void Timers()
        {
            var now = DateTime.Now;
            st.Timers = new List<TimerItem>
            {
                new() { Minutes = 10, Ends = now.AddSeconds(4 * 60 + 12) },
                new() { Minutes = 30, Ends = now.AddMinutes(27) },
                new() { Minutes = 5, Ends = now.AddSeconds(-20), Done = true },
            };
        }

        foreach (var lang in langs ?? new[] { "ru", "tg" })
        {
            yield return ($"{lang}-idle", () => { Base(lang); Collapse(); });
            yield return ($"{lang}-idle-thirsty", () => { Base(lang); Water(250, 3.5); Collapse(); });
            yield return ($"{lang}-idle-busy", () => { Base(lang); Track(true); _power.Fake(true, true, 64); _focus.Preview(TimeSpan.FromSeconds(24 * 60 + 12), true); Collapse(); });
            yield return ($"{lang}-idle-lyrics", () => { Base(lang); Track(true); Lyrics(); Collapse(); });
            yield return ($"{lang}-idle-privacy", () => { Base(lang); _fakeCam = "Zoom"; _fakeMic = "Zoom"; Collapse(); });
            yield return ($"{lang}-music", () => { Base(lang); Track(true); Expand("music"); });
            yield return ($"{lang}-music-lyrics", () => { Base(lang); Track(true); Lyrics(); Expand("music"); });
            yield return ($"{lang}-music-empty", () => { Base(lang); Expand("music"); });
            yield return ($"{lang}-day", () => { Base(lang); Tasks(lang); Expand("day"); });
            yield return ($"{lang}-day-empty", () => { Base(lang); Expand("day"); });
            yield return ($"{lang}-day-calendar", () => { Base(lang); Tasks(lang); Calendar(lang); Expand("day"); });
            yield return ($"{lang}-day-calendar-now", () => { Base(lang); Calendar(lang, soon: false); Expand("day"); });
            yield return ($"{lang}-day-ramadan", () => { Base(lang); _fakeRamadan = true; Tasks(lang); Calendar(lang); Expand("day"); });
            yield return ($"{lang}-peek-event", () => { Base(lang); Calendar(lang); Collapse(); Alert("event", Res("Sky"), CalendarGeo, lang == "tg" ? "Мулоқот бо даста" : "Созвон с командой", $"{Strings.Format("in", Duration(TimeSpan.FromMinutes(10)))} · 15:00", 20, Strings.Get("cal.join"), () => { }); });
            yield return ($"{lang}-peek-iftar", () => { Base(lang); Collapse(); Alert("ramadan", Res("Violet"), MoonGeo, Strings.Get("ramadan.iftarNow"), Strings.Format("ramadan.iftar", "17:12"), 30); });
            yield return ($"{lang}-day-many", () => { Base(lang); Tasks(lang, 3); Expand("day"); });
            yield return ($"{lang}-day-typing", () => { Base(lang); Tasks(lang); TaskInputRow.Visibility = Visibility.Visible; TaskInput.Text = lang == "tg" ? "чой баъди 20 дақ" : "чай через 20 мин"; Expand("day"); });
            yield return ($"{lang}-day-typing-empty", () => { Base(lang); TaskInputRow.Visibility = Visibility.Visible; Expand("day"); });
            yield return ($"{lang}-focus-timers", () => { Base(lang); Timers(); Expand("focus"); });
            yield return ($"{lang}-idle-timer", () => { Base(lang); Timers(); Collapse(); });
            yield return ($"{lang}-peek-task", () => { Base(lang); Collapse(); Alert("task", Res("Violet"), TaskGeo, lang == "tg" ? "Ба модар занг задан" : "Позвонить маме", Strings.Format("task.remind", "18:30"), 15, Strings.Get("task.done"), () => { }); });
            yield return ($"{lang}-peek-timer", () => { Base(lang); Collapse(); Alert("timer", Res("Amber"), WatchGeo, Strings.Format("timer.done", Strings.Format("timer.min", 10)), "", 10); });
            yield return ($"{lang}-shelf", () => { Base(lang); st.Shelf = ShelfFiles(lang); Expand("shelf"); });
            yield return ($"{lang}-shelf-empty", () => { Base(lang); Expand("shelf"); });
            yield return ($"{lang}-shelf-drop", () => { Base(lang); _dropHover = true; Expand("shelf"); });
            yield return ($"{lang}-peek-download", () => { Base(lang); Collapse(); DownloadPeek(L(lang, "Договор аренды.pdf", "Lease agreement.pdf", "Шартнома.pdf"), 2_457_600, 1, null); });
            yield return ($"{lang}-peek-download-long", () => { Base(lang); Collapse(); DownloadPeek("Отчёт_по_проекту_Jazira_ноябрь_2026_финальная_версия.docx", 734_003, 1, null); });
            yield return ($"{lang}-peek-download-exe", () => { Base(lang); Collapse(); DownloadPeek("JaziraSetup-0.3.0.exe", 61_865_984, 1, null); });
            yield return ($"{lang}-peek-downloads", () => { Base(lang); Collapse(); DownloadPeek("a.jpg", 1, 3, null); });
            yield return ($"{lang}-peek-shot", () => { Base(lang); Collapse(); ShotPeek(@"C:\x.png", Strings.Format("shot.copied", "2880×1800")); });
            yield return ($"{lang}-focus", () => { Base(lang); _focus.Preview(TimeSpan.FromSeconds(24 * 60 + 12), true); Expand("focus"); });
            yield return ($"{lang}-water", () => { Base(lang); Water(1250, 0.3); Expand("water"); });
            yield return ($"{lang}-water-thirsty", () => { Base(lang); Water(500, 3.5); Expand("water"); });
            yield return ($"{lang}-water-fry", () => { Base(lang); Pet(0, 0, false); Water(1250, 0.3); Expand("water"); });
            yield return ($"{lang}-water-sad", () => { Base(lang); Pet(6, 0, false, 3); Water(250, 5); Expand("water"); });
            yield return ($"{lang}-water-sanke-new", () => { Base(lang); Pet(5, 3, true); Water(2000, 0.1); Expand("water"); });
            yield return ($"{lang}-water-two", () => { Base(lang); Pet(12, 8, false); Water(1500, 0.3); Expand("water"); });
            yield return ($"{lang}-water-gold", () => { Base(lang); Pet(30, 16, true); Water(2250, 0.2); Expand("water"); });
            yield return ($"{lang}-idle-two", () => { Base(lang); Pet(12, 8, false); Collapse(); });
            yield return ($"{lang}-idle-gold", () => { Base(lang); Pet(30, 16, true); Water(2250, 0.2); Collapse(); });
            yield return ($"{lang}-peek-koi-new", () => { Base(lang); Pet(5, 3, true); Water(2000, 0.1); Collapse();
                ShowPeek("koi", Res("Amber"), FishGeo, Strings.Get("koi.new.sanke"), KoiLineText(st), seconds: 5); });
            yield return ($"{lang}-peek-koi-saved", () => { Base(lang); Collapse();
                ShowPeek("koi", Res("Coral"), FishGeo, Strings.Get("koi.saved"), "koi-2026-11-04.png", seconds: 8, action: Strings.Get("koi.open"), onAction: () => { }); });
            yield return ($"{lang}-sys", () => { Base(lang); _sysmon.Fake(0.23, 0.61, 16UL << 30, 1_258_291, 40_960, null, false, new BatteryHealth.Info(50_000, 42_367, 0)); Expand("sys"); });
            yield return ($"{lang}-sys-hot", () => { Base(lang); _sysmon.Fake(0.96, 0.91, 8UL << 30, 0, 0, 87.4, true, new BatteryHealth.Info(57_000, 31_200, 612)); Expand("sys"); });
            yield return ($"{lang}-sys-desktop", () => { Base(lang); _power.Fake(false, true, 100); _sysmon.Fake(0.07, 0.33, 32UL << 30, null, null, 41.2, false, null); Expand("sys"); });
            yield return ($"{lang}-peek-battery", () => { Base(lang); Collapse(); _power.Fake(true, false, 10); _lowWarned = false; CheckLowBattery(); });
            yield return ($"{lang}-local", () => { Base(lang); st.Prayer = true; _local.Fake(16.4, 1, 9.2122m, 0.11372m); Expand("local"); });
            yield return ($"{lang}-peek-charge", () => { Base(lang); Collapse(); _power.Fake(true, true, 78); OnPlugChanged(true); });
            yield return ($"{lang}-peek-track", () => { Base(lang); Collapse(); Track(true); ShowTrackPeek(); });
            yield return ($"{lang}-peek-water", () => { Base(lang); Collapse(); ShowWaterPeek(); });
            yield return ($"{lang}-peek-focus", () => { Base(lang); Collapse(); OnFocusFinishedPreview(); });
            yield return ($"{lang}-peek-volume", () => { Base(lang); Collapse(); VolumePeek(0.64f, false); });
            yield return ($"{lang}-peek-layout", () => { Base(lang); Collapse(); OnLayout(CultureInfo.GetCultureInfo(lang == "tg" ? "tg-Cyrl-TJ" : "ru-RU")); });
            yield return ($"{lang}-peek-camera", () => { Base(lang); Collapse(); OnPrivacy("Zoom", null); });
            yield return ($"{lang}-peek-prayer", () => { Base(lang); Collapse(); PrayerPeek(3, DateTime.Today.AddHours(15).AddMinutes(12)); });
            yield return ($"{lang}-peek-update", () => { Base(lang); Collapse(); ShowUpdateReady(new Version(0, 3, 0), () => { }); });
            yield return ($"{lang}-peek-autohide", () => { Base(lang); Collapse();
                ShowPeek("info", Res("Lagoon"), HideGeo, Strings.Get("autohide.on"), Strings.Get("autohide.hintTop"), seconds: 4); });
        }

        foreach (var theme in Theme.All.Skip(1))
        {
            yield return ($"ru-theme-{theme.Id}-idle", () => { Base("ru"); st.Theme = theme.Id; ApplyLook(); Collapse(); });
            yield return ($"ru-theme-{theme.Id}-music", () => { Base("ru"); st.Theme = theme.Id; ApplyLook(); Track(true); Lyrics(); Expand("music"); });
            yield return ($"ru-theme-{theme.Id}-water", () => { Base("ru"); st.Theme = theme.Id; ApplyLook(); Water(1250, 0.3); Expand("water"); });
        }
        yield return ("ru-notch-idle", () => { Base("ru"); st.Shape = "notch"; ApplyLook(); Collapse(); });
        yield return ("ru-notch-music", () => { Base("ru"); st.Shape = "notch"; ApplyLook(); Track(true); Lyrics(); Expand("music"); });
        yield return ("ru-notch-peek", () => { Base("ru"); st.Shape = "notch"; ApplyLook(); Collapse(); VolumePeek(0.4f, false); });
        // Автоскрытие: на месте острова — только полоска
        yield return ("ru-hidden", () => { Base("ru"); Collapse(); Tuck(); });
        yield return ("ru-notch-hidden", () => { Base("ru"); st.Shape = "notch"; ApplyLook(); Collapse(); Tuck(); });
        yield return ("ru-float-hidden", () => { Base("ru"); st.Docked = false; ApplyLook(); Collapse(); Tuck(); });
        yield return ("ru-theme-light-hidden", () => { Base("ru"); st.Theme = "light"; ApplyLook(); Collapse(); Tuck(); });
    }

    // Снимки: подставить историю питомца — age дней с целью, из них последние streak подряд
    // (по сегодня, если today, иначе по вчера); остальные — раньше, через gap дней пропуска
    void FakePet(int age, int streak, bool today, int gap)
    {
        var days = new List<string>();
        var d = today ? DateTime.Today : DateTime.Today.AddDays(-1);
        if (streak == 0) d = DateTime.Today.AddDays(-gap);
        for (int i = 0; i < age; i++)
        {
            if (i == streak && streak > 0) d = d.AddDays(-gap);
            days.Add(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            d = d.AddDays(-1);
        }
        AppState.Current.KoiDays = days;
    }

    // Картинки для сторис: малёк, санкэ в серии, золотая с подругой
    internal IEnumerable<(string Name, Action Setup)> KoiCardStates()
    {
        var st = AppState.Current;
        void Set(string lang, int age, int streak, int ml)
        {
            Strings.Lang = lang;
            FakePet(age, streak, ml >= st.WaterGoalMl, 2);
            st.WaterLog[AppState.Today] = ml;
        }
        yield return ("card-ru-fry", () => Set("ru", 0, 0, 500));
        yield return ("card-ru-sanke", () => Set("ru", 5, 4, 2000));
        yield return ("card-ru-gold", () => Set("ru", 22, 14, 2000));
        yield return ("card-en-two", () => Set("en", 11, 9, 2000));
        yield return ("card-tg-gold", () => Set("tg", 31, 16, 2000));
    }

    void OnFocusFinishedPreview() =>
        ShowPeek("focus", Res("Lagoon"), CupGeo, Strings.Get("focus.done"), seconds: 6);
}
