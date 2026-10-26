/**
 * Jazira — приём форм с сайта: лист ожидания и отзывы.
 *
 * Скрипт привязан к Google-таблице: пишет строки на листы «waitlist» и «feedback»
 * (создаёт их сам) и, если в свойствах скрипта заданы TG_TOKEN и TG_CHAT,
 * дублирует каждую заявку в Telegram. Как развернуть — apps-script/README.md.
 */

const SHEETS = {
  waitlist: ["Время", "Язык", "Контакт"],
  feedback: ["Время", "Язык", "Сообщение", "Контакт"]
};
const LIMIT_PER_MIN = 30; // защита от наплыва ботов: больше заявок в минуту не принимаем

function doPost(e) {
  let d;
  try {
    d = JSON.parse((e && e.postData && e.postData.contents) || "{}");
  } catch (err) {
    return json_({ ok: false, error: "bad json" });
  }
  if (d.website) return json_({ ok: true }); // ловушка для ботов: это поле люди не видят

  const action = String(d.action || "");
  if (!SHEETS[action]) return json_({ ok: false, error: "bad action" });
  const lang = clip_(d.lang, 5), contact = clip_(d.contact, 120), msg = clip_(d.msg, 1500);
  if (action === "waitlist" && contact.length < 3) return json_({ ok: false, error: "empty" });
  if (action === "feedback" && msg.length < 3) return json_({ ok: false, error: "empty" });
  if (overLimit_()) return json_({ ok: false, error: "busy" });

  const row = action === "waitlist" ? [new Date(), lang, contact] : [new Date(), lang, msg, contact];
  const lock = LockService.getScriptLock();
  lock.waitLock(10000);
  try {
    sheet_(action).appendRow(row.map(safe_));
  } finally {
    lock.releaseLock();
  }
  notify_(action, lang, contact, msg);
  return json_({ ok: true });
}

// Проверка в браузере: открыть адрес /exec — должен ответить {"ok":true,...}
function doGet() {
  return json_({ ok: true, service: "jazira-forms" });
}

function sheet_(name) {
  const book = SpreadsheetApp.getActiveSpreadsheet();
  let sh = book.getSheetByName(name);
  if (!sh) {
    sh = book.insertSheet(name);
    sh.appendRow(SHEETS[name]);
    sh.setFrozenRows(1);
  }
  return sh;
}

function notify_(action, lang, contact, msg) {
  const props = PropertiesService.getScriptProperties();
  const token = props.getProperty("TG_TOKEN"), chat = props.getProperty("TG_CHAT");
  if (!token || !chat) return;
  const text = action === "waitlist"
    ? "🏝 Новая запись в лист ожидания\n" + contact + "\nЯзык: " + lang
    : "💬 Новый отзыв\n" + msg + (contact ? "\n\nКонтакт: " + contact : "") + "\nЯзык: " + lang;
  try {
    UrlFetchApp.fetch("https://api.telegram.org/bot" + token + "/sendMessage", {
      method: "post",
      payload: { chat_id: chat, text: text, disable_web_page_preview: "true" },
      muteHttpExceptions: true
    });
  } catch (err) {
    console.warn("Telegram: " + err); // заявка уже в таблице — уведомление не главное
  }
}

function overLimit_() {
  const cache = CacheService.getScriptCache();
  const key = "n" + Math.floor(Date.now() / 60000);
  const n = Number(cache.get(key) || 0) + 1;
  cache.put(key, String(n), 120);
  return n > LIMIT_PER_MIN;
}

function clip_(v, max) {
  return String(v == null ? "" : v).trim().slice(0, max);
}

// Значения, начинающиеся с = + - @, таблица приняла бы за формулу — экранируем апострофом
function safe_(v) {
  return typeof v === "string" && /^[=+\-@]/.test(v) ? "'" + v : v;
}

function json_(o) {
  return ContentService.createTextOutput(JSON.stringify(o)).setMimeType(ContentService.MimeType.JSON);
}

/** Запустите вручную из редактора: проверит Telegram и попросит разрешение на внешние запросы. */
function testTelegram() {
  notify_("feedback", "ru", "", "Проверка связи с сайтом Jazira");
}
