"""Собирает PNG из исходников: иконки 64/192/512 из assets/icon.svg и og.png из tools/og.html.

Запуск: python tools/render_assets.py   (нужен Playwright; браузер — установленный Chrome или Edge)
"""
import pathlib

from playwright.sync_api import sync_playwright

ROOT = pathlib.Path(__file__).resolve().parent.parent
ASSETS = ROOT / "assets"


def launch(p):
    for channel in ("chrome", "msedge"):
        try:
            return p.chromium.launch(channel=channel)
        except Exception:  # нет такого браузера — пробуем следующий
            pass
    return p.chromium.launch()


def main():
    svg = (ASSETS / "icon.svg").read_text(encoding="utf-8")
    svg = svg.replace("<svg ", '<svg width="100%" height="100%" ', 1)
    with sync_playwright() as p:
        browser = launch(p)
        for size in (64, 192, 512):
            page = browser.new_page(viewport={"width": size, "height": size})
            page.set_content(
                '<html><body style="margin:0;background:transparent">'
                f'<div style="width:{size}px;height:{size}px">{svg}</div>'
                "</body></html>"
            )
            page.screenshot(path=str(ASSETS / f"icon-{size}.png"), omit_background=True)
            page.close()
            print(f"icon-{size}.png")

        page = browser.new_page(viewport={"width": 1200, "height": 630})
        page.goto((ROOT / "tools" / "og.html").as_uri())
        page.wait_for_load_state("networkidle")
        page.evaluate("document.fonts.ready")
        page.screenshot(path=str(ASSETS / "og.png"))
        print("og.png")
        browser.close()


if __name__ == "__main__":
    main()
