"""Landing-page first render, cold (empty cache) and warm (reload in the same context), on localhost and
on a throttled network, plus the bytes that crossed the wire. encodedDataLength counts compressed bytes,
so it shows what nginx's compression saves; localhost timings alone cannot."""
import sys, time
from playwright.sync_api import sync_playwright

url = sys.argv[1]
# 20 Mbit/s down, 5 Mbit/s up, 40 ms latency.
THROTTLED = {"offline": False, "latency": 40,
             "downloadThroughput": 20_000_000 / 8, "uploadThroughput": 5_000_000 / 8}


def run(p, profile):
    browser = p.chromium.launch()
    context = browser.new_context()
    page = context.new_page()
    cdp = context.new_cdp_session(page)
    cdp.send("Network.enable")
    if profile == "throttled":
        cdp.send("Network.emulateNetworkConditions", THROTTLED)
    received = [0]
    cdp.on("Network.loadingFinished", lambda e: received.__setitem__(0, received[0] + int(e["encodedDataLength"])))
    prefix = "load" if profile == "local" else f"load.{profile}"
    for label in ("cold", "warm"):
        received[0] = 0
        start = time.perf_counter()
        page.goto(url, wait_until="commit")
        # Blazor removes the loading SVG when the first component renders.
        # Wait for it to exist first: at "commit" the DOM may still be empty and "detached" would pass at once.
        page.wait_for_selector("svg.loading-progress", state="attached", timeout=300_000)
        page.wait_for_selector("svg.loading-progress", state="detached", timeout=300_000)
        elapsed = time.perf_counter() - start
        page.wait_for_load_state("networkidle", timeout=300_000)
        print(f"{prefix}.{label}.seconds: {elapsed:.2f}")
        print(f"{prefix}.{label}.transferred.kb: {received[0] // 1024}")
    browser.close()


with sync_playwright() as p:
    for profile in ("local", "throttled"):
        run(p, profile)
