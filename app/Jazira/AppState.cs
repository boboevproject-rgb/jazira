using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jazira;

// Настройки, журнал воды и полка — только на этом компьютере: %APPDATA%\Jazira\state.json
public sealed class AppState
{
    public string? Lang { get; set; }
    public bool ShowIsland { get; set; } = true;
    public int WaterGoalMl { get; set; } = 2000;
    public Dictionary<string, int> WaterLog { get; set; } = new();
    public DateTime? LastDrink { get; set; }
    public int Page { get; set; }            // 0.1: номер страницы; с 0.2 — PageName
    public string? PageName { get; set; }

    // Вид и место
    public string Theme { get; set; } = "night";
    public string Shape { get; set; } = "pill";   // pill — капсула; notch — вырез, слитый с краем экрана
    public bool Docked { get; set; } = true;      // прикреплён к верху экрана или стоит где угодно
    public string? Monitor { get; set; }          // имя монитора Windows; пусто — основной
    public double FloatX { get; set; } = 0.5;     // свободное место: центр острова в долях рабочей области
    public double FloatY { get; set; } = 0.25;
    public double Scale { get; set; } = 1.0;
    public bool CoverTint { get; set; } = true;

    // Функции
    public bool Lyrics { get; set; } = true;
    public bool Koi { get; set; } = true;
    public bool Indicators { get; set; } = true;
    public bool LocalPage { get; set; } = true;
    public bool Prayer { get; set; }
    public string City { get; set; } = "dushanbe";
    public bool AutoUpdate { get; set; } = true;
    public List<string> Shelf { get; set; } = new();
    public bool DayPage { get; set; } = true;
    public bool DownloadsToShelf { get; set; } = true;  // новый файл в «Загрузках» — сам на полку
    public bool ShotsToShelf { get; set; } = true;      // снимки экрана — на полку; Ctrl+PrtScn снимает экран
    public bool SysPage { get; set; } = true;           // страница «Система»
    public bool LowBattery { get; set; } = true;        // предупредить, когда батарея 10 %
    public string? CalendarUrl { get; set; }   // секретная iCal-ссылка Google или Outlook — только здесь, в журнал не пишется
    public bool Ramadan { get; set; } = true;  // в месяц Рамазан — отсчёт до ифтара и сухура
    public List<TaskItem> Tasks { get; set; } = new();
    public List<TimerItem> Timers { get; set; } = new();

    // Рыбка-питомец: дни, когда цель по воде выполнена. По ним кои растёт, а самая длинная серия
    // открывает окраски и вторую рыбку — насовсем, даже если потом серия прервалась (KoiPet.cs)
    public List<string>? KoiDays { get; set; }

    public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jazira");
    static string FilePath => Path.Combine(Dir, "state.json");
    public static AppState Current { get; private set; } = new();
    public static string Today => DateTime.Now.ToString("yyyy-MM-dd");
    public static bool ReadOnly; // снимки (--snapshot) меняют настройки только в памяти — файл не трогаем

    [JsonIgnore]
    public int WaterToday => WaterLog.TryGetValue(Today, out var v) ? v : 0;

    public void AddWater(int ml)
    {
        WaterLog[Today] = Math.Max(0, WaterToday + ml);
        if (ml > 0) LastDrink = DateTime.Now;
        // Храним два месяца — дальше история не нужна
        var cutoff = DateTime.Now.AddDays(-60).ToString("yyyy-MM-dd");
        foreach (var day in WaterLog.Keys.Where(k => string.CompareOrdinal(k, cutoff) < 0).ToList()) WaterLog.Remove(day);
        KoiPet.Sync(this);
        Save();
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath)) Current = JsonSerializer.Deserialize<AppState>(File.ReadAllText(FilePath)) ?? new();
        }
        catch (Exception) { Current = new(); } // испорченный файл — начинаем с чистого листа
        KoiPet.Migrate(Current);
    }

    public static void Save()
    {
        if (ReadOnly) return;
        try
        {
            Directory.CreateDirectory(Dir);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, FilePath, true);
        }
        catch (Exception ex) { App.Log(ex); }
    }
}
