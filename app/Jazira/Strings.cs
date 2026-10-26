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
        ["water.undo"] = new[] { "Отменить", "Undo", "Бекор" },
        ["ml"] = new[] { "мл", "ml", "мл" },
        ["l"] = new[] { "л", "L", "л" },
        ["charge.on"] = new[] { "Зарядка подключена", "Charger connected", "Барқгир пайваст шуд" },
        ["charge.off"] = new[] { "Работа от батареи", "On battery", "Аз ҳисоби батарея" },
        ["tray.show"] = new[] { "Показывать остров", "Show the island", "Нишон додани ҷазира" },
        ["tray.autostart"] = new[] { "Запускать вместе с Windows", "Start with Windows", "Бо Windows оғоз шавад" },
        ["tray.water"] = new[] { "Цель по воде", "Water goal", "Ҳадафи об" },
        ["tray.lang"] = new[] { "Язык", "Language", "Забон" },
        ["tray.site"] = new[] { "Сайт Jazira", "Jazira website", "Сайти Jazira" },
        ["tray.exit"] = new[] { "Выход", "Exit", "Баромад" },
    };

    public static string Get(string key)
    {
        if (!T.TryGetValue(key, out var v)) return key;
        int i = Array.IndexOf(Langs, Lang);
        return v[i < 0 ? 0 : i];
    }

    // Числа: «1 250» по-русски и по-таджикски, «1,250» по-английски
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Lang == "en" ? "en-US" : "ru-RU");

    public static string Detect()
    {
        var code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return code == "ru" || code == "tg" ? code : "en";
    }
}
