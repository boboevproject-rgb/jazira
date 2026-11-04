using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Jazira;

sealed record UpdateInfo(Version Version, string ZipUrl, string? ShaUrl, string? ShaInNotes);

// Автообновление из релизов GitHub этого репозитория. Обновление ставится, только если у архива
// есть контрольная сумма SHA-256 (файл .sha256 рядом или строка в описании релиза) и она совпала.
static class Updater
{
    const string Repo = "boboevproject-rgb/jazira";

    // Для проверки на своём компьютере можно подставить локальный адрес — и только локальный
    static string Api =>
        Environment.GetEnvironmentVariable("JAZIRA_UPDATE_API") is string s && s.StartsWith("http://127.0.0.1:", StringComparison.Ordinal)
            ? s : $"https://api.github.com/repos/{Repo}/releases?per_page=10";

    static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Jazira", "update");

    public static Version Current => Version.Parse(App.Version);

    // Поставлено через winget: портативный пакет лежит в ...\WinGet\Packages\ и запускается через ссылку
    // в ...\WinGet\Links\. Тогда обновляет winget — подмена файла на месте запутала бы его в версиях.
    public static bool ByWinget => IsWingetPath(Environment.ProcessPath);

    internal static bool IsWingetPath(string? path) =>
        path != null && (path.Contains(@"\WinGet\Packages\", StringComparison.OrdinalIgnoreCase)
                      || path.Contains(@"\WinGet\Links\", StringComparison.OrdinalIgnoreCase));

    public static async Task<UpdateInfo?> CheckAsync()
    {
        using var doc = JsonDocument.Parse(await Net.Http.GetStringAsync(Api));
        UpdateInfo? best = null;
        foreach (var r in doc.RootElement.EnumerateArray())
        {
            if (r.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
            var tag = r.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var v) || v <= Current || (best != null && v <= best.Version)) continue;

            var zipName = $"Jazira-{v}-win-x64.zip";
            string? zip = null, sha = null;
            foreach (var a in r.GetProperty("assets").EnumerateArray())
            {
                var name = a.GetProperty("name").GetString();
                var url = a.GetProperty("browser_download_url").GetString();
                if (name == zipName) zip = url;
                else if (name == zipName + ".sha256") sha = url;
            }
            var notes = r.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() ?? "" : "";
            var inNotes = Regex.Match(notes, @"\b[0-9a-fA-F]{64}\b");
            if (zip == null || (sha == null && !inNotes.Success)) continue; // без контрольной суммы не обновляемся
            best = new UpdateInfo(v, zip, sha, inNotes.Success ? inNotes.Value : null);
        }
        return best;
    }

    // Скачивает архив, сверяет SHA-256 и достаёт новый Jazira.exe; возвращает путь к нему
    public static async Task<string> DownloadAsync(UpdateInfo u)
    {
        Directory.CreateDirectory(Dir);
        string expected = u.ShaUrl != null
            ? (await Net.Http.GetStringAsync(u.ShaUrl)).Trim().Split(' ', '\t', '\r', '\n')[0]
            : u.ShaInNotes!;

        var zipPath = Path.Combine(Dir, $"Jazira-{u.Version}.zip");
        using (var resp = await Net.Http.GetAsync(u.ZipUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            resp.EnsureSuccessStatusCode();
            await using var f = File.Create(zipPath);
            await resp.Content.CopyToAsync(f);
        }

        string actual;
        await using (var f = File.OpenRead(zipPath)) actual = Convert.ToHexString(await SHA256.HashDataAsync(f));
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(zipPath);
            throw new InvalidDataException($"SHA-256 архива не совпал: {actual} вместо {expected}");
        }

        var exe = Path.Combine(Dir, $"Jazira-{u.Version}.exe");
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            var entry = zip.GetEntry("Jazira.exe") ?? throw new InvalidDataException("В архиве нет Jazira.exe");
            entry.ExtractToFile(exe, overwrite: true);
        }
        File.Delete(zipPath);
        return exe;
    }

    // Работающий exe нельзя перезаписать, но можно переименовать: старый → Jazira.exe.old,
    // новый — на его место, запуск новой версии; старая тут же закрывается.
    public static bool Apply(string newExe)
    {
        var cur = Environment.ProcessPath;
        if (cur == null || !File.Exists(newExe)) return false;
        var old = cur + ".old";
        try
        {
            if (File.Exists(old)) File.Delete(old);
            File.Move(cur, old);
            try { File.Copy(newExe, cur); }
            catch (Exception) { File.Move(old, cur); throw; }
            Process.Start(new ProcessStartInfo(cur, "--updated") { UseShellExecute = false });
            return true;
        }
        catch (Exception ex) { App.Log(ex); return false; }
    }

    // После обновления: убрать старый exe и скачанный файл. Старая копия может ещё закрываться —
    // тогда удалится при следующем запуске.
    public static void CleanUp()
    {
        try
        {
            var old = Environment.ProcessPath + ".old";
            if (File.Exists(old)) File.Delete(old);
            if (Directory.Exists(Dir))
                foreach (var f in Directory.GetFiles(Dir)) File.Delete(f);
        }
        catch (Exception) { }
    }
}
