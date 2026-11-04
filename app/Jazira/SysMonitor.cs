using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Jazira;

// Страница «Система»: процессор, память, нагрев, сеть, износ батареи. Всё берётся у самой Windows —
// без драйверов и без прав администратора. Процессор опрашивается раз в секунду всегда (это доли
// микросекунды — нужно для графика за минуту), остальное — только пока страница открыта.
sealed class SysMonitor
{
    public const int History = 60;

    readonly Queue<double> _cpuHistory = new();
    ulong _idle, _kernel, _user;
    long _rx = -1, _tx = -1;
    DateTime _netAt;
    double? _firstTemp;
    bool _heatBusy;
    DateTime _batteryAt;

    public double Cpu { get; private set; } = -1;         // 0…1; -1 — ещё не знаем
    public IReadOnlyCollection<double> CpuHistory => _cpuHistory;
    public double MemLoad { get; private set; }           // 0…1
    public ulong MemTotal { get; private set; }
    public ulong MemUsed { get; private set; }
    public double? DownBps { get; private set; }
    public double? UpBps { get; private set; }
    public bool HeatKnown { get; private set; }           // Windows вообще сообщает о нагреве
    public double? TempC { get; private set; }            // только если датчик похож на настоящий
    public bool Throttling { get; private set; }          // процессор сбавляет частоту из-за нагрева
    public BatteryHealth.Info? Battery { get; private set; }

    public event Action? HeatChanged;

    public void SampleCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return;
        ulong i = idle.Value, k = kernel.Value, u = user.Value; // время ядра уже включает простой
        if (_kernel != 0)
        {
            ulong total = (k - _kernel) + (u - _user);
            if (total > 0)
            {
                Cpu = Math.Clamp(1 - (double)(i - _idle) / total, 0, 1);
                _cpuHistory.Enqueue(Cpu);
                while (_cpuHistory.Count > History) _cpuHistory.Dequeue();
            }
        }
        _idle = i; _kernel = k; _user = u;
    }

    public void SampleMemory()
    {
        var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref m)) return;
        MemTotal = m.ullTotalPhys;
        MemUsed = m.ullTotalPhys - m.ullAvailPhys;
        MemLoad = MemTotal > 0 ? (double)MemUsed / MemTotal : 0;
    }

    // Скорость сети — сумма по всем подключённым адаптерам, кроме петли и туннелей
    public void SampleNetwork()
    {
        long rx = 0, tx = 0;
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                var s = ni.GetIPStatistics();
                rx += s.BytesReceived;
                tx += s.BytesSent;
            }
        }
        catch (Exception) { return; }
        var now = DateTime.UtcNow;
        double sec = (now - _netAt).TotalSeconds;
        if (_rx >= 0 && sec > 0.2 && sec < 10 && rx >= _rx && tx >= _tx)
        {
            DownBps = (rx - _rx) / sec;
            UpBps = (tx - _tx) / sec;
        }
        else if (sec >= 10) { DownBps = UpBps = null; } // страницу долго не открывали — старое число не показываем
        _rx = rx; _tx = tx; _netAt = now;
    }

    // Нагрев: тепловые зоны ACPI из счётчиков Windows. Часто это не ядро процессора, а плата, и у многих
    // ноутбуков зона всегда показывает одно и то же (например, 27,8 °C) — такую температуру не показываем.
    // Зато «процент пассивного предела» < 100 честно говорит, что процессор тормозит из-за нагрева.
    public void SampleHeat()
    {
        if (_heatBusy) return;
        _heatBusy = true;
        Task.Run(() =>
        {
            double? max = null;
            int limit = 100;
            bool known = false;
            try
            {
                using var q = new ManagementObjectSearcher(@"root\cimv2",
                    "SELECT HighPrecisionTemperature, PercentPassiveLimit FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation");
                foreach (ManagementObject o in q.Get())
                {
                    known = true;
                    double k = Convert.ToDouble(o["HighPrecisionTemperature"]) / 10; // десятые доли кельвина
                    double c = k - 273.15;
                    if (c > -20 && c < 130 && (max == null || c > max)) max = c;
                    limit = Math.Min(limit, Convert.ToInt32(o["PercentPassiveLimit"]));
                }
            }
            catch (Exception) { }
            return (known, max, limit);
        }).ContinueWith(t =>
        {
            _heatBusy = false;
            var (known, max, limit) = t.Result;
            HeatKnown = known;
            Throttling = known && limit < 100;
            if (max is double c)
            {
                _firstTemp ??= c;
                bool real = Math.Abs(c - _firstTemp.Value) > 0.5 || c >= 35; // меняется или похожа на рабочую
                TempC = real ? c : null;
            }
            HeatChanged?.Invoke();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    // Ёмкость батареи меняется медленно — читаем при открытии страницы и не чаще раза в 10 минут
    public void SampleBattery()
    {
        if (DateTime.Now - _batteryAt < TimeSpan.FromMinutes(10) && _batteryAt != default) return;
        _batteryAt = DateTime.Now;
        Battery = BatteryHealth.Read();
    }

    // ---- Подставные данные для снимков ----
    public void Fake(double cpu, double mem, ulong total, double? down, double? up, double? temp, bool throttling, BatteryHealth.Info? battery)
    {
        _cpuHistory.Clear();
        var rnd = new Random(7);
        for (int i = 0; i < History; i++)
            _cpuHistory.Enqueue(Math.Clamp(cpu * (0.55 + 0.45 * Math.Sin(i / 6.0) * Math.Sin(i / 2.3)) + rnd.NextDouble() * 0.08, 0.02, 1));
        Cpu = cpu;
        MemTotal = total; MemUsed = (ulong)(total * mem); MemLoad = mem;
        DownBps = down; UpBps = up;
        HeatKnown = true; TempC = temp; Throttling = throttling;
        Battery = battery;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct FILETIME64 { public uint Low, High; public ulong Value => ((ulong)High << 32) | Low; }

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out FILETIME64 idle, out FILETIME64 kernel, out FILETIME64 user);
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);
}

// Износ батареи: заводская ёмкость и сколько батарея держит сейчас — тот же источник, что у
// «powercfg /batteryreport», только без прав администратора (запрос к драйверу батареи)
static class BatteryHealth
{
    public sealed record Info(int DesignedMWh, int FullMWh, int Cycles)
    {
        public double? Wear => DesignedMWh > 0 && FullMWh > 0 ? Math.Clamp(1 - (double)FullMWh / DesignedMWh, 0, 1) : null;
    }

    static Guid BatteryClass = new("72631E54-78A4-11D0-BCF7-00AA00B7B32A");
    const uint DIGCF_PRESENT = 0x2, DIGCF_DEVICEINTERFACE = 0x10;
    const uint IOCTL_BATTERY_QUERY_TAG = 0x294040, IOCTL_BATTERY_QUERY_INFORMATION = 0x294044;
    const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000, FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3;
    const uint BATTERY_SYSTEM_BATTERY = 0x80000000, BATTERY_CAPACITY_RELATIVE = 0x40000000;

    public static Info? Read()
    {
        try
        {
            IntPtr set = SetupDiGetClassDevs(ref BatteryClass, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (set == new IntPtr(-1)) return null;
            try
            {
                for (uint i = 0; i < 8; i++)
                {
                    var did = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref BatteryClass, i, ref did)) break;
                    SetupDiGetDeviceInterfaceDetail(set, ref did, IntPtr.Zero, 0, out uint need, IntPtr.Zero);
                    if (need == 0) continue;
                    IntPtr buf = Marshal.AllocHGlobal((int)need);
                    try
                    {
                        Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6); // размер заголовка SP_DEVICE_INTERFACE_DETAIL_DATA_W
                        if (!SetupDiGetDeviceInterfaceDetail(set, ref did, buf, need, out _, IntPtr.Zero)) continue;
                        var path = Marshal.PtrToStringUni(buf + 4);
                        if (path == null) continue;
                        using var h = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                        if (h.IsInvalid) continue;
                        uint wait = 0;
                        if (!DeviceIoControl(h, IOCTL_BATTERY_QUERY_TAG, ref wait, 4, out uint tag, 4, out _, IntPtr.Zero) || tag == 0) continue;
                        var q = new BATTERY_QUERY_INFORMATION { BatteryTag = tag, InformationLevel = 0 };
                        if (!DeviceIoControl(h, IOCTL_BATTERY_QUERY_INFORMATION, ref q, Marshal.SizeOf<BATTERY_QUERY_INFORMATION>(),
                                             out BATTERY_INFORMATION bi, Marshal.SizeOf<BATTERY_INFORMATION>(), out _, IntPtr.Zero)) continue;
                        if ((bi.Capabilities & BATTERY_SYSTEM_BATTERY) == 0) continue;     // батарея мыши или ИБП — не наша
                        if ((bi.Capabilities & BATTERY_CAPACITY_RELATIVE) != 0) return null; // ёмкость не в мВт·ч — износ не посчитать
                        return new Info((int)bi.DesignedCapacity, (int)bi.FullChargedCapacity, (int)bi.CycleCount);
                    }
                    finally { Marshal.FreeHGlobal(buf); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
        }
        catch (Exception ex) { App.Log(ex); }
        return null;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }

    [StructLayout(LayoutKind.Sequential)]
    struct BATTERY_QUERY_INFORMATION { public uint BatteryTag; public int InformationLevel; public int AtRate; }

    [StructLayout(LayoutKind.Sequential)]
    struct BATTERY_INFORMATION
    {
        public uint Capabilities;
        public byte Technology, Reserved1, Reserved2, Reserved3;
        public uint Chemistry;
        public uint DesignedCapacity, FullChargedCapacity, DefaultAlert1, DefaultAlert2, CriticalBias, CycleCount;
    }

    [DllImport("setupapi.dll", SetLastError = true)]
    static extern IntPtr SetupDiGetClassDevs(ref Guid cls, IntPtr enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfo, ref Guid cls, uint index, ref SP_DEVICE_INTERFACE_DATA data);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detail, uint size, out uint required, IntPtr devInfo);
    [DllImport("setupapi.dll")]
    static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(SafeFileHandle h, uint code, ref uint input, int inSize, out uint output, int outSize, out uint returned, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(SafeFileHandle h, uint code, ref BATTERY_QUERY_INFORMATION input, int inSize, out BATTERY_INFORMATION output, int outSize, out uint returned, IntPtr overlapped);
}
