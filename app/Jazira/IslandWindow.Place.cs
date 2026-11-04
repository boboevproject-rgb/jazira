using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Forms = System.Windows.Forms;

namespace Jazira;

// Место и вид острова: монитор, «прикреплён к верху» или свободно, перетаскивание мышью,
// масштаб, вид (тема), форма (капсула или вырез у края) и подсветка цветом обложки.
public partial class IslandWindow
{
    const double BaseW = 520, BaseH = 260; // окно с запасом под раскрытый остров и его тень
    IntPtr _hwnd;
    bool _pressed, _dragging;
    Native.POINT _pressCursor;
    Native.RECT _pressRect;

    void MakeToolWindow()
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        Native.SetWindowLong(_hwnd, Native.GWL_EXSTYLE, Native.GetWindowLong(_hwnd, Native.GWL_EXSTYLE) | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
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

    // Монитор из настроек; если его отключили — основной
    static Forms.Screen TargetScreen()
    {
        var id = AppState.Current.Monitor;
        foreach (var s in Forms.Screen.AllScreens)
            if (s.DeviceName == id) return s;
        return Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
    }

    // Окно ставится в физических пикселях выбранного монитора: так верно и при разном масштабе экранов.
    // Прикреплён — по центру верхнего края (под панелью задач, если она сверху), свободно — где оставили.
    void Place()
    {
        if (_hwnd == IntPtr.Zero || _dragging) return;
        var st = AppState.Current;
        var wa = TargetScreen().WorkingArea;
        double k = Native.ScaleAt(wa.Left + wa.Width / 2, wa.Top + wa.Height / 2);
        int w = (int)Math.Round(Width * k), h = (int)Math.Round(Height * k);
        int x, y;
        if (st.Docked)
        {
            x = wa.Left + (wa.Width - w) / 2;
            y = wa.Top;
        }
        else
        {
            x = (int)Math.Round(wa.Left + st.FloatX * wa.Width - w / 2.0);
            y = (int)Math.Round(wa.Top + st.FloatY * wa.Height);
            x = Math.Clamp(x, wa.Left, Math.Max(wa.Left, wa.Right - w));
            y = Math.Clamp(y, wa.Top, Math.Max(wa.Top, wa.Bottom - h)); // раскрытый остров не уходит за край
        }
        Native.SetWindowPos(_hwnd, IntPtr.Zero, x, y, w, h, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
    }

    void ApplyScale()
    {
        double s = _instant ? 1 : Math.Clamp(AppState.Current.Scale, 0.7, 1.6);
        ScaleHost.LayoutTransform = Math.Abs(s - 1) < 0.001 ? Transform.Identity : new ScaleTransform(s, s);
        Width = BaseW * s;
        Height = BaseH * s;
    }

    // ---- Перетаскивание: потянул за остров — он поехал; отпустил у верхнего края — прилип ----

    void Island_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_hwnd == IntPtr.Zero) return;
        Native.GetCursorPos(out _pressCursor);
        Native.GetWindowRect(_hwnd, out _pressRect);
        _pressed = true;
        Island.CaptureMouse();
        e.Handled = true;
    }

    void Island_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed) return;
        Native.GetCursorPos(out var p);
        int dx = p.X - _pressCursor.X, dy = p.Y - _pressCursor.Y;
        if (!_dragging)
        {
            if (Math.Abs(dx) + Math.Abs(dy) < 6) return; // простой щелчок — не перетаскивание
            _dragging = true;
            _hoverIn.Stop();
            _hoverOut.Stop();
        }
        Native.SetWindowPos(_hwnd, IntPtr.Zero, _pressRect.Left + dx, _pressRect.Top + dy, 0, 0,
                            Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
    }

    void Island_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed) return;
        _pressed = false;
        Island.ReleaseMouseCapture();
        EndDrag();
    }

    void Island_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!_pressed) return;
        _pressed = false;
        EndDrag();
    }

    void EndDrag()
    {
        if (!_dragging)
        {
            if (_mode == Mode.Expanded && !CursorOverIsland()) _hoverOut.Start();
            return;
        }
        _dragging = false;
        var st = AppState.Current;
        Native.GetWindowRect(_hwnd, out var r);
        int cx = (r.Left + r.Right) / 2;
        double k = Native.ScaleAt(cx, r.Top + 20);
        double s = Math.Clamp(st.Scale, 0.7, 1.6);
        int islandTop = r.Top + (int)Math.Round(Island.Margin.Top * k * s);
        var scr = Forms.Screen.FromPoint(new System.Drawing.Point(cx, islandTop + (int)(17 * k * s)));
        var wa = scr.WorkingArea;
        st.Monitor = scr.Primary ? null : scr.DeviceName;
        if (islandTop - wa.Top < 48 * k) st.Docked = true; // отпустили у верхнего края — прилип
        else
        {
            st.Docked = false;
            st.FloatX = Math.Clamp((cx - wa.Left) / (double)wa.Width, 0, 1);
            st.FloatY = Math.Clamp((r.Top - wa.Top) / (double)wa.Height, 0, 1);
        }
        AppState.Save();
        ApplyShape();
        Place();
        SettingsChanged?.Invoke();
        if (_mode == Mode.Expanded && !CursorOverIsland()) _hoverOut.Start();
    }

    // Курсор над островом — по настоящим координатам экрана (после перетаскивания файлов WPF может «не знать»)
    bool CursorOverIsland()
    {
        if (_hwnd == IntPtr.Zero || !IsVisible) return Island.IsMouseOver;
        Native.GetCursorPos(out var p);
        var pt = Island.PointFromScreen(new Point(p.X, p.Y));
        return pt.X >= 0 && pt.Y >= 0 && pt.X <= Island.ActualWidth && pt.Y <= Island.ActualHeight;
    }

    // ---- Вид ------------------------------------------------------------------------------------

    // Вырез: остров слит с верхним краем экрана, скруглены только нижние углы
    bool Notch => AppState.Current.Docked && AppState.Current.Shape == "notch";

    void ApplyShape()
    {
        Island.Margin = new Thickness(0, Notch ? 0 : 6, 0, 0);
        Island.BorderThickness = Notch ? new Thickness(1, 0, 1, 1) : new Thickness(1);
        UpdateCorners(new Size(Island.ActualWidth, Island.ActualHeight));
    }

    // Скругление — по высоте острова; содержимое обрезается по той же форме
    void Island_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateCorners(e.NewSize);

    void UpdateCorners(Size size)
    {
        double r = Math.Min(size.Height / 2, 26);
        Island.CornerRadius = Notch ? new CornerRadius(0, 0, r, r) : new CornerRadius(r);
        var inner = new Size(Math.Max(0, size.Width - 2), Math.Max(0, size.Height - (Notch ? 1 : 2)));
        Body.Clip = Notch ? BottomRounded(inner, Math.Max(0, r - 1))
                          : new RectangleGeometry(new Rect(inner), Math.Max(0, r - 1), Math.Max(0, r - 1));
        UpdateShade(size, r);
    }

    // Тень повторяет остров по размеру и форме, а сам остров в слое тени вырезан
    void UpdateShade(Size size, double r)
    {
        double hostW = ShadeHost.ActualWidth, hostH = ShadeHost.ActualHeight;
        if (hostW <= 0 || size.Width <= 0) return;
        var at = new Point((hostW - size.Width) / 2, Island.Margin.Top);
        Glow.SetShape(new Rect(at, size), r, Notch);
        Geometry hole = Notch ? BottomRounded(size, r, at) : new RectangleGeometry(new Rect(at, size), r, r);
        var clip = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0, 0, hostW, hostH)), hole);
        clip.Freeze();
        ShadeHost.Clip = clip;
    }

    static Geometry BottomRounded(Size s, double r, Point at = default)
    {
        r = Math.Min(r, Math.Min(s.Width, s.Height) / 2);
        double x = at.X, y = at.Y;
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(x, y), true, true);
            c.LineTo(new Point(x + s.Width, y), false, false);
            c.LineTo(new Point(x + s.Width, y + s.Height - r), false, false);
            c.ArcTo(new Point(x + s.Width - r, y + s.Height), new Size(r, r), 0, false, SweepDirection.Clockwise, false, false);
            c.LineTo(new Point(x + r, y + s.Height), false, false);
            c.ArcTo(new Point(x, y + s.Height - r), new Size(r, r), 0, false, SweepDirection.Clockwise, false, false);
        }
        g.Freeze();
        return g;
    }

    void ApplyLook()
    {
        var theme = Theme.Get(AppState.Current.Theme);
        theme.Apply(this);
        IdleKoi.Outline = theme.Light;
        ApplyShape();
        ApplyTint(force: true);
    }

    // ---- Подсветка цветом обложки ----------------------------------------------------------------

    Color? _coverColor, _tintShown;
    object? _coverSeen;
    bool _tintInit;

    void ApplyTint(bool force = false)
    {
        var st = AppState.Current;
        var theme = Theme.Get(st.Theme);
        Color? c = st.CoverTint && HasTrack && _media.IsPlaying ? _coverColor : null;
        if (!force && _tintInit && c == _tintShown) return;
        _tintInit = true;
        _tintShown = c;

        var border = c is Color a ? Color.FromArgb(0x99, a.R, a.G, a.B) : theme.Border;
        if (Island.BorderBrush is not SolidColorBrush b || b.IsFrozen)
        {
            b = new SolidColorBrush(border);
            Island.BorderBrush = b;
        }
        AnimateColor(b, SolidColorBrush.ColorProperty, border);
        AnimateColor(Glow, SoftShadow.ColorProperty, c ?? Colors.Black);
        AnimateNumber(Glow, SoftShadow.StrengthProperty, c != null ? 0.42 : theme.ShadowOpacity);
        AnimateNumber(Glow, SoftShadow.SpreadProperty, c != null ? 20 : 13);
        AnimateNumber(Glow, SoftShadow.DepthProperty, c != null ? 0 : 3);

        Resources["EqFill"] = c is Color e ? Theme.Brush(e) : Res("Lagoon");
        Resources["ProgFill"] = c is Color p ? Theme.Brush(p) : Res("Fg");
        Resources["LyricFg"] = c is Color l && !theme.Light ? Theme.Brush(CoverColor.Lighten(l, 0.35)) : Res("Fg");
    }

    void AnimateColor(DependencyObject target, DependencyProperty prop, Color to)
    {
        var a = (IAnimatable)target;
        if (_instant) { a.BeginAnimation(prop, null); target.SetValue(prop, to); }
        else a.BeginAnimation(prop, new ColorAnimation(to, TimeSpan.FromMilliseconds(700)));
    }

    void AnimateNumber(DependencyObject target, DependencyProperty prop, double to)
    {
        var a = (IAnimatable)target;
        if (_instant) { a.BeginAnimation(prop, null); target.SetValue(prop, to); }
        else a.BeginAnimation(prop, new DoubleAnimation(to, TimeSpan.FromMilliseconds(700)));
    }

    // ---- Настройки из меню ------------------------------------------------------------------------

    // Меню в трее поменяло AppState — применить всё сразу
    public void ApplySettings()
    {
        var name = PageName;
        ApplyLook();
        ApplyScale();
        Place();
        ApplyFeatures();
        var pages = Pages;
        _page = Math.Max(0, Array.IndexOf(pages, name));
        RefreshAll();
        if (_mode == Mode.Expanded) Expand(PageName);
        else if (_mode == Mode.Idle) AnimateTo(IdleSize());
        AppState.Save();
    }

    void ApplyFeatures()
    {
        var st = AppState.Current;
        if (!st.Lyrics) _lyrics.Clear();
        else if (HasTrack) _lyrics.Load(_media.Title, _media.Artist, _media.Duration);
        if (_sys != null) _sys.Enabled = st.Indicators;
        if (_audio != null) _audio.WatchVolume = st.Indicators;
        if (st.LocalPage && !_instant) _ = _local.RefreshAsync();
    }
}
