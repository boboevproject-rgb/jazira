using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Jazira;

sealed record City(string Id, double Lat, double Lon, string Ru, string En, string Tg)
{
    public string Name => Strings.Lang switch { "en" => En, "tg" => Tg, _ => Ru };
}

// Страница «Сегодня»: погода (Open-Meteo, без ключа), официальный курс сомони (Национальный банк Таджикистана)
// и время намаза (считается на компьютере, в сеть не ходит).
sealed class LocalInfo
{
    public static readonly City[] Cities =
    {
        new("dushanbe", 38.5598, 68.7870, "Душанбе", "Dushanbe", "Душанбе"),
        new("khujand", 40.2826, 69.6222, "Худжанд", "Khujand", "Хуҷанд"),
        new("bokhtar", 37.8364, 68.7803, "Бохтар", "Bokhtar", "Бохтар"),
        new("kulob", 37.9146, 69.7845, "Куляб", "Kulob", "Кӯлоб"),
        new("khorog", 37.4897, 71.5530, "Хорог", "Khorog", "Хоруғ"),
        new("istaravshan", 39.9108, 69.0064, "Истаравшан", "Istaravshan", "Истаравшан"),
        new("panjakent", 39.4952, 67.6093, "Пенджикент", "Panjakent", "Панҷакент"),
        new("tursunzoda", 38.5108, 68.2303, "Турсунзаде", "Tursunzoda", "Турсунзода"),
        new("isfara", 40.1265, 70.6253, "Исфара", "Isfara", "Исфара"),
        new("vahdat", 38.5531, 69.0197, "Вахдат", "Vahdat", "Ваҳдат"),
        new("moscow", 55.7558, 37.6173, "Москва", "Moscow", "Маскав"),
        new("spb", 59.9343, 30.3351, "Санкт-Петербург", "Saint Petersburg", "Санкт-Петербург"),
    };

    public static City CityOf(string? id) => Cities.FirstOrDefault(c => c.Id == id) ?? Cities[0];

    public double? Temp { get; private set; }
    public int Code { get; private set; }
    public bool IsDay { get; private set; } = true;
    public string WeatherCity { get; private set; } = "";
    public decimal? Usd { get; private set; }
    public decimal? Rub { get; private set; }
    public DateTime RatesDate { get; private set; }

    DateTime _weatherAt = DateTime.MinValue, _ratesAt = DateTime.MinValue;
    bool _busy;

    public event Action? Changed;

    // Погода — раз в 30 минут, курс — раз в 3 часа; при ошибке — повтор через 5 минут
    public async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var city = CityOf(AppState.Current.City);
            bool changed = false;
            if (WeatherCity != city.Id || DateTime.Now - _weatherAt > TimeSpan.FromMinutes(30))
            {
                try { await LoadWeatherAsync(city); _weatherAt = DateTime.Now; changed = true; }
                catch (Exception) { _weatherAt = DateTime.Now - TimeSpan.FromMinutes(25); }
            }
            if (DateTime.Now - _ratesAt > TimeSpan.FromHours(3))
            {
                try { changed |= await LoadRatesAsync(); _ratesAt = DateTime.Now; }
                catch (Exception) { _ratesAt = DateTime.Now - TimeSpan.FromHours(3) + TimeSpan.FromMinutes(5); }
            }
            if (changed) Changed?.Invoke();
        }
        finally { _busy = false; }
    }

    async Task LoadWeatherAsync(City c)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"https://api.open-meteo.com/v1/forecast?latitude={c.Lat}&longitude={c.Lon}&current=temperature_2m,weather_code,is_day");
        using var doc = JsonDocument.Parse(await Net.Http.GetStringAsync(url));
        var cur = doc.RootElement.GetProperty("current");
        Temp = cur.GetProperty("temperature_2m").GetDouble();
        Code = cur.GetProperty("weather_code").GetInt32();
        IsDay = cur.GetProperty("is_day").GetInt32() == 1;
        WeatherCity = c.Id;
    }

    // Официальный курс НБТ на сегодня; рано утром его ещё нет — тогда вчерашний
    async Task<bool> LoadRatesAsync()
    {
        for (int back = 0; back < 4; back++)
        {
            var day = DateTime.Today.AddDays(-back);
            var xml = await Net.Http.GetStringAsync($"https://nbt.tj/en/kurs/export_xml.php?date={day:yyyy-MM-dd}&export=xmlout");
            var root = XDocument.Parse(xml).Root;
            decimal? usd = Rate(root, "USD"), rub = Rate(root, "RUB");
            if (usd == null) continue;
            Usd = usd;
            Rub = rub;
            RatesDate = day;
            return true;
        }
        return false;
    }

    static decimal? Rate(XElement? root, string code)
    {
        var v = root?.Elements("Valute").FirstOrDefault(e => (string?)e.Element("CharCode") == code);
        if (v == null) return null;
        if (!decimal.TryParse((string?)v.Element("Value"), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)) return null;
        int.TryParse((string?)v.Element("Nominal"), out var nominal);
        return value / Math.Max(1, nominal);
    }

    // Код погоды WMO → ключ текста
    public static string WeatherKey(int code) => code switch
    {
        0 => "wx.clear",
        1 or 2 => "wx.partly",
        3 => "wx.cloudy",
        45 or 48 => "wx.fog",
        >= 51 and <= 57 => "wx.drizzle",
        >= 61 and <= 67 => "wx.rain",
        >= 71 and <= 77 => "wx.snow",
        >= 80 and <= 82 => "wx.showers",
        85 or 86 => "wx.snow",
        >= 95 => "wx.storm",
        _ => "wx.cloudy",
    };

    // ---- Для снимков ----
    public void Fake(double temp, int code, decimal usd, decimal rub)
    {
        Temp = temp; Code = code; IsDay = true; Usd = usd; Rub = rub;
        RatesDate = DateTime.Today;
        WeatherCity = CityOf(AppState.Current.City).Id;
    }
}

// Время намаза по астрономическим формулам: метод Всемирной исламской лиги (фаджр 18°, иша 17°),
// аср — по ханафитскому мазхабу (тень вдвое длиннее предмета). Возможна разница в пару минут
// с расписанием мечети — в подсказке об этом сказано.
static class PrayerTimes
{
    public static readonly string[] Keys = { "fajr", "sunrise", "dhuhr", "asr", "maghrib", "isha" };

    // Время по UTC для календарного дня date
    public static DateTime[] ForDate(DateTime date, double lat, double lon)
    {
        double jd = JulianDay(date.Year, date.Month, date.Day) - lon / 360.0 + 0.5; // около местного полудня
        var (decl, eqt) = Sun(jd);
        double dhuhr = 12 - lon / 15 - eqt;

        double sunrise = dhuhr - HourAngle(0.833, lat, decl);
        double sunset = dhuhr + HourAngle(0.833, lat, decl);
        double fajr = dhuhr - HourAngle(18, lat, decl);
        double isha = dhuhr + HourAngle(17, lat, decl);
        double asrAlt = Deg(Math.Atan(1 / (2 + Math.Tan(Rad(Math.Abs(lat - decl))))));
        double asr = dhuhr + HourAngle(-asrAlt, lat, decl);

        // Белые ночи на севере: солнце не опускается на 18° — берём долю ночи
        double night = 24 - (sunset - sunrise);
        if (double.IsNaN(fajr)) fajr = sunrise - night * 18 / 60;
        if (double.IsNaN(isha)) isha = sunset + night * 17 / 60;

        var day = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
        return new[] { fajr, sunrise, dhuhr, asr, sunset, isha }
            .Select(h => day.AddMinutes(Math.Round(h * 60)))
            .ToArray();
    }

    // Угол солнца под горизонтом angle (для восхода 0,833° — с учётом рефракции) → часы от полудня
    static double HourAngle(double angle, double lat, double decl)
    {
        double cos = (-Math.Sin(Rad(angle)) - Math.Sin(Rad(decl)) * Math.Sin(Rad(lat))) / (Math.Cos(Rad(decl)) * Math.Cos(Rad(lat)));
        return cos < -1 || cos > 1 ? double.NaN : Deg(Math.Acos(cos)) / 15;
    }

    // Склонение солнца и уравнение времени (в часах)
    static (double Decl, double EqT) Sun(double jd)
    {
        double d = jd - 2451545.0;
        double g = Fix(357.529 + 0.98560028 * d, 360);
        double q = Fix(280.459 + 0.98564736 * d, 360);
        double l = Fix(q + 1.915 * Math.Sin(Rad(g)) + 0.020 * Math.Sin(Rad(2 * g)), 360);
        double e = 23.439 - 0.00000036 * d;
        double ra = Fix(Deg(Math.Atan2(Math.Cos(Rad(e)) * Math.Sin(Rad(l)), Math.Cos(Rad(l)))) / 15, 24);
        double decl = Deg(Math.Asin(Math.Sin(Rad(e)) * Math.Sin(Rad(l))));
        double eqt = q / 15 - ra;
        if (eqt > 12) eqt -= 24;
        if (eqt < -12) eqt += 24;
        return (decl, eqt);
    }

    static double JulianDay(int y, int m, int day)
    {
        if (m <= 2) { y--; m += 12; }
        int a = y / 100, b = 2 - a + a / 4;
        return Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + day + b - 1524.5;
    }

    static double Fix(double v, double range) { v %= range; return v < 0 ? v + range : v; }
    static double Rad(double d) => d * Math.PI / 180;
    static double Deg(double r) => r * 180 / Math.PI;
}
