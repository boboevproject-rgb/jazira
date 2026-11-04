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

        ["koi.line"] = new[] { "Кои: {0} · серия {1}", "Koi: {0} · streak {1}", "Кои: {0} · силсила {1}" },
        ["koi.age"] = new[] { "Кои: {0}", "Koi: {0}", "Кои: {0}" },
        ["koi.fry"] = new[] { "Малёк растёт с каждой целью", "A fry, grows with every goal", "Моҳича бо ҳар ҳадаф калон мешавад" },
        ["koi.sad"] = new[] { "Кои скучала — начнём заново", "Koi missed you — fresh start", "Кои дилтанг шуд — аз нав" },
        ["koi.next"] = new[] { "До «{1}» — ещё {0} подряд", "{0} more in a row for “{1}”", "То «{1}» — боз {0} пай дар пай" },
        ["koi.new.sanke"] = new[] { "Новая окраска — санкэ!", "New colors — sanke!", "Ранги нав — санке!" },
        ["koi.new.second"] = new[] { "В пруду вторая рыбка!", "A second koi joined!", "Моҳии дуюм пайдо шуд!" },
        ["koi.new.ogon"] = new[] { "Золотая кои — огон!", "Golden koi — ogon!", "Кои тиллоӣ — огон!" },
        ["koi.goal"] = new[] { "Цель выполнена — кои подросла", "Goal reached — your koi grew", "Ҳадаф иҷро шуд — кои калон шуд" },
        ["koi.share"] = new[] { "Картинка «Моя кои» для сторис", "“My koi” picture for stories", "Расми «Кои ман» барои сторис" },
        ["koi.saved"] = new[] { "Картинка готова: в буфере и на полке", "Picture ready: copied and on the shelf", "Расм тайёр: дар буфер ва дар раф" },
        ["koi.open"] = new[] { "Открыть", "Open", "Кушодан" },
        ["koi.fail"] = new[] { "Не удалось сохранить картинку", "Couldn't save the picture", "Расмро нигоҳ дошта нашуд" },
        ["kind.kohaku"] = new[] { "белая с красным кохаку", "white-and-red kohaku", "кохакуи сафеду сурх" },
        ["kind.sanke"] = new[] { "трёхцветная санкэ", "tricolor sanke", "санкеи серанга" },
        ["kind.ogon"] = new[] { "золотая огон", "golden ogon", "огони тиллоӣ" },
        ["chip.kohaku"] = new[] { "кохаку", "kohaku", "кохаку" },
        ["chip.sanke"] = new[] { "санкэ", "sanke", "санке" },
        ["chip.second"] = new[] { "вторая рыбка", "second koi", "моҳии дуюм" },
        ["chip.ogon"] = new[] { "огон", "ogon", "огон" },
        ["card.title"] = new[] { "Моя кои — {0}", "My koi — {0}", "Кои ман — {0}" },
        ["card.first"] = new[] { "Моя кои — первый день", "My koi — day one", "Кои ман — рӯзи аввал" },
        ["card.streak"] = new[] { "{0} подряд", "{0} in a row", "{0} пай дар пай" },
        ["card.grow"] = new[] { "Растёт с каждым днём, когда я пью норму воды", "It grows every day I drink enough water", "Ҳар рӯзе, ки меъёри обро менӯшам, калон мешавад" },
        ["card.app"] = new[] { "Jazira — остров для Windows", "Jazira — an island for Windows", "Jazira — ҷазира барои Windows" },
        ["day.title"] = new[] { "Мой день", "My day", "Рӯзи ман" },
        ["day.add"] = new[] { "+ Задача", "+ Task", "+ Вазифа" },
        ["day.empty"] = new[] { "Задач нет. Например: «позвонить маме в 18:30»", "No tasks yet. Try “call mom at 18:30”", "Вазифа нест. Масалан: «ба модар занг соати 18:30»" },
        ["day.hint"] = new[] { "Задача… можно «в 18:30» или «через 20 мин»", "Task… add “at 18:30” or “in 20 min”", "Вазифа… «соати 18:30» ё «баъди 20 дақ»" },
        ["day.more"] = new[] { "и ещё {0}", "{0} more", "боз {0}" },
        ["day.tomorrow"] = new[] { "завтра", "tomorrow", "фардо" },
        ["cal.menu"] = new[] { "Календарь", "Calendar", "Тақвим" },
        ["cal.connect"] = new[] { "Подключить: скопируйте iCal-ссылку и нажмите сюда", "Connect: copy the iCal link, then click here", "Пайваст: истиноди iCal-ро нусха кунед ва ин ҷоро пахш кунед" },
        ["cal.help"] = new[] { "Где взять ссылку (Google, Outlook)", "Where to find the link (Google, Outlook)", "Истинодро аз куҷо гирифтан (Google, Outlook)" },
        ["cal.connected"] = new[] { "Подключён · обновлён в {0}", "Connected · updated {0}", "Пайваст · навсозӣ {0}" },
        ["cal.waiting"] = new[] { "Подключён · загрузка…", "Connected · loading…", "Пайваст · боргирӣ…" },
        ["cal.failed"] = new[] { "Не удалось загрузить календарь", "Couldn't load the calendar", "Тақвимро боргирӣ карда нашуд" },
        ["cal.refresh"] = new[] { "Обновить сейчас", "Refresh now", "Ҳозир навсозӣ" },
        ["cal.disconnect"] = new[] { "Отключить календарь", "Disconnect calendar", "Ҷудо кардани тақвим" },
        ["cal.nolink"] = new[] { "В буфере нет iCal-ссылки (https://…)", "No iCal link in the clipboard (https://…)", "Дар буфер истиноди iCal нест (https://…)" },
        ["cal.ok"] = new[] { "Календарь подключён", "Calendar connected", "Тақвим пайваст шуд" },
        ["cal.join"] = new[] { "Подключиться", "Join", "Пайвастан" },
        ["cal.now"] = new[] { "идёт · до {0}", "now · until {0}", "ҳозир · то {0}" },
        ["cal.starts"] = new[] { "Начинается сейчас", "Starting now", "Ҳозир оғоз мешавад" },
        ["cal.empty"] = new[] { "Встреч в ближайшие сутки нет", "No meetings in the next 24 hours", "Дар 24 соати наздик вохӯрӣ нест" },
        ["ramadan.iftar"] = new[] { "Ифтар в {0}", "Iftar at {0}", "Ифтор соати {0}" },
        ["ramadan.suhoor"] = new[] { "Сухур до {0}", "Suhoor until {0}", "Саҳарӣ то {0}" },
        ["ramadan.day"] = new[] { "Рамазан · день {0}", "Ramadan · day {0}", "Рамазон · рӯзи {0}" },
        ["ramadan.iftarSoon"] = new[] { "Через 15 минут ифтар", "Iftar in 15 minutes", "Баъди 15 дақиқа ифтор" },
        ["ramadan.iftarNow"] = new[] { "Время ифтара", "Time for iftar", "Вақти ифтор" },
        ["ramadan.suhoorSoon"] = new[] { "Сухур: осталось 30 минут", "Suhoor ends in 30 minutes", "Саҳарӣ: 30 дақиқа монд" },
        ["task.remind"] = new[] { "Напоминание · {0}", "Reminder · {0}", "Хотиррасон · {0}" },
        ["task.done"] = new[] { "Готово", "Done", "Тайёр" },
        ["timer.label"] = new[] { "Таймер", "Timer", "Таймер" },
        ["timer.min"] = new[] { "{0} мин", "{0} min", "{0} дақ" },
        ["timer.done"] = new[] { "Таймер {0} — время вышло", "{0} timer is up", "Таймери {0} — вақт тамом" },
        ["ml"] = new[] { "мл", "ml", "мл" },
        ["l"] = new[] { "л", "L", "л" },
        ["charge.on"] = new[] { "Зарядка подключена", "Charger connected", "Барқгир пайваст шуд" },
        ["charge.off"] = new[] { "Работа от батареи", "On battery", "Аз ҳисоби батарея" },

        ["shelf.title"] = new[] { "Полка", "Shelf", "Раф" },
        ["shelf.hint"] = new[] { "Перетащите сюда файлы — потом вытащите куда нужно", "Drop files here, drag them out wherever you need", "Файлҳоро ба ин ҷо кашед — баъд ба ҷои лозимӣ кашида баред" },
        ["shelf.hintAuto"] = new[] { "Перетащите сюда файлы — скачанное и снимки экрана появятся сами", "Drop files here — downloads and screenshots show up on their own", "Файлҳоро ба ин ҷо кашед — боргириҳо ва аксҳои экран худашон пайдо мешаванд" },
        ["shelf.drop"] = new[] { "Отпустите — файл останется на полке", "Let go — the file stays on the shelf", "Раҳо кунед — файл дар раф мемонад" },
        ["shelf.clear"] = new[] { "Очистить", "Clear", "Тоза кардан" },
        ["shelf.open"] = new[] { "Открыть", "Open", "Кушодан" },
        ["shelf.reveal"] = new[] { "В папке", "Show", "Дар ҷузвдон" },
        ["shelf.shot"] = new[] { "Снимок", "Screenshot", "Акс" },
        ["dl.done"] = new[] { "Скачано", "Downloaded", "Боргирӣ шуд" },
        ["dl.many"] = new[] { "Скачано файлов: {0}", "{0} files downloaded", "Файлҳои боргиришуда: {0}" },
        ["dl.shelf"] = new[] { "на полке", "on the shelf", "дар раф" },
        ["shot.saved"] = new[] { "Снимок экрана на полке", "Screenshot on the shelf", "Акси экран дар раф" },
        ["shot.copied"] = new[] { "и в буфере обмена · {0}", "and in the clipboard · {0}", "ва дар буфер · {0}" },
        ["shot.fail"] = new[] { "Не получилось снять экран", "Couldn't take a screenshot", "Акси экран гирифта нашуд" },
        ["shot.file"] = new[] { "Снимок экрана", "Screenshot", "Акси экран" },
        ["hotkey.busy"] = new[] { "занято другой программой", "taken by another app", "барномаи дигар гирифтааст" },
        ["size.b"] = new[] { "{0} Б", "{0} B", "{0} Б" },
        ["size.kb"] = new[] { "{0} КБ", "{0} KB", "{0} КБ" },
        ["size.mb"] = new[] { "{0} МБ", "{0} MB", "{0} МБ" },
        ["size.gb"] = new[] { "{0} ГБ", "{0} GB", "{0} ГБ" },

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
        ["f.day"] = new[] { "Страница «Мой день»: задачи и напоминания", "“My day” page: tasks and reminders", "Саҳифаи «Рӯзи ман»: вазифаҳо ва хотиррасонҳо" },
        ["f.ramadan"] = new[] { "Режим Рамазана: ифтар и сухур (расчёт, сверяйте с мечетью)", "Ramadan mode: iftar and suhoor (calculated, check your mosque)", "Реҷаи Рамазон: ифтор ва саҳарӣ (ҳисобӣ, бо масҷид муқоиса кунед)" },
        ["f.downloads"] = new[] { "Скачанное — на полку", "Downloads go to the shelf", "Боргириҳо — ба раф" },
        ["f.shots"] = new[] { "Снимки экрана — на полку", "Screenshots go to the shelf", "Аксҳои экран — ба раф" },
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

    // «1 день», «3 дня», «5 дней»; «1 day», «2 days»; «5 рӯз»
    public static string Days(int n) => Lang switch
    {
        "en" => n == 1 ? "1 day" : $"{n} days",
        "tg" => $"{n} рӯз",
        _ => $"{n} " + (n % 10 == 1 && n % 100 != 11 ? "день" : n % 10 is >= 2 and <= 4 && (n % 100 < 12 || n % 100 > 14) ? "дня" : "дней"),
    };

    // Коротко, для тесной строки в острове: «14 дн.», «14 d», «14 рӯз»
    public static string DaysShort(int n) => Lang switch
    {
        "en" => $"{n} d",
        "tg" => $"{n} рӯз",
        _ => $"{n} дн.",
    };

    // Числа: «1 250» по-русски и по-таджикски, «1,250» по-английски
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Lang == "en" ? "en-US" : "ru-RU");

    // Даты словами: «ср, 4 ноября», «Wed, 4 November», по-таджикски — если Windows знает таджикский
    public static CultureInfo DateCulture
    {
        get
        {
            if (Lang == "tg")
                try { return CultureInfo.GetCultureInfo("tg-Cyrl-TJ"); } catch (CultureNotFoundException) { }
            return Lang == "en" ? CultureInfo.GetCultureInfo("en-GB") : CultureInfo.GetCultureInfo("ru-RU");
        }
    }

    public static string Detect()
    {
        var code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return code == "ru" || code == "tg" ? code : "en";
    }
}
