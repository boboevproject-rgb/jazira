"""Проверка переводов: все ключи на месте, во всех трёх языках, без лишнего и без опечаток алфавита.

Что проверяется:
  * у каждого ключа в assets/i18n.js есть ru, en и tg, и в них одинаковые {подстановки};
  * в таджикском нет букв, которых нет в таджикском алфавите (ц, щ, ы, ь);
  * каждый ключ из index.html (data-t, data-t-attr) и из main.js есть в словаре, и наоборот;
  * запасной русский текст в index.html совпадает со словарём (его видят поисковики и браузеры без JS);
  * элементы с data-t не вложены друг в друга (иначе перевод родителя сотрёт ребёнка).

Запуск: python tools/check_i18n.py   (код выхода 1, если есть ошибки)
"""
import html.parser
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
LANGS = ("ru", "en", "tg")
NOT_TAJIK = re.compile(r"[цщыьЦЩЫЬ]")
KEY_LITERAL = re.compile(r'"([a-z][a-z0-9]*(?:\.[a-z0-9]+)+)"')
VOID = {"area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr"}


def load_dict():
    src = (ROOT / "assets" / "i18n.js").read_text(encoding="utf-8")
    body = src[src.index("const T = ") + len("const T = "):src.rindex("};") + 1]
    return json.loads(body)


def norm(s):
    return re.sub(r"\s+", " ", re.sub(r"<[^>]+>", "", s)).strip()


class Page(html.parser.HTMLParser):
    """Собирает ключи и запасной текст элементов с data-t."""

    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.texts = []      # (ключ, текст)
        self.attrs = []      # (ключ, атрибут, значение)
        self.states = set()  # значения data-s у кнопок демо
        self.nested = []
        self.stack = []      # открытые data-t: [ключ, глубина, текст]
        self.depth = 0

    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if "data-s" in a:
            self.states.add(a["data-s"])
        for pair in (a.get("data-t-attr") or "").split(";"):
            if ":" in pair:
                attr, key = (s.strip() for s in pair.split(":", 1))
                self.attrs.append((key, attr, a.get(attr, "")))
        if tag in VOID:
            return
        self.depth += 1
        if "data-t" in a:
            if self.stack:
                self.nested.append(a["data-t"])
            self.stack.append([a["data-t"], self.depth, ""])

    def handle_endtag(self, tag):
        if tag in VOID:
            return
        if self.stack and self.stack[-1][1] == self.depth:
            key, _, text = self.stack.pop()
            self.texts.append((key, text))
        self.depth -= 1

    def handle_data(self, data):
        for item in self.stack:
            item[2] += data


def main():
    T = load_dict()
    main_js = (ROOT / "assets" / "main.js").read_text(encoding="utf-8")
    cfg = {k: (re.search(rf'{k}:\s*"([^"]*)"', main_js) or [None, ""])[1] for k in ("brand", "author", "version")}
    page = Page()
    page.feed((ROOT / "index.html").read_text(encoding="utf-8"))
    errors, warnings = [], []

    for key, entry in T.items():
        langs = [l for l in LANGS if isinstance(entry.get(l), str) and entry[l].strip()]
        if len(langs) != 3:
            errors.append(f"{key}: нет перевода — {', '.join(l for l in LANGS if l not in langs)}")
            continue
        holes = {l: set(re.findall(r"\{\w+\}", entry[l])) for l in LANGS}
        if len({frozenset(v) for v in holes.values()}) > 1:
            errors.append(f"{key}: разные подстановки {holes}")
        for bad in NOT_TAJIK.findall(entry["tg"]):
            errors.append(f"{key}: в таджикском тексте буква «{bad}», её нет в таджикском алфавите")

    used = {k for k, _ in page.texts} | {k for k, _, _ in page.attrs}
    used |= {k for k in KEY_LITERAL.findall(main_js)}
    used |= {f"demo.cap.{s}" for s in page.states}
    for key in sorted(used - T.keys()):
        errors.append(f"{key}: используется, но нет в словаре")
    for key in sorted(T.keys() - used):
        warnings.append(f"{key}: есть в словаре, но нигде не используется")
    for key in page.nested:
        errors.append(f"{key}: data-t внутри другого data-t")

    def ru(key):
        return norm(re.sub(r"\{(\w+)\}", lambda m: cfg.get(m.group(1), m.group(0)), T[key]["ru"]))

    for key, text in page.texts:
        if key in T and norm(text) != ru(key):
            errors.append(f"{key}: в index.html «{norm(text)}», в словаре «{ru(key)}»")
    for key, attr, value in page.attrs:
        if key in T and norm(value) != ru(key):
            errors.append(f"{key}: атрибут {attr} в index.html «{value}», в словаре «{ru(key)}»")

    for w in warnings:
        print("предупреждение:", w)
    for e in errors:
        print("ОШИБКА:", e)
    print(f"\nключей в словаре: {len(T)}, используется: {len(used & T.keys())}, ошибок: {len(errors)}")
    sys.exit(1 if errors else 0)


if __name__ == "__main__":
    main()
