"""Landing-page first render time, cold (empty cache) and warm (same context, reload)."""
import sys, time
from playwright.sync_api import sync_playwright

url = sys.argv[1]
with sync_playwright() as p:
    browser = p.chromium.launch()
    context = browser.new_context()
    page = context.new_page()
    for label in ("cold", "warm"):
        start = time.perf_counter()
        page.goto(url, wait_until="commit")
        # Blazor removes the loading SVG when the first component renders.
        # Wait for it to exist first: at "commit" the DOM may still be empty and "detached" would pass at once.
        page.wait_for_selector("svg.loading-progress", state="attached", timeout=120_000)
        page.wait_for_selector("svg.loading-progress", state="detached", timeout=120_000)
        print(f"load.{label}.seconds: {time.perf_counter() - start:.2f}")
    browser.close()
