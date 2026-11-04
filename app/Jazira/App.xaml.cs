using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Jazira;

public partial class App : Application
{
    public const string Version = "0.2.0";
    public const string Site = "https://boboevproject-rgb.github.io/jazira/";
    public const string CalendarHelp = "https://github.com/boboevproject-rgb/jazira/blob/main/app/README.md#calendar";

    Mutex? _mutex;
    bool _ownsMutex;
    TrayIcon? _tray;
    IslandWindow? _island;
    readonly DispatcherTimer _updateTimer = new();
    string? _readyExe;
    bool _updateBusy;

    public Version? ReadyVersion { get; private set; } // скачанная и проверенная новая версия

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

        // Один остров на пользователя. Сразу после обновления старая копия ещё закрывается — подождём её.
        bool updated = Array.IndexOf(e.Args, "--updated") >= 0;
        _mutex = new Mutex(false, @"Local\Jazira.Island");
        try { _ownsMutex = _mutex.WaitOne(updated ? TimeSpan.FromSeconds(15) : TimeSpan.Zero); }
        catch (AbandonedMutexException) { _ownsMutex = true; } // прошлая копия закрылась аварийно — остров наш
        if (!_ownsMutex) { Shutdown(); return; }

        Updater.CleanUp();
        _island = new IslandWindow();
        _island.ApplyVisibility();
        _tray = new TrayIcon(_island, this);
        _island.MenuRequested += _tray.ShowMenu;
        _island.SettingsChanged += _tray.Rebuild;
        if (updated) _island.Dispatcher.InvokeAsync(() => _island.ShowInfo(Strings.Format("update.done", Version)), DispatcherPriority.ApplicationIdle);

        // Первая проверка обновлений — через минуту после запуска, дальше — раз в 6 часов
        _updateTimer.Interval = TimeSpan.FromMinutes(1);
        _updateTimer.Tick += (_, _) =>
        {
            _updateTimer.Interval = TimeSpan.FromHours(6);
            if (AppState.Current.AutoUpdate) CheckUpdates(manual: false);
        };
        _updateTimer.Start();
    }

    public async void CheckUpdates(bool manual)
    {
        if (_updateBusy || _island == null) return;
        _updateBusy = true;
        try
        {
            var u = await Updater.CheckAsync();
            if (u == null)
            {
                if (manual) _island.ShowInfo(Strings.Get("update.latest"));
                return;
            }
            if (ReadyVersion != u.Version || _readyExe == null || !File.Exists(_readyExe))
            {
                _readyExe = await Updater.DownloadAsync(u);
                ReadyVersion = u.Version;
                _tray?.Rebuild();
            }
            _island.ShowUpdateReady(u.Version, ApplyUpdate);
        }
        catch (Exception ex)
        {
            Log(ex);
            if (manual) _island.ShowInfo(Strings.Get("update.fail"));
        }
        finally { _updateBusy = false; }
    }

    public void ApplyUpdate()
    {
        if (_readyExe == null) return;
        AppState.Save();
        if (!Updater.Apply(_readyExe)) { _island?.ShowInfo(Strings.Get("update.fail")); return; }
        // Новая копия ждёт, пока эта отпустит остров
        if (_ownsMutex) { _mutex?.ReleaseMutex(); _ownsMutex = false; }
        Shutdown();
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
