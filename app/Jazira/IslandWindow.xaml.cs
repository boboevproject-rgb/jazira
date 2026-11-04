using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Jazira;

// Остров у верхнего края экрана (или там, куда его перетащили). В покое — время, значки или строка песни;
// навёл курсор — раскрывается в страницы «Музыка / Полка / Фокус / Вода / Сегодня»;
// события (зарядка, песня, громкость, раскладка, камера, вода…) — короткий «пик».
// Остальное — в соседних файлах: IslandWindow.Place.cs (место, вид, перетаскивание),
// IslandWindow.Shelf.cs (полка), IslandWindow.Live.cs (тексты, индикаторы, «Сегодня», снимки).
public partial class IslandWindow : Window
{
    enum Mode { Idle, Expanded, Peek }
    static readonly string[] AllPages = { "music", "day", "shelf", "focus", "water", "local" };
    static readonly Size IdleBase = new(196, 34);

    static Geometry G(string d) { var g = Geometry.Parse(d); g.Freeze(); return g; }
    static readonly Geometry PlayGeo = G("M8 5v14l11-7z");
    static readonly Geometry PauseGeo = G("M7 5h3.6v14H7z M13.4 5H17v14h-3.4z");
    static readonly Geometry PrevGeo = G("M6 5h2.4v14H6z M19 5v14L9 12z");
    static readonly Geometry NextGeo = G("M15.6 5H18v14h-2.4z M5 5v14l10-7z");
    static readonly Geometry NoteGeo = G("M9 4.5 19 3v12.2a2.8 2.8 0 1 1-1.6-2.5V6.3l-6.8 1v9.9a2.8 2.8 0 1 1-1.6-2.5z");
    static readonly Geometry BoltGeo = G("M13 2 5 13h6l-1 9 8-11h-6z");
    static readonly Geometry DropGeo = G("M12 3c3.6 4.3 6 7.7 6 10.7a6 6 0 0 1-12 0C6 10.7 8.4 7.3 12 3z");
    static readonly Geometry WatchGeo = G("M10 2h4v2h-4z M12 5a8 8 0 1 0 0 16 8 8 0 0 0 0-16z");
    static readonly Geometry CupGeo = G("M4 8h12v6a4 4 0 0 1-4 4H8a4 4 0 0 1-4-4z M16 9.5h2a2.5 2.5 0 0 1 0 5h-2v-1.6h2a.9.9 0 0 0 0-1.8h-2z");
    static readonly Geometry ResetGeo = G("M4.5 12a7.5 7.5 0 1 0 2.2-5.3 M4.5 4.5v4h4");
    static readonly Geometry LeftGeo = G("M7 1 1 7l6 6");
    static readonly Geometry RightGeo = G("M1 1l6 6-6 6");
    static readonly Geometry CrossGeo = G("M1 1 9 9 M9 1 1 9");
    static readonly Geometry SunGeo = G("M7.8 12a4.2 4.2 0 1 0 8.4 0a4.2 4.2 0 1 0 -8.4 0z M19.3 12a1.3 1.3 0 1 0 2.6 0a1.3 1.3 0 1 0 -2.6 0z M16.78 18.08a1.3 1.3 0 1 0 2.6 0a1.3 1.3 0 1 0 -2.6 0z M10.7 20.6a1.3 1.3 0 1 0 2.6 0a1.3 1.3 0 1 0 -2.6 0z M4.62 18.08a1.3 1.3 0 1 0 2.6 0a1.3 1.3 0 1 0 -2.6 0z M2.1 12a1.3 1.3 0 1 0 2.6 0a1.3 1.3 0 1 0 -2.6 0z M4.62 5.92a1.3 1.3 0 1 0 2.6 0a1.3 1.3 0 1 0 -2.6 0z M10.7 3.4a1.3 1.3 0 1 0 2.6 0a1.3 1.3 0 1 0 -2.6 0z M16.78 5.92a1.3 1.3 0 1 0 2.6 0a1.3 1.3 0 1 0 -2.6 0z");
    static readonly Geometry MoonGeo = G("M14 3.2A9 9 0 1 0 20.8 14A7.2 7.2 0 0 1 14 3.2z");
    static readonly Geometry CloudGeo = G("M7 19a5 5 0 0 1-.9-9.92A6.5 6.5 0 0 1 18.6 8.3 4.6 4.6 0 0 1 18 19z");
    const string SmallCloud = "M7 14a4.2 4.2 0 0 1-.7-8.35A5.6 5.6 0 0 1 17.4 5 3.9 3.9 0 0 1 17 14z";
    static readonly Geometry RainGeo = G(SmallCloud + " M8 16.5h1.6l-1.2 4H6.8z M12.2 16.5h1.6l-1.2 4H11z M16.4 16.5H18l-1.2 4h-1.6z");
    static readonly Geometry SnowGeo = G(SmallCloud + " M7.6 18.5a1.1 1.1 0 1 0 2.2 0a1.1 1.1 0 1 0 -2.2 0z M11.8 20.2a1.1 1.1 0 1 0 2.2 0a1.1 1.1 0 1 0 -2.2 0z M16 18.5a1.1 1.1 0 1 0 2.2 0a1.1 1.1 0 1 0 -2.2 0z");
    static readonly Geometry StormGeo = G(SmallCloud + " M12.6 15l-3.2 4.6h2.6l-1 4.2 4.2-6h-2.7l1.5-2.8z");
    static readonly Geometry FogGeo = G("M3 8h18v2H3z M5 12h16v2H5z M3 16h14v2H3z");
    static readonly Geometry SpeakerGeo = G("M3 9h4l5-4.5v15L7 15H3z M15.5 8.6a4.8 4.8 0 0 1 0 6.8l-1.3-1.3a3 3 0 0 0 0-4.2z M18.3 5.8a8.8 8.8 0 0 1 0 12.4L17 16.9a7 7 0 0 0 0-9.8z");
    static readonly Geometry MuteGeo = G("M3 9h4l5-4.5v15L7 15H3z M15 9.6l1.2-1.2 2.4 2.4 2.4-2.4 1.2 1.2-2.4 2.4 2.4 2.4-1.2 1.2-2.4-2.4-2.4 2.4-1.2-1.2 2.4-2.4z");
    static readonly Geometry KeyboardGeo = G("M3 6h18a1.5 1.5 0 0 1 1.5 1.5v9A1.5 1.5 0 0 1 21 18H3a1.5 1.5 0 0 1-1.5-1.5v-9A1.5 1.5 0 0 1 3 6z M5 9h2v2H5z M8.5 9h2v2h-2z M12 9h2v2h-2z M15.5 9h2v2h-2z M7 13.5h10v1.8H7z");
    static readonly Geometry CapsGeo = G("M12 3l8 8.5h-4.6V16H8.6v-4.5H4z M8 18h8v2.2H8z");
    static readonly Geometry CameraGeo = G("M3.5 6.5h11a1.5 1.5 0 0 1 1.5 1.5v8a1.5 1.5 0 0 1-1.5 1.5h-11A1.5 1.5 0 0 1 2 16V8a1.5 1.5 0 0 1 1.5-1.5z M17 10.2l5-3v9.6l-5-3z");
    static readonly Geometry MicGeo = G("M12 2.5a3.2 3.2 0 0 1 3.2 3.2v5.6a3.2 3.2 0 0 1-6.4 0V5.7A3.2 3.2 0 0 1 12 2.5z M5.5 11h1.8a4.7 4.7 0 0 0 9.4 0h1.8a6.5 6.5 0 0 1-5.6 6.4V21h-1.8v-3.6A6.5 6.5 0 0 1 5.5 11z");
    static readonly Geometry FishGeo = G("M2.5 12C5.4 7.6 10.4 6.1 14.4 8.1L20.5 4.6v14.8l-6.1-3.5C10.4 17.9 5.4 16.4 2.5 12z M6.6 10.9a1.1 1.1 0 1 0 0 2.2a1.1 1.1 0 1 0 0-2.2z");
    static readonly Geometry ShareGeo = G("M12 2.5l5 5-1.5 1.5-2.4-2.4V15h-2.2V6.6L8.5 9 7 7.5z M4.5 12.5h2.2v6.3h10.6v-6.3h2.2V21h-15z");
    static readonly Geometry UpdateGeo = G("M11 3h2v9.2l3.3-3.3 1.4 1.4L12 16l-5.7-5.7 1.4-1.4 3.3 3.3z M4 18.5h16v2H4z");

    readonly MediaService _media;
    readonly PowerService _power;
    readonly FocusTimer _focus = new();
    readonly LyricsService _lyrics = new();
    readonly LocalInfo _local = new();
    readonly AudioWatch? _audio;
    readonly SystemWatch? _sys;
    readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly DispatcherTimer _fast = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    readonly DispatcherTimer _hoverIn = new() { Interval = TimeSpan.FromMilliseconds(70) };
    readonly DispatcherTimer _hoverOut = new() { Interval = TimeSpan.FromMilliseconds(380) };
    readonly DispatcherTimer _peekEnd = new();
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly DateTime _startedAt = DateTime.Now;
    readonly bool _instant; // снимки: без анимаций
    Mode _mode = Mode.Idle;
    int _page;
    int _lastMinute = -1;
    DateTime _lastWaterReminder = DateTime.Now; // первое напоминание — не раньше чем через час после запуска
    DateTime _lastFullscreenCheck;
    Action? _peekAction;
    string _peekKind = "";

    public event Action? MenuRequested;    // правый клик по острову — то же меню, что в трее
    public event Action? SettingsChanged;  // остров перетащили: изменились место или монитор

    public IslandWindow(bool snapshot = false)
    {
        InitializeComponent();
        _instant = snapshot;
        _media = new MediaService(Dispatcher);
        _power = new PowerService(Dispatcher);
        _page = Math.Max(0, Array.IndexOf(Pages, AppState.Current.PageName ?? ""));

        PrevIcon.Data = PrevGeo; NextIcon.Data = NextGeo; CoverNote.Data = NoteGeo;
        IdleBolt.Data = BoltGeo; WaterDrop.Data = DropGeo; ResetIcon.Data = ResetGeo; KoiShareIcon.Data = ShareGeo;
        NavLeftIcon.Data = LeftGeo; NavRightIcon.Data = RightGeo;
        ProgTrack.SizeChanged += (_, _) => RefreshMusicProgress();
        WaterTrack.SizeChanged += (_, _) => RefreshWater();
        ShadeHost.SizeChanged += (_, _) => UpdateCorners(Island.RenderSize); // сменили масштаб — вырез тени на новое место
        ApplyLook();
        RefreshAll();
        if (snapshot) return;

        _audio = new AudioWatch();
        _sys = new SystemWatch(Dispatcher);
        SourceInitialized += (_, _) => MakeToolWindow();
        Deactivated += (_, _) => { if (_typing) EndTyping(hide: false); }; // кликнули мимо — клавиатура снова ваша
        Loaded += (_, _) => { ApplyScale(); Place(); };
        DpiChanged += (_, _) => { if (!_dragging) Dispatcher.InvokeAsync(Place, DispatcherPriority.Background); };
        SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.InvokeAsync(Place);
        _hoverIn.Tick += (_, _) => { _hoverIn.Stop(); if (CursorOverIsland()) Expand(); };
        _hoverOut.Tick += (_, _) =>
        {
            _hoverOut.Stop();
            if (_mode != Mode.Expanded || _pressed || _dragOut || _typing) return; // печатаете задачу — не сворачиваемся
            if (CursorOverIsland()) { _hoverOut.Start(); return; } // курсор ещё над островом (например, после перетаскивания файла)
            Collapse();
        };
        _peekEnd.Tick += (_, _) => { _peekEnd.Stop(); if (_mode == Mode.Peek) Collapse(); };
        _tick.Tick += (_, _) => OnTick();
        _fast.Tick += (_, _) => OnFast();
        WireLive();
        _tick.Start();
        _ = _media.StartAsync();
        ApplyFeatures();
    }

    // ---- Покой, раскрытие, страницы ------------------------------------------------------------

    string[] Pages => AllPages.Where(p => (p != "local" || AppState.Current.LocalPage) && (p != "day" || AppState.Current.DayPage)).ToArray();
    string PageName => Pages[Math.Clamp(_page, 0, Pages.Length - 1)];
    bool HasTrack => _media.HasSession && _media.Title.Length > 0;

    void OnTick()
    {
        var now = DateTime.Now;
        _focus.Tick();
        IdleTime.Text = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (now.Minute != _lastMinute)
        {
            _lastMinute = now.Minute;
            _power.Read();
            RefreshWater(); // новый день — счётчик с нуля; и вода в пруду рыбки понемногу мелеет
            CheckWaterReminder(now);
            if (_mode == Mode.Expanded && PageName == "day" && !_typing) RefreshDay(); // время задач «краснеет» вовремя
            CheckPrayer(now);
            if (AppState.Current.LocalPage) _ = _local.RefreshAsync();
            if (_mode == Mode.Expanded && PageName == "local") RefreshLocal();
        }
        CheckTasks(now);
        CheckTimers(now);
        CheckCalendar(now);
        if (now.Second == 0) CheckRamadan(now);
        RefreshIdleRight();
        if (_mode == Mode.Expanded)
        {
            if (PageName == "music" && _media.IsPlaying) RefreshMusicProgress();
            if (PageName == "focus") RefreshFocus();
        }
        if ((now - _lastFullscreenCheck).TotalSeconds >= 2)
        {
            _lastFullscreenCheck = now;
            ApplyVisibility();
        }
    }

    void Island_MouseEnter(object sender, MouseEventArgs e)
    {
        _hoverOut.Stop();
        // Уведомление с кнопкой («+250 мл», «Обновить») не раскрываем — даём нажать кнопку
        if (_mode == Mode.Peek && _peekAction != null) { _peekEnd.Stop(); return; }
        if (_mode != Mode.Expanded) _hoverIn.Start();
    }

    void Island_MouseLeave(object sender, MouseEventArgs e)
    {
        _hoverIn.Stop();
        if (_mode == Mode.Peek && _peekAction != null)
        {
            _peekEnd.Interval = TimeSpan.FromSeconds(2);
            _peekEnd.Start();
            return;
        }
        if (_mode == Mode.Expanded && !_pressed) _hoverOut.Start();
    }

    void Island_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_mode == Mode.Expanded) GoPage(e.Delta < 0 ? 1 : -1);
    }

    void Island_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        MenuRequested?.Invoke();
    }

    // Наведение открывает музыку, если она играет, иначе — последнюю страницу
    void Expand(string? page = null)
    {
        _peekEnd.Stop();
        var pages = Pages;
        if (page != null) _page = Math.Max(0, Array.IndexOf(pages, page));
        else if (_mode != Mode.Expanded && _media.IsPlaying) _page = 0;
        _page = Math.Clamp(_page, 0, pages.Length - 1);
        _mode = Mode.Expanded;
        _peekKind = "";
        _peekAction = null;
        NavLayer.Visibility = Visibility.Visible;
        ShowPanel(PanelOf(PageName));
        RefreshAll();
        AnimateTo(PageSize());
    }

    FrameworkElement PanelOf(string page) => page switch
    {
        "music" => MusicPanel,
        "day" => DayPanel,
        "shelf" => ShelfPanel,
        "focus" => FocusPanel,
        "water" => WaterPanel,
        _ => LocalPanel,
    };

    Size PageSize() => PageName switch
    {
        "music" => !HasTrack ? new Size(420, 96) : LyricBlock.Visibility == Visibility.Visible ? new Size(420, 194) : new Size(420, 150),
        "shelf" => new Size(420, 122),
        "day" => DaySize(),
        "focus" => new Size(420, 150),
        "water" => new Size(440, 118),
        "local" => new Size(AppState.Current.Prayer ? 460 : 400, 112),
        _ => new Size(420, 112),
    };

    void Collapse()
    {
        if (_typing) EndTyping(hide: false);
        _mode = Mode.Idle;
        _peekKind = "";
        _peekAction = null;
        NavLayer.Visibility = Visibility.Collapsed;
        ShowPanel(IdlePanel);
        RefreshIdle();
        AnimateTo(IdleSize());
    }

    void GoPage(int delta)
    {
        var pages = Pages;
        _page = (_page + delta + pages.Length) % pages.Length;
        AppState.Current.PageName = PageName;
        Expand(PageName);
    }

    // Растёт — пружиной с перелётом, как капля; сжимается — мягко, без «проваливания» внутрь
    void AnimateTo(Size size, bool gentle = false)
    {
        if (_instant)
        {
            Island.BeginAnimation(WidthProperty, null);
            Island.BeginAnimation(HeightProperty, null);
            Island.Width = size.Width;
            Island.Height = size.Height;
            return;
        }
        Animate(WidthProperty, size.Width, Island.ActualWidth, gentle);
        Animate(HeightProperty, size.Height, Island.ActualHeight, gentle);
    }

    void Animate(DependencyProperty prop, double to, double from, bool gentle)
    {
        bool grow = to > from + 0.5;
        IEasingFunction ease = gentle ? new CubicEase { EasingMode = EasingMode.EaseOut }
                             : grow ? new SpringEase()
                             : new BackEase { Amplitude = 0.18, EasingMode = EasingMode.EaseOut };
        var ms = gentle ? 300 : grow ? 640 : 380;
        Island.BeginAnimation(prop, new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease });
    }

    void ShowPanel(FrameworkElement target)
    {
        foreach (var p in new FrameworkElement[] { IdlePanel, MusicPanel, DayPanel, ShelfPanel, FocusPanel, WaterPanel, LocalPanel, PeekPanel })
        {
            if (p == target) continue;
            p.BeginAnimation(OpacityProperty, null);
            p.Opacity = 0;
            p.Visibility = Visibility.Collapsed;
        }
        target.Visibility = Visibility.Visible;
        if (_instant) { target.BeginAnimation(OpacityProperty, null); target.Opacity = 1; return; }
        // Содержимое проявляется, когда форма почти «доехала»
        target.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { BeginTime = TimeSpan.FromMilliseconds(140) });
    }

    // ---- Короткие уведомления -----------------------------------------------------------------

    // kind — тип уведомления: повтор того же типа (громкость крутят) обновляет текст без новой анимации
    void ShowPeek(string kind, Brush iconBg, Geometry? icon, string title, string sub = "", string right = "",
                  Brush? rightBrush = null, double seconds = 3, string? action = null, Action? onAction = null,
                  double iconRadius = 14, double? bar = null)
    {
        if (_mode == Mode.Expanded || _pressed || _dragOut) return; // открытый остров и так всё показывает
        bool same = _mode == Mode.Peek && _peekKind == kind;
        _mode = Mode.Peek;
        _peekKind = kind;
        PeekIconBox.Background = iconBg;
        PeekIconBox.CornerRadius = new CornerRadius(iconRadius);
        PeekIcon.Data = icon;
        PeekIcon.Visibility = icon != null ? Visibility.Visible : Visibility.Collapsed;
        PeekTitle.Text = title;
        PeekSub.Text = sub;
        PeekSub.Visibility = sub.Length > 0 && bar == null ? Visibility.Visible : Visibility.Collapsed;
        PeekBarTrack.Visibility = bar != null ? Visibility.Visible : Visibility.Collapsed;
        if (bar is double f)
        {
            f = Math.Clamp(f, 0, 1);
            if (_instant || !same) { PeekBarScale.BeginAnimation(ScaleTransform.ScaleXProperty, null); PeekBarScale.ScaleX = f; }
            else PeekBarScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(f, TimeSpan.FromMilliseconds(140)));
        }
        PeekRight.Text = right;
        PeekRight.Foreground = rightBrush ?? Res("Fg");
        _peekAction = onAction;
        PeekAction.Visibility = action != null ? Visibility.Visible : Visibility.Collapsed;
        PeekActionText.Text = action ?? "";
        if (!same) ShowPanel(PeekPanel);
        AnimateTo(new Size(action != null ? 380 : 330, sub.Length > 0 || bar != null ? 50 : 42), gentle: same);
        _peekEnd.Interval = TimeSpan.FromSeconds(seconds);
        _peekEnd.Stop();
        _peekEnd.Start();
    }

    void OnPlugChanged(bool on) =>
        ShowPeek("charge", on ? Res("Green") : Res("Muted"), BoltGeo, Strings.Get(on ? "charge.on" : "charge.off"),
                 right: $"{_power.Percent}%", rightBrush: on ? Res("Green") : null);

    void OnTrackChanged()
    {
        // При запуске плеер сообщает текущую песню — это не «новая», молчим
        if ((DateTime.Now - _startedAt).TotalSeconds < 4) return;
        ShowTrackPeek();
    }

    void ShowTrackPeek()
    {
        var bg = _media.Cover != null ? new ImageBrush(_media.Cover) { Stretch = Stretch.UniformToFill } : Res("CoverGradient");
        ShowPeek("track", bg, _media.Cover == null ? NoteGeo : null, _media.Title, _media.Artist, iconRadius: 7);
    }

    void OnFocusFinished(bool wasFocus)
    {
        Chime.Play();
        ShowPeek("focus", wasFocus ? Res("Lagoon") : Res("Coral"), wasFocus ? CupGeo : WatchGeo,
                 Strings.Get(wasFocus ? "focus.done" : "focus.breakDone"), seconds: 6);
        RefreshFocus();
        RefreshIdleRight();
    }

    void CheckWaterReminder(DateTime now)
    {
        var st = AppState.Current;
        if (now.Hour < 9 || now.Hour >= 21 || st.WaterToday >= st.WaterGoalMl) return;
        var last = st.LastDrink ?? DateTime.Today.AddHours(9);
        if (now - last < TimeSpan.FromHours(1) || now - _lastWaterReminder < TimeSpan.FromHours(1)) return;
        _lastWaterReminder = now;
        ShowWaterPeek();
    }

    void ShowWaterPeek()
    {
        var st = AppState.Current;
        var ml = Strings.Get("ml");
        ShowPeek("water", Res("Sky"), DropGeo, Strings.Get(st.Koi ? "water.koiRemind" : "water.remind"),
                 $"{st.WaterToday.ToString("#,0", Strings.Culture)} / {st.WaterGoalMl.ToString("#,0", Strings.Culture)} {ml}",
                 seconds: 8, action: $"+250 {ml}", onAction: () => AddWater(250));
    }

    // ---- Обновление содержимого ---------------------------------------------------------------

    public void ApplyTexts() => RefreshAll();

    public void RefreshAll()
    {
        RefreshIdle();
        RefreshMusic();
        RefreshDay();
        RefreshShelf();
        RefreshFocus();
        RefreshTimers();
        RefreshWater();
        RefreshLocal();
    }

    void RefreshIdle()
    {
        var st = AppState.Current;
        bool playing = _media.IsPlaying;
        IdleEq.Visibility = playing ? Visibility.Visible : Visibility.Collapsed;
        IdleKoi.Visibility = !playing && st.Koi ? Visibility.Visible : Visibility.Collapsed;
        IdleSun.Visibility = !playing && !st.Koi ? Visibility.Visible : Visibility.Collapsed;
        var line = CurrentLyric();
        bool lyric = st.Lyrics && playing && line.Length > 0;
        IdleLyric.Text = line;
        IdleLyric.Visibility = lyric ? Visibility.Visible : Visibility.Collapsed;
        IdleTime.Visibility = lyric ? Visibility.Collapsed : Visibility.Visible;
        IdleTime.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        DotCam.Visibility = st.Indicators && CamApp != null ? Visibility.Visible : Visibility.Collapsed;
        DotMic.Visibility = st.Indicators && MicApp != null ? Visibility.Visible : Visibility.Collapsed;
        if (_instant) { Eq1.Height = 8; Eq2.Height = 14; Eq3.Height = 6; Eq4.Height = 11; }
        RefreshIdleRight();
        UpdateFast();
    }

    // Ширина свёрнутого острова: строка песни раздвигает его, точки камеры и микрофона — чуть-чуть
    Size IdleSize()
    {
        double w = IdleBase.Width;
        if (IdleLyric.Visibility == Visibility.Visible)
        {
            var ft = new FormattedText(IdleLyric.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(IdleLyric.FontFamily, IdleLyric.FontStyle, IdleLyric.FontWeight, IdleLyric.FontStretch),
                IdleLyric.FontSize, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            w = Math.Clamp(ft.WidthIncludingTrailingWhitespace + 34 + 48 + 26 + 6, 230, 460);
        }
        if (DotCam.Visibility == Visibility.Visible) w += 12;
        if (DotMic.Visibility == Visibility.Visible) w += 12;
        return new Size(w, IdleBase.Height);
    }

    // Справа в покое: идущий таймер важнее заряда
    void RefreshIdleRight()
    {
        var timer = SoonestTimer();
        if (timer is TimeSpan t && (!_focus.Running || t < _focus.Left))
        {
            IdleRight.Text = Fmt(t);
            IdleRight.Foreground = Res("Amber");
            IdleBolt.Visibility = Visibility.Collapsed;
        }
        else if (_focus.Running)
        {
            IdleRight.Text = Fmt(_focus.Left);
            IdleRight.Foreground = Res(_focus.IsBreak ? "Lagoon" : "Coral");
            IdleBolt.Visibility = Visibility.Collapsed;
        }
        else if (_power.HasBattery)
        {
            IdleRight.Text = $"{_power.Percent}%";
            IdleRight.Foreground = Res("Dim");
            IdleBolt.Visibility = _power.Charging ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            IdleRight.Text = "";
            IdleBolt.Visibility = Visibility.Collapsed;
        }
    }

    void RefreshMusic()
    {
        bool has = HasTrack;
        MusicTitle.Text = has ? _media.Title : Strings.Get("music.none");
        MusicArtist.Text = has ? _media.Artist : Strings.Get("music.hint");
        MusicApp.Text = has ? _media.AppName : "";
        MusicApp.Visibility = has && _media.AppName.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        // Нет длительности (радио, стрим) — полосу прячем, но место держим, чтобы кнопки не прыгали
        ProgressRow.Visibility = !has ? Visibility.Collapsed : _media.Duration > TimeSpan.Zero ? Visibility.Visible : Visibility.Hidden;
        MusicControls.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        CoverBox.Background = _media.Cover != null ? new ImageBrush(_media.Cover) { Stretch = Stretch.UniformToFill } : Res("CoverGradient");
        CoverNote.Visibility = _media.Cover != null ? Visibility.Collapsed : Visibility.Visible;
        PlayIcon.Data = _media.IsPlaying ? PauseGeo : PlayGeo;
        LyricBlock.Visibility = has && AppState.Current.Lyrics && _lyrics.Lines != null ? Visibility.Visible : Visibility.Collapsed;
        RefreshLyricTexts();
        RefreshMusicProgress();
    }

    void RefreshMusicProgress()
    {
        var d = _media.Duration;
        var p = _media.Position;
        PosText.Text = Fmt(p);
        DurText.Text = "-" + Fmt(d > p ? d - p : TimeSpan.Zero);
        double f = d.TotalSeconds > 0 ? Math.Clamp(p.TotalSeconds / d.TotalSeconds, 0, 1) : 0;
        ProgFill.Width = ProgTrack.ActualWidth * f;
    }

    void RefreshFocus()
    {
        var accent = Res(_focus.IsBreak ? "Lagoon" : "Coral");
        FocusTime.Text = Fmt(_focus.Left);
        FocusLabel.Text = Strings.Get(_focus.IsBreak ? "focus.break" : "focus.title");
        FocusStartText.Text = Strings.Get(_focus.Running ? "focus.pause" : "focus.start");
        FocusModeText.Text = Strings.Get(_focus.IsBreak ? "focus.toFocus" : "focus.toBreak");
        FocusIcon.Data = _focus.IsBreak ? CupGeo : WatchGeo;
        FocusIcon.Fill = accent;
        FocusArc.Stroke = accent;
        FocusTrack.Stroke = new SolidColorBrush(((SolidColorBrush)accent).Color) { Opacity = 0.22 };
        FocusStartBtn.Background = accent;
        // Дуга — сколько осталось: полный круг в начале, тает к концу.
        // Длина штриха у эллипса считается в толщинах линии.
        double circumference = Math.PI * (FocusRing.Width - FocusArc.StrokeThickness) / FocusArc.StrokeThickness;
        double left = Math.Clamp(_focus.LeftFraction, 0, 1);
        FocusArc.Visibility = left > 0.002 ? Visibility.Visible : Visibility.Hidden;
        FocusArc.StrokeDashArray = new DoubleCollection { left * circumference, 1000 };
    }

    void RefreshWater()
    {
        var st = AppState.Current;
        if (KoiPet.Sync(st)) AppState.Save(); // новый день или новая цель — сегодняшний день засчитан заново
        int now = st.WaterToday, goal = st.WaterGoalMl;
        var ml = Strings.Get("ml");
        WaterNow.Text = now.ToString("#,0", Strings.Culture);
        WaterGoalRun.Text = $" / {goal.ToString("#,0", Strings.Culture)} {ml}";
        double f = goal > 0 ? Math.Min(1, now / (double)goal) : 0;
        WaterFill.Width = WaterTrack.ActualWidth * f;
        Water250Text.Text = $"+250 {ml}";
        WaterUndoText.Text = Strings.Get("water.undo");

        WaterKoi.Visibility = st.Koi ? Visibility.Visible : Visibility.Collapsed;
        KoiShare.Visibility = WaterKoi.Visibility;
        KoiShare.ToolTip = Strings.Get("koi.share");
        WaterDrop.Visibility = st.Koi ? Visibility.Collapsed : Visibility.Visible;
        var (level, energy, thirsty) = KoiMood();
        bool sad = KoiPet.Sad(st);
        var kind = KoiPet.Kind(st);
        double growth = KoiPet.Growth(st);
        KoiKind? second = KoiPet.HasSecond(st) ? KoiPet.SecondKind(st) : null;
        foreach (var koi in new[] { WaterKoi, IdleKoi })
        {
            koi.Level = level;
            koi.Energy = energy;
            koi.Sad = sad;
            koi.SetFish(kind, growth, second, growth * 0.6);
        }
        WaterNote.Text = st.Koi && sad && f < 1 ? Strings.Get("koi.sad")
            : st.Koi && thirsty && f < 1 ? Strings.Get("water.thirsty")
            : f >= 1 ? $"{Strings.Get("water.today")} · {Strings.Get("water.goal")}"
            : $"{Strings.Get("water.today")} · {(int)Math.Round(f * 100)}%";

        // Под водой — сама рыбка: сколько ей дней и какая серия; в день открытия — новость
        KoiLine.Visibility = st.Koi ? Visibility.Visible : Visibility.Collapsed;
        var news = KoiPet.NewsToday(st);
        int age = KoiPet.Age(st);
        KoiLine.Text = news != null ? Strings.Get("koi.new." + news)
            : age == 0 ? Strings.Get("koi.fry")
            : KoiLineText(st);
        KoiLine.Foreground = news != null ? Res("Amber") : Res("Faint");
        WaterKoi.ToolTip = KoiPet.Next(st) is { } next
            ? Strings.Format("koi.next", Strings.Days(Math.Max(1, next.At - KoiPet.Streak(st))), Strings.Get("chip." + next.What))
            : null;
    }

    // Настроение рыбки: чем дольше вы не пили, тем мельче пруд и тем она вялее
    static (double Level, double Energy, bool Thirsty) KoiMood()
    {
        var st = AppState.Current;
        double hours = st.LastDrink is DateTime d ? (DateTime.Now - d).TotalHours : 8;
        if (st.WaterToday >= st.WaterGoalMl) hours = Math.Min(hours, 1); // цель выполнена — рыбке хорошо весь день
        return (Math.Clamp(1 - hours / 4.5, 0.22, 1), Math.Clamp(1 - hours / 3, 0.15, 1), hours >= 2);
    }

    void AddWater(int ml)
    {
        var st = AppState.Current;
        bool before = st.WaterToday >= st.WaterGoalMl;
        st.AddWater(ml);
        if (ml > 0) { WaterKoi.Splash(); IdleKoi.Splash(); }
        RefreshWater();
        if (before || st.WaterToday < st.WaterGoalMl || !st.Koi) return;

        // Цель выполнена только что: рыбка подросла, а может, открылось что-то новое
        var news = KoiPet.NewsToday(st);
        if (news != null) Chime.Play();
        ShowPeek("koi", Res(news != null ? "Amber" : "Sky"), FishGeo,
                 Strings.Get(news != null ? "koi.new." + news : "koi.goal"),
                 KoiLineText(st), seconds: 5);
    }

    // «Кои: 14 дн. · серия 5»; серии нет — только возраст
    static string KoiLineText(AppState st)
    {
        int streak = KoiPet.Streak(st);
        return streak > 0 ? Strings.Format("koi.line", Strings.DaysShort(KoiPet.Age(st)), streak)
                          : Strings.Format("koi.age", Strings.DaysShort(KoiPet.Age(st)));
    }

    // Картинка «Моя кои» для сторис: в «Изображения\Jazira», в буфер обмена и на полку
    public void ShareKoi()
    {
        try
        {
            var image = KoiCard.Render(AppState.Current);
            var path = KoiCard.Save(image);
            try { Clipboard.SetImage(image); }
            catch (Exception ex) { App.Log(ex); } // буфер занят другой программой — картинка всё равно на полке
            AddToShelf(new[] { path });
            if (_mode == Mode.Expanded) Collapse();
            ShowPeek("koi", Res("Coral"), FishGeo, Strings.Get("koi.saved"), System.IO.Path.GetFileName(path), seconds: 8,
                     action: Strings.Get("koi.open"),
                     onAction: () => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }));
        }
        catch (Exception ex)
        {
            App.Log(ex);
            ShowInfo(Strings.Get("koi.fail"));
        }
    }

    // ---- Кнопки -------------------------------------------------------------------------------

    void Prev_Click(object sender, RoutedEventArgs e) => _media.Previous();
    void PlayPause_Click(object sender, RoutedEventArgs e) => _media.PlayPause();
    void Next_Click(object sender, RoutedEventArgs e) => _media.Next();
    void NavLeft_Click(object sender, RoutedEventArgs e) => GoPage(-1);
    void NavRight_Click(object sender, RoutedEventArgs e) => GoPage(1);
    void FocusStart_Click(object sender, RoutedEventArgs e) { _focus.Toggle(); RefreshFocus(); RefreshIdleRight(); }
    void FocusMode_Click(object sender, RoutedEventArgs e) { _focus.SwitchMode(); RefreshFocus(); RefreshIdleRight(); }
    void FocusReset_Click(object sender, RoutedEventArgs e) { _focus.Reset(); RefreshFocus(); RefreshIdleRight(); }
    void Water250_Click(object sender, RoutedEventArgs e) => AddWater(250);
    void WaterUndo_Click(object sender, RoutedEventArgs e) => AddWater(-250);
    void KoiShare_Click(object sender, RoutedEventArgs e) => ShareKoi();

    void PeekAction_Click(object sender, RoutedEventArgs e)
    {
        var action = _peekAction;
        _peekAction = null;
        _peekEnd.Stop();
        Collapse();
        action?.Invoke();
    }

    Brush Res(string key) => (Brush)FindResource(key);

    static string Fmt(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
}
