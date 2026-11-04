using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Jazira;

// Следит за одной папкой («Загрузки» или «Снимки экрана») и сообщает о новых готовых файлах.
// Браузер сначала пишет временный файл (.crdownload, .part…) и только в конце даёт ему настоящее имя,
// Firefox вдобавок заранее кладёт пустой файл с настоящим именем. Поэтому файл считается готовым, когда
// у него настоящее имя, рядом нет недокачанной копии, размер больше нуля и две проверки подряд не менялся,
// а сам файл можно открыть на чтение. Старые файлы, которые просто перенесли в папку, не считаются.
// Читаются только имя, размер и время файла; содержимое не читается и никуда не отправляется.
sealed class FolderWatch : IDisposable
{
    static readonly string[] TempExt =
    {
        ".crdownload", ".part", ".partial", ".download", ".opdownload", ".tmp", ".temp",
        ".!ut", ".!qb", ".aria2", ".downloading", ".filepart",
    };
    static readonly TimeSpan Settle = TimeSpan.FromSeconds(1);
    static readonly TimeSpan GiveUp = TimeSpan.FromMinutes(10);
    static readonly TimeSpan Fresh = TimeSpan.FromMinutes(30);

    public enum Verdict { Wait, Drop, Ready }

    public sealed class Pending
    {
        public DateTime Since, Due;
        public long Size = -1;
        public int Stable;
    }

    readonly Dispatcher _ui;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    readonly Dictionary<string, Pending> _pending = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, DateTime> _done = new(StringComparer.OrdinalIgnoreCase);
    FileSystemWatcher? _fsw;
    bool _enabled;

    public string? Dir { get; }
    public event Action<List<string>>? Arrived;

    public FolderWatch(Dispatcher ui, string? dir)
    {
        _ui = ui;
        Dir = dir;
        _timer.Tick += (_, _) => Check();
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (value) EnsureStarted();
            else Stop();
        }
    }

    // Папки «Снимки экрана» может ещё не быть — тогда пробуем снова раз в минуту
    public void EnsureStarted()
    {
        if (!_enabled || _fsw != null || Dir == null || !Directory.Exists(Dir)) return;
        try
        {
            var w = new FileSystemWatcher(Dir)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName,
                InternalBufferSize = 64 * 1024,
            };
            w.Created += (_, e) => Post(e.FullPath);
            w.Renamed += (_, e) => Post(e.FullPath);
            w.Error += (_, _) => _ui.BeginInvoke(() => { Stop(); EnsureStarted(); }); // переполнение — начать заново
            w.EnableRaisingEvents = true;
            _fsw = w;
        }
        catch (Exception ex) { App.Log(ex); }
    }

    void Stop()
    {
        _fsw?.Dispose();
        _fsw = null;
        _pending.Clear();
        _timer.Stop();
    }

    // Свой файл (снимок, сделанный островом) — не сообщать о нём ещё раз
    public void Ignore(string path) => _done[path] = DateTime.Now;

    void Post(string path) => _ui.BeginInvoke(() => Touch(path));

    void Touch(string path)
    {
        if (!_enabled || !IsCandidate(Path.GetFileName(path))) return;
        var now = DateTime.Now;
        if (_done.TryGetValue(path, out var at) && now - at < TimeSpan.FromMinutes(1)) return;
        if (!_pending.TryGetValue(path, out var p)) _pending[path] = p = new Pending { Since = now };
        p.Due = now + Settle;
        p.Stable = 0;
        _timer.Start();
    }

    void Check()
    {
        var now = DateTime.Now;
        var ready = new List<string>();
        foreach (var (path, p) in _pending.ToList())
        {
            if (now < p.Due) continue;
            var v = Probe(path, p, now);
            if (v == Verdict.Wait) { p.Due = now + Settle; continue; }
            _pending.Remove(path);
            if (v == Verdict.Ready) { ready.Add(path); _done[path] = now; }
        }
        if (_pending.Count == 0) _timer.Stop(); // в покое таймер не тикает
        foreach (var k in _done.Where(d => now - d.Value > TimeSpan.FromMinutes(5)).Select(d => d.Key).ToList()) _done.Remove(k);
        if (ready.Count > 0) Arrived?.Invoke(ready);
    }

    // Одна проверка файла; Wait — посмотреть снова через секунду
    public static Verdict Probe(string path, Pending p, DateTime now)
    {
        try
        {
            if (now - p.Since > GiveUp) return Verdict.Drop;
            var fi = new FileInfo(path);
            if (!fi.Exists) return Verdict.Drop;
            // Недокачанная копия рядом — браузер ещё пишет; когда закончит, переименует её, и мы узнаем снова
            if (TempExt.Any(ext => File.Exists(path + ext))) return Verdict.Drop;
            if ((fi.Attributes & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.Temporary)) != 0) return Verdict.Drop;
            if (fi.CreationTime < now - Fresh && fi.LastWriteTime < now - Fresh) return Verdict.Drop; // старый файл просто перенесли
            if (fi.Length == 0) return now - p.Since > TimeSpan.FromSeconds(30) ? Verdict.Drop : Verdict.Wait;
            if (fi.Length != p.Size) { p.Size = fi.Length; p.Stable = 0; return Verdict.Wait; }
            if (++p.Stable < 2) return Verdict.Wait;
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) { }
            return Verdict.Ready;
        }
        catch (IOException) { return Verdict.Wait; }            // файл ещё занят
        catch (UnauthorizedAccessException) { return Verdict.Drop; }
    }

    public static bool IsCandidate(string name)
    {
        if (name.Length == 0 || name.StartsWith('.') || name.StartsWith("~$")) return false;
        var lower = name.ToLowerInvariant();
        if (lower is "desktop.ini" or "thumbs.db") return false;
        return !TempExt.Any(ext => lower.EndsWith(ext));
    }

    public void Dispose() => Stop();
}

// Настоящие пути «Загрузок» и «Снимков экрана»: их можно перенести, а OneDrive переносит «Изображения» к себе
static class KnownFolder
{
    static readonly Guid DownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");
    static readonly Guid ScreenshotsId = new("B7BEDE81-DF94-4682-A7D8-57A52620B86F");

    public static string Downloads =>
        Get(DownloadsId) ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public static string Screenshots =>
        Get(ScreenshotsId) ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");

    static string? Get(Guid id)
    {
        try
        {
            if (SHGetKnownFolderPath(id, 0, IntPtr.Zero, out var p) != 0) return null;
            try { return Marshal.PtrToStringUni(p); }
            finally { Marshal.FreeCoTaskMem(p); }
        }
        catch (Exception) { return null; }
    }

    [DllImport("shell32.dll")]
    static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out IntPtr path);
}
