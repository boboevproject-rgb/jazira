"""Проверка страницы в настоящем браузере (без окна): три языка, компьютер и телефон, формы и демо.

Поднимает tools/serve.py на свободном порту, заявки пишет во временный файл, скриншоты кладёт в shots/.
Запуск: python tools/test_page.py   (нужен Playwright; браузер — установленный Chrome или Edge)
"""
import json
import pathlib
import sys
import tempfile
import threading

from playwright.sync_api import sync_playwright

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import serve  # noqa: E402
from check_i18n import load_dict  # noqa: E402

ROOT = serve.ROOT
SHOTS = ROOT / "shots"
T = load_dict()
VIEWPORTS = {"desktop": (1366, 900), "mobile": (390, 844)}
STATES = ["idle", "music", "focus", "water", "files", "charge"]
problems = []


def text(key, lang):
    return T[key][lang].replace("{brand}", "Jazira").replace("{author}", "Bilol")


def expect(ok, what):
    print(("ok   " if ok else "FAIL ") + what)
    if not ok:
        problems.append(what)


def launch(p):
    for channel in ("chrome", "msedge"):
        try:
            return p.chromium.launch(channel=channel)
        except Exception:
            pass
    return p.chromium.launch()


def check_layout(page, label):
    overflow = page.evaluate("document.documentElement.scrollWidth - document.documentElement.clientWidth")
    expect(overflow <= 0, f"{label}: нет горизонтальной прокрутки (лишние {overflow}px)")
    raw = page.evaluate("[...document.querySelectorAll('[data-t]')].filter(e => e.innerHTML === e.dataset.t).map(e => e.dataset.t)")
    expect(not raw, f"{label}: все тексты переведены {raw or ''}")
    # Содержимое каждого состояния острова помещается в остров
    tight = []
    for s in STATES:
        page.evaluate(f"document.getElementById('island').dataset.state = '{s}'")
        page.wait_for_timeout(750)
        spill = page.evaluate(f"""(() => {{
            const st = document.querySelector('.st-{s}');
            return st.scrollWidth - st.clientWidth;
        }})()""")
        if spill > 1:
            tight.append(f"{s}+{spill}px")
    expect(not tight, f"{label}: содержимое острова помещается {tight or ''}")
    page.evaluate("document.getElementById('island').dataset.state = 'idle'")


def main():
    SHOTS.mkdir(exist_ok=True)
    log = pathlib.Path(tempfile.mkdtemp()) / "submissions.jsonl"
    serve.LOG = log
    server = serve.http.server.ThreadingHTTPServer(("127.0.0.1", 0), serve.Handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    base = f"http://127.0.0.1:{server.server_address[1]}"

    with sync_playwright() as p:
        browser = launch(p)

        # ---- Вёрстка: 3 языка × 2 экрана ----
        for vp, (w, h) in VIEWPORTS.items():
            for lang in ("ru", "en", "tg"):
                ctx = browser.new_context(viewport={"width": w, "height": h}, reduced_motion="reduce")
                page = ctx.new_page()
                errors = []
                page.on("pageerror", lambda e: errors.append(str(e)))
                page.on("console", lambda m: errors.append(m.text) if m.type == "error" else None)
                page.goto(f"{base}/?lang={lang}")
                page.wait_for_load_state("networkidle")
                page.evaluate("document.fonts.ready")
                label = f"{vp}/{lang}"
                expect(page.evaluate("document.documentElement.lang") == lang, f"{label}: lang={lang}")
                expect(page.title() == text("meta.title", lang), f"{label}: заголовок вкладки на нужном языке")
                check_layout(page, label)
                page.screenshot(path=str(SHOTS / f"{vp}-{lang}-top.png"))
                page.screenshot(path=str(SHOTS / f"{vp}-{lang}-full.png"), full_page=True)
                expect(not errors, f"{label}: нет ошибок в консоли {errors or ''}")
                ctx.close()

        # ---- Поведение: демо, формы, смена языка ----
        ctx = browser.new_context(viewport={"width": 1366, "height": 900})
        page = ctx.new_page()
        page.goto(f"{base}/?lang=ru")
        page.wait_for_load_state("networkidle")

        idle_w = page.eval_on_selector("#island", "e => e.getBoundingClientRect().width")
        page.click(".chips button[data-s=music]")
        page.wait_for_timeout(900)
        music_w = page.eval_on_selector("#island", "e => e.getBoundingClientRect().width")
        expect(page.get_attribute("#island", "data-state") == "music" and music_w > idle_w * 1.8,
               f"демо: кнопка «Музыка» раскрывает остров ({idle_w:.0f} → {music_w:.0f}px)")
        expect(page.inner_text("#cap") == text("demo.cap.music", "ru"), "демо: подпись сменилась")

        hero = page.locator(".hero .js-wl")
        hero.locator("input[name=contact]").fill("abc")
        hero.locator("button[type=submit]").click()
        expect(hero.locator(".wl-msg").inner_text() == text("wl.err.bad", "ru"), "лист ожидания: опечатка в контакте ловится")
        hero.locator("input[name=contact]").fill("@bilol_test")
        hero.locator("button[type=submit]").click()
        page.wait_for_selector(".hero .js-wl.joined", timeout=5000)
        expect(page.locator(".js-wl.joined").count() == 2, "лист ожидания: обе формы показывают «вы в списке»")

        fb = page.locator(".js-fb")
        fb.locator("button[type=submit]").click()
        expect(fb.locator(".fb-msg").inner_text() == text("fb.err.short", "ru"), "отзыв: пустое сообщение не уходит")
        fb.locator("textarea").fill("Хочу показ времени намаза")
        fb.locator("input[name=contact]").fill("test@example.com")
        fb.locator("button[type=submit]").click()
        page.wait_for_selector(".fb-msg.ok", timeout=5000)
        expect(fb.locator("textarea").input_value() == "", "отзыв: форма очищена после отправки")

        page.click(".lang button[data-lang=tg]")
        expect(page.evaluate("document.documentElement.lang") == "tg", "язык: переключился на таджикский")
        expect(fb.locator(".fb-msg").inner_text() == text("fb.ok", "tg"), "язык: сообщение формы тоже переведено")
        page.goto(f"{base}/")  # без ?lang — язык должен взяться из сохранённого выбора
        page.wait_for_load_state("networkidle")
        expect(page.evaluate("document.documentElement.lang") == "tg", "язык: выбор запомнился после перезагрузки")
        expect(page.locator(".js-wl.joined").count() == 2, "лист ожидания: запись запомнилась после перезагрузки")
        ctx.close()
        browser.close()

    server.shutdown()
    recs = [json.loads(line) for line in log.read_text(encoding="utf-8").splitlines()] if log.exists() else []
    expect([r["action"] for r in recs] == ["waitlist", "feedback"], f"сервер принял ровно 2 заявки: {[r['action'] for r in recs]}")
    expect(any(r.get("contact") == "@bilol_test" and r.get("lang") == "ru" for r in recs), "в заявке контакт и язык")
    print(f"\nскриншоты: {SHOTS}")
    print(f"проблем: {len(problems)}")
    sys.exit(1 if problems else 0)


if __name__ == "__main__":
    main()
