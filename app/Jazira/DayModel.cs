using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Jazira;

// Задача на странице «Мой день». Время напоминания необязательно.
public sealed class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    public DateTime? Due { get; set; }
    public bool Done { get; set; }
    public DateTime? DoneAt { get; set; }
    public bool Reminded { get; set; }  // напоминание уже показано — второй раз не звеним
}

// Кухонный таймер: их может идти несколько сразу, рядом с помодоро
public sealed class TimerItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Minutes { get; set; }
    public DateTime Ends { get; set; }
    public bool Done { get; set; }
}

// Время из текста задачи: «позвонить маме в 18:30», «чай через 20 мин», «завтра отчёт»,
// «call mom at 6:30», «in 2 h», «фардо соати 9:00», «баъди 15 дақ». Найденное время из текста убираем.
static class TaskParser
{
    const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    static readonly Regex Tomorrow = new(@"(?<![\p{L}])(завтра|tomorrow|фардо)(?![\p{L}])", Opt);
    static readonly Regex After = new(
        @"(?<![\p{L}])(через|in|баъди)\s+(\d{1,3})\s*(минут[ауы]?|мин\.?|minutes?|mins?|m|час(?:а|ов)?|ч\.?|hours?|hrs?|h|соат|дақиқа|дақ)(?![\p{L}])", Opt);
    static readonly Regex At = new(@"(?:(?<![\p{L}])(?:в|во|at|соати|соат)\s+)?(?<![\d:.])([01]?\d|2[0-3])[:.]([0-5]\d)(?![\d])", Opt);

    public static (string Text, DateTime? Due) Parse(string input, DateTime now)
    {
        var text = input.Trim();
        DateTime? due = null;
        bool tomorrow = false;

        var t = Tomorrow.Match(text);
        if (t.Success) { tomorrow = true; text = Cut(text, t); }

        var a = After.Match(text);
        if (a.Success)
        {
            int n = int.Parse(a.Groups[2].Value, CultureInfo.InvariantCulture);
            var unit = a.Groups[3].Value.ToLowerInvariant();
            bool hours = unit.StartsWith("ч") || unit.StartsWith("h") || unit.StartsWith("час") || unit == "соат";
            due = now.AddMinutes(hours ? n * 60 : n);
            text = Cut(text, a);
        }
        else
        {
            var m = At.Match(text);
            if (m.Success)
            {
                int h = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), min = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                var day = now.Date.AddDays(tomorrow ? 1 : 0);
                var at = day.AddHours(h).AddMinutes(min);
                if (!tomorrow && at <= now) at = at.AddDays(1); // 9:00, а сейчас уже 10 — значит, завтра
                due = at;
                text = Cut(text, m);
            }
            else if (tomorrow) due = now.Date.AddDays(1).AddHours(9); // «завтра» без времени — в 9 утра
        }

        text = Regex.Replace(text, @"\s{2,}", " ");
        text = Regex.Replace(text, @"\s+([,;.])", "$1").Trim(' ', ',', ';', '—', '-', '.');
        return (text.Length > 0 ? text : input.Trim(), due);
    }

    static string Cut(string s, Match m) => s.Remove(m.Index, m.Length).Insert(m.Index, " ");

    // «18:30», «завтра 9:00», «12.11 18:30»
    public static string Short(DateTime due, DateTime now)
    {
        var time = due.ToString("H:mm", CultureInfo.InvariantCulture);
        if (due.Date == now.Date) return time;
        if (due.Date == now.Date.AddDays(1)) return Strings.Get("day.tomorrow") + " " + time;
        return due.ToString("dd.MM", CultureInfo.InvariantCulture) + " " + time;
    }
}
