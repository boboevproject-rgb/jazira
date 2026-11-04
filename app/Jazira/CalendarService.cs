using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Jazira;

public sealed record CalEvent(string Uid, string Title, DateTime Start, DateTime End, bool AllDay, string? Link);

// Ближайшие встречи из Google или Outlook по секретной iCal-ссылке. Только чтение: остров ничего
// не отправляет в календарь. Ссылка хранится в state.json на этом компьютере и в журнал не пишется.
sealed class CalendarService
{
    public IReadOnlyList<CalEvent> Events { get; private set; } = Array.Empty<CalEvent>();
    public DateTime? LoadedAt { get; private set; }
    public bool Failed { get; private set; }
    public event Action? Changed;
    bool _busy;

    // Google: «Настройки → календарь → Секретный адрес в формате iCal»; Outlook: «Опубликовать календарь → ICS».
    // webcal:// — то же самое по https.
    public static string? Normalize(string? text)
    {
        var s = text?.Trim() ?? "";
        if (s.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase)) s = "https://" + s[9..];
        if (!Uri.TryCreate(s, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps) return null;
        if (s.Contains(' ') || s.Length > 2000) return null;
        return s;
    }

    public async Task RefreshAsync(string url)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var text = await Net.Http.GetStringAsync(url);
            if (!text.Contains("BEGIN:VCALENDAR", StringComparison.Ordinal)) throw new FormatException("not an iCalendar file");
            var now = DateTime.Now;
            Events = Ics.Parse(text, now.AddHours(-12), now.AddDays(8));
            LoadedAt = now;
            Failed = false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException)
        {
            Failed = true; // в журнал — без ссылки: она секретная
            App.Log(new Exception($"calendar: {ex.GetType().Name} {(ex as HttpRequestException)?.StatusCode}"));
        }
        finally { _busy = false; }
        Changed?.Invoke();
    }

    public void Clear()
    {
        Events = Array.Empty<CalEvent>();
        LoadedAt = null;
        Failed = false;
        Changed?.Invoke();
    }

    public void Fake(IEnumerable<CalEvent> events) { Events = events.ToList(); LoadedAt = DateTime.Now; Failed = false; }

    // Ближайшая встреча: идущая сейчас или следующая в ближайшие сутки (события «на весь день» не считаем)
    public CalEvent? Next(DateTime now) =>
        Events.Where(e => !e.AllDay && e.End > now && e.Start < now.AddHours(24)).OrderBy(e => e.Start).FirstOrDefault();
}

// Разбор iCalendar (RFC 5545) в объёме, который реально выдают Google и Outlook:
// часовые пояса (IANA и Windows), события на весь день, повторы DAILY/WEEKLY/MONTHLY/YEARLY
// с INTERVAL, COUNT, UNTIL, BYDAY (в том числе «первый понедельник»), BYMONTHDAY, исключения EXDATE,
// перенесённые повторы (RECURRENCE-ID) и отменённые встречи.
static class Ics
{
    sealed class Prop
    {
        public string Value = "";
        public Dictionary<string, string> Params = new(StringComparer.OrdinalIgnoreCase);
    }

    sealed class Raw
    {
        public readonly Dictionary<string, List<Prop>> Props = new(StringComparer.OrdinalIgnoreCase);
        public Prop? One(string name) => Props.TryGetValue(name, out var l) ? l[0] : null;
        public IEnumerable<Prop> All(string name) => Props.TryGetValue(name, out var l) ? l : Enumerable.Empty<Prop>();
        public string Text(string name) => One(name) is Prop p ? Unescape(p.Value) : "";
    }

    public static List<CalEvent> Parse(string text, DateTime from, DateTime to)
    {
        var raws = new List<Raw>();
        Raw? cur = null;
        foreach (var line in Unfold(text))
        {
            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase)) { cur = new Raw(); continue; }
            if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase)) { if (cur != null) raws.Add(cur); cur = null; continue; }
            if (cur == null) continue;
            var (name, prop) = Split(line);
            if (name.Length == 0) continue;
            if (!cur.Props.TryGetValue(name, out var list)) cur.Props[name] = list = new List<Prop>();
            list.Add(prop);
        }

        var result = new List<CalEvent>();
        // Перенесённые или отменённые отдельные повторы: (UID, исходное время) → заменяют сгенерированные
        var moved = new HashSet<(string, DateTime)>();
        foreach (var r in raws.Where(r => r.One("RECURRENCE-ID") != null))
        {
            var rid = r.One("RECURRENCE-ID")!;
            if (Time(rid) is (DateTime orig, _)) moved.Add((r.Text("UID"), Minute(orig)));
        }

        foreach (var r in raws)
        {
            try
            {
                if (r.Text("STATUS").Equals("CANCELLED", StringComparison.OrdinalIgnoreCase)) continue;
                var startProp = r.One("DTSTART");
                if (startProp == null || Time(startProp) is not (DateTime start, bool allDay)) continue;
                TimeSpan length = r.One("DTEND") is Prop endProp && Time(endProp) is (DateTime end, _) ? end - start
                                : r.One("DURATION") is Prop dur ? ParseDuration(dur.Value)
                                : allDay ? TimeSpan.FromDays(1) : TimeSpan.Zero;
                if (length < TimeSpan.Zero) length = TimeSpan.Zero;
                var uid = r.Text("UID");
                var title = r.Text("SUMMARY");
                if (title.Length == 0) title = "—";
                var link = MeetingLink(r);

                var rrule = r.One("RRULE");
                if (rrule == null || r.One("RECURRENCE-ID") != null)
                {
                    if (start < to && start + length > from) result.Add(new CalEvent(uid, title, start, start + length, allDay, link));
                    continue;
                }

                var ex = new HashSet<DateTime>(r.All("EXDATE").SelectMany(p => p.Value.Split(',').Select(v => Time(new Prop { Value = v, Params = p.Params })))
                                                              .Where(t => t != null).Select(t => Minute(t!.Value.Item1)));
                foreach (var occ in Occurrences(startProp, rrule.Value, allDay, to))
                {
                    if (occ + length <= from) continue;
                    if (ex.Contains(Minute(occ)) || moved.Contains((uid, Minute(occ)))) continue;
                    result.Add(new CalEvent(uid, title, occ, occ + length, allDay, link));
                }
            }
            catch (Exception) { /* одно кривое событие не должно ломать весь календарь */ }
        }
        return result.OrderBy(e => e.Start).ToList();
    }

    static DateTime Minute(DateTime t) => new(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0);

    // Строки длиннее 75 символов в iCalendar переносятся: продолжение начинается с пробела или табуляции
    static IEnumerable<string> Unfold(string text)
    {
        var sb = new StringBuilder();
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t')) { sb.Append(raw, 1, raw.Length - 1); continue; }
            if (sb.Length > 0) yield return sb.ToString();
            sb.Clear().Append(raw);
        }
        if (sb.Length > 0) yield return sb.ToString();
    }

    // «DTSTART;TZID="(UTC+05:00) Tashkent":20261104T150000» → имя, параметры, значение (двоеточие в кавычках — не разделитель)
    static (string, Prop) Split(string line)
    {
        bool quoted = false;
        int colon = -1;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') quoted = !quoted;
            else if (line[i] == ':' && !quoted) { colon = i; break; }
        }
        if (colon < 0) return ("", new Prop());
        var head = line[..colon].Split(';');
        var prop = new Prop { Value = line[(colon + 1)..] };
        foreach (var p in head.Skip(1))
        {
            int eq = p.IndexOf('=');
            if (eq > 0) prop.Params[p[..eq]] = p[(eq + 1)..].Trim('"');
        }
        return (head[0].Trim(), prop);
    }

    // Время в местном поясе компьютера; true — событие на весь день
    static (DateTime, bool)? Time(Prop p)
    {
        var v = p.Value.Trim();
        if (v.Length == 8 || (p.Params.TryGetValue("VALUE", out var kind) && kind.Equals("DATE", StringComparison.OrdinalIgnoreCase)))
            return DateTime.TryParseExact(v[..Math.Min(8, v.Length)], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? (d, true) : null;
        bool utc = v.EndsWith('Z');
        if (!DateTime.TryParseExact(v.TrimEnd('Z'), "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) return null;
        if (utc) return (DateTime.SpecifyKind(t, DateTimeKind.Utc).ToLocalTime(), false);
        if (p.Params.TryGetValue("TZID", out var tzid) && Zone(tzid) is TimeZoneInfo tz)
            return (FromZone(t, tz), false);
        return (t, false); // «плавающее» время — как есть
    }

    static DateTime FromZone(DateTime wall, TimeZoneInfo tz)
    {
        try { return TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(wall, DateTimeKind.Unspecified), tz, TimeZoneInfo.Local); }
        catch (ArgumentException) { return wall.AddHours(1); } // время «провалилось» при переводе часов вперёд
    }

    static readonly Dictionary<string, TimeZoneInfo?> Zones = new(StringComparer.OrdinalIgnoreCase);

    // Google пишет пояса по IANA («Asia/Dushanbe»), Outlook — по-виндовски («West Asia Standard Time»)
    static TimeZoneInfo? Zone(string id)
    {
        if (Zones.TryGetValue(id, out var z)) return z;
        try { z = TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception) { z = null; }
        return Zones[id] = z;
    }

    // PT1H30M, P1D, -PT15M
    static TimeSpan ParseDuration(string v)
    {
        var m = Regex.Match(v, @"^([+-])?P(?:(\d+)W)?(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?)?$");
        if (!m.Success) return TimeSpan.Zero;
        int G(int i) => m.Groups[i].Success ? int.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture) : 0;
        var span = new TimeSpan(G(2) * 7 + G(3), G(4), G(5), G(6));
        return m.Groups[1].Value == "-" ? -span : span;
    }

    static readonly string[] Days = { "SU", "MO", "TU", "WE", "TH", "FR", "SA" };

    // Повторы считаются по «настенным часам» исходного пояса, потом каждый переводится в местное время —
    // так встреча в 10:00 остаётся в 10:00 и после перехода на летнее время
    static IEnumerable<DateTime> Occurrences(Prop startProp, string rule, bool allDay, DateTime to)
    {
        var parts = rule.Split(';').Select(x => x.Split('=')).Where(x => x.Length == 2)
                        .ToDictionary(x => x[0].ToUpperInvariant(), x => x[1], StringComparer.OrdinalIgnoreCase);
        string freq = parts.GetValueOrDefault("FREQ", "").ToUpperInvariant();
        int interval = int.TryParse(parts.GetValueOrDefault("INTERVAL"), out var iv) && iv > 0 ? iv : 1;
        int? count = int.TryParse(parts.GetValueOrDefault("COUNT"), out var c) ? c : null;
        DateTime? until = parts.TryGetValue("UNTIL", out var u) && Time(new Prop { Value = u }) is (DateTime ut, _) ? ut : null;
        if (until is DateTime un && un.TimeOfDay == TimeSpan.Zero && u!.Length == 8) until = un.AddDays(1).AddTicks(-1);

        // Настенное время начала и способ перевода в местное
        var raw = startProp.Value.Trim();
        bool utc = raw.EndsWith('Z');
        DateTime wall;
        Func<DateTime, DateTime> toLocal;
        if (allDay) { wall = Time(startProp)!.Value.Item1; toLocal = x => x; }
        else
        {
            DateTime.TryParseExact(raw.TrimEnd('Z'), "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out wall);
            if (utc) toLocal = x => DateTime.SpecifyKind(x, DateTimeKind.Utc).ToLocalTime();
            else if (startProp.Params.TryGetValue("TZID", out var tzid) && Zone(tzid) is TimeZoneInfo tz) toLocal = x => FromZone(x, tz);
            else toLocal = x => x;
        }

        var byDay = parts.TryGetValue("BYDAY", out var bd)
            ? bd.Split(',').Select(s => s.Trim().ToUpperInvariant()).Select(s => (Ord: int.TryParse(s[..^2], out var o) ? o : 0, Day: Array.IndexOf(Days, s[^2..]))).Where(x => x.Day >= 0).ToList()
            : new List<(int Ord, int Day)>();
        var byMonthDay = parts.TryGetValue("BYMONTHDAY", out var bmd)
            ? bmd.Split(',').Select(s => int.TryParse(s, out var n) ? n : 0).Where(n => n != 0).ToList()
            : new List<int>();

        int made = 0;
        var tod = wall.TimeOfDay;
        for (int step = 0; step < 4000; step++)
        {
            IEnumerable<DateTime> batch;
            switch (freq)
            {
                case "DAILY":
                    batch = new[] { wall.Date.AddDays((double)step * interval) + tod };
                    break;
                case "WEEKLY":
                {
                    // Неделя с понедельника; без BYDAY — день недели начала
                    var monday = wall.Date.AddDays(-(((int)wall.DayOfWeek + 6) % 7)).AddDays(7.0 * step * interval);
                    var days = byDay.Count > 0 ? byDay.Select(x => x.Day) : new[] { (int)wall.DayOfWeek };
                    batch = days.Select(d => monday.AddDays((d + 6) % 7) + tod).OrderBy(x => x).ToList();
                    break;
                }
                case "MONTHLY":
                {
                    var month = new DateTime(wall.Year, wall.Month, 1).AddMonths(step * interval);
                    batch = MonthDays(month, byDay, byMonthDay.Count > 0 ? byMonthDay : byDay.Count > 0 ? new List<int>() : new List<int> { wall.Day })
                            .Select(d => d + tod).ToList();
                    break;
                }
                case "YEARLY":
                {
                    int y = wall.Year + step * interval;
                    batch = wall.Month == 2 && wall.Day == 29 && !DateTime.IsLeapYear(y) ? Array.Empty<DateTime>()
                          : new[] { new DateTime(y, wall.Month, wall.Day) + tod };
                    break;
                }
                default:
                    yield break;
            }
            foreach (var w in batch)
            {
                if (w < wall) continue;
                if (until is DateTime end && toLocal(w) > end) yield break;
                if (count is int n && made >= n) yield break;
                made++;
                var local = toLocal(w);
                if (local > to) yield break;
                yield return local;
            }
        }
    }

    // Дни месяца для MONTHLY: числа (BYMONTHDAY, -1 — последний день) и «второй вторник» (BYDAY=2TU, -1FR)
    static IEnumerable<DateTime> MonthDays(DateTime first, List<(int Ord, int Day)> byDay, List<int> monthDays)
    {
        int len = DateTime.DaysInMonth(first.Year, first.Month);
        var set = new SortedSet<DateTime>();
        foreach (var md in monthDays)
        {
            int d = md > 0 ? md : len + md + 1;
            if (d >= 1 && d <= len) set.Add(first.AddDays(d - 1));
        }
        foreach (var (ord, day) in byDay)
        {
            var all = Enumerable.Range(0, len).Select(i => first.AddDays(i)).Where(x => (int)x.DayOfWeek == day).ToList();
            if (ord == 0) foreach (var x in all) set.Add(x);
            else if (ord > 0 && ord <= all.Count) set.Add(all[ord - 1]);
            else if (ord < 0 && -ord <= all.Count) set.Add(all[all.Count + ord]);
        }
        return set;
    }

    static string Unescape(string v) =>
        v.Replace("\\n", "\n").Replace("\\N", "\n").Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\");

    static readonly Regex Url = new(@"https://[^\s""'<>\\]+", RegexOptions.IgnoreCase);
    static readonly string[] MeetHosts = { "meet.google.com", "zoom.us", "teams.microsoft.com", "teams.live.com", "telemost.yandex.ru", "webex.com", "meet.jit.si", "whereby.com", "skype.com", "discord.gg" };

    // Ссылка на созвон: Google Meet, Zoom, Teams, Телемост… — из специальных полей, места или описания
    static string? MeetingLink(Raw r)
    {
        foreach (var field in new[] { "X-GOOGLE-CONFERENCE", "X-MICROSOFT-SKYPETEAMSMEETINGURL", "X-MICROSOFT-ONLINEMEETINGCONFLINK", "URL", "LOCATION", "DESCRIPTION" })
            foreach (Match m in Url.Matches(r.Text(field)))
            {
                var link = m.Value.TrimEnd('.', ',', ')', ';', '>');
                if (Uri.TryCreate(link, UriKind.Absolute, out var u) && MeetHosts.Any(h => u.Host.Equals(h, StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase)))
                    return link;
            }
        return null;
    }
}
