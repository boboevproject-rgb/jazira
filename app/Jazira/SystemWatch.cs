using System;
using System.Globalization;
using System.IO;
using System.Management;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Jazira;

// Индикаторы системы: раскладка клавиатуры, Caps Lock, кто держит камеру и микрофон, яркость экрана.
// Раскладка и Caps Lock — опрос 3 раза в секунду (это дёшево), камера и микрофон — раз в 2 секунды,
// яркость — событие Windows (WMI), есть только у встроенных экранов ноутбуков.
sealed class SystemWatch
{
    readonly Dispatcher _ui;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    IntPtr _fgWindow;
    int _lang = -1;
    bool? _caps;
    int _ticks;
    ManagementEventWatcher? _brightness;

    public string? CameraApp { get; private set; }
    public string? MicApp { get; private set; }

    public event Action<CultureInfo>? LayoutChanged;
    public event Action<bool>? CapsChanged;
    public event Action<string?, string?>? PrivacyChanged; // камера стала занята (app) / микрофон стал занят (app)
    public event Action<int>? BrightnessChanged;

    public SystemWatch(Dispatcher ui)
    {
        _ui = ui;
        _timer.Tick += (_, _) => Poll();
    }

    public bool Enabled
    {
        get => _timer.IsEnabled;
        set
        {
            if (value == _timer.IsEnabled) return;
            _timer.IsEnabled = value;
            if (value)
            {
                _lang = -1; _caps = null; // после включения первое чтение — не «изменение»
                ReadPrivacy(silent: true);
                if (_brightness == null) Task.Run(StartBrightness);
            }
            else
            {
                CameraApp = MicApp = null;
                PrivacyChanged?.Invoke(null, null);
            }
        }
    }

    void Poll()
    {
        // Раскладка: событие только если переключили в том же окне — смена окна не считается
        var fg = Native.GetForegroundWindow();
        uint tid = Native.GetWindowThreadProcessId(fg, out _);
        int lang = (int)((long)Native.GetKeyboardLayout(tid) & 0xFFFF);
        if (lang != 0)
        {
            if (fg == _fgWindow && _lang > 0 && lang != _lang) LayoutChanged?.Invoke(Culture(lang));
            _fgWindow = fg;
            _lang = lang;
        }

        bool caps = (Native.GetKeyState(0x14) & 1) != 0;
        if (_caps is bool was && was != caps) CapsChanged?.Invoke(caps);
        _caps = caps;

        if (++_ticks % 7 == 0) ReadPrivacy(silent: false);
    }

    void ReadPrivacy(bool silent)
    {
        string? cam = InUse("webcam"), mic = InUse("microphone");
        bool camStarted = cam != null && CameraApp == null, micStarted = mic != null && MicApp == null;
        bool changed = cam != CameraApp || mic != MicApp;
        CameraApp = cam;
        MicApp = mic;
        if (changed) PrivacyChanged?.Invoke(!silent && camStarted ? cam : null, !silent && micStarted ? mic : null);
    }

    // Windows сама записывает, какое приложение сейчас пользуется камерой или микрофоном:
    // у такого приложения в журнале есть время начала и ещё нет времени конца.
    static string? InUse(string capability)
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\" + capability);
            if (root == null) return null;
            foreach (var name in root.GetSubKeyNames())
            {
                using var key = root.OpenSubKey(name);
                if (key == null) continue;
                if (name == "NonPackaged")
                {
                    foreach (var app in key.GetSubKeyNames())
                    {
                        using var k = key.OpenSubKey(app);
                        if (Active(k)) return Pretty(app, packaged: false);
                    }
                }
                else if (Active(key)) return Pretty(name, packaged: true);
            }
        }
        catch (Exception) { }
        return null;
    }

    static bool Active(RegistryKey? k) =>
        k?.GetValue("LastUsedTimeStart") is long start && start != 0 && k.GetValue("LastUsedTimeStop") is long stop && stop == 0;

    // «C:#Program Files#Zoom#bin#Zoom.exe» → «Zoom»; «Microsoft.WindowsCamera_8wekyb3d8bbwe» → «WindowsCamera»
    static string Pretty(string id, bool packaged)
    {
        if (!packaged) return Path.GetFileNameWithoutExtension(id.Replace('#', '\\'));
        var name = id.Split('_')[0];
        int dot = name.LastIndexOf('.');
        return dot >= 0 ? name[(dot + 1)..] : name;
    }

    static CultureInfo Culture(int lang)
    {
        try { return CultureInfo.GetCultureInfo(lang); }
        catch (Exception) { return CultureInfo.InvariantCulture; }
    }

    void StartBrightness()
    {
        try
        {
            var w = new ManagementEventWatcher(new ManagementScope(@"root\WMI"), new EventQuery("SELECT * FROM WmiMonitorBrightnessEvent"));
            w.EventArrived += (_, e) =>
            {
                int v = Convert.ToInt32(e.NewEvent["Brightness"], CultureInfo.InvariantCulture);
                _ui.InvokeAsync(() => { if (Enabled) BrightnessChanged?.Invoke(v); });
            };
            w.Start();
            _brightness = w;
        }
        catch (Exception) { } // настольный монитор: Windows не знает его яркость — индикатора нет
    }
}
