using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Jazira;

public partial class App : Application
{
    public const string Version = "0.3.1";
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

        // Остров рисуется без видеокарты: окно маленькое, а драйвер видеокарты держит ~140 МБ памяти.
        // Замер на ноутбуке с Intel Iris Xe в тестовом прогоне (--live-test): личная память 75 МБ вместо 214,
        // процессор даже чуть ниже (свёрнут 2,3 % ядра вместо 2,6 %, раскрыт с рыбкой 9,0 % вместо 9,9 %).
        // В обычной работе (медиа-пульт, тексты, звук) — около 160 МБ вместо ~300 у 0.2. --gpu-render — по-старому.
        if (Array.IndexOf(e.Args, "--gpu-render") < 0)
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

        // Jazira.exe --video <папка> <ffmpeg.exe> <ru|en|tg>: ролик для сайта (Snapshot.Video)
        int video = Array.IndexOf(e.Args, "--video");
        if (video >= 0 && video + 3 < e.Args.Length)
        {
            try { Snapshot.Video(e.Args[video + 1], e.Args[video + 2], e.Args[video + 3]); }
            catch (Exception ex) { File.AppendAllText(Path.Combine(e.Args[video + 1], "video.txt"), ex + Environment.NewLine); }
            Shutdown();
            return;
        }

        // Jazira.exe --live-test <папка>: живая проверка рядом с вашим островом (IslandWindow.Test.cs)
        int live = Array.IndexOf(e.Args, "--live-test");
        if (live >= 0)
        {
            AppState.ReadOnly = true;
            AppState.Current.ShowIsland = true; // только в памяти
            AppState.Current.AutoHide = false;  // автоскрытие тест включает сам (IslandWindow.Test.cs)
            var dir = live + 1 < e.Args.Length ? e.Args[live + 1] : "live-test";
            Directory.CreateDirectory(dir);
            var test = new IslandWindow { TestOffscreen = true };
            test.Show();
            RunLiveTest(test, dir);
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

    async void RunLiveTest(IslandWindow w, string dir)
    {
        try { await w.RunLiveTest(dir, 70); }
        catch (Exception ex) { File.AppendAllText(Path.Combine(dir, "live-test.txt"), ex + Environment.NewLine); }
        Shutdown();
    }

    public async void CheckUpdates(bool manual)
    {
        if (_updateBusy || _island == null) return;
        if (Updater.ByWinget)
        {
            if (manual) _island.ShowInfo(Strings.Get("update.winget"));
            return;
        }
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
