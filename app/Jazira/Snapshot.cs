using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Jazira;

// Jazira.exe --snapshot <папка>: рисует остров во всех состояниях в PNG (масштаб 200 %),
// не показывая его на экране. Так вёрстку можно проверить без мыши и без живого плеера.
static class Snapshot
{
    public static void Render(string dir)
    {
        Directory.CreateDirectory(dir);
        AppState.ReadOnly = true; // состояния ниже подменяют настройки в памяти — файл пользователя не трогаем
        KoiPond.Still = true;
        WriteSelfTest(Path.Combine(dir, "selftest.txt"));
        var w = new IslandWindow(snapshot: true) { Left = -4000, Top = -4000 };
        w.Root.Background = new SolidColorBrush(Color.FromRgb(0x1D, 0x2B, 0x33)); // «рабочий стол» под островом
        w.Show();
        foreach (var (name, setup) in w.SnapshotStates(FakeCover()))
        {
            setup();
            w.RefreshAll();
            w.UpdateLayout();
            Save(w, Path.Combine(dir, name + ".png"));
        }
        foreach (var (name, setup) in w.KoiCardStates())
        {
            setup();
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(KoiCard.Render(AppState.Current)));
            using var f = File.Create(Path.Combine(dir, name + ".png"));
            enc.Save(f);
        }
        w.Close();
    }

    static void Save(IslandWindow w, string path)
    {
        const double dpi = 192, k = dpi / 96;
        var root = w.Root;
        var rtb = new RenderTargetBitmap((int)(root.ActualWidth * k), (int)(root.ActualHeight * k), dpi, dpi, PixelFormats.Pbgra32);
        rtb.Render(root);
        // Вырезаем остров с полями под тень
        double x = Math.Max(0, (root.ActualWidth - w.Island.ActualWidth) / 2 - 18);
        double width = Math.Min(root.ActualWidth - x, w.Island.ActualWidth + 36);
        double height = Math.Min(root.ActualHeight, w.Island.ActualHeight + 30);
        var crop = new CroppedBitmap(rtb, new Int32Rect((int)(x * k), 0, (int)(width * k), (int)(height * k)));
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(crop));
        using var f = File.Create(path);
        enc.Save(f);
    }

    // Расчёты без экрана: время намаза на сегодня и разбор текста песни — чтобы сверить глазами
    static void WriteSelfTest(string path)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var city in LocalInfo.Cities)
        {
            var t = PrayerTimes.ForDate(DateTime.Today, city.Lat, city.Lon);
            sb.Append($"{city.En,-18}");
            for (int i = 0; i < t.Length; i++) sb.Append($" {PrayerTimes.Keys[i]} {t[i]:HH:mm}Z");
            sb.AppendLine();
        }
        var lines = LyricsService.Parse("[00:35.66]Look at the stars\n[00:38.46]Look how they shine\n[01:02.5][01:40.00]Twice\n[00:40.36]");
        sb.AppendLine($"lyrics: {lines?.Length} lines, first {lines?[0].At} '{lines?[0].Text}', last {lines?[^1].At} '{lines?[^1].Text}'");
        KoiSelfTest(sb);
        TaskParserSelfTest(sb);
        File.WriteAllText(path, sb.ToString());
    }

    // Время из текста задачи: «в 18:30», «через 20 мин», «завтра», по-английски и по-таджикски
    static void TaskParserSelfTest(System.Text.StringBuilder sb)
    {
        var now = new DateTime(2026, 11, 4, 10, 0, 0);
        int fails = 0;
        foreach (var (input, text, due) in new (string, string, DateTime?)[]
        {
            ("позвонить маме в 18:30", "позвонить маме", now.Date.AddHours(18.5)),
            ("чай через 20 мин", "чай", now.AddMinutes(20)),
            ("через 1 час созвон", "созвон", now.AddHours(1)),
            ("завтра отчёт", "отчёт", now.Date.AddDays(1).AddHours(9)),
            ("купить хлеб", "купить хлеб", null),
            ("call mom at 9:15", "call mom", now.Date.AddDays(1).AddHours(9.25)),
            ("in 2 h stretch", "stretch", now.AddHours(2)),
            ("фардо соати 8:00 бозор", "бозор", now.Date.AddDays(1).AddHours(8)),
            ("баъди 15 дақ нон", "нон", now.AddMinutes(15)),
            ("встреча завтра в 14.00, взять паспорт", "встреча, взять паспорт", now.Date.AddDays(1).AddHours(14)),
        })
        {
            var (t, d) = TaskParser.Parse(input, now);
            bool ok = t == text && d == due;
            if (!ok) fails++;
            sb.AppendLine($"task {(ok ? "ok  " : "FAIL")} '{input}' -> '{t}' {d:yyyy-MM-dd HH:mm} (want '{text}' {due:yyyy-MM-dd HH:mm})");
        }
        sb.AppendLine($"tasks: {(fails == 0 ? "all ok" : fails + " FAILED")}");
    }

    // Питомец: серия, грусть, открытия и учёт сегодняшнего дня — на выдуманных историях
    static void KoiSelfTest(System.Text.StringBuilder sb)
    {
        static string D(int ago) => DateTime.Today.AddDays(-ago).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        static AppState S(int todayMl, params int[] agos)
        {
            var st = new AppState { KoiDays = new() };
            foreach (var a in agos) st.KoiDays.Add(D(a));
            st.WaterLog[AppState.Today] = todayMl;
            return st;
        }
        int fails = 0;
        void Check(string name, object got, object want)
        {
            bool ok = Equals(got, want);
            if (!ok) fails++;
            sb.AppendLine($"koi {(ok ? "ok  " : "FAIL")} {name}: {got} (want {want})");
        }

        var a = S(500, 1, 2, 3);
        Check("streak through yesterday", KoiPet.Streak(a), 3);
        Check("kind at 3", KoiPet.Kind(a), KoiKind.Sanke);
        Check("not sad after yesterday", KoiPet.Sad(a), false);
        Check("no news when today not done", KoiPet.NewsToday(a), null);

        var b = S(2000, 0, 1, 2, 10, 11);
        Check("streak with today", KoiPet.Streak(b), 3);
        Check("sanke unlocked today", KoiPet.NewsToday(b), "sanke");
        Check("age counts all goal days", KoiPet.Age(b), 5);

        var c = S(250, 3, 4);
        Check("sad after a missed day", KoiPet.Sad(c), true);
        Check("streak broken", KoiPet.Streak(c), 0);
        Check("colors stay after a break", KoiPet.Kind(S(0, 5, 6, 7, 8)), KoiKind.Sanke);

        var e = S(2000, Run(0, 14));
        Check("ogon at 14", KoiPet.NewsToday(e), "ogon");
        Check("second koi at 7+", KoiPet.HasSecond(e), true);
        Check("best 14", KoiPet.Best(e), 14);
        Check("second koi from 7", KoiPet.NewsToday(S(2000, Run(0, 7))), "second");
        Check("new user not sad", KoiPet.Sad(S(0)), false);

        var f = S(2000);
        Check("sync adds today", KoiPet.Sync(f) && f.KoiDays!.Contains(AppState.Today), true);
        f.WaterLog[AppState.Today] = 1750;
        Check("undo below goal removes today", KoiPet.Sync(f) && !f.KoiDays!.Contains(AppState.Today), true);

        var m = new AppState { WaterGoalMl = 2000 };
        m.WaterLog[D(1)] = 2250; m.WaterLog[D(2)] = 1500; m.WaterLog[D(3)] = 2000;
        KoiPet.Migrate(m);
        Check("migrate from water log", string.Join(",", m.KoiDays!), $"{D(3)},{D(1)}");

        var lang = Strings.Lang;
        Strings.Lang = "ru";
        Check("ru plural", string.Join("|", new[] { 1, 2, 5, 11, 12, 21, 22, 25, 112 }.Select(Strings.Days)),
              "1 день|2 дня|5 дней|11 дней|12 дней|21 день|22 дня|25 дней|112 дней");
        Strings.Lang = lang;
        sb.AppendLine($"koi: {(fails == 0 ? "all ok" : fails + " FAILED")}");

        static int[] Run(int from, int count) => System.Linq.Enumerable.Range(from, count).ToArray();
    }

    // Обложка-заглушка: градиент «море и закат»
    static BitmapSource FakeCover()
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            var brush = new LinearGradientBrush(Color.FromRgb(0x12, 0x30, 0x3F), Color.FromRgb(0xF3, 0xB5, 0x7A), 90);
            dc.DrawRectangle(brush, null, new Rect(0, 0, 128, 128));
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xFF, 0xB1, 0x5C)), null, new Point(64, 84), 26, 26);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x10, 0x46, 0x50)), null, new Rect(0, 84, 128, 44));
        }
        var rtb = new RenderTargetBitmap(128, 128, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }
}
