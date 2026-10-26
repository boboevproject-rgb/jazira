// Jazira: язык, демо острова, формы листа ожидания и отзывов.
"use strict";

// ---- Настройки сайта: всё, что меняется при запуске, — здесь -------------------------------------
const CONFIG = {
  brand: "Jazira",
  author: "Bilol",
  // Адрес веб-приложения Google Apps Script (заканчивается на /exec), см. apps-script/README.md.
  // Пока пусто: на localhost формы уходят в tools/serve.py, на сайте показывают «форма не подключена».
  formUrl: "",
  // Ссылка на установщик и номер версии. Пока ссылки нет, сайт работает как лист ожидания.
  downloadUrl: "",
  version: "",
  // Telegram для связи — ссылка в подвале (пусто — ссылка скрыта).
  telegram: "https://t.me/bilol_bj"
};

const LANGS = ["ru", "en", "tg"];
const VARS = { brand: CONFIG.brand, author: CONFIG.author, version: CONFIG.version };
let LANG = "ru";

const store = {
  get(k) { try { return localStorage.getItem(k); } catch (e) { return null; } },
  set(k, v) { try { localStorage.setItem(k, v); } catch (e) { /* приватный режим — не страшно */ } }
};

// ---- Переводы ------------------------------------------------------------------------------------
function tr(key) {
  const e = T[key];
  if (!e) return key;
  return (e[LANG] ?? e.ru).replace(/\{(\w+)\}/g, (m, k) => VARS[k] ?? m);
}

// Текст элемента по ключу; ключ запоминается в data-t, чтобы смена языка перевела и его
function setText(el, key) {
  if (key) { el.dataset.t = key; el.innerHTML = tr(key); }
  else { delete el.dataset.t; el.textContent = ""; }
}

function applyLang(lang) {
  LANG = LANGS.includes(lang) ? lang : "ru";
  document.documentElement.lang = LANG;
  document.querySelectorAll("[data-t]").forEach(el => { el.innerHTML = tr(el.dataset.t); });
  document.querySelectorAll("[data-t-attr]").forEach(el => {
    el.dataset.tAttr.split(";").forEach(pair => {
      const [attr, key] = pair.split(":").map(s => s.trim());
      if (attr && key) el.setAttribute(attr, tr(key));
    });
  });
  document.title = tr("meta.title");
  document.querySelector('meta[name="description"]').content = tr("meta.desc");
  const sw = document.querySelector(".lang");
  sw.style.setProperty("--i", LANGS.indexOf(LANG));
  sw.querySelectorAll("button").forEach(b => b.setAttribute("aria-pressed", String(b.dataset.lang === LANG)));
  store.set("jazira-lang", LANG);
}

// ?lang=tg в адресе → сохранённый выбор → язык браузера → русский
function startLang() {
  const q = new URLSearchParams(location.search).get("lang");
  if (LANGS.includes(q)) return q;
  const saved = store.get("jazira-lang");
  if (LANGS.includes(saved)) return saved;
  for (const l of navigator.languages || [navigator.language || ""]) {
    const code = l.toLowerCase().slice(0, 2);
    if (LANGS.includes(code)) return code;
  }
  return "ru";
}

document.querySelectorAll(".lang button").forEach(b => b.addEventListener("click", () => applyLang(b.dataset.lang)));

// ---- Бренд, год и режим «вышел / скоро» -----------------------------------------------------------
document.body.classList.toggle("live", Boolean(CONFIG.downloadUrl));
document.querySelectorAll(".js-dl").forEach(a => { a.href = CONFIG.downloadUrl || "#waitlist"; });
document.querySelectorAll(".js-brand").forEach(e => { e.textContent = CONFIG.brand; });
document.querySelectorAll(".js-year").forEach(e => { e.textContent = new Date().getFullYear(); });
if (CONFIG.telegram) document.querySelectorAll(".js-tg").forEach(a => { a.href = CONFIG.telegram; a.hidden = false; });

applyLang(startLang());

// ---- Демо острова: состояния сменяют друг друга, пока экран на виду ------------------------------
const island = document.getElementById("island");
const cap = document.getElementById("cap");
const chips = document.querySelectorAll(".chips button");
const ORDER = ["idle", "music", "focus", "water", "files", "charge"];
const reduceMotion = matchMedia("(prefers-reduced-motion: reduce)").matches;
let auto = !reduceMotion, autoTimer = 0, resumeTimer = 0, demoVisible = false;

function showState(s) {
  island.dataset.state = s;
  chips.forEach(b => b.setAttribute("aria-pressed", String(b.dataset.s === s)));
  setText(cap, "demo.cap." + s);
}

function scheduleNext() {
  clearTimeout(autoTimer);
  if (!auto || !demoVisible || document.hidden) return;
  autoTimer = setTimeout(() => {
    showState(ORDER[(ORDER.indexOf(island.dataset.state) + 1) % ORDER.length]);
    scheduleNext();
  }, 3600);
}

// После ручного выбора даём рассмотреть состояние, потом автосмена возвращается
chips.forEach(b => b.addEventListener("click", () => {
  showState(b.dataset.s);
  auto = false;
  clearTimeout(autoTimer); clearTimeout(resumeTimer);
  if (!reduceMotion) resumeTimer = setTimeout(() => { auto = true; scheduleNext(); }, 15000);
}));
new IntersectionObserver(es => { demoVisible = es[0].isIntersecting; scheduleNext(); }, { threshold: .3 })
  .observe(island.closest(".screen"));
document.addEventListener("visibilitychange", scheduleNext);

function clock() {
  const d = new Date();
  const t = String(d.getHours()).padStart(2, "0") + ":" + String(d.getMinutes()).padStart(2, "0");
  document.querySelectorAll(".js-clock").forEach(e => { e.textContent = t; });
}
clock(); setInterval(clock, 15000);

// Таймер фокуса тикает, только когда он показан
const FOCUS_SEC = 25 * 60;
const focusEl = document.querySelector(".js-focus"), ringEl = document.querySelector(".ring-fg");
let focusLeft = 24 * 60 + 12;
function drawFocus() {
  focusEl.textContent = Math.floor(focusLeft / 60) + ":" + String(focusLeft % 60).padStart(2, "0");
  ringEl.style.strokeDashoffset = String(100 * (1 - focusLeft / FOCUS_SEC));
}
drawFocus();
setInterval(() => {
  if (island.dataset.state !== "focus" || document.hidden) return;
  focusLeft = focusLeft > 0 ? focusLeft - 1 : FOCUS_SEC;
  drawFocus();
}, 1000);

showState("idle");

// ---- Формы ------------------------------------------------------------------------------------------
const LOCAL = ["localhost", "127.0.0.1", "[::1]"].includes(location.hostname);
const FORM_URL = CONFIG.formUrl || (LOCAL ? "/dev/form" : "");

// Тело — JSON строкой без своих заголовков: такой «простой» запрос Apps Script принимает без CORS-префлайта
async function send(data) {
  if (!FORM_URL) throw new Error("off");
  const res = await fetch(FORM_URL, { method: "POST", body: JSON.stringify({ ...data, lang: LANG }) });
  const out = await res.json();
  if (!out.ok) throw new Error(out.error || "failed");
}

const CONTACT_RE = [
  /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/,               // email
  /^(@|https?:\/\/t\.me\/)?[A-Za-z0-9_]{5,32}$/,  // Telegram: @ник или ссылка t.me
  /^\+?[\d\s()-]{7,20}$/                          // телефон
];
const validContact = v => CONTACT_RE.some(re => re.test(v));

function fail(el, key, field) {
  setText(el, key);
  el.classList.add("err");
  if (field) field.focus();
}

const waitlists = document.querySelectorAll(".js-wl");
if (store.get("jazira-joined") === "1") waitlists.forEach(f => f.classList.add("joined"));

waitlists.forEach(form => {
  const msg = form.querySelector(".wl-msg"), btn = form.querySelector("button[type=submit]");
  form.addEventListener("submit", async ev => {
    ev.preventDefault();
    const contact = form.contact.value.trim();
    msg.className = "wl-msg";
    if (!contact) return fail(msg, "wl.err.empty", form.contact);
    if (!validContact(contact)) return fail(msg, "wl.err.bad", form.contact);
    btn.disabled = true;
    setText(msg, "form.sending");
    try {
      await send({ action: "waitlist", contact, website: form.website.value });
      store.set("jazira-joined", "1");
      setText(msg, null);
      waitlists.forEach(f => f.classList.add("joined"));
    } catch (e) {
      fail(msg, e.message === "off" ? "form.off" : "form.fail");
    } finally {
      btn.disabled = false;
    }
  });
});

const fb = document.querySelector(".js-fb");
fb.addEventListener("submit", async ev => {
  ev.preventDefault();
  const out = fb.querySelector(".fb-msg"), btn = fb.querySelector("button[type=submit]");
  const msg = fb.msg.value.trim();
  out.className = "fb-msg";
  if (msg.length < 3) return fail(out, "fb.err.short", fb.msg);
  btn.disabled = true;
  setText(out, "form.sending");
  try {
    await send({ action: "feedback", msg, contact: fb.contact.value.trim(), website: fb.website.value });
    fb.reset();
    setText(out, "fb.ok");
    out.classList.add("ok");
  } catch (e) {
    fail(out, e.message === "off" ? "form.off" : "form.fail");
  } finally {
    btn.disabled = false;
  }
});
