using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Jazira;

// Остров у верхнего края экрана. В покое — время и значки; навёл курсор — раскрывается
// в страницы «Музыка / Фокус / Вода»; события (зарядка, новая песня, вода) — короткий «пик».
public partial class IslandWindow : Window
{
    enum Mode { Idle, Expanded, Peek }
    static readonly string[] Pages = { "music", "focus", "water" };
    static readonly Size IdleSize = new(196, 34);

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

    readonly MediaService _media;
    readonly PowerService _power;
    readonly FocusTimer _focus = new();
    readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly DispatcherTimer _hoverIn = new() { Interval = TimeSpan.FromMilliseconds(70) };
    readonly DispatcherTimer _hoverOut = new() { Interval = TimeSpan.FromMilliseconds(380) };
    readonly DispatcherTimer _peekEnd = new();
    readonly DateTime _startedAt = DateTime.Now;
    readonly bool _instant; // снимки: без анимаций
    Mode _mode = Mode.Idle;
    int _page;
    int _lastMinute = -1;
    string _lastDay = AppState.Today;
    DateTime _lastWaterReminder = DateTime.Now; // первое напоминание — не раньше чем через час после запуска
    DateTime _lastFullscreenCheck;
    Action? _peekAction;
    bool _eqOn;

    public IslandWindow(bool snapshot = false)
    {
        InitializeComponent();
        _instant = snapshot;
        _media = new MediaService(Dispatcher);
        _power = new PowerService(Dispatcher);
        _page = Math.Clamp(AppState.Current.Page, 0, Pages.Length - 1);

        PrevIcon.Data = PrevGeo; NextIcon.Data = NextGeo; CoverNote.Data = NoteGeo;
        IdleBolt.Data = BoltGeo; WaterDrop.Data = DropGeo; ResetIcon.Data = ResetGeo;
        NavLeftIcon.Data = LeftGeo; NavRightIcon.Data = RightGeo;
        ProgTrack.SizeChanged += (_, _) => RefreshMusicProgress();
        WaterTrack.SizeChanged += (_, _) => RefreshWater();
        ApplyTexts();
        if (snapshot) return;

        SourceInitialized += (_, _) => MakeToolWindow();
        Loaded += (_, _) => PlaceAtTop();
        SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.InvokeAsync(PlaceAtTop);
        _hoverIn.Tick += (_, _) => { _hoverIn.Stop(); Expand(); };
        _hoverOut.Tick += (_, _) => { _hoverOut.Stop(); Collapse(); };
        _peekEnd.Tick += (_, _) => { _peekEnd.Stop(); if (_mode == Mode.Peek) Collapse(); };
        _tick.Tick += (_, _) => OnTick();
        _media.Changed += () =>
        {
            RefreshMusic();
            RefreshIdle();
            if (_mode == Mode.Expanded && Pages[_page] == "music") AnimateTo(PageSize()); // плеер включили или закрыли
        };
        _media.TrackChanged += OnTrackChanged;
        _power.Changed += RefreshIdle;
        _power.PlugChanged += OnPlugChanged;
        _focus.Finished += OnFocusFinished;
        _tick.Start();
        _ = _media.StartAsync();
    }

    // ---- Окно ----------------------------------------------------------------------------------

    void MakeToolWindow()
    {
        var h = new WindowInteropHelper(this).Handle;
        Native.SetWindowLong(h, Native.GWL_EXSTYLE, Native.GetWindowLong(h, Native.GWL_EXSTYLE) | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
    }

    // По центру верхнего края основного монитора; если панель задач сверху — сразу под ней
    void PlaceAtTop()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top;
    }

    public void ApplyVisibility()
    {
        bool show = AppState.Current.ShowIsland && !Native.FullscreenAppActive();
        if (show && !IsVisible)
        {
            Show();
            Topmost = false; Topmost = true; // вернуться поверх окон после полноэкранного приложения
        }
        else if (!show && IsVisible) Hide();
    }

    void OnTick()
    {
        var now = DateTime.Now;
        _focus.Tick();
        IdleTime.Text = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (now.Minute != _lastMinute)
        {
            _lastMinute = now.Minute;
            _power.Read();
            if (AppState.Today != _lastDay) { _lastDay = AppState.Today; RefreshWater(); }
            CheckWaterReminder(now);
        }
        RefreshIdleRight();
        if (_mode == Mode.Expanded)
        {
            if (Pages[_page] == "music" && _media.IsPlaying) RefreshMusicProgress();
            if (Pages[_page] == "focus") RefreshFocus();
        }
        if ((now - _lastFullscreenCheck).TotalSeconds >= 2)
        {
            _lastFullscreenCheck = now;
            ApplyVisibility();
        }
    }

    // ---- Раскрытие и сворачивание -------------------------------------------------------------

    void Island_MouseEnter(object sender, MouseEventArgs e)
    {
        _hoverOut.Stop();
        if (_mode != Mode.Expanded) _hoverIn.Start();
    }

    void Island_MouseLeave(object sender, MouseEventArgs e)
    {
        _hoverIn.Stop();
        if (_mode == Mode.Expanded) _hoverOut.Start();
    }

    void Island_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_mode == Mode.Expanded) GoPage(e.Delta < 0 ? 1 : -1);
    }

    // Наведение открывает музыку, если она играет, иначе — последнюю страницу
    void Expand(int? page = null)
    {
        _peekEnd.Stop();
        if (page is int p) _page = p;
        else if (_mode != Mode.Expanded && _media.IsPlaying) _page = 0;
        _mode = Mode.Expanded;
        SetEq(false);
        NavLayer.Visibility = Visibility.Visible;
        ShowPanel(Pages[_page] switch { "music" => MusicPanel, "focus" => FocusPanel, _ => WaterPanel });
        AnimateTo(PageSize());
        RefreshAll();
    }

    // Музыка без плеера — компактная карточка, с плеером — с полосой и кнопками
    Size PageSize() => Pages[_page] != "music" ? new Size(420, 112)
                     : HasTrack ? new Size(420, 150) : new Size(420, 96);

    bool HasTrack => _media.HasSession && _media.Title.Length > 0;

    void Collapse()
    {
        _mode = Mode.Idle;
        NavLayer.Visibility = Visibility.Collapsed;
        ShowPanel(IdlePanel);
        AnimateTo(IdleSize);
        RefreshIdle();
    }

    void GoPage(int delta)
    {
        _page = (_page + delta + Pages.Length) % Pages.Length;
        AppState.Current.Page = _page;
        Expand(_page);
    }

    void AnimateTo(Size size)
    {
        if (_instant)
        {
            Island.BeginAnimation(WidthProperty, null);
            Island.BeginAnimation(HeightProperty, null);
            Island.Width = size.Width;
            Island.Height = size.Height;
            return;
        }
        // Лёгкий «пружинящий» перелёт размера, как у капли
        var ease = new BackEase { Amplitude = 0.28, EasingMode = EasingMode.EaseOut };
        var d = new Duration(TimeSpan.FromMilliseconds(430));
        Island.BeginAnimation(WidthProperty, new DoubleAnimation(size.Width, d) { EasingFunction = ease });
        Island.BeginAnimation(HeightProperty, new DoubleAnimation(size.Height, d) { EasingFunction = ease });
    }

    void ShowPanel(FrameworkElement target)
    {
        foreach (var p in new FrameworkElement[] { IdlePanel, MusicPanel, FocusPanel, WaterPanel, PeekPanel })
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

    // Скругление — по высоте острова; содержимое обрезается по той же форме
    void Island_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double r = Math.Min(e.NewSize.Height / 2, 26);
        Island.CornerRadius = new CornerRadius(r);
        var inner = new Size(Math.Max(0, e.NewSize.Width - 2), Math.Max(0, e.NewSize.Height - 2));
        Body.Clip = new RectangleGeometry(new Rect(inner), Math.Max(0, r - 1), Math.Max(0, r - 1));
    }

    // ---- Короткие уведомления -----------------------------------------------------------------

    void ShowPeek(Brush iconBg, Geometry? icon, string title, string sub, string right, Brush? rightBrush,
                  double seconds, string? action = null, Action? onAction = null, double iconRadius = 14)
    {
        if (_mode == Mode.Expanded) return; // открытый остров и так всё показывает
        _mode = Mode.Peek;
        SetEq(false);
        PeekIconBox.Background = iconBg;
        PeekIconBox.CornerRadius = new CornerRadius(iconRadius);
        PeekIcon.Data = icon;
        PeekIcon.Visibility = icon != null ? Visibility.Visible : Visibility.Collapsed;
        PeekTitle.Text = title;
        PeekSub.Text = sub;
        PeekSub.Visibility = sub.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        PeekRight.Text = right;
        PeekRight.Foreground = rightBrush ?? Brushes.White;
        _peekAction = onAction;
        PeekAction.Visibility = action != null ? Visibility.Visible : Visibility.Collapsed;
        PeekActionText.Text = action ?? "";
        ShowPanel(PeekPanel);
        AnimateTo(new Size(action != null ? 380 : 330, sub.Length > 0 ? 50 : 42));
        _peekEnd.Interval = TimeSpan.FromSeconds(seconds);
        _peekEnd.Stop();
        _peekEnd.Start();
    }

    void OnPlugChanged(bool on) =>
        ShowPeek(on ? Res("Green") : Res("Muted"), BoltGeo, Strings.Get(on ? "charge.on" : "charge.off"), "",
                 $"{_power.Percent}%", on ? Res("Green") : null, 3);

    void OnTrackChanged()
    {
        // При запуске плеер сообщает текущую песню — это не «новая», молчим
        if ((DateTime.Now - _startedAt).TotalSeconds < 4) return;
        ShowTrackPeek();
    }

    void ShowTrackPeek()
    {
        var bg = _media.Cover != null ? new ImageBrush(_media.Cover) { Stretch = Stretch.UniformToFill } : Res("CoverGradient");
        ShowPeek(bg, _media.Cover == null ? NoteGeo : null, _media.Title, _media.Artist, "", null, 3, iconRadius: 7);
    }

    void OnFocusFinished(bool wasFocus)
    {
        Chime.Play();
        ShowPeek(wasFocus ? Res("Lagoon") : Res("Coral"), wasFocus ? CupGeo : WatchGeo,
                 Strings.Get(wasFocus ? "focus.done" : "focus.breakDone"), "", "", null, 6);
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
        ShowPeek(Res("Sky"), DropGeo, Strings.Get("water.remind"),
                 $"{st.WaterToday.ToString("#,0", Strings.Culture)} / {st.WaterGoalMl.ToString("#,0", Strings.Culture)} {ml}",
                 "", null, 8, $"+250 {ml}", () => AddWater(250));
    }

    // ---- Обновление содержимого ---------------------------------------------------------------

    public void ApplyTexts() => RefreshAll();

    public void RefreshAll()
    {
        RefreshIdle();
        RefreshMusic();
        RefreshFocus();
        RefreshWater();
    }

    void RefreshIdle()
    {
        bool playing = _media.IsPlaying;
        IdleEq.Visibility = playing ? Visibility.Visible : Visibility.Collapsed;
        IdleSun.Visibility = playing ? Visibility.Collapsed : Visibility.Visible;
        SetEq(playing && _mode == Mode.Idle && !_instant); // анимация — только пока её видно
        IdleTime.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        RefreshIdleRight();
    }

    // Справа в покое: идущий таймер важнее заряда
    void RefreshIdleRight()
    {
        if (_focus.Running)
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

    void SetEq(bool on)
    {
        if (on == _eqOn) return;
        _eqOn = on;
        var bars = new[] { Eq1, Eq2, Eq3, Eq4 };
        double[] peaks = { 12, 16, 10, 14 }, periods = { 0.42, 0.55, 0.37, 0.5 };
        for (int i = 0; i < bars.Length; i++)
            bars[i].BeginAnimation(HeightProperty, on
                ? new DoubleAnimation(4, peaks[i], TimeSpan.FromSeconds(periods[i])) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() }
                : null);
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
        int now = st.WaterToday, goal = st.WaterGoalMl;
        var ml = Strings.Get("ml");
        WaterNow.Text = now.ToString("#,0", Strings.Culture);
        WaterGoalRun.Text = $" / {goal.ToString("#,0", Strings.Culture)} {ml}";
        double f = goal > 0 ? Math.Min(1, now / (double)goal) : 0;
        WaterFill.Width = WaterTrack.ActualWidth * f;
        WaterNote.Text = $"{Strings.Get("water.today")} · {(int)Math.Round(f * 100)}%" + (f >= 1 ? " · " + Strings.Get("water.goal") : "");
        Water250Text.Text = $"+250 {ml}";
        WaterUndoText.Text = Strings.Get("water.undo");
    }

    void AddWater(int ml)
    {
        AppState.Current.AddWater(ml);
        RefreshWater();
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

    void PeekAction_Click(object sender, RoutedEventArgs e)
    {
        var action = _peekAction;
        _peekAction = null;
        action?.Invoke();
        _peekEnd.Stop();
        Collapse();
    }

    Brush Res(string key) => (Brush)FindResource(key);

    static string Fmt(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : $"{(int)t.TotalMinutes}:{t.Seconds:00}";

    // ---- Снимки всех состояний (Jazira.exe --snapshot) ------------------------------------------

    internal IEnumerable<(string Name, Action Setup)> SnapshotStates(BitmapSource cover)
    {
        void Track(bool playing) => _media.Fake("Morning Tide", "Sea Lanterns", "Spotify", cover, playing,
                                                TimeSpan.FromSeconds(84), TimeSpan.FromSeconds(212));
        void Water(int ml) => AppState.Current.WaterLog[AppState.Today] = ml; // без сохранения на диск

        foreach (var lang in new[] { "ru", "tg" })
        {
            yield return ($"{lang}-idle", () => { Strings.Lang = lang; _media.FakeNone(); _power.Fake(true, false, 78); _focus.Reset(); Collapse(); });
            yield return ($"{lang}-idle-busy", () => { Track(true); _power.Fake(true, true, 64); _focus.Preview(TimeSpan.FromSeconds(24 * 60 + 12), true); Collapse(); });
            yield return ($"{lang}-music", () => { Track(true); Expand(0); });
            yield return ($"{lang}-music-empty", () => { _media.FakeNone(); Expand(0); });
            yield return ($"{lang}-focus", () => { _focus.Preview(TimeSpan.FromSeconds(24 * 60 + 12), true); Expand(1); });
            yield return ($"{lang}-water", () => { Water(1250); Expand(2); });
            yield return ($"{lang}-peek-charge", () => { Collapse(); _power.Fake(true, true, 78); OnPlugChanged(true); });
            yield return ($"{lang}-peek-track", () => { Collapse(); Track(true); ShowTrackPeek(); });
            yield return ($"{lang}-peek-water", () => { Collapse(); Water(750); ShowWaterPeek(); });
            yield return ($"{lang}-peek-focus", () => { Collapse(); OnFocusFinishedPreview(); });
        }
    }

    void OnFocusFinishedPreview() =>
        ShowPeek(Res("Lagoon"), CupGeo, Strings.Get("focus.done"), "", "", null, 6);
}
