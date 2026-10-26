// Прогоняет apps-script/Code.gs в Node с заглушками сервисов Google — проверка логики без развёртывания.
// Запуск: node tools/test-apps-script.mjs
import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";

const code = fs.readFileSync(new URL("../apps-script/Code.gs", import.meta.url), "utf8");

function makeEnv({ telegramThrows = false } = {}) {
  const rows = {}, sent = [], cache = new Map(), props = { TG_TOKEN: "TOKEN", TG_CHAT: "42" };
  const sheet = name => ({ appendRow: r => rows[name].push(r), setFrozenRows() {} });
  const ctx = {
    SpreadsheetApp: { getActiveSpreadsheet: () => ({
      getSheetByName: n => (rows[n] ? sheet(n) : null),
      insertSheet: n => { rows[n] = []; return sheet(n); }
    }) },
    LockService: { getScriptLock: () => ({ waitLock() {}, releaseLock() {} }) },
    PropertiesService: { getScriptProperties: () => ({ getProperty: k => props[k] ?? null }) },
    UrlFetchApp: { fetch: (url, opts) => { if (telegramThrows) throw new Error("network"); sent.push({ url, opts }); } },
    CacheService: { getScriptCache: () => ({ get: k => cache.get(k) ?? null, put: (k, v) => cache.set(k, v) }) },
    ContentService: { MimeType: { JSON: "json" }, createTextOutput: s => ({ setMimeType: () => JSON.parse(s) }) },
    console: { warn() {} }
  };
  vm.createContext(ctx);
  vm.runInContext(code, ctx);
  vm.runInContext("Date.now = () => 1700000000000", ctx); // одна и та же минута для счётчика
  const post = d => ctx.doPost({ postData: { contents: typeof d === "string" ? d : JSON.stringify(d) } });
  return { ctx, rows, sent, props, post };
}

let n = 0;
const test = (name, fn) => { fn(); n++; console.log("ok -", name); };
// Массивы из песочницы vm — из другого «мира», строгое сравнение их не примет; копируем в обычные
const plain = x => JSON.parse(JSON.stringify(x));

test("лист ожидания: строка с заголовком и уведомление в Telegram", () => {
  const { rows, sent, post } = makeEnv();
  assert.deepEqual(post({ action: "waitlist", contact: "@bilol_test", lang: "tg" }), { ok: true });
  assert.deepEqual(plain(rows.waitlist[0]), ["Время", "Язык", "Контакт"]);
  assert.equal(rows.waitlist.length, 2);
  assert.equal(rows.waitlist[1][1], "tg");
  assert.equal(rows.waitlist[1][2], "'@bilol_test"); // @ экранирован
  assert.equal(sent.length, 1);
  assert.match(sent[0].url, /botTOKEN\/sendMessage$/);
  assert.equal(sent[0].opts.payload.chat_id, "42");
  assert.match(sent[0].opts.payload.text, /@bilol_test/);
});

test("отзыв: формула в тексте экранируется, длина обрезается", () => {
  const { rows, post } = makeEnv();
  assert.deepEqual(post({ action: "feedback", msg: "=HYPERLINK(\"x\")", contact: "a@b.tj", lang: "ru" }), { ok: true });
  assert.equal(rows.feedback[1][2], "'=HYPERLINK(\"x\")");
  post({ action: "feedback", msg: "x".repeat(5000), lang: "ru" });
  assert.equal(rows.feedback[2][2].length, 1500);
});

test("ловушка для ботов: ok, но ничего не записано и не отправлено", () => {
  const { rows, sent, post } = makeEnv();
  assert.deepEqual(post({ action: "waitlist", contact: "@spam_bot", website: "http://spam" }), { ok: true });
  assert.equal(rows.waitlist, undefined);
  assert.equal(sent.length, 0);
});

test("плохие запросы отклоняются", () => {
  const { rows, post } = makeEnv();
  assert.equal(post("{oops").ok, false);
  assert.equal(post({ action: "delete_all" }).ok, false);
  assert.equal(post({ action: "waitlist", contact: " " }).ok, false);
  assert.equal(post({ action: "feedback", msg: "hi" }).ok, false);
  assert.deepEqual(Object.keys(rows), []);
});

test("без TG_TOKEN заявка сохраняется, Telegram молчит", () => {
  const { rows, sent, props, post } = makeEnv();
  delete props.TG_TOKEN;
  assert.equal(post({ action: "waitlist", contact: "a@b.tj" }).ok, true);
  assert.equal(rows.waitlist.length, 2);
  assert.equal(sent.length, 0);
});

test("сбой Telegram не теряет заявку", () => {
  const { rows, post } = makeEnv({ telegramThrows: true });
  assert.equal(post({ action: "waitlist", contact: "a@b.tj" }).ok, true);
  assert.equal(rows.waitlist.length, 2);
});

test("не больше 30 заявок в минуту", () => {
  const { post } = makeEnv();
  for (let i = 0; i < 30; i++) assert.equal(post({ action: "waitlist", contact: "user" + i + "@b.tj" }).ok, true);
  assert.deepEqual(post({ action: "waitlist", contact: "late@b.tj" }), { ok: false, error: "busy" });
});

test("doGet отвечает для проверки адреса", () => {
  const { ctx } = makeEnv();
  assert.equal(ctx.doGet().ok, true);
});

console.log(`\n${n} проверок Code.gs пройдено`);
