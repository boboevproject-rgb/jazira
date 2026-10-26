using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jazira;

// Настройки и журнал воды — только на этом компьютере: %APPDATA%\Jazira\state.json
public sealed class AppState
{
    public string? Lang { get; set; }
    public bool ShowIsland { get; set; } = true;
    public int WaterGoalMl { get; set; } = 2000;
    public Dictionary<string, int> WaterLog { get; set; } = new();
    public DateTime? LastDrink { get; set; }
    public int Page { get; set; }

    public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jazira");
    static string FilePath => Path.Combine(Dir, "state.json");
    public static AppState Current { get; private set; } = new();
    public static string Today => DateTime.Now.ToString("yyyy-MM-dd");

    [JsonIgnore]
    public int WaterToday => WaterLog.TryGetValue(Today, out var v) ? v : 0;

    public void AddWater(int ml)
    {
        WaterLog[Today] = Math.Max(0, WaterToday + ml);
        if (ml > 0) LastDrink = DateTime.Now;
        // Храним два месяца — дальше история не нужна
        var cutoff = DateTime.Now.AddDays(-60).ToString("yyyy-MM-dd");
        foreach (var day in WaterLog.Keys.Where(k => string.CompareOrdinal(k, cutoff) < 0).ToList()) WaterLog.Remove(day);
        Save();
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath)) Current = JsonSerializer.Deserialize<AppState>(File.ReadAllText(FilePath)) ?? new();
        }
        catch (Exception) { Current = new(); } // испорченный файл — начинаем с чистого листа
    }

    public static void Save()
    {
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
