using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace Jazira;

public partial class App : Application
{
    public const string Version = "0.1.0";
    public const string Site = "https://boboevproject-rgb.github.io/jazira/";

    Mutex? _mutex;
    bool _ownsMutex;
    TrayIcon? _tray;
    IslandWindow? _island;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, ev) => { Log(ev.Exception); ev.Handled = true; };
        AppState.Load();
        Strings.Lang = AppState.Current.Lang ?? Strings.Detect();

        // Jazira.exe --snapshot <папка>: картинки всех состояний острова — для проверки вёрстки
        int snap = Array.IndexOf(e.Args, "--snapshot");
        if (snap >= 0)
        {
            Snapshot.Render(snap + 1 < e.Args.Length ? e.Args[snap + 1] : "snapshots");
            Shutdown();
            return;
        }

        _mutex = new Mutex(true, @"Local\Jazira.Island", out _ownsMutex);
        if (!_ownsMutex) { Shutdown(); return; } // уже запущен — второй остров не нужен

        _island = new IslandWindow();
        _island.ApplyVisibility();
        _tray = new TrayIcon(_island);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        if (_ownsMutex)
        {
            AppState.Save();
            _mutex?.ReleaseMutex();
        }
        base.OnExit(e);
    }

    public static void Log(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(AppState.Dir);
            File.AppendAllText(Path.Combine(AppState.Dir, "error.log"), $"{DateTime.Now:s} {ex}\n\n");
        }
        catch (Exception) { /* журнал не важнее работы острова */ }
    }
}
