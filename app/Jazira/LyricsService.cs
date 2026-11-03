using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Jazira;

// Общий HTTP-клиент: сервисы просят, чтобы приложение представлялось по имени
static class Net
{
    public static readonly HttpClient Http = Create();

    static HttpClient Create()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd($"Jazira/{App.Version} (+https://github.com/boboevproject-rgb/jazira)");
        return h;
    }
}

readonly record struct LyricLine(TimeSpan At, string Text);

// Тексты песен в такт — бесплатная открытая база LRCLIB (lrclib.net), без ключа.
// Туда уходят только название песни, исполнитель и длительность. Нет текста — остров просто без него.
sealed class LyricsService
{
    readonly Dictionary<string, LyricLine[]?> _cache = new();
    string _want = "";

    public LyricLine[]? Lines { get; private set; }
    public event Action? Changed;

    public async void Load(string title, string artist, TimeSpan duration)
    {
        var key = artist + "\n" + title;
        if (key == _want) return;
        _want = key;
        if (Lines != null) { Lines = null; Changed?.Invoke(); }
        if (title.Length == 0) return;

        if (!_cache.TryGetValue(key, out var lines))
        {
            try
            {
                lines = await FetchAsync(title, artist, duration);
                if (_cache.Count > 80) _cache.Clear();
                _cache[key] = lines; // «текста нет» тоже запоминаем, чтобы не спрашивать снова
            }
            catch (Exception) { lines = null; } // нет сети — без текста; при следующем включении песни спросим ещё раз
        }
        if (_want != key) return; // пока искали, песня сменилась
        Lines = lines;
        Changed?.Invoke();
    }

    public void Clear()
    {
        _want = "";
        if (Lines == null) return;
        Lines = null;
        Changed?.Invoke();
    }

    // Номер строки, которая звучит в момент t; -1 — песня ещё не дошла до первой строки
    public int IndexAt(TimeSpan t)
    {
        var l = Lines;
        if (l == null || l.Length == 0 || t < l[0].At) return -1;
        int lo = 0, hi = l.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (l[mid].At <= t) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    static async Task<LyricLine[]?> FetchAsync(string title, string artist, TimeSpan duration)
    {
        HttpRequestException? error = null;
        foreach (var (a, t) in Candidates(title, artist))
        {
            try
            {
                var lines = (a.Length > 0 ? await GetAsync(a, t, duration) : null) ?? await SearchAsync(a, t, duration);
                if (lines != null) return lines;
            }
            catch (HttpRequestException ex) { error = ex; } // на «Исполнитель - Песня» сервер иногда отвечает 503 — пробуем следующий вариант
        }
        if (error != null) throw error; // так «текста нет» не попадёт в кэш
        return null;
    }

    static async Task<LyricLine[]?> GetAsync(string artist, string title, TimeSpan duration)
    {
        var url = $"https://lrclib.net/api/get?artist_name={E(artist)}&track_name={E(title)}";
        if (duration.TotalSeconds > 20) url += "&duration=" + (int)Math.Round(duration.TotalSeconds);
        using var resp = await Net.Http.GetAsync(url);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return Parse(Str(doc.RootElement, "syncedLyrics"));
    }

    // Поиск: берём вариант с синхронным текстом и самой близкой длительностью
    static async Task<LyricLine[]?> SearchAsync(string artist, string title, TimeSpan duration)
    {
        var url = artist.Length > 0
            ? $"https://lrclib.net/api/search?artist_name={E(artist)}&track_name={E(title)}"
            : $"https://lrclib.net/api/search?q={E(title)}";
        using var doc = JsonDocument.Parse(await Net.Http.GetStringAsync(url));
        string? best = null;
        double bestDiff = double.MaxValue;
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var synced = Str(item, "syncedLyrics");
            if (string.IsNullOrWhiteSpace(synced)) continue;
            double diff = duration.TotalSeconds > 20 && item.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number
                ? Math.Abs(d.GetDouble() - duration.TotalSeconds) : 0;
            if (diff < bestDiff) { bestDiff = diff; best = synced; }
        }
        // Длительность сильно отличается — скорее всего, другая версия песни, текст «уедет»
        return bestDiff <= 8 ? Parse(best) : null;
    }

    static readonly Regex Tag = new(@"\[(\d+):(\d+(?:[.:]\d+)?)\]", RegexOptions.Compiled);

    public static LyricLine[]? Parse(string? lrc)
    {
        if (string.IsNullOrWhiteSpace(lrc)) return null;
        var list = new List<LyricLine>();
        foreach (var raw in lrc.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var ms = Tag.Matches(line);
            if (ms.Count == 0) continue;
            var text = line[(ms[^1].Index + ms[^1].Length)..].Trim();
            foreach (Match m in ms)
            {
                double sec = double.Parse(m.Groups[2].Value.Replace(':', '.'), CultureInfo.InvariantCulture);
                list.Add(new(TimeSpan.FromMinutes(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                             + TimeSpan.FromMilliseconds(Math.Round(sec * 1000)), text));
            }
        }
        list.Sort((x, y) => x.At.CompareTo(y.At));
        return list.Count > 0 ? list.ToArray() : null;
    }

    // Браузеры часто отдают «Исполнитель - Песня (Official Video)» с именем канала вместо исполнителя
    static IEnumerable<(string Artist, string Title)> Candidates(string title, string artist)
    {
        var t = Clean(title);
        var a = Regex.Replace(artist, @"(\s+-\s+Topic|VEVO)$", "", RegexOptions.IgnoreCase).Trim();
        var seen = new HashSet<string>();
        var list = new List<(string, string)>();
        if (a.Length > 0) list.Add((a, t));
        int dash = t.IndexOf(" - ", StringComparison.Ordinal);
        if (dash < 0) dash = t.IndexOf(" – ", StringComparison.Ordinal);
        if (dash > 0) list.Add((t[..dash].Trim(), t[(dash + 3)..].Trim()));
        if (a.Length == 0 && dash < 0) list.Add(("", t));
        return list.Where(c => seen.Add(c.Item1 + "\n" + c.Item2));
    }

    static string Clean(string s) =>
        Regex.Replace(s, @"\s*[\(\[][^\)\]]*(official|video|audio|lyric|клип|премьера|\bmv\b|\bhd\b|4k|visuali[sz]er)[^\)\]]*[\)\]]", "",
                      RegexOptions.IgnoreCase).Trim();

    static string E(string s) => Uri.EscapeDataString(s);

    static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    // ---- Для снимков ----
    public void Fake(LyricLine[]? lines)
    {
        _want = "fake";
        Lines = lines;
    }
}
