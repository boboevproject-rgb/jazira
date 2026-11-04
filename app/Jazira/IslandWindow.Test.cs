using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Forms = System.Windows.Forms;

namespace Jazira;

// Jazira.exe --live-test <папка>: живая проверка без мыши и клавиатуры, рядом с уже запущенным островом.
// Окно стоит за краем экрана, настройки не сохраняются, буфер обмена и ваши папки не трогаются.
// Проверяет: горячие клавиши на настоящем окне, что остров не попадает в снимок экрана (на 0,4 с
// встаёт в правый нижний угол), настоящий календарь Google по публичной ссылке, батарею и нагрев.
// Потом просто работает в покое — снаружи меряют процессор и память.
public partial class IslandWindow
{
    public bool TestOffscreen { get; init; }

    public async Task RunLiveTest(string dir, int idleSeconds)
    {
        var log = new StringBuilder();
        var path = Path.Combine(dir, "live-test.txt");
        void Log(string s)
        {
            log.AppendLine($"{DateTime.Now:HH:mm:ss} {s}");
            File.WriteAllText(path, log.ToString());
        }

        await Task.Delay(1500);
        Log($"window ok={_hwnd != IntPtr.Zero}; hotkeys: Ctrl+PrtScn {(ShotKeyOk ? "registered" : "BUSY")}, Ctrl+Alt+J {(IslandKeyOk ? "registered" : "BUSY")}");
        _keys?.Remove("shot");   // клавиши проверили — сразу отпускаем, чтобы тестовая копия ничего не сняла
        _keys?.Remove("island");
        Log($"watching downloads: {_downloads?.Dir} ({_downloads?.Enabled}); screenshots: {_shots?.Dir} ({_shots?.Enabled})");

        // Остров не должен попадать в снимок: снимаем угол экрана с островом и с исключением
        if (Native.FullscreenAppActive()) Log("exclude: skipped, a fullscreen app is active");
        else
        {
            var wa = (Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0]).WorkingArea;
            Native.GetWindowRect(_hwnd, out var r);
            int w = r.Right - r.Left, h = r.Bottom - r.Top, x = wa.Right - w, y = wa.Bottom - h;
            Native.SetWindowPos(_hwnd, IntPtr.Zero, x, y, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            await Task.Delay(250);
            using var shown = Grab(x, y, w, h);
            bool set = Native.SetWindowDisplayAffinity(_hwnd, Native.WDA_EXCLUDEFROMCAPTURE);
            await Task.Delay(120);
            using var hidden = Grab(x, y, w, h);
            Native.SetWindowDisplayAffinity(_hwnd, Native.WDA_NONE);
            Place(); // обратно за край экрана
            int diff = Diff(shown, hidden);
            Log($"exclude: affinity set={set}, pixels that changed {diff} of {w * h / 4} sampled → {(set && diff > 2000 ? "island excluded from capture" : "NOT excluded, fallback to fading")}");
        }

        // Настоящий календарь Google: публичный календарь праздников, только чтение
        var cal = new CalendarService();
        var sw = Stopwatch.StartNew();
        await cal.RefreshAsync("https://calendar.google.com/calendar/ical/ru.russian%23holiday%40group.v.calendar.google.com/public/basic.ics");
        var next = cal.Events.Where(e => e.End > DateTime.Now).OrderBy(e => e.Start).FirstOrDefault();
        Log($"calendar: failed={cal.Failed}, {cal.Events.Count} events in {sw.ElapsedMilliseconds} ms; next: {next?.Title} {next?.Start:yyyy-MM-dd} allDay={next?.AllDay}");

        var b = BatteryHealth.Read();
        Log(b == null ? "battery: not reported" : $"battery: designed {b.DesignedMWh}, full {b.FullMWh} mWh, wear {b.Wear:P0}, cycles {b.Cycles}");

        sw.Restart();
        var heat = new TaskCompletionSource();
        _sysmon.HeatChanged += () => heat.TrySetResult();
        _sysmon.SampleHeat();
        await Task.WhenAny(heat.Task, Task.Delay(10000));
        Log($"heat: known={_sysmon.HeatKnown}, temp={(_sysmon.TempC is double t ? $"{t:0.0}" : "hidden (sensor looks static)")}, throttling={_sysmon.Throttling}, query {sw.ElapsedMilliseconds} ms");

        Log($"idle for {idleSeconds} s, pid {Environment.ProcessId}");
        await Task.Delay(idleSeconds * 1000);
        Log("done");
    }

    static System.Drawing.Bitmap Grab(int x, int y, int w, int h)
    {
        var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(w, h));
        return bmp;
    }

    // Сколько точек (каждая вторая по обеим осям) заметно отличается
    static int Diff(System.Drawing.Bitmap a, System.Drawing.Bitmap b)
    {
        int n = 0;
        for (int y = 0; y < a.Height; y += 2)
            for (int x = 0; x < a.Width; x += 2)
            {
                var p = a.GetPixel(x, y);
                var q = b.GetPixel(x, y);
                if (Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B) > 24) n++;
            }
        return n;
    }
}
