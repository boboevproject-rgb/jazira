using System;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Jazira;

// Мелкие системные вещи: стиль окна, полноэкранные приложения, автозапуск, звук.
static class Native
{
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;   // не показывать в Alt+Tab
    public const int WS_EX_NOACTIVATE = 0x08000000;   // клик по острову не отнимает фокус у вашего окна

    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);

    // Игра, фильм или презентация во весь экран — остров уходит, чтобы не мешать
    public static bool FullscreenAppActive()
    {
        try
        {
            // 2 — полноэкранное приложение, 3 — Direct3D во весь экран, 4 — режим презентации
            if (SHQueryUserNotificationState(out int s) == 0) return s == 2 || s == 3 || s == 4;
        }
        catch (Exception) { }
        return false;
    }
}

static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "Jazira";

    public static bool Enabled
    {
        get
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(Name) is string;
        }
    }

    public static void Set(bool on)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) k.SetValue(Name, $"\"{Environment.ProcessPath}\"");
        else k.DeleteValue(Name, false);
    }
}

static class Chime
{
    static SoundPlayer? _player; // держим ссылку, пока звук играет

    // Мягкий двухнотный сигнал. Синтезируется в памяти — без звуковых файлов.
    public static void Play()
    {
        try
        {
            const int rate = 44100;
            var notes = new[] { (Hz: 880.0, Start: 0.0), (Hz: 660.0, Start: 0.32) };
            int n = (int)(rate * 1.1);
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
            {
                w.Write("RIFF"u8); w.Write(36 + n * 2); w.Write("WAVE"u8);
                w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write("data"u8); w.Write(n * 2);
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / rate, v = 0;
                    foreach (var note in notes)
                    {
                        double dt = t - note.Start;
                        if (dt < 0) continue;
                        double env = Math.Min(1, dt * 250) * Math.Exp(-4.5 * dt); // быстрая атака, плавное затухание
                        v += Math.Sin(2 * Math.PI * note.Hz * dt) * env;
                    }
                    w.Write((short)(v * 0.22 * short.MaxValue));
                }
            }
            _player = new SoundPlayer(new MemoryStream(ms.ToArray()));
            _player.Play();
        }
        catch (Exception ex) { App.Log(ex); }
    }
}
