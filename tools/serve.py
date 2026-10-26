"""Локальный сервер Jazira: отдаёт сайт и принимает формы на /dev/form.

Заявки дописываются в dev-submissions.jsonl в корне проекта — так формы можно проверить
без Google-таблицы. Проверки те же, что в apps-script/Code.gs.

Запуск: python tools/serve.py [порт]   (по умолчанию 8791, слушает только 127.0.0.1)
"""
import datetime
import http.server
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
LOG = ROOT / "dev-submissions.jsonl"


def check(d):
    """Возвращает (запись, None) или (None, ошибка) — как doPost в Code.gs."""
    if not isinstance(d, dict):
        return None, "bad json"
    if d.get("website"):
        return {}, None  # ловушка для ботов: делаем вид, что всё хорошо, но не сохраняем
    action = str(d.get("action") or "")
    if action not in ("waitlist", "feedback"):
        return None, "bad action"
    clip = lambda k, n: str(d.get(k) or "").strip()[:n]
    rec = {"time": datetime.datetime.now().isoformat(timespec="seconds"), "action": action,
           "lang": clip("lang", 5), "contact": clip("contact", 120)}
    if action == "feedback":
        rec["msg"] = clip("msg", 1500)
        if len(rec["msg"]) < 3:
            return None, "empty"
    elif len(rec["contact"]) < 3:
        return None, "empty"
    return rec, None


class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(ROOT), **kwargs)

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def do_POST(self):
        if self.path != "/dev/form":
            self.send_error(404)
            return
        size = int(self.headers.get("Content-Length") or 0)
        if size > 10_000:
            self.reply({"ok": False, "error": "too big"}, 413)
            return
        try:
            data = json.loads(self.rfile.read(size) or b"{}")
        except ValueError:
            data = None
        rec, err = check(data)
        if err:
            self.reply({"ok": False, "error": err})
            return
        if rec:
            with LOG.open("a", encoding="utf-8") as f:
                f.write(json.dumps(rec, ensure_ascii=False) + "\n")
            print(f"[форма] {rec['action']}: {rec.get('contact', '')} {rec.get('msg', '')[:60]}", flush=True)
        self.reply({"ok": True})

    def reply(self, obj, code=200):
        body = json.dumps(obj).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


def main():
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8791
    server = http.server.ThreadingHTTPServer(("127.0.0.1", port), Handler)
    print(f"Jazira: http://localhost:{port}/  (Ctrl+C или закрыть окно — остановить)", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
