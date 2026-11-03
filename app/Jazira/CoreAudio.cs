using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Jazira;

// Звук на выходе по умолчанию (динамики, наушники): громкость и текущий уровень сигнала.
// Системный Core Audio напрямую — без сторонних библиотек.
sealed class AudioWatch
{
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    IMMDeviceEnumerator? _enum;
    IAudioEndpointVolume? _vol;
    IAudioMeterInformation? _meter;
    string? _deviceId;
    int _ticks;
    bool _known;

    public float Volume { get; private set; }
    public bool Muted { get; private set; }
    public event Action? VolumeChanged;

    // Следить за громкостью (индикатор) — отдельно от уровня сигнала для эквалайзера
    public bool WatchVolume { get => _timer.IsEnabled; set => _timer.IsEnabled = value && _enum != null; }

    public AudioWatch()
    {
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))!;
            _enum = (IMMDeviceEnumerator)Activator.CreateInstance(type)!;
            Attach();
        }
        catch (Exception ex) { App.Log(ex); _enum = null; }
        _timer.Tick += (_, _) => Poll();
    }

    void Attach()
    {
        _vol = null;
        _meter = null;
        _deviceId = null;
        if (_enum == null) return;
        try
        {
            _enum.GetDefaultAudioEndpoint(0 /* вывод */, 1 /* мультимедиа */, out var dev);
            dev.GetId(out var id);
            _deviceId = id;
            var iidVol = typeof(IAudioEndpointVolume).GUID;
            dev.Activate(ref iidVol, 23, IntPtr.Zero, out var vol);
            _vol = (IAudioEndpointVolume)vol;
            var iidMeter = typeof(IAudioMeterInformation).GUID;
            dev.Activate(ref iidMeter, 23, IntPtr.Zero, out var meter);
            _meter = (IAudioMeterInformation)meter;
            Read(out var v, out var m);
            Volume = v;
            Muted = m;
            _known = true;
        }
        catch (Exception) { _vol = null; _meter = null; } // нет устройства вывода — индикатора нет
    }

    void Read(out float volume, out bool muted)
    {
        volume = 0;
        muted = false;
        if (_vol == null) return;
        _vol.GetMasterVolumeLevelScalar(out volume);
        _vol.GetMute(out muted);
    }

    // Четыре раза в секунду: изменилась ли громкость. Раз в три секунды — не сменилось ли устройство.
    void Poll()
    {
        try
        {
            if (++_ticks % 12 == 0 && _enum != null)
            {
                _enum.GetDefaultAudioEndpoint(0, 1, out var dev);
                dev.GetId(out var id);
                if (id != _deviceId) { Attach(); return; } // подключили наушники — новая громкость не «изменение»
            }
            if (_vol == null) return;
            Read(out var v, out var m);
            bool changed = _known && (Math.Abs(v - Volume) > 0.004 || m != Muted);
            Volume = v;
            Muted = m;
            _known = true;
            if (changed) VolumeChanged?.Invoke();
        }
        catch (Exception) { Attach(); }
    }

    // Уровень сигнала 0…1 для эквалайзера; -1 — узнать нельзя
    public float Peak()
    {
        try
        {
            if (_meter == null) return -1;
            _meter.GetPeakValue(out var p);
            return p;
        }
        catch (Exception) { _meter = null; return -1; }
    }
}

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator
{
    void EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
    void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice
{
    void Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    void OpenPropertyStore(int access, out IntPtr properties);
    void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
}

[ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioMeterInformation
{
    void GetPeakValue(out float peak);
}

[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioEndpointVolume
{
    void RegisterControlChangeNotify(IntPtr notify);
    void UnregisterControlChangeNotify(IntPtr notify);
    void GetChannelCount(out uint count);
    void SetMasterVolumeLevel(float levelDb, ref Guid context);
    void SetMasterVolumeLevelScalar(float level, ref Guid context);
    void GetMasterVolumeLevel(out float levelDb);
    void GetMasterVolumeLevelScalar(out float level);
    void SetChannelVolumeLevel(uint channel, float levelDb, ref Guid context);
    void SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
    void GetChannelVolumeLevel(uint channel, out float levelDb);
    void GetChannelVolumeLevelScalar(uint channel, out float level);
    void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
    void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}
