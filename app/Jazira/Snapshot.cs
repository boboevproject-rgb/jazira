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
        CalendarSelfTest(sb);
        FolderWatchSelfTest(sb);
        FolderWatchLiveTest(sb);
        CaptureSelfTest(sb);
        File.WriteAllText(path, sb.ToString());
    }

    // Скачанное: какие имена браузеры дают недокачанным файлам и когда файл считается готовым
    static void FolderWatchSelfTest(System.Text.StringBuilder sb)
    {
        int fails = 0;
        void Check(string name, object? got, object? want)
        {
            bool ok = Equals(got, want);
            if (!ok) fails++;
            sb.AppendLine($"files {(ok ? "ok  " : "FAIL")} {name}: {got} (want {want})");
        }
        Check("name pdf", FolderWatch.IsCandidate("договор.pdf"), true);
        Check("name upper ext", FolderWatch.IsCandidate("Photo.JPG"), true);
        Check("name chrome temp", FolderWatch.IsCandidate("договор.pdf.crdownload"), false);
        Check("name chrome unconfirmed", FolderWatch.IsCandidate("Unconfirmed 123456.crdownload"), false);
        Check("name firefox part", FolderWatch.IsCandidate("film.mkv.part"), false);
        Check("name office lock", FolderWatch.IsCandidate("~$отчёт.docx"), false);
        Check("name desktop.ini", FolderWatch.IsCandidate("desktop.ini"), false);

        var dir = Path.Combine(Path.GetTempPath(), "jazira-selftest-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var now = DateTime.Now;
            string F(string n, int bytes) { var p = Path.Combine(dir, n); File.WriteAllBytes(p, new byte[bytes]); return p; }
            string Run(string path, int steps)
            {
                var p = new FolderWatch.Pending { Since = now };
                var log = new System.Collections.Generic.List<string>();
                var v = FolderWatch.Verdict.Wait;
                for (int i = 0; i < steps && v == FolderWatch.Verdict.Wait; i++) { v = FolderWatch.Probe(path, p, now); log.Add(v.ToString()); }
                return string.Join(">", log);
            }
            Check("ready when size is stable", Run(F("a.pdf", 10), 5), "Wait>Wait>Ready");
            var b = F("b.zip", 0); F("b.zip.part", 100);
            Check("firefox placeholder", Run(b, 5), "Drop");
            var c = F("c.png", 10);
            File.SetCreationTime(c, now.AddHours(-3)); File.SetLastWriteTime(c, now.AddHours(-3));
            Check("old file moved in", Run(c, 5), "Drop");
            Check("missing file", Run(Path.Combine(dir, "nope.pdf"), 5), "Drop");
            Check("empty file waits", Run(F("d.txt", 0), 2), "Wait>Wait");
            var e = F("e.bin", 10);
            using (new FileStream(e, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Check("locked file waits", Run(e, 4), "Wait>Wait>Wait>Wait");
        }
        finally { try { Directory.Delete(dir, true); } catch (Exception) { } }
        sb.AppendLine($"files: {(fails == 0 ? "all ok" : fails + " FAILED")}");
    }

    // Настоящее слежение за папкой: как пишут файлы Chrome (Unconfirmed → .crdownload → имя) и Firefox
    // (пустой файл с именем + .part, потом .part поверх), плюс старый файл, перенесённый в папку, и свой снимок
    static void FolderWatchLiveTest(System.Text.StringBuilder sb)
    {
        var dir = Path.Combine(Path.GetTempPath(), "jazira-watch-" + Guid.NewGuid().ToString("N")[..8]);
        var other = dir + "-other";
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(other);
        var got = new System.Collections.Generic.List<string>();
        try
        {
            using var w = new FolderWatch(System.Windows.Threading.Dispatcher.CurrentDispatcher, dir);
            w.Arrived += files => got.AddRange(files.Select(f => Path.GetFileName(f)));
            w.Enabled = true;
            string P(string n) => Path.Combine(dir, n);

            File.WriteAllBytes(P("Unconfirmed 4711.crdownload"), new byte[300]);
            Pump(300);
            File.Move(P("Unconfirmed 4711.crdownload"), P("chrome.pdf.crdownload"));
            Pump(300);
            File.Move(P("chrome.pdf.crdownload"), P("chrome.pdf"));

            File.WriteAllBytes(P("firefox.zip"), Array.Empty<byte>());
            File.WriteAllBytes(P("firefox.zip.part"), new byte[500]);
            Pump(1800);
            File.Move(P("firefox.zip.part"), P("firefox.zip"), true);

            var old = Path.Combine(other, "old.jpg");
            File.WriteAllBytes(old, new byte[50]);
            File.SetCreationTime(old, DateTime.Now.AddDays(-3));
            File.SetLastWriteTime(old, DateTime.Now.AddDays(-3));
            File.Move(old, P("old.jpg"));

            w.Ignore(P("own.png"));
            File.WriteAllBytes(P("own.png.tmp"), new byte[40]);
            File.Move(P("own.png.tmp"), P("own.png"));
            Pump(5000);
        }
        finally
        {
            try { Directory.Delete(dir, true); Directory.Delete(other, true); } catch (Exception) { }
        }
        got.Sort(StringComparer.Ordinal);
        var result = string.Join(",", got);
        sb.AppendLine($"watch {(result == "chrome.pdf,firefox.zip" ? "ok  " : "FAIL")} arrived: {result} (want chrome.pdf,firefox.zip)");
    }

    // Пока ждём — окна и таймеры живут (нужно FileSystemWatcher и таймеру проверки)
    static void Pump(int ms)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        t.Tick += (_, _) => { t.Stop(); frame.Continue = false; };
        t.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    // Снимок экрана — только в память (никуда не сохраняется) — и свободна ли Ctrl+PrtScn
    static void CaptureSelfTest(System.Text.StringBuilder sb)
    {
        try
        {
            var img = ScreenShot.Capture();
            var b = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).Bounds;
            bool ok = img.PixelWidth == b.Width && img.PixelHeight == b.Height;
            sb.AppendLine($"shot {(ok ? "ok  " : "FAIL")} capture {img.PixelWidth}×{img.PixelHeight}, screen {b.Width}×{b.Height}");
        }
        catch (Exception ex) { sb.AppendLine($"shot FAIL capture: {ex.Message}"); }
        var src = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("jazira-hotkey-probe"));
        try
        {
            using var keys = new HotKeys(src.Handle);
            bool free = keys.Set("probe", HotKeys.Ctrl, 0x2C, () => { });
            sb.AppendLine($"hotkey Ctrl+PrtScn: {(free ? "free" : "taken by another app")}");
        }
        finally { src.Dispose(); }
    }

    // Календарь: разовые встречи в UTC и в чужом поясе, еженедельный повтор с исключением и переносом,
    // «второй вторник месяца», ежедневный до даты, отменённая встреча, весь день, перенос длинной строки
    static void CalendarSelfTest(System.Text.StringBuilder sb)
    {
        const string ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
            "BEGIN:VEVENT\r\nUID:a\r\nDTSTART:20261104T100000Z\r\nDTEND:20261104T103000Z\r\nSUMMARY:Standup\r\nX-GOOGLE-CONFERENCE:https://meet.google.com/abc-defg-hij\r\nEND:VEVENT\r\n" +
            "BEGIN:VEVENT\r\nUID:b\r\nDTSTART;TZID=Europe/Moscow:20261105T150000\r\nDTEND;TZID=Europe/Moscow:20261105T160000\r\nSUMMARY:Bank\r\nDESCRIPTION:Join Zoom\\, here: https://us02web.zoom.us/j/123?pwd=x\\nThanks\r\nEND:VEVENT\r\n" +
            "BEGIN:VEVENT\r\nUID:w1\r\nDTSTART;TZID=Asia/Dushanbe:20261102T090000\r\nDTEND;TZID=Asia/Dushanbe:20261102T093000\r\nRRULE:FREQ=WEEKLY;BYDAY=MO,WE\r\nEXDATE;TZID=Asia/Dushanbe:20261104T090000\r\nSUMMARY:Weekly\r\nEND:VEVENT\r\n" +
            "BEGIN:VEVENT\r\nUID:w1\r\nRECURRENCE-ID;TZID=Asia/Dushanbe:20261109T090000\r\nDTSTART;TZID=Asia/Dushanbe:20261109T113000\r\nDTEND;TZID=Asia/Dushanbe:20261109T120000\r\nSUMMARY:Weekly (moved)\r\nEND:VEVENT\r\n" +
            "BEGIN:VEVENT\r\nUID:m\r\nDTSTART;TZID=Asia/Dushanbe:20261013T180000\r\nDURATION:PT1H\r\nRRULE:FREQ=MONTHLY;BYDAY=2TU;COUNT=5\r\nSUMMARY:Board\r\nEND:VEVENT\r\n" +
            "BEGIN:VEVENT\r\nUID:d\r\nDTSTART:20261103T060000Z\r\nDTEND:20261103T061500Z\r\nRRULE:FREQ=DAILY;UNTIL=20261105T060000Z\r\nSUMMARY:Pills\r\nEND:VEVENT\r\n" +
            "BEGIN:VEVENT\r\nUID:x\r\nDTSTART:20261106T100000Z\r\nSTATUS:CANCELLED\r\nSUMMARY:Gone\r\nEND:VEVENT\r\n" +
            "BEGIN:VEVENT\r\nUID:f\r\nDTSTART;VALUE=DATE:20261106\r\nDTEND;VALUE=DATE:20261107\r\nSUMMARY:Holiday with a very long na\r\n me folded\r\nEND:VEVENT\r\n" +
            "END:VCALENDAR\r\n";
        var ev = Ics.Parse(ics, new DateTime(2026, 11, 1), new DateTime(2026, 12, 31));
        DateTime Utc(int d, int h, int m = 0) => new DateTime(2026, 11, d, h, m, 0, DateTimeKind.Utc).ToLocalTime();
        DateTime Zone(string id, int mo, int d, int h, int m = 0) =>
            TimeZoneInfo.ConvertTime(new DateTime(2026, mo, d, h, m, 0), TimeZoneInfo.FindSystemTimeZoneById(id), TimeZoneInfo.Local);
        string Times(string title) => string.Join(",", ev.Where(e => e.Title == title).Select(e => e.Start.ToString("MM-dd HH:mm")));
        int fails = 0;
        void Check(string name, object? got, object? want)
        {
            bool ok = Equals(got, want);
            if (!ok) fails++;
            sb.AppendLine($"cal {(ok ? "ok  " : "FAIL")} {name}: {got} (want {want})");
        }
        var a = ev.FirstOrDefault(e => e.Title == "Standup");
        Check("utc time", a?.Start, Utc(4, 10));
        Check("meet link", a?.Link, "https://meet.google.com/abc-defg-hij");
        var b = ev.FirstOrDefault(e => e.Title == "Bank");
        Check("tzid time", b?.Start, Zone("Europe/Moscow", 11, 5, 15));
        Check("zoom link", b?.Link, "https://us02web.zoom.us/j/123?pwd=x");
        Check("weekly with exdate", string.Join(",", Times("Weekly").Split(',').Take(3)),
              string.Join(",", new[] { Zone("Asia/Dushanbe", 11, 2, 9), Zone("Asia/Dushanbe", 11, 11, 9), Zone("Asia/Dushanbe", 11, 16, 9) }.Select(t => t.ToString("MM-dd HH:mm"))));
        Check("moved occurrence", Times("Weekly (moved)"), Zone("Asia/Dushanbe", 11, 9, 11, 30).ToString("MM-dd HH:mm"));
        Check("second tuesday", Times("Board"), $"{Zone("Asia/Dushanbe", 11, 10, 18):MM-dd HH:mm},{Zone("Asia/Dushanbe", 12, 8, 18):MM-dd HH:mm}");
        Check("daily until", Times("Pills"), $"{Utc(3, 6):MM-dd HH:mm},{Utc(4, 6):MM-dd HH:mm},{Utc(5, 6):MM-dd HH:mm}");
        Check("cancelled skipped", ev.Any(e => e.Title == "Gone"), false);
        var f = ev.FirstOrDefault(e => e.Title.StartsWith("Holiday"));
        Check("all-day + folded", $"{f?.AllDay} {f?.Title}", "True Holiday with a very long name folded");
        Check("normalize webcal", CalendarService.Normalize(" webcal://calendar.google.com/calendar/ical/x/basic.ics "), "https://calendar.google.com/calendar/ical/x/basic.ics");
        Check("reject http", CalendarService.Normalize("http://example.com/a.ics"), null);
        sb.AppendLine($"calendar: {(fails == 0 ? "all ok" : fails + " FAILED")}");
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
