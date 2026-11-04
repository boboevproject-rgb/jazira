using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Jazira;

// Страница «Система» (процессор, память, нагрев, сеть, износ батареи), предупреждение о батарее 10 %
// и горячая клавиша, которая открывает остров и даёт управлять им с клавиатуры: ← → — страницы, Esc — закрыть.
public partial class IslandWindow
{
    const string IslandKey = "Ctrl+Alt+J";
    const uint VK_J = 0x4A;
    static readonly Geometry BatteryGeo = G("M3.5 7h14a1.5 1.5 0 0 1 1.5 1.5v7a1.5 1.5 0 0 1-1.5 1.5h-14A1.5 1.5 0 0 1 2 15.5v-7A1.5 1.5 0 0 1 3.5 7z M20 10h2v4h-2z");

    readonly SysMonitor _sysmon = new();
    bool _lowWarned, _keyOpen;
    IntPtr _prevFg;
    int _sysTicks;

    public bool IslandKeyOk { get; private set; } = true;

    public string IslandKeyMenuText() =>
        $"{Strings.Get("tray.open")} · {IslandKey}" + (IslandKeyOk ? "" : $" — {Strings.Get("hotkey.busy")}");

    // ---- Опрос ---------------------------------------------------------------------------------

    // Каждую секунду: процессор — всегда (для графика за минуту), остальное — пока страница открыта
    void SysTick()
    {
        _sysmon.SampleCpu();
        if (_mode != Mode.Expanded || PageName != "sys") return;
        _sysmon.SampleMemory();
        _sysmon.SampleNetwork();
        if (++_sysTicks % 5 == 0) _sysmon.SampleHeat();
        RefreshSys();
    }

    // Страницу только что открыли — сразу свежие цифры
    void SysOpened()
    {
        if (_instant) return;
        _sysTicks = 0;
        _sysmon.SampleMemory();
        _sysmon.SampleNetwork();
        _sysmon.SampleHeat();
        _sysmon.SampleBattery();
    }

    // ---- Вид -----------------------------------------------------------------------------------

    void RefreshSys()
    {
        var m = _sysmon;
        SysTitle.Text = Strings.Get("sys.title");
        SysUptime.Text = Strings.Format("sys.uptime", Uptime(_instant ? TimeSpan.FromHours(77.3) : TimeSpan.FromMilliseconds(Environment.TickCount64)));

        SysTiles.Children.Clear();
        double cpu = Math.Max(0, m.Cpu);
        SysTiles.Children.Add(Tile(m.Cpu < 0 ? "—" : $"{Math.Round(cpu * 100)}%", Strings.Get("sys.cpu"), null,
                                   cpu > 0.85 ? Res("Coral") : null, Spark(m.CpuHistory, Res("Lagoon"))));

        double gib = 1024.0 * 1024 * 1024;
        var c = Strings.Culture;
        string memOf = Strings.Format("sys.memOf", (m.MemUsed / gib).ToString("0.0", c), Math.Round(m.MemTotal / gib).ToString("0", c));
        SysTiles.Children.Add(Tile($"{Math.Round(m.MemLoad * 100)}%", Strings.Get("sys.mem"), memOf,
                                   m.MemLoad > 0.85 ? Res("Amber") : null, Bar(m.MemLoad, m.MemLoad > 0.85 ? Res("Amber") : Res("Sky"))));

        string heat, heatSub;
        Brush? heatBrush = null;
        if (!m.HeatLoaded) { heat = "…"; heatSub = ""; }
        else if (!m.HeatKnown) { heat = "—"; heatSub = Strings.Get("sys.noHeat"); }
        else
        {
            heat = m.TempC is double t ? $"{Math.Round(t)}°" : Strings.Get(m.Throttling ? "sys.heatHot" : "sys.heatOk");
            heatSub = Strings.Get(m.Throttling ? "sys.freqLow" : "sys.freqOk");
            if (m.Throttling) heatBrush = Res("Coral");
        }
        var heatTile = Tile(heat, Strings.Get("sys.heat"), heatSub, heatBrush, null);
        heatTile.ToolTip = Strings.Get("sys.heatTip");
        SysTiles.Children.Add(heatTile);

        SysTiles.Children.Add(Tile(m.DownBps is double d ? Speed(d) : "—", Strings.Get("sys.net"),
                                   m.UpBps is double u ? "↑ " + Speed(u) : null, null, null));

        if (_power.HasBattery || m.Battery != null)
        {
            SysBattery.Visibility = Visibility.Visible;
            SysBattery.Text = BatteryText(m.Battery);
        }
        else SysBattery.Visibility = Visibility.Collapsed;
    }

    static string BatteryText(BatteryHealth.Info? b)
    {
        if (b == null || b.FullMWh <= 0) return Strings.Get("sys.batteryUnknown");
        var c = Strings.Culture;
        string full = (b.FullMWh / 1000.0).ToString("0.0", c);
        string text = b.Wear is double w
            ? Strings.Format("sys.battery", Math.Round(w * 100), full, (b.DesignedMWh / 1000.0).ToString("0.0", c))
            : Strings.Format("sys.batteryFull", full);
        if (b.Cycles > 0) text += Strings.Format("sys.cycles", b.Cycles);
        return text;
    }

    Size SysSize() => new(420, SysBattery.Visibility == Visibility.Visible ? 146 : 124);

    FrameworkElement Tile(string value, string caption, string? detail, Brush? valueBrush, FrameworkElement? graph)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
        var v = new TextBlock
        {
            Text = value, FontSize = 17, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis,
            FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
        };
        if (valueBrush != null) v.Foreground = valueBrush; else v.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
        stack.Children.Add(v);
        var cap = new TextBlock { Text = caption, FontSize = 11, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        cap.SetResourceReference(TextBlock.ForegroundProperty, "Dim");
        stack.Children.Add(cap);
        var det = new TextBlock { Text = detail ?? "", FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis };
        det.SetResourceReference(TextBlock.ForegroundProperty, "Dim");
        det.Opacity = 0.8;
        stack.Children.Add(det);
        var slot = new Grid { Height = 16, Margin = new Thickness(0, 4, 0, 0) };
        if (graph != null) slot.Children.Add(graph);
        stack.Children.Add(slot);
        return stack;
    }

    // График загрузки процессора за последнюю минуту
    static FrameworkElement Spark(IReadOnlyCollection<double> values, Brush stroke)
    {
        const double w = 76, h = 16;
        int n = SysMonitor.History, i = n - values.Count;
        var pts = new PointCollection();
        foreach (var v in values)
        {
            pts.Add(new Point(i * w / (n - 1), h - 1 - v * (h - 2)));
            i++;
        }
        var canvas = new Canvas { Width = w, Height = h, ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Left };
        if (pts.Count < 2) return canvas;
        var color = ((SolidColorBrush)stroke).Color;
        var area = new PointCollection(pts) { new Point(pts[^1].X, h), new Point(pts[0].X, h) };
        canvas.Children.Add(new Polygon { Points = area, Fill = Theme.Brush(color, 0x33) });
        canvas.Children.Add(new Polyline { Points = pts, Stroke = stroke, StrokeThickness = 1.4, StrokeLineJoin = PenLineJoin.Round });
        return canvas;
    }

    FrameworkElement Bar(double f, Brush fill)
    {
        const double w = 76;
        var grid = new Grid { Width = w, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        var track = new Border { Height = 5, CornerRadius = new CornerRadius(2.5) };
        track.SetResourceReference(Border.BackgroundProperty, "Track");
        grid.Children.Add(track);
        grid.Children.Add(new Border
        {
            Height = 5, CornerRadius = new CornerRadius(2.5), Background = fill,
            Width = Math.Max(5, w * Math.Clamp(f, 0, 1)), HorizontalAlignment = HorizontalAlignment.Left,
        });
        return grid;
    }

    static string Speed(double bytesPerSecond) => Strings.Format("sys.speed", Bytes((long)bytesPerSecond));

    static string Uptime(TimeSpan t) =>
        t.TotalDays >= 1 ? $"{Strings.DaysShort((int)t.TotalDays)} {t.Hours} {Strings.Get("h")}"
                         : $"{(int)t.TotalHours} {Strings.Get("h")} {t.Minutes} {Strings.Get("min")}";

    // ---- Батарея 10 % ------------------------------------------------------------------------

    void CheckLowBattery()
    {
        if (!_power.HasBattery) return;
        if (_power.Charging || _power.Percent > 15) { _lowWarned = false; return; } // зарядили — в следующий раз снова предупредим
        if (_power.Percent > 10 || _lowWarned || !AppState.Current.LowBattery) return;
        _lowWarned = true;
        if (!_instant) Chime.Play();
        Alert("battery", Res("Coral"), BatteryGeo, Strings.Format("battery.low", _power.Percent), Strings.Get("battery.plug"), 20);
    }

    // ---- Горячая клавиша острова и управление с клавиатуры -----------------------------------

    void ApplyIslandKey()
    {
        if (_keys == null) return;
        IslandKeyOk = _keys.Set("island", HotKeys.Ctrl | HotKeys.Alt, VK_J, OpenByKey);
    }

    // Ctrl+Alt+J: открыть остров (ещё раз — закрыть). Остров берёт клавиатуру: ← → листают страницы,
    // пробел на странице музыки — пауза, Esc или щелчок мимо — закрыть и вернуть клавиатуру вашему окну.
    public void OpenByKey()
    {
        if (_instant) return;
        if (_mode == Mode.Expanded) { Collapse(); return; }
        if (!AppState.Current.ShowIsland)
        {
            AppState.Current.ShowIsland = true; // остров выключали — клавиша возвращает его
            AppState.Save();
            SettingsChanged?.Invoke();
        }
        ApplyVisibility();
        if (!IsVisible) return; // полноэкранная игра или фильм — не мешаем
        Expand();
        _keyOpen = true;
        TakeKeyboard();
        Focus();
    }

    void Island_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_keyOpen || _typing || _mode != Mode.Expanded) return;
        switch (e.Key)
        {
            case Key.Left: GoPage(-1); e.Handled = true; break;
            case Key.Right: GoPage(1); e.Handled = true; break;
            case Key.Escape: Collapse(); e.Handled = true; break;
            case Key.Space when PageName == "music": _media.PlayPause(); e.Handled = true; break;
        }
    }

    // Снять «не активировать» и стать активным окном; запоминаем, чьё окно было активным
    void TakeKeyboard()
    {
        if (_hwnd == IntPtr.Zero) return;
        var fg = Native.GetForegroundWindow();
        if (fg != _hwnd) _prevFg = fg;
        Native.SetWindowLong(_hwnd, Native.GWL_EXSTYLE, Native.GetWindowLong(_hwnd, Native.GWL_EXSTYLE) & ~Native.WS_EX_NOACTIVATE);
        Activate();
    }

    // Вернуть «не активировать» и отдать клавиатуру окну, в котором вы были (если остров всё ещё активен)
    void ReleaseKeyboard()
    {
        if (_hwnd == IntPtr.Zero) return;
        Native.SetWindowLong(_hwnd, Native.GWL_EXSTYLE, Native.GetWindowLong(_hwnd, Native.GWL_EXSTYLE) | Native.WS_EX_NOACTIVATE);
        Keyboard.ClearFocus();
        var prev = _prevFg;
        _prevFg = IntPtr.Zero;
        if (prev != IntPtr.Zero && Native.GetForegroundWindow() == _hwnd) Native.SetForegroundWindow(prev);
    }
}
