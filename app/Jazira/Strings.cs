using System;
using System.Collections.Generic;
using System.Globalization;

namespace Jazira;

// Тексты приложения на трёх языках, как на сайте: ru — русский, en — английский, tg — таджикский.
static class Strings
{
    public static readonly string[] Langs = { "ru", "en", "tg" };
    public static string Lang { get; set; } = "ru";

    static readonly Dictionary<string, string[]> T = new()
    {
        ["music.none"] = new[] { "Ничего не играет", "Nothing playing", "Ҳеҷ чиз садо намедиҳад" },
        ["music.hint"] = new[] { "Включите музыку в любом плеере", "Start music in any player", "Мусиқиро дар ҳар плеер фаъол кунед" },
        ["focus.title"] = new[] { "Фокус", "Focus", "Тамаркуз" },
        ["focus.break"] = new[] { "Перерыв", "Break", "Танаффус" },
        ["focus.start"] = new[] { "Старт", "Start", "Оғоз" },
        ["focus.pause"] = new[] { "Пауза", "Pause", "Таваққуф" },
        ["focus.toBreak"] = new[] { "Перерыв 5 мин", "5 min break", "Танаффус 5 дақ" },
        ["focus.toFocus"] = new[] { "Фокус 25 мин", "25 min focus", "Тамаркуз 25 дақ" },
        ["focus.done"] = new[] { "Фокус завершён — время отдохнуть", "Focus done — time for a break", "Тамаркуз анҷом ёфт — вақти истироҳат" },
        ["focus.breakDone"] = new[] { "Перерыв окончен — за работу", "Break's over — back to it", "Танаффус тамом шуд — ба кор" },
        ["water.today"] = new[] { "Сегодня", "Today", "Имрӯз" },
        ["water.goal"] = new[] { "цель достигнута", "goal reached", "ҳадаф иҷро шуд" },
        ["water.remind"] = new[] { "Пора выпить воды", "Time for some water", "Вақти нӯшидани об" },
        ["water.thirsty"] = new[] { "Рыбка хочет пить", "Your koi is thirsty", "Моҳӣ об мехоҳад" },
        ["water.koiRemind"] = new[] { "Рыбке не хватает воды", "Your koi needs water", "Ба моҳӣ об намерасад" },
        ["water.undo"] = new[] { "Отменить", "Undo", "Бекор" },
        ["ml"] = new[] { "мл", "ml", "мл" },
        ["l"] = new[] { "л", "L", "л" },
        ["charge.on"] = new[] { "Зарядка подключена", "Charger connected", "Барқгир пайваст шуд" },
        ["charge.off"] = new[] { "Работа от батареи", "On battery", "Аз ҳисоби батарея" },

        ["shelf.title"] = new[] { "Полка", "Shelf", "Раф" },
        ["shelf.hint"] = new[] { "Перетащите сюда файлы — потом вытащите куда нужно", "Drop files here, drag them out wherever you need", "Файлҳоро ба ин ҷо кашед — баъд ба ҷои лозимӣ кашида баред" },
        ["shelf.drop"] = new[] { "Отпустите — файл останется на полке", "Let go — the file stays on the shelf", "Раҳо кунед — файл дар раф мемонад" },
        ["shelf.clear"] = new[] { "Очистить", "Clear", "Тоза кардан" },

        ["vol.title"] = new[] { "Громкость", "Volume", "Баландии овоз" },
        ["vol.muted"] = new[] { "Звук выключен", "Sound off", "Овоз хомӯш" },
        ["bright.title"] = new[] { "Яркость", "Brightness", "Равшанӣ" },
        ["caps.on"] = new[] { "Caps Lock включён", "Caps Lock on", "Caps Lock фаъол" },
        ["caps.off"] = new[] { "Caps Lock выключен", "Caps Lock off", "Caps Lock хомӯш" },
        ["layout.sub"] = new[] { "Раскладка клавиатуры", "Keyboard layout", "Забони клавиатура" },
        ["cam.on"] = new[] { "Камера включена", "Camera is on", "Камера фаъол аст" },
        ["mic.on"] = new[] { "Микрофон включён", "Microphone is on", "Микрофон фаъол аст" },

        ["wx.clear"] = new[] { "Ясно", "Clear", "Соф" },
        ["wx.partly"] = new[] { "Переменная облачность", "Partly cloudy", "Қисман абрнок" },
        ["wx.cloudy"] = new[] { "Облачно", "Cloudy", "Абрнок" },
        ["wx.fog"] = new[] { "Туман", "Fog", "Туман" },
        ["wx.drizzle"] = new[] { "Морось", "Drizzle", "Борони майда" },
        ["wx.rain"] = new[] { "Дождь", "Rain", "Борон" },
        ["wx.showers"] = new[] { "Ливень", "Showers", "Борони сахт" },
        ["wx.snow"] = new[] { "Снег", "Snow", "Барф" },
        ["wx.storm"] = new[] { "Гроза", "Thunderstorm", "Раъду барқ" },
        ["local.loading"] = new[] { "Загрузка…", "Loading…", "Боргирӣ…" },
        ["local.offline"] = new[] { "Нет связи", "Offline", "Пайваст нест" },
        ["rate.note"] = new[] { "Нацбанк РТ", "NBT", "БМТ" },
        ["tjs"] = new[] { "смн", "TJS", "смн" },
        ["prayer.next"] = new[] { "Следующий намаз", "Next prayer", "Намози навбатӣ" },
        ["prayer.now"] = new[] { "Время намаза", "Prayer time", "Вақти намоз" },
        ["prayer.fajr"] = new[] { "Бомдод", "Fajr", "Бомдод" },
        ["prayer.sunrise"] = new[] { "Восход", "Sunrise", "Тулӯъ" },
        ["prayer.dhuhr"] = new[] { "Пешин", "Dhuhr", "Пешин" },
        ["prayer.asr"] = new[] { "Аср", "Asr", "Аср" },
        ["prayer.maghrib"] = new[] { "Шом", "Maghrib", "Шом" },
        ["prayer.isha"] = new[] { "Хуфтан", "Isha", "Хуфтан" },
        ["in"] = new[] { "через {0}", "in {0}", "баъди {0}" },
        ["h"] = new[] { "ч", "h", "соат" },
        ["min"] = new[] { "мин", "min", "дақ" },

        ["update.ready"] = new[] { "Доступна Jazira {0}", "Jazira {0} is available", "Jazira {0} дастрас аст" },
        ["update.btn"] = new[] { "Обновить", "Update", "Навсозӣ" },
        ["update.done"] = new[] { "Jazira обновлена до {0}", "Updated to Jazira {0}", "Jazira то {0} навсозӣ шуд" },
        ["update.latest"] = new[] { "У вас последняя версия", "You're up to date", "Версияи охирин насб аст" },
        ["update.fail"] = new[] { "Не удалось проверить обновления", "Couldn't check for updates", "Навсозиро санҷида нашуд" },

        ["tray.show"] = new[] { "Показывать остров", "Show the island", "Нишон додани ҷазира" },
        ["tray.autostart"] = new[] { "Запускать вместе с Windows", "Start with Windows", "Бо Windows оғоз шавад" },
        ["tray.water"] = new[] { "Цель по воде", "Water goal", "Ҳадафи об" },
        ["tray.lang"] = new[] { "Язык", "Language", "Забон" },
        ["tray.site"] = new[] { "Сайт Jazira", "Jazira website", "Сайти Jazira" },
        ["tray.exit"] = new[] { "Выход", "Exit", "Баромад" },
        ["tray.look"] = new[] { "Вид", "Look", "Намуд" },
        ["theme.night"] = new[] { "Ночь", "Night", "Шаб" },
        ["theme.glass"] = new[] { "Стекло", "Glass", "Шиша" },
        ["theme.lagoon"] = new[] { "Лагуна", "Lagoon", "Лагуна" },
        ["theme.sunset"] = new[] { "Закат", "Sunset", "Ғуруб" },
        ["theme.light"] = new[] { "Светлая", "Light", "Равшан" },
        ["shape.pill"] = new[] { "Форма: капсула", "Shape: capsule", "Шакл: капсула" },
        ["shape.notch"] = new[] { "Форма: вырез у края экрана", "Shape: notch at the edge", "Шакл: часпида ба канори экран" },
        ["tray.tint"] = new[] { "Подсветка цветом обложки", "Glow in the cover's color", "Равшанӣ бо ранги муқова" },
        ["tray.place"] = new[] { "Расположение", "Position", "Ҷойгиршавӣ" },
        ["place.dock"] = new[] { "Прикрепить к верху экрана", "Pin to the top of the screen", "Ба болои экран часпонидан" },
        ["place.float"] = new[] { "Свободно — перетащите мышью куда угодно", "Free — drag it anywhere", "Озод — бо муш ба ҳар ҷо кашед" },
        ["place.monitor"] = new[] { "Монитор {0}", "Monitor {0}", "Монитор {0}" },
        ["place.primary"] = new[] { "основной", "primary", "асосӣ" },
        ["tray.scale"] = new[] { "Размер", "Size", "Андоза" },
        ["tray.features"] = new[] { "Функции", "Features", "Имкониятҳо" },
        ["f.lyrics"] = new[] { "Тексты песен (lrclib.net)", "Song lyrics (lrclib.net)", "Матни сурудҳо (lrclib.net)" },
        ["f.koi"] = new[] { "Рыбка-кои", "Koi fish", "Моҳии кои" },
        ["f.indicators"] = new[] { "Индикаторы: громкость, раскладка, камера", "Indicators: volume, keyboard, camera", "Нишондиҳандаҳо: овоз, забон, камера" },
        ["f.local"] = new[] { "Страница «Сегодня»: погода и курс", "“Today” page: weather and rates", "Саҳифаи «Имрӯз»: обу ҳаво ва қурб" },
        ["f.prayer"] = new[] { "Время намаза (расчёт, сверяйте с мечетью)", "Prayer times (calculated, check your mosque)", "Вақти намоз (ҳисобӣ, бо масҷид муқоиса кунед)" },
        ["tray.city"] = new[] { "Город", "City", "Шаҳр" },
        ["tray.autoupdate"] = new[] { "Обновлять автоматически", "Update automatically", "Худкор навсозӣ шавад" },
        ["tray.check"] = new[] { "Проверить обновления", "Check for updates", "Санҷиши навсозӣ" },
        ["tray.install"] = new[] { "Обновить до {0}", "Update to {0}", "Навсозӣ то {0}" },
    };

    public static string Get(string key)
    {
        if (!T.TryGetValue(key, out var v)) return key;
        int i = Array.IndexOf(Langs, Lang);
        return v[i < 0 ? 0 : i];
    }

    public static string Format(string key, params object[] args) => string.Format(Culture, Get(key), args);

    // Числа: «1 250» по-русски и по-таджикски, «1,250» по-английски
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Lang == "en" ? "en-US" : "ru-RU");

    public static string Detect()
    {
        var code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return code == "ru" || code == "tg" ? code : "en";
    }
}
