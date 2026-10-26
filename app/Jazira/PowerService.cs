using System;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Jazira;

// Батарея и зарядка. Windows сама сообщает о подключении и отключении питания — опрашивать не нужно.
sealed class PowerService
{
    readonly Dispatcher _ui;

    public bool HasBattery { get; private set; }
    public bool Charging { get; private set; }  // подключено питание
    public int Percent { get; private set; }

    public event Action? Changed;
    public event Action<bool>? PlugChanged;     // true — зарядку подключили

    public PowerService(Dispatcher ui)
    {
        _ui = ui;
        Read();
        SystemEvents.PowerModeChanged += OnPower;
    }

    void OnPower(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.StatusChange) return;
        _ui.InvokeAsync(() =>
        {
            bool was = Charging;
            Read();
            Changed?.Invoke();
            if (HasBattery && was != Charging) PlugChanged?.Invoke(Charging);
        });
    }

    // Процент меняется без событий — остров перечитывает его раз в минуту
    public void Read()
    {
        if (_fake) return;
        var p = Forms.SystemInformation.PowerStatus;
        HasBattery = (p.BatteryChargeStatus & Forms.BatteryChargeStatus.NoSystemBattery) == 0 && p.BatteryLifePercent <= 1f;
        Charging = p.PowerLineStatus == Forms.PowerLineStatus.Online;
        Percent = (int)Math.Round(p.BatteryLifePercent * 100);
    }

    // ---- Подставные данные для снимков ----
    bool _fake;

    public void Fake(bool hasBattery, bool charging, int percent)
    {
        _fake = true;
        HasBattery = hasBattery; Charging = charging; Percent = percent;
    }
}
