using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Jazira;

// Автоскрытие (меню «Место» → «Прятать…»): курсор ушёл с острова — через секунду он уезжает за верхний
// край (свободно стоящий — тает на месте), остаётся тонкая полоска. Подвели курсор к полоске — остров
// выезжает. Уведомления, Ctrl+Alt+J и файл, который тащат к полоске, показывают его сами.
// Пока остров спрятан, окно пропускает мышь насквозь: под ним можно щёлкать как обычно.
// Курсор проверяется таймером по координатам экрана — сквозное окно событий мыши не получает.
public partial class IslandWindow
{
    static readonly TimeSpan HideAfter = TimeSpan.FromSeconds(1.2);          // курсор ушёл — через столько прячемся
    static readonly TimeSpan RevealAfter = TimeSpan.FromMilliseconds(150);   // курсор у полоски — через столько выезжаем
    const double HotStrip = 12;  // прикреплённый остров: высота чувствительной полосы у края, в точках острова
    const double HotPad = 16;    // и сколько она шире острова с каждой стороны

    static readonly Geometry HideGeo = G("M4 3h16v2.2H4z M12 8.5l6 6-1.5 1.5-3.4-3.4V21h-2.2v-8.4L7.5 16 6 14.5z");

    readonly DispatcherTimer _hidePoll = new() { Interval = TimeSpan.FromMilliseconds(80) };
    bool _tucked, _autoHideOn;
    DateTime? _outSince, _inSince;

    double UiScale => _instant ? 1 : Math.Clamp(AppState.Current.Scale, 0.7, 1.6);

    void WireAutoHide()
    {
        _hidePoll.Tick += (_, _) => AutoHideTick();
        ApplyAutoHide(startup: true);
    }

    // Включили или выключили в меню; после запуска — пусть остров секунды три побудет на виду
    void ApplyAutoHide(bool startup = false)
    {
        bool on = AppState.Current.AutoHide && !_instant;
        bool turnedOn = on && !_autoHideOn && !startup;
        _autoHideOn = on;
        if (!on)
        {
            _hidePoll.Stop();
            Reveal();
            return;
        }
        _inSince = null;
        _outSince = DateTime.Now + (startup ? TimeSpan.FromSeconds(2) : TimeSpan.Zero);
        _hidePoll.Start();
        if (turnedOn)
            ShowPeek("info", Res("Lagoon"), HideGeo, Strings.Get("autohide.on"),
                     Strings.Get(AppState.Current.Docked ? "autohide.hintTop" : "autohide.hint"), seconds: 4);
    }

    void AutoHideTick()
    {
        if (!IsVisible || _hwnd == IntPtr.Zero) return; // остров выключен или идёт игра во весь экран
        var now = DateTime.Now;
        Native.GetCursorPos(out var p);
        if (_tucked)
        {
            if (!InHotZone(p)) { _inSince = null; return; }
            _inSince ??= now;
            if (now - _inSince >= RevealAfter) Reveal();
            return;
        }
        // Открыт, показывает уведомление, его тащат, в него печатают — не прячемся
        bool busy = _mode != Mode.Idle || _pressed || _dragging || _dropHover || _dragOut || _keyOpen || _typing;
        if (busy || InHotZone(p) || CursorOverIsland()) { _outSince = null; return; }
        _outSince ??= now;
        if (now - _outSince >= HideAfter) Tuck();
    }

    bool InHotZone(Native.POINT p)
    {
        Native.GetWindowRect(_hwnd, out var r);
        double k = VisualTreeHelper.GetDpi(this).DpiScaleX * UiScale;
        return HotZone(r, k, Island.Margin.Top, Island.ActualWidth, Island.ActualHeight, AppState.Current.Docked).Contains(p.X, p.Y);
    }

    // Где курсор «зовёт» спрятанный остров (пиксели экрана). Прикреплённый — полоса у верхнего края
    // над островом: курсор, брошенный вверх, упирается в край как раз там. Свободный — место самого острова.
    // k — пикселей экрана в одной точке острова (масштаб Windows × размер острова из меню).
    internal static Rect HotZone(Native.RECT window, double k, double marginTop, double islandW, double islandH, bool docked)
    {
        double cx = (window.Left + window.Right) / 2.0;
        double w = (islandW + 2 * HotPad) * k;
        return docked
            ? new Rect(cx - w / 2, window.Top - 4 * k, w, (HotStrip + 4) * k)
            : new Rect(cx - w / 2, window.Top + (marginTop - 6) * k, w, (islandH + 12) * k);
    }

    void Tuck()
    {
        if (_tucked) return;
        _tucked = true;
        _inSince = null;
        _hoverIn.Stop();
        SetClickThrough(true);
        UpdateFast(); // эквалайзер и строки песни не рисуем — их всё равно не видно
        // Прикреплённый уезжает вверх целиком вместе с тенью; свободный тает, чуть приподнимаясь
        double y = AppState.Current.Docked ? -(Island.Margin.Top + Island.ActualHeight + 32) : -6;
        Slide(y, 0, reveal: false);
        FadeHandle(true);
    }

    void Reveal()
    {
        if (!_tucked) return;
        _tucked = false;
        _outSince = _inSince = null;
        SetClickThrough(false);
        if (_lyrics.Lines != null) _lyricIndex = _lyrics.IndexAt(_media.Position + LyricLead);
        RefreshLyricTexts();
        if (_mode == Mode.Idle)
        {
            RefreshIdle();
            AnimateTo(IdleSize(), gentle: true);
        }
        UpdateFast();
        Slide(0, 1, reveal: true);
        FadeHandle(false);
    }

    void Slide(double y, double opacity, bool reveal)
    {
        if (_instant)
        {
            SlideShift.BeginAnimation(TranslateTransform.YProperty, null);
            SlideShift.Y = y;
            SlideHost.BeginAnimation(OpacityProperty, null);
            SlideHost.Opacity = opacity;
            return;
        }
        IEasingFunction ease = reveal ? new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }
                                      : new CubicEase { EasingMode = EasingMode.EaseIn };
        int ms = reveal ? 360 : 260;
        SlideShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease });
        SlideHost.BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(reveal ? 200 : ms)));
    }

    void FadeHandle(bool show)
    {
        double to = show ? 0.9 : 0;
        if (_instant) { Handle.BeginAnimation(OpacityProperty, null); Handle.Opacity = to; return; }
        Handle.BeginAnimation(OpacityProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(show ? 220 : 120))
        { BeginTime = TimeSpan.FromMilliseconds(show ? 160 : 0) });
    }

    // Спрятанный остров не ловит мышь: щелчки и колесо уходят окну под ним
    void SetClickThrough(bool on)
    {
        if (_hwnd == IntPtr.Zero) return;
        int ex = Native.GetWindowLong(_hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLong(_hwnd, Native.GWL_EXSTYLE, on ? ex | Native.WS_EX_TRANSPARENT : ex & ~Native.WS_EX_TRANSPARENT);
    }

    bool ClickThrough => _hwnd != IntPtr.Zero && (Native.GetWindowLong(_hwnd, Native.GWL_EXSTYLE) & Native.WS_EX_TRANSPARENT) != 0;

    // Полоска стоит там, где остров: у прикреплённого — у самого края (у выреза — слита с ним),
    // у свободного — посередине его высоты
    void ApplyHandleShape()
    {
        bool notch = Notch;
        Handle.Margin = new Thickness(0, notch ? 0 : AppState.Current.Docked ? 3 : 6 + IdleBase.Height / 2 - 3, 0, 0);
        Handle.CornerRadius = notch ? new CornerRadius(0, 0, 3, 3) : new CornerRadius(3);
        Handle.BorderThickness = notch ? new Thickness(1, 0, 1, 1) : new Thickness(1);
    }
}
