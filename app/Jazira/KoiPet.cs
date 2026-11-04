using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Jazira;

// Окраски кои: белая с красным → трёхцветная → золотая
public enum KoiKind { Kohaku, Sanke, Ogon }

// Рыбка-питомец. Каждый день, когда цель по воде выполнена, кои подрастает.
// Самая длинная серия дней без пропусков открывает окраски и вторую рыбку — насовсем.
// Пропустили день — рыбка грустит, пока вы снова не выполните цель. Умереть она не может.
// Хранится только список дней с выполненной целью (AppState.KoiDays) — всё остальное считается из него.
static class KoiPet
{
    public const int SankeAt = 3, SecondAt = 7, OgonAt = 14, GrowDays = 30;

    static string Key(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    static string TodayKey => Key(DateTime.Today);

    // Сколько дней кои живёт — дней с выполненной целью
    public static int Age(AppState st) => st.KoiDays?.Count ?? 0;

    // От малька до взрослой рыбки — за месяц выполненных целей
    public static double Growth(AppState st) => Math.Min(1, Age(st) / (double)GrowDays);

    // Текущая серия: дни подряд по сегодня; пока сегодняшняя цель впереди — по вчера, серия не сгорает
    public static int Streak(AppState st)
    {
        var days = new HashSet<string>(st.KoiDays ?? new());
        var d = DateTime.Today;
        if (!days.Contains(Key(d))) d = d.AddDays(-1);
        int n = 0;
        while (days.Contains(Key(d))) { n++; d = d.AddDays(-1); }
        return n;
    }

    // Самая длинная серия; skipToday — без сегодняшнего дня (чтобы понять, что открылось именно сегодня)
    static int Longest(AppState st, bool skipToday)
    {
        var today = TodayKey;
        int best = 0, run = 0;
        DateTime? prev = null;
        foreach (var k in (st.KoiDays ?? new()).Distinct().OrderBy(k => k, StringComparer.Ordinal))
        {
            if (skipToday && k == today) continue;
            if (!DateTime.TryParseExact(k, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) continue;
            run = prev is DateTime p && (d - p).TotalDays == 1 ? run + 1 : 1;
            best = Math.Max(best, run);
            prev = d;
        }
        return best;
    }

    public static int Best(AppState st) => Longest(st, skipToday: false);

    static KoiKind KindFor(int best) => best >= OgonAt ? KoiKind.Ogon : best >= SankeAt ? KoiKind.Sanke : KoiKind.Kohaku;
    public static KoiKind Kind(AppState st) => KindFor(Best(st));
    public static bool HasSecond(AppState st) => Best(st) >= SecondAt;
    public static KoiKind SecondKind(AppState st) => Kind(st) == KoiKind.Ogon ? KoiKind.Sanke : KoiKind.Kohaku;

    // До чего осталось дотянуть текущей серией (для подсказки): (порог, что откроется) или null — всё открыто
    public static (int At, string What)? Next(AppState st)
    {
        int best = Best(st);
        foreach (var (at, what) in new[] { (SankeAt, "sanke"), (SecondAt, "second"), (OgonAt, "ogon") })
            if (best < at) return (at, what);
        return null;
    }

    // Что открылось сегодняшней целью: «sanke», «second», «ogon» или null
    public static string? NewsToday(AppState st)
    {
        if (st.KoiDays == null || !st.KoiDays.Contains(TodayKey)) return null;
        int before = Longest(st, skipToday: true), now = Best(st);
        foreach (var (at, what) in new[] { (OgonAt, "ogon"), (SecondAt, "second"), (SankeAt, "sanke") })
            if (before < at && now >= at) return what;
        return null;
    }

    // Грустит: цель уже выполнялась, но последний такой день — позавчера или раньше, а сегодня цель ещё впереди
    public static bool Sad(AppState st)
    {
        var days = st.KoiDays;
        if (days == null || days.Count == 0 || days.Contains(TodayKey)) return false;
        var last = days.Max(StringComparer.Ordinal)!;
        return string.CompareOrdinal(last, Key(DateTime.Today.AddDays(-1))) < 0;
    }

    // Сегодняшний день засчитан ровно тогда, когда цель выполнена: отменили стакан — снова не засчитан.
    // Меняется только сегодняшний день — прошлые остаются как были, даже если поменяли цель.
    public static bool Sync(AppState st)
    {
        st.KoiDays ??= new();
        var today = TodayKey;
        bool hit = st.WaterToday >= st.WaterGoalMl, has = st.KoiDays.Contains(today);
        if (hit == has) return false;
        if (hit) st.KoiDays.Add(today);
        else st.KoiDays.RemoveAll(k => k == today);
        return true;
    }

    // Версии до 0.3 списка не было: дни с выполненной целью берём из журнала воды (он хранит два месяца)
    public static void Migrate(AppState st)
    {
        if (st.KoiDays != null) return;
        st.KoiDays = st.WaterLog.Where(kv => kv.Value >= st.WaterGoalMl)
                                .Select(kv => kv.Key)
                                .OrderBy(k => k, StringComparer.Ordinal)
                                .ToList();
    }
}
