# Versão 2.0.0 — C: Deploy and load performance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Faster first load (compressed client, explicit cache headers), faster deploys (one shared Dockerfile that restores from a cached layer and compiles once), with no user-visible change.

**Architecture:** nginx (`deploy/nginx/default.conf`, bind-mounted in the long-lived `web`) gains `gzip_static` + on-the-fly gzip and a `Cache-Control` per file type. The three per-project Dockerfiles become one multi-stage `deploy/Dockerfile` whose `api`/`client`/`smoke` targets `deploy.sh` builds in turn. The bench harness is rebuilt around `deploy.sh` so "before" and "after" are measured the same way.

**Tech Stack:** bash, bats-core 1.11 (`deploy/tests/run-bats.sh`), nginx 1.27-alpine, Docker BuildKit, .NET 8 SDK, Playwright (Python, Docker image), Python 3, ImageMagick + oxipng (alpine 3.20 container).

**Spec:** `docs/superpowers/archive/specs/2026-10-18-versao-2-0-0-c-performance-design.md`

## Global Constraints

- Production (192.168.80.10) is never contacted. The running `ruinarpg` compose project is never touched: only throwaway names (`ruinarpg-bench`, `ruinarpg-nginx-bats-<pid>`, `ruinarpg-eq-*`, `ruinarpg-bats-<pid>`).
- Same behavior for the end user: same pixels, same pt-BR formatting, same features, same API responses.
- `dotnet build`: 0 warnings, 0 errors. TDD (Técnico R0011): every test is written and seen failing before the change it covers.
- Image tags stay `ruinarpg-api|client|smoke:<full sha>`; `build_image` still skips a tag that exists.
- Pinned images: `nginx:1.27-alpine`, `alpine:3.20`, `mcr.microsoft.com/dotnet/sdk:8.0`, `mcr.microsoft.com/dotnet/aspnet:8.0`, `mcr.microsoft.com/playwright/python:v1.48.0-jammy`.
- Out of scope: Brotli at the origin, JS-side Brotli, `InvariantGlobalization`, AOT, lazy-loading, images other than `favicon.png`/`branding/logo-grande.png`, any change to the `web` container image.
- Code comments in English, log/user messages in pt-BR, commit subjects in Portuguese (`feat:`, `fix:`, `docs:`, `test:`, `perf:`), ending with:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_0147BrQYd9VkRSAjqX7B1Pmn
  ```
- Push after each task (`git push`; branch `release/2.0.0` already tracks origin).

## Review Focus

1. A browser or tool that does not send `Accept-Encoding: gzip` must get the plain bytes, not a `.gz` body — pinned in Task 2 (`x.wasm` without `Accept-Encoding`).
2. `.wasm` must keep `Content-Type: application/wasm` when served from its `.gz` sibling, or Blazor's streaming compile fails and the app does not start — pinned in Task 2.
3. A deep link refresh (`/campanhas/<id>`) must still return `index.html`, and with `no-cache`, or a stale shell survives a deploy — pinned in Task 2.
4. A build cache whose mounts were pruned but whose `restore` layer survived must still build — pinned in Task 3 (rebuild after `docker builder prune --filter type=exec.cachemount`).
5. Responses carrying tokens (`/api/auth/*`) must never be compressed (BREACH) — pinned in Task 2.

---

## File map

| File | Responsibility |
|---|---|
| `tools/bench/lib.sh` | + `bench_clone`, `bench_deploy`, `bench_teardown`: a throwaway `ruinarpg-bench` stack deployed by the ref's own `deploy.sh` |
| `tools/bench/measure-{load,api-latency,deploy,payload}.sh`, `load_time.py`, `README.md` | Measurements on the new harness; bytes on the wire, throttled load, full deploy |
| `tools/bench/compose.bench.yml`, `tools/bench/bench.env` | Deleted (pre-F layout) |
| `deploy/nginx/default.conf` | Compression + `Cache-Control` per file type |
| `deploy/tests/nginx.bats` | Header test against a throwaway nginx with the real config |
| `.github/workflows/release.yml` | CI also runs `nginx.bats` |
| `deploy/Dockerfile` | Shared multi-stage build, targets `api`, `client`, `smoke` |
| `deploy/deploy.sh` (`build_images`) | Builds the three targets |
| `deploy/tests/deploy.bats` | + unit test of `build_images` |
| `src/RuinaRPG.Api/Dockerfile`, `infra/client/Dockerfile`, `tools/RuinaRPG.Smoke/Dockerfile` | Deleted |
| `tools/bench/equivalencia-imagens.sh` | One-off: old vs new images, file by file |
| `tools/imagens/recomprimir-png.sh` | Lossless PNG recompression with a pixel check |
| `src/RuinaRPG.Client/wwwroot/favicon.png`, `.../branding/logo-grande.png` | Recompressed |
| `src/RuinaRPG.Infrastructure/Rules/RulebookRenderer.cs` | Only if Task 5's rule triggers: memo of `SplitIntoSections` |
| `Docs/Requisitos/Requisitos - Técnico.md`, `CLAUDE.md`, `README.md`, baseline doc, H notes | Docs |

---

### Task 1: Bench harness on `deploy.sh`, and the "before" numbers

**Files:**
- Modify: `tools/bench/lib.sh`, `tools/bench/measure-load.sh`, `tools/bench/load_time.py`, `tools/bench/measure-api-latency.sh`, `tools/bench/measure-deploy.sh`, `tools/bench/measure-payload.sh`, `tools/bench/README.md`
- Delete: `tools/bench/compose.bench.yml`, `tools/bench/bench.env`

**Interfaces:**
- Produces (used by Task 5): every stack script takes `<label> [ref]` (ref defaults to `HEAD`) and records `key: value` lines in `tools/bench/results/<label>.txt`. Keys: `load.{cold,warm}.seconds`, `load.{cold,warm}.transferred.kb`, `load.throttled.{cold,warm}.seconds`, `load.throttled.{cold,warm}.transferred.kb`, `latency./api/...` (`p50=…s p95=…s`), `deploy.full.{cold,warm}.seconds`, `deploy.full.{cold,warm}.build.seconds`, `payload.framework.{files,raw.kb,gzip.kb,sdk-gz.kb,sdk-br.kb}`.

This task has no product code; its "test" is that each script runs end-to-end and prints its keys. Each script is run against ref `3040ed0` (the commit before any C code) with label `antes-C`.

- [ ] **Step 1: `lib.sh` — the deploy-based stack**

Replace `bench_compose` (and its comment) in `tools/bench/lib.sh` with:

```bash
BENCH_URL="http://127.0.0.1:8088"

# bench_clone <ref> — a shared clone of this repo at <ref>, with a Production .env on 127.0.0.1:8088.
# Sets BENCH_TMP, BENCH_CLONE, BENCH_SHA. The stack is deployed by the clone's own deploy/deploy.sh, so
# "before" and "after" refs are measured with the deploy code of their own commit.
bench_clone() {
  BENCH_TMP="$(mktemp -d)"
  BENCH_CLONE="$BENCH_TMP/repo"
  BENCH_THROWAWAY_SHAS=()
  git clone --quiet --shared "$BENCH_ROOT" "$BENCH_CLONE"
  git -C "$BENCH_CLONE" checkout --quiet --detach "$1"
  BENCH_SHA="$(git -C "$BENCH_CLONE" rev-parse HEAD)"
  sed -e 's/^ASPNETCORE_ENVIRONMENT=.*/ASPNETCORE_ENVIRONMENT=Production/' \
      -e "s/^JWT_SIGNING_KEY=.*/JWT_SIGNING_KEY=$(openssl rand -hex 24)/" \
      -e 's#^CORS_ALLOWED_ORIGINS=.*#CORS_ALLOWED_ORIGINS=http://127.0.0.1:8088#' \
      "$BENCH_CLONE/.env.example" > "$BENCH_CLONE/.env"
  printf 'HTTP_PORT=8088\nHTTP_BIND=127.0.0.1\n' >> "$BENCH_CLONE/.env"
}

# bench_commit <message> [file-to-append-a-comment-to] — a commit that exists only in the clone, so its
# image tags are new and deploy.sh really builds. Sets BENCH_SHA.
bench_commit() {
  if [[ -n "${2:-}" ]]; then echo "// bench-touch $(date +%s%N)" >> "$BENCH_CLONE/$2"; git -C "$BENCH_CLONE" add -- "$2"; fi
  git -C "$BENCH_CLONE" -c user.name=bench -c user.email=bench@localhost commit --quiet --allow-empty -m "$1"
  BENCH_SHA="$(git -C "$BENCH_CLONE" rev-parse HEAD)"
  BENCH_THROWAWAY_SHAS+=("$BENCH_SHA")
}

# bench_deploy — deploys BENCH_SHA into the throwaway ruinarpg-bench project; never the user's stack.
bench_deploy() {
  RUINA_REPO_DIR="$BENCH_CLONE" RUINA_PROJECT=ruinarpg-bench RUINA_BACKUP_DIR="$BENCH_TMP/backups" \
  RUINA_SKIP_FETCH=1 RUINA_DRAIN_SECONDS=0 RUINA_PRUNE_IMAGES=0 RUINA_SMOKE_CMD=true \
    "$BENCH_CLONE/deploy/deploy.sh" deploy "$BENCH_SHA"
}

# bench_teardown — the bench stack with its volumes, the clone, and the images of throwaway commits
# (those tags exist nowhere else). Images of real commits are kept: a real deploy may reuse them.
bench_teardown() {
  docker compose -p ruinarpg-bench -f "$BENCH_CLONE/docker-compose.yml" --env-file "$BENCH_CLONE/.env" \
    --profile tools down --volumes --remove-orphans > /dev/null 2>&1 || true
  local s
  for s in "${BENCH_THROWAWAY_SHAS[@]}"; do
    docker rmi "ruinarpg-api:$s" "ruinarpg-client:$s" "ruinarpg-smoke:$s" > /dev/null 2>&1 || true
  done
  # Backups are written by containers and may be root-owned.
  docker run --rm -v "$BENCH_TMP:/t" alpine:3.20 rm -rf /t/backups > /dev/null 2>&1 || true
  rm -rf "$BENCH_TMP"
}
```

`LABEL` stays `$1`. Add after it: `BENCH_REF="${2:-HEAD}"`.

- [ ] **Step 2: `measure-load.sh` and `load_time.py`**

`tools/bench/measure-load.sh`:

```bash
#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
bench_clone "$BENCH_REF"
trap bench_teardown EXIT
bench_deploy > "$RESULTS_DIR/$LABEL-load-deploy.log" 2>&1
# The container joins the host network, so the bench stack's 127.0.0.1:8088 is reachable as-is.
docker run --rm --network host -v "$BENCH_DIR:/bench:ro" mcr.microsoft.com/playwright/python:v1.48.0-jammy \
  bash -c "pip install -q playwright==1.48.0 && python /bench/load_time.py $BENCH_URL/" \
  | grep '^load\.' | while IFS=': ' read -r k v; do record "$k" "$v"; done
```

`tools/bench/load_time.py`:

```python
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
```

- [ ] **Step 3: `measure-api-latency.sh`**

Replace its first lines (through the health wait) with the harness, keep the seeding and loop, and make every timed request fail loudly on a non-200 (Production has a per-IP limiter on the login endpoints; none of the measured endpoints is limited, and this check proves it):

```bash
#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
base="$BENCH_URL"
bench_clone "$BENCH_REF"
trap bench_teardown EXIT
bench_deploy > "$RESULTS_DIR/$LABEL-latency-deploy.log" 2>&1
```

and in the loop:

```bash
  times=$(for _ in $(seq 1 50); do curl -s -o /dev/null -w '%{http_code} %{time_total}\n' --compressed "${auth[@]}" "$base$e"; done)
  if grep -qv '^200 ' <<< "$times"; then echo "measure-api-latency.sh: $e answered non-200: $(grep -v '^200 ' <<< "$times" | head -1)" >&2; exit 1; fi
  times=$(cut -d' ' -f2 <<< "$times" | sort -n)
```

(`--compressed` makes curl send `Accept-Encoding` like a browser, so latency is measured as browsers get it, through nginx.)

- [ ] **Step 4: `measure-deploy.sh` — full deploy, cold and warm**

```bash
#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
require_free_disk_gb 10
bench_clone "$BENCH_REF"
trap bench_teardown EXIT

# seconds_between <log> <first-line-regex> <second-line-regex> — from deploy.sh's UTC timestamps.
seconds_between() {
  local a b
  a="$(grep -m1 -E "$2" "$1" | cut -d' ' -f1)"; b="$(grep -m1 -E "$3" "$1" | cut -d' ' -f1)"
  echo $(( $(date -d "$b" +%s) - $(date -d "$a" +%s) ))
}

# Cold: an empty commit (new tags) on an empty build cache. docker builder prune wipes the host-wide
# build cache, as the old script did.
bench_commit "bench cold"
docker builder prune -af > /dev/null
rm -f "$BENCH_CLONE/deploy/state/deploy.log"
timed bench_deploy > "$RESULTS_DIR/$LABEL-deploy-cold.log" 2>&1
(( STATUS == 0 )) || { echo "cold deploy failed; see $RESULTS_DIR/$LABEL-deploy-cold.log" >&2; exit 1; }
record "deploy.full.cold.seconds" "$ELAPSED"
record "deploy.full.cold.build.seconds" "$(seconds_between "$BENCH_CLONE/deploy/state/deploy.log" 'build de ruinarpg-api' 'publicando o cliente')"

# Warm: a one-line, comment-only change in an Api source file, committed in the clone only.
bench_commit "bench warm" src/RuinaRPG.Api/Program.cs
rm -f "$BENCH_CLONE/deploy/state/deploy.log"
timed bench_deploy > "$RESULTS_DIR/$LABEL-deploy-warm.log" 2>&1
(( STATUS == 0 )) || { echo "warm deploy failed; see $RESULTS_DIR/$LABEL-deploy-warm.log" >&2; exit 1; }
record "deploy.full.warm.seconds" "$ELAPSED"
record "deploy.full.warm.build.seconds" "$(seconds_between "$BENCH_CLONE/deploy/state/deploy.log" 'build de ruinarpg-api' 'publicando o cliente')"
```

The log patterns are `deploy.sh`'s own messages (`log "build de $image:$sha"`, `log "publicando o cliente $1 em webroot/releases"`, both present at `3040ed0`). The script no longer edits the working tree, so the old "uncommitted Program.cs" guard goes.

- [ ] **Step 5: `measure-payload.sh` — SDK `.gz`/`.br` totals**

After the existing `raw`/`gz` loop add, and delete the `payload.served.encoding` line:

```bash
sdk_gz=$(find "$fw" -type f -name '*.gz' -printf '%s\n' | awk '{s+=$1} END {print s+0}')
sdk_br=$(find "$fw" -type f -name '*.br' -printf '%s\n' | awk '{s+=$1} END {print s+0}')
record "payload.framework.sdk-gz.kb" "$(( sdk_gz / 1024 ))"
record "payload.framework.sdk-br.kb" "$(( sdk_br / 1024 ))"
```

`measure-payload.sh` publishes from the working tree and takes no ref; that is fine (the client code does not change in C).

- [ ] **Step 6: Delete the pre-F files and update the README**

```bash
git rm tools/bench/compose.bench.yml tools/bench/bench.env
```

In `tools/bench/README.md`: scripts take `<label> [ref]`; the stack scripts deploy `<ref>` (default `HEAD`) with that ref's own `deploy/deploy.sh` into the throwaway `ruinarpg-bench` project on `127.0.0.1:8088`, then remove it with its volumes and the images of throwaway commits; `measure-deploy.sh` times a full `deploy.sh deploy` cold (after `docker builder prune -af`, host-wide) and warm (one-line Api change committed only in the clone), with the build stage taken from the deploy log; `measure-load.sh` adds bytes on the wire and a throttled profile (20 Mbit/s, 40 ms). Replace the last paragraph's `compose.bench.yml`/`bench.env` mention accordingly.

- [ ] **Step 7: Syntax check, then run "before" against `3040ed0`**

```bash
bash -n tools/bench/lib.sh tools/bench/measure-*.sh && python3 -m py_compile tools/bench/load_time.py
bash tools/bench/measure-payload.sh antes-C
bash tools/bench/measure-load.sh antes-C 3040ed0
bash tools/bench/measure-api-latency.sh antes-C 3040ed0
bash tools/bench/measure-deploy.sh antes-C 3040ed0
cat tools/bench/results/antes-C.txt
```

Expected: every key from **Interfaces** present; `load.cold.transferred.kb` around 15000–16500 (nothing compressed at `3040ed0`); all latency lines recorded (no non-200). Afterwards `docker ps -a --filter name=ruinarpg-bench` is empty and `docker volume ls --filter name=ruinarpg-bench` is empty. Copy `antes-C.txt` into the report (results/ is git-ignored).

- [ ] **Step 8: Commit**

```bash
git add tools/bench
git commit -m "test(bench): medições sobre o deploy.sh (bytes transferidos, rede limitada, deploy completo)"
git push
```

---

### Task 2: nginx compression and cache headers

**Files:**
- Create: `deploy/tests/nginx.bats`
- Modify: `deploy/nginx/default.conf`, `.github/workflows/release.yml:43`, `Docs/Requisitos/Requisitos - Técnico.md` (deploy section, after the paragraph that starts "O deploy normal é o pipeline")

**Interfaces:**
- Produces: the C.1 header policy, which Task 5 measures and the H notes check through Cloudflare.

- [ ] **Step 1: Write the failing test**

`deploy/tests/nginx.bats`:

```bash
# Headers of deploy/nginx/default.conf (Técnico, deploy section): compression and Cache-Control per file
# type. A throwaway nginx with the real config, a fake webroot and a stub upstream; never the real stack.

setup_file() {
  export NGX_NET="ruinarpg-nginx-bats-$$" NGX_WEB="ruinarpg-nginx-bats-web-$$" NGX_STUB="ruinarpg-nginx-bats-stub-$$"
  local repo root="$BATS_FILE_TMPDIR/ngx" big r
  repo="$(cd "$BATS_TEST_DIRNAME/../.." && pwd)"
  # Bodies over gzip_min_length (1024), so size never decides whether a response is compressed.
  big="$(head -c 3000 /dev/zero | tr '\0' 'a')"
  r="$root/webroot/releases/r1"
  mkdir -p "$r/_framework" "$r/css" "$root/images" "$root/stub"
  ln -s releases/r1 "$root/webroot/current"
  printf '<!DOCTYPE html><html><body>shell %s</body></html>' "$big" > "$r/index.html"
  printf '{"x":"%s"}' "$big" > "$r/_framework/blazor.boot.json"
  printf 'wasm%s' "$big" > "$r/_framework/x.wasm"
  gzip -9 -k "$r/_framework/x.wasm"
  printf 'var a="%s";' "$big" > "$r/_framework/dotnet.runtime.8.0.0.abcdefghij.js"
  gzip -9 -k "$r/_framework/dotnet.runtime.8.0.0.abcdefghij.js"
  printf 'var b="%s";' "$big" > "$r/_framework/dotnet.js"
  printf 'body{content:"%s"}' "$big" > "$r/css/app.css"
  printf 'png' > "$root/images/abc.png"
  printf 'server { listen 8080; location / { default_type application/json; return 200 %s; } }\n' "'{\"x\":\"$big\"}'" > "$root/stub/default.conf"
  echo 'upstream app { server stub:8080; }' > "$root/upstream.conf"
  # nginx's workers run as uid 101; bats' temp dirs are private to us.
  chmod -R a+rX "$BATS_FILE_TMPDIR"
  docker network create "$NGX_NET" > /dev/null
  docker run -d --name "$NGX_STUB" --network "$NGX_NET" --network-alias stub \
    -v "$root/stub/default.conf:/etc/nginx/conf.d/default.conf:ro" nginx:1.27-alpine > /dev/null
  docker run -d --name "$NGX_WEB" --network "$NGX_NET" -p 127.0.0.1::80 \
    -v "$repo/deploy/nginx/default.conf:/etc/nginx/conf.d/default.conf:ro" \
    -v "$root/upstream.conf:/etc/nginx/upstream.conf:ro" \
    -v "$root/webroot:/webroot:ro" -v "$root/images:/images:ro" nginx:1.27-alpine > /dev/null
  export NGX_URL="http://$(docker port "$NGX_WEB" 80/tcp | head -1)"
  local i
  for i in $(seq 1 50); do curl -fs -o /dev/null "$NGX_URL/index.html" && return 0; sleep 0.2; done
  docker logs "$NGX_WEB" >&2
  return 1
}

teardown_file() {
  docker rm -f "$NGX_WEB" "$NGX_STUB" > /dev/null 2>&1 || true
  docker network rm "$NGX_NET" > /dev/null 2>&1 || true
}

# headers <path> [curl args...] — response headers, lower-cased names, no CR.
headers() { local p="$1"; shift; curl -fsS -o /dev/null -D - "$@" "$NGX_URL$p" | tr -d '\r' | sed -E 's/^([^:]+):/\L\1:/'; }
gz() { headers "$1" -H 'Accept-Encoding: gzip'; }
# Exactly one Cache-Control, with this value.
cache_is() { [ "$(grep -c '^cache-control:' <<< "$1")" -eq 1 ] && grep -qx "cache-control: $2" <<< "$1"; }

@test "index.html: no-cache, gzip, Vary" {
  h="$(gz /index.html)"
  cache_is "$h" "no-cache"
  grep -qx 'content-encoding: gzip' <<< "$h"
  grep -qix 'vary: accept-encoding' <<< "$h"
}

@test "an SPA route falls back to index.html with no-cache" {
  h="$(gz /campanhas/123)"
  cache_is "$h" "no-cache"
  run curl -fsS "$NGX_URL/campanhas/123"
  [[ "$output" == *"shell"* ]]
}

@test "blazor.boot.json: no-cache" {
  cache_is "$(gz /_framework/blazor.boot.json)" "no-cache"
}

@test "a .wasm is served from its .gz with the wasm content type and no-cache" {
  h="$(gz /_framework/x.wasm)"
  grep -qx 'content-encoding: gzip' <<< "$h"
  grep -qx 'content-type: application/wasm' <<< "$h"
  grep -qix 'vary: accept-encoding' <<< "$h"
  cache_is "$h" "no-cache"
}

@test "without Accept-Encoding the plain bytes are served" {
  h="$(headers /_framework/x.wasm)"
  ! grep -q '^content-encoding:' <<< "$h"
  run curl -fsS "$NGX_URL/_framework/x.wasm"
  [ "${#output}" -eq 3004 ]
}

@test "fingerprinted dotnet.*.<hash>.js is immutable for a year" {
  h="$(gz /_framework/dotnet.runtime.8.0.0.abcdefghij.js)"
  cache_is "$h" "public, max-age=31536000, immutable"
  grep -qx 'content-encoding: gzip' <<< "$h"
}

@test "dotnet.js without a hash is no-cache" {
  cache_is "$(gz /_framework/dotnet.js)" "no-cache"
}

@test "CSS without a .gz is compressed on the fly and no-cache" {
  h="$(gz /css/app.css)"
  grep -qx 'content-encoding: gzip' <<< "$h"
  cache_is "$h" "no-cache"
}

@test "uploaded images are private and immutable" {
  cache_is "$(headers /images/abc.png)" "private, max-age=31536000, immutable"
}

@test "API JSON is compressed" {
  grep -qx 'content-encoding: gzip' <<< "$(gz /api/campaigns)"
}

@test "auth responses are never compressed" {
  ! grep -q '^content-encoding:' <<< "$(gz /api/auth/login)"
}

@test "SignalR is never compressed" {
  ! grep -q '^content-encoding:' <<< "$(gz /hubs/notifications)"
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `deploy/tests/run-bats.sh deploy/tests/nginx.bats`
Expected: the cache tests other than `index.html`/`blazor.boot.json`, the compression tests and the `/api/` test FAIL (no `gzip`, no `Cache-Control`); "without Accept-Encoding", "auth" and "SignalR" pass already. `docker ps -a --filter name=ruinarpg-nginx-bats` empty afterwards.

- [ ] **Step 3: Write `deploy/nginx/default.conf`**

```nginx
include /etc/nginx/upstream.conf;   # defines "upstream app { … }" — rewritten by deploy/deploy.sh

# Compression (Técnico, deploy section). _framework ships .gz files from the SDK: gzip_static serves
# them. Other text (CSS/JS outside _framework, API JSON) is compressed on the fly. Vary keeps
# Cloudflare's cached variants apart.
gzip on;
gzip_comp_level 5;
gzip_min_length 1024;
gzip_vary on;
gzip_proxied any;
gzip_types text/css application/javascript application/json image/svg+xml text/plain;

# Maintenance window (Técnico R0014): the request never reaches the API; the client retries on this header.
server {
    listen 127.0.0.1:8081;
    location / {
        add_header X-Ruina-Manutencao 1 always;
        return 503;
    }
}

server {
    listen 80;

    location /api/ {
        # Matches ImagesController's [RequestSizeLimit(20_000_000)].
        client_max_body_size 20m;
        proxy_pass http://app;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        # Keeps the per-IP rate limiter (Técnico R0006) per client instead of one bucket.
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }

    # Token responses are never compressed: tokens next to request-derived data is what BREACH needs.
    location /api/auth/ {
        gzip off;
        proxy_pass http://app;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }

    location /hubs/ {
        gzip off;
        proxy_pass http://app;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }

    # Uploaded images live in their own volume, served apart from the client build (no more shadowing).
    # Every upload gets a new GUID name and is never overwritten. private: browsers cache it, the
    # Cloudflare edge does not, so a deleted image is not left reachable there.
    location /images/ {
        alias /images/;
        add_header Cache-Control "private, max-age=31536000, immutable";
    }

    location = /index.html {
        root /webroot/current;
        add_header Cache-Control "no-cache";
    }

    location = /_framework/blazor.boot.json {
        root /webroot/current;
        gzip_static on;
        add_header Cache-Control "no-cache";
    }

    # The only client files whose name changes with their content.
    location ~ ^/_framework/dotnet\.[^/]*\.[a-z0-9]{10}\.js$ {
        root /webroot/current;
        gzip_static on;
        add_header Cache-Control "public, max-age=31536000, immutable";
    }

    # Everything else keeps its name across releases: revalidated (ETag → 304) on every use, so no
    # browser or Cloudflare edge serves an old file after a deploy. Blazor also keeps .wasm/.dat in its
    # own cache, keyed by the integrity hash in blazor.boot.json.
    location / {
        root /webroot/current;
        gzip_static on;
        add_header Cache-Control "no-cache";
        try_files $uri $uri/ /index.html;
    }
}
```

- [ ] **Step 4: Run the test to see it pass**

Run: `deploy/tests/run-bats.sh deploy/tests/nginx.bats`
Expected: 12 tests, 0 failures. Also `docker run --rm -v "$PWD/deploy/nginx/default.conf:/etc/nginx/conf.d/default.conf:ro" -v "$PWD/deploy/nginx/upstream.conf.example:/etc/nginx/upstream.conf:ro" --add-host api-blue:127.0.0.1 nginx:1.27-alpine nginx -t` prints `syntax is ok` / `test is successful`.

- [ ] **Step 5: CI and Técnico**

`.github/workflows/release.yml` line 43 becomes:

```yaml
      - run: deploy/tests/run-bats.sh deploy/tests/gate.bats deploy/tests/fingerprint.bats deploy/tests/nginx.bats
```

In `Docs/Requisitos/Requisitos - Técnico.md`, after the paragraph "O deploy normal é o pipeline (R0013–R0015). …", add:

```markdown
O nginx entrega o cliente comprimido: os arquivos de `_framework` saem do `.gz` gerado pelo publish (`gzip_static`) e o restante do texto (CSS/JS fora de `_framework` e o JSON da api) é comprimido na hora, exceto `/api/auth/` (tokens) e `/hubs/`. Toda resposta do cliente e das imagens leva `Cache-Control` explícito: `no-cache` (revalidação por ETag) para tudo cujo nome não muda entre versões — inclusive `index.html` e `blazor.boot.json` —, um ano `immutable` só para os `dotnet.*.<hash>.js` de `_framework`, e `private`, um ano, `immutable` para `/images/` (cada upload ganha um nome novo). Assim nenhum navegador nem a borda do Cloudflare serve um arquivo antigo depois de um deploy.
```

- [ ] **Step 6: Commit**

```bash
git add deploy/nginx/default.conf deploy/tests/nginx.bats .github/workflows/release.yml "Docs/Requisitos/Requisitos - Técnico.md"
git commit -m "perf(nginx): cliente comprimido e Cache-Control explícito por tipo de arquivo"
git push
```

---

### Task 3: One shared Dockerfile

**Files:**
- Create: `deploy/Dockerfile`, `tools/bench/equivalencia-imagens.sh`
- Modify: `deploy/deploy.sh:184-188` (`build_images`), `deploy/tests/deploy.bats` (new test after the `select_prunable` test), `CLAUDE.md:72`, `README.md:44`, `Docs/Requisitos/Requisitos - Técnico.md` (deploy section)
- Delete: `src/RuinaRPG.Api/Dockerfile`, `infra/client/Dockerfile`, `tools/RuinaRPG.Smoke/Dockerfile`

**Interfaces:**
- Consumes: nothing from Tasks 1–2.
- Produces: `deploy/Dockerfile` targets `api`, `client`, `smoke`; `build_images <sha>` unchanged in signature.

Record `BASE=$(git rev-parse HEAD)` before starting; the equivalence script compares against it.

- [ ] **Step 1: Write the failing bats unit test**

In `deploy/tests/deploy.bats`, after the `select_prunable` test:

```bash
@test "build_images builds the three targets of deploy/Dockerfile with one GIT_SHA, skipping tags that exist" {
  run bash -c '
    source "$1"
    log() { :; }
    docker() {
      if [[ "$1 $2" == "image inspect" ]]; then [[ "$3" == ruinarpg-client:* ]]; return; fi
      echo "docker $*"
    }
    RUINA_REPO_DIR=/repo build_images abc123' _ "$RUINA_REPO_DIR/deploy/deploy.sh"
  [ "$status" -eq 0 ]
  [ "${#lines[@]}" -eq 2 ]
  [ "${lines[0]}" = "docker build -q -f /repo/deploy/Dockerfile --target api --build-arg GIT_SHA=abc123 -t ruinarpg-api:abc123 /repo" ]
  [ "${lines[1]}" = "docker build -q -f /repo/deploy/Dockerfile --target smoke --build-arg GIT_SHA=abc123 -t ruinarpg-smoke:abc123 /repo" ]
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `deploy/tests/run-bats.sh --filter 'build_images builds' deploy/tests/deploy.bats`
Expected: FAIL (lines name `src/RuinaRPG.Api/Dockerfile` etc.). `setup_file` clones the repo but builds nothing.

- [ ] **Step 3: Write `deploy/Dockerfile`**

```dockerfile
# The three deploy images (Técnico, deploy section) from one build: deploy/deploy.sh builds the targets
# api, client and smoke in turn, and BuildKit reuses the shared stages, so the solution is restored and
# compiled once per deploy.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src
# Only the project files: this layer, with the restored packages in it, is reused until a .csproj
# changes. A new project referenced by these must be added here, or its restore fails loudly.
COPY src/RuinaRPG.Domain/RuinaRPG.Domain.csproj src/RuinaRPG.Domain/
COPY src/RuinaRPG.Contracts/RuinaRPG.Contracts.csproj src/RuinaRPG.Contracts/
COPY src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj src/RuinaRPG.Infrastructure/
COPY src/RuinaRPG.Api/RuinaRPG.Api.csproj src/RuinaRPG.Api/
COPY src/RuinaRPG.Client/RuinaRPG.Client.csproj src/RuinaRPG.Client/
COPY tools/RuinaRPG.Smoke/RuinaRPG.Smoke.csproj tools/RuinaRPG.Smoke/
# The packages stay in the layer, never only in a cache mount: a pruned mount must not break a build
# whose restore layer survived. The mount on NuGet's http-cache only saves the downloads when a .csproj
# changes.
RUN --mount=type=cache,id=ruinarpg-nuget-http,target=/root/.local/share/NuGet/http-cache \
    dotnet restore src/RuinaRPG.Api/RuinaRPG.Api.csproj \
 && dotnet restore src/RuinaRPG.Client/RuinaRPG.Client.csproj \
 && dotnet restore tools/RuinaRPG.Smoke/RuinaRPG.Smoke.csproj

FROM restore AS build
ARG GIT_SHA=dev
COPY src/ src/
COPY tools/RuinaRPG.Smoke/ tools/RuinaRPG.Smoke/
# Embedded in RuinaRPG.Infrastructure (see its .csproj).
COPY ["Docs/Sistema RPG/", "Docs/Sistema RPG/"]
# One RUN: MSBuild compiles each shared project once and reuses it for the next publish.
RUN dotnet publish src/RuinaRPG.Api/RuinaRPG.Api.csproj -c Release --no-restore -o /out/api -p:SourceRevisionId=$GIT_SHA \
 && dotnet publish src/RuinaRPG.Client/RuinaRPG.Client.csproj -c Release --no-restore -o /out/client -p:SourceRevisionId=$GIT_SHA \
 && dotnet publish tools/RuinaRPG.Smoke/RuinaRPG.Smoke.csproj -c Release --no-restore -o /out/smoke -p:SourceRevisionId=$GIT_SHA

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS api
WORKDIR /app
COPY --from=build /out/api .
EXPOSE 8080
ENTRYPOINT ["dotnet", "RuinaRPG.Api.dll"]

# Holds the published client; deploy/deploy.sh copies /app/wwwroot into webroot/releases/<sha>.
FROM alpine:3.20 AS client
COPY --from=build /out/client/wwwroot /app/wwwroot

# aspnet, not runtime: the smoke uses RuinaRPG.Infrastructure (Identity), which needs Microsoft.AspNetCore.App.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS smoke
WORKDIR /app
COPY --from=build /out/smoke .
ENTRYPOINT ["dotnet", "RuinaRPG.Smoke.dll"]
```

No `# syntax=` line: it would pull a frontend image from Docker Hub on every host; BuildKit's built-in frontend (Docker ≥ 23) supports `--mount=type=cache`.

If `dotnet publish --no-restore` of the Client fails because restore did not cover the `browser-wasm` runtime pack, drop `--no-restore` from **that one** publish (restore is then a fast no-op when packages are present) and say so in the report.

- [ ] **Step 4: `build_images` and the old Dockerfiles**

`deploy/deploy.sh`:

```bash
# One Dockerfile, three targets; the same GIT_SHA for all three, or BuildKit could not share the build stage.
build_images() {
  local target
  for target in api client smoke; do
    build_image "ruinarpg-$target" deploy/Dockerfile "$1" --target "$target" --build-arg "GIT_SHA=$1"
  done
}
```

```bash
git rm src/RuinaRPG.Api/Dockerfile infra/client/Dockerfile tools/RuinaRPG.Smoke/Dockerfile
grep -rn "infra/client\|RuinaRPG.Api/Dockerfile\|Smoke/Dockerfile" --include=*.sh --include=*.yml --include=*.bats --include=Makefile --include=*.md . \
  | grep -v '^./docs/superpowers/' | grep -v '/.worktrees/\|/.bats/'
```

The `grep` must only list `CLAUDE.md`, `README.md` and `Requisitos - Técnico.md` (fixed in Step 8); anything else that builds from the old paths is updated here.

- [ ] **Step 5: Run the unit test to see it pass**

Run: `deploy/tests/run-bats.sh --filter 'build_images builds' deploy/tests/deploy.bats`
Expected: PASS.

- [ ] **Step 6: Equivalence script**

`tools/bench/equivalencia-imagens.sh` (mode 755):

```bash
#!/usr/bin/env bash
# One-off check for 2.0.0 C.2: the deploy images built by the old per-project Dockerfiles (at <old-ref>)
# and by deploy/Dockerfile (working tree) hold the same files with the same bytes and the same image
# config. Prints every difference; exits 1 on any unexpected one. Uses its own tags; never the real stack.
set -euo pipefail
old_ref="${1:?uso: $0 <ref com os Dockerfiles antigos>}"
repo="$(cd "$(dirname "$0")/../.." && pwd)"
tmp="$(mktemp -d)"
tag="eq$$"
sha=equivalencia
cleanup() {
  git -C "$repo" worktree remove --force "$tmp/old" > /dev/null 2>&1 || true
  local t
  for t in api client smoke; do docker rmi "ruinarpg-eq-old-$t:$tag" "ruinarpg-eq-new-$t:$tag" > /dev/null 2>&1 || true; done
  rm -rf "$tmp"
}
trap cleanup EXIT

git -C "$repo" worktree add --quiet --detach "$tmp/old" "$old_ref"
docker build -q -f "$tmp/old/src/RuinaRPG.Api/Dockerfile" --build-arg "GIT_SHA=$sha" -t "ruinarpg-eq-old-api:$tag" "$tmp/old" > /dev/null
docker build -q -f "$tmp/old/infra/client/Dockerfile" --build-arg "GIT_SHA=$sha" -t "ruinarpg-eq-old-client:$tag" "$tmp/old" > /dev/null
docker build -q -f "$tmp/old/tools/RuinaRPG.Smoke/Dockerfile" -t "ruinarpg-eq-old-smoke:$tag" "$tmp/old" > /dev/null
for t in api client smoke; do
  docker build -q -f "$repo/deploy/Dockerfile" --target "$t" --build-arg "GIT_SHA=$sha" -t "ruinarpg-eq-new-$t:$tag" "$repo" > /dev/null
done

# manifest <image> — "<path> <sha256>" per regular file, "<path> -> <target>" per symlink, sorted.
manifest() {
  local c
  c="$(docker create "$1")"
  docker export "$c" | python3 -c '
import hashlib, sys, tarfile
out = []
with tarfile.open(fileobj=sys.stdin.buffer, mode="r|") as t:
    for m in t:
        if m.isfile():
            out.append(f"{m.name} {hashlib.sha256(t.extractfile(m).read()).hexdigest()}")
        elif m.issym():
            out.append(f"{m.name} -> {m.linkname}")
print("\n".join(sorted(out)))'
  docker rm "$c" > /dev/null
}
config() { docker image inspect --format '{{json .Config.Entrypoint}} {{json .Config.Cmd}} {{.Config.WorkingDir}} {{json .Config.ExposedPorts}} {{json .Config.Env}}' "$1"; }

fail=0
for t in api client smoke; do
  manifest "ruinarpg-eq-old-$t:$tag" > "$tmp/old-$t"
  manifest "ruinarpg-eq-new-$t:$tag" > "$tmp/new-$t"
  diff -u "$tmp/old-$t" "$tmp/new-$t" > "$tmp/diff-$t" || true
  # Expected: the old smoke Dockerfile never passed GIT_SHA, so its RuinaRPG assemblies carried no
  # SourceRevisionId; the shared build stamps all three images alike.
  if [[ "$t" == smoke ]]; then
    grep -E '^[-+]app/RuinaRPG\.[A-Za-z.]+\.dll ' "$tmp/diff-$t" | sed 's/^/esperado: /' || true
    unexpected="$(grep -E '^[-+][^-+]' "$tmp/diff-$t" | grep -vE '^[-+]app/RuinaRPG\.[A-Za-z.]+\.dll ' || true)"
  else
    unexpected="$(grep -E '^[-+][^-+]' "$tmp/diff-$t" || true)"
  fi
  if [[ -n "$unexpected" ]]; then echo "FALHA $t: arquivos diferentes"; echo "$unexpected"; fail=1; else echo "OK    $t: arquivos"; fi
  if [[ "$(config "ruinarpg-eq-old-$t:$tag")" == "$(config "ruinarpg-eq-new-$t:$tag")" ]]; then
    echo "OK    $t: config"
  else
    echo "FALHA $t: config"; config "ruinarpg-eq-old-$t:$tag"; config "ruinarpg-eq-new-$t:$tag"; fail=1
  fi
done
exit "$fail"
```

- [ ] **Step 7: Run the equivalence check, the prune case, and the real deploy tests**

```bash
git add -A deploy tools/bench/equivalencia-imagens.sh src/RuinaRPG.Api tools/RuinaRPG.Smoke infra
git commit -m "perf(deploy): um Dockerfile compartilhado compila a solução uma vez por deploy"
tools/bench/equivalencia-imagens.sh "$BASE"
```

Expected: six `OK` lines (`api`/`client`/`smoke` × files/config), plus `esperado:` lines for smoke assemblies only. Any other difference: investigate and fix before going on (a file missing from the `COPY` list shows up here).

Review Focus 4 — a pruned cache mount must not break a build whose restore layer survived:

```bash
docker build -q -f deploy/Dockerfile --target api --build-arg GIT_SHA=prune1 -t ruinarpg-eq-prune:1 .
docker builder prune -f --filter type=exec.cachemount
echo "// prune-check" >> src/RuinaRPG.Api/Program.cs
docker build -f deploy/Dockerfile --target api --build-arg GIT_SHA=prune2 -t ruinarpg-eq-prune:2 . 2>&1 | grep -E 'CACHED|restore|ERROR' | head
git checkout -- src/RuinaRPG.Api/Program.cs
docker rmi ruinarpg-eq-prune:1 ruinarpg-eq-prune:2
```

Expected: the second build succeeds, its `restore` step is `CACHED`, no `ERROR`.

Then the real deploy paths (throwaway `ruinarpg-bats-<pid>` stacks; they build HEAD's images with the new Dockerfile):

Run: `deploy/tests/run-bats.sh deploy/tests/deploy.bats deploy/tests/rollback.bats`
Expected: all pass.

- [ ] **Step 8: Docs**

- `CLAUDE.md` line 72: replace the `infra/client/` bullet with ``- `deploy/Dockerfile` — the one multi-stage build of the three deploy images (targets `api`, `client`, `smoke`): restored and compiled once per deploy; the client is published to versioned releases under the nginx webroot. `deploy/nginx/` holds the long-lived nginx config (static client, compression and cache headers, `/api/` and `/hubs/` proxy to the active color).``
- `README.md` line 44: remove the `infra/client/` line; in the `deploy/` line, add `Dockerfile` to the list (`deploy.sh (blue-green), Dockerfile (as três imagens), gate.sh, …`).
- `Requisitos - Técnico.md`: in "Cada serviço builda sua própria imagem a partir de um `Dockerfile`…" write instead "As imagens da api, do cliente e do smoke saem de um único `deploy/Dockerfile` de vários estágios, que instala tudo que precisa (runtime do .NET, dependências do build, etc.) e compila a solução uma vez por deploy;" (keep the rest of the sentence); in "O build do cliente fica em `infra/client/` e a configuração do nginx em `deploy/nginx/`." write "O build das imagens fica em `deploy/Dockerfile` e a configuração do nginx em `deploy/nginx/`."

```bash
git add CLAUDE.md README.md "Docs/Requisitos/Requisitos - Técnico.md"
git commit -m "docs: deploy/Dockerfile substitui os Dockerfiles por projeto"
git push
```

---

### Task 4: Lossless recompression of the startup images

**Files:**
- Create: `tools/imagens/recomprimir-png.sh`
- Modify: `src/RuinaRPG.Client/wwwroot/favicon.png`, `src/RuinaRPG.Client/wwwroot/branding/logo-grande.png`

- [ ] **Step 1: Write the script with its pixel check**

`tools/imagens/recomprimir-png.sh` (mode 755):

```bash
#!/usr/bin/env bash
# Lossless PNG recompression (2.0.0 C.3): oxipng in a throwaway alpine container; a file is replaced only
# if ImageMagick finds 0 differing pixels (RGBA) between the original and the result.
# --check <a.png> <b.png> only runs the pixel comparison (exit 0 when identical).
set -euo pipefail
run() {
  docker run --rm -v "$1:/w" alpine:3.20 sh -c "apk add --no-cache -q oxipng imagemagick > /dev/null && $2"
}
if [[ "${1:-}" == --check ]]; then
  d="$(mktemp -d)"; trap 'rm -rf "$d"' EXIT
  cp "$2" "$d/a.png"; cp "$3" "$d/b.png"
  out="$(run "$d" 'magick compare -metric AE /w/a.png /w/b.png null: 2>&1 || true')"
  echo "pixels diferentes: $out"
  [[ "$out" == 0 ]]
  exit
fi
(( $# )) || { echo "uso: $0 <arquivo.png>... | --check <a.png> <b.png>" >&2; exit 2; }
for f in "$@"; do
  d="$(mktemp -d)"
  cp "$f" "$d/orig.png"; cp "$f" "$d/new.png"
  run "$d" 'oxipng -q -o max --strip safe /w/new.png && chmod a+r /w/new.png'
  if "$0" --check "$d/orig.png" "$d/new.png"; then
    echo "$f: $(stat -c %s "$d/orig.png") -> $(stat -c %s "$d/new.png") bytes"
    cp "$d/new.png" "$f"
  else
    echo "$f: pixels mudaram; arquivo mantido" >&2; rm -rf "$d"; exit 1
  fi
  rm -rf "$d"
done
```

(No `--alpha`: it would rewrite the color of fully transparent pixels, which the check rightly counts.)

- [ ] **Step 2: See the check fail on different images, pass on equal ones**

```bash
tools/imagens/recomprimir-png.sh --check src/RuinaRPG.Client/wwwroot/favicon.png src/RuinaRPG.Client/wwwroot/icon-192.png; echo "exit=$?"
tools/imagens/recomprimir-png.sh --check src/RuinaRPG.Client/wwwroot/favicon.png src/RuinaRPG.Client/wwwroot/favicon.png; echo "exit=$?"
```

Expected: the first exits non-zero (different size or pixels, the message shows the error or a count); the second prints `pixels diferentes: 0` and `exit=0`.

- [ ] **Step 3: Recompress**

```bash
git show HEAD:src/RuinaRPG.Client/wwwroot/favicon.png > /tmp/claude-favicon-orig.png
tools/imagens/recomprimir-png.sh src/RuinaRPG.Client/wwwroot/favicon.png src/RuinaRPG.Client/wwwroot/branding/logo-grande.png
tools/imagens/recomprimir-png.sh --check /tmp/claude-favicon-orig.png src/RuinaRPG.Client/wwwroot/favicon.png
rm /tmp/claude-favicon-orig.png
```

Expected: two "a -> b bytes" lines with b < a, and the re-check against the committed original prints `0`. If oxipng cannot shrink a file, it stays as it was (record that).

- [ ] **Step 4: Client tests**

Run: `dotnet test tests/RuinaRPG.Tests.Client`
Expected: all pass (nothing references image bytes, but the wwwroot is part of the client build).

- [ ] **Step 5: Commit**

```bash
git add tools/imagens/recomprimir-png.sh src/RuinaRPG.Client/wwwroot/favicon.png src/RuinaRPG.Client/wwwroot/branding/logo-grande.png
git commit -m "perf(cliente): favicon e logo recomprimidos sem perda (mesmos pixels)"
git push
```

---

### Task 5: "After" numbers, the `/api/rulebook` rule, and docs

**Files:**
- Modify: `docs/superpowers/specs/2026-10-16-versao-2-0-0-baseline.md`, `docs/superpowers/specs/2026-10-17-versao-2-0-0-h-notas-do-cutover.md`
- Modify only if Step 2 triggers: `src/RuinaRPG.Infrastructure/Rules/RulebookRenderer.cs`, a test in `tests/RuinaRPG.Tests.Integration/`

**Interfaces:**
- Consumes: Task 1's scripts and keys; `tools/bench/results/antes-C.txt` (if it is gone, re-run Task 1 Step 7 against `3040ed0`).

- [ ] **Step 1: Measure "after"**

```bash
bash tools/bench/measure-payload.sh depois-C
bash tools/bench/measure-load.sh depois-C
bash tools/bench/measure-api-latency.sh depois-C
bash tools/bench/measure-deploy.sh depois-C
cat tools/bench/results/depois-C.txt
```

Success criteria to check (spec): `load.cold.transferred.kb` ≤ 7168; `deploy.full.warm.build.seconds` ≤ half of `antes-C`'s. If one misses, stop and report it with the numbers (DONE_WITH_CONCERNS); do not tune blindly.

- [ ] **Step 2: `/api/rulebook` rule**

Read `latency./api/rulebook` p50 from `depois-C.txt`.

- **≤ 0.030 s:** no code change. Record "rulebook: p50 X s ≤ 30 ms, sem mudança" in the report and go to Step 3.
- **> 0.030 s:** profile before changing anything. Temporarily wrap each `Build*Async` in `GetDocuments` and each `SplitIntoSections` call with `Stopwatch` and `Console.Error.WriteLine`, run `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~Rulebook` a few times, read the timings, and **revert** the instrumentation. Only if `SplitIntoSections` (the Markdown pipeline) dominates:
  1. Find (or, if missing, first write and see pass on the current code) an integration test proving that a Markdown override saved through `RulebookDocumentsController` after a first `GET /api/rulebook` shows up in the next `GET` — the memo must never hide a change.
  2. Rename the current method to `SplitIntoSectionsUncached` and add:

  ```csharp
  // Memo of the pure Markdown→sections step only (2.0.0 C.5): same input, same output, so no response
  // can change. Everything read from the database is still read on every request. Overrides add keys,
  // so the memo is bounded.
  private static readonly ConcurrentDictionary<(string Markdown, int SplitLevel, int? GroupLevel), (string? IntroHtml, RulebookSection[] Sections)> SectionsMemo = new();

  private static (string? IntroHtml, List<RulebookSection> Sections) SplitIntoSections(string markdown, int splitLevel, int? groupLevel = null)
  {
      if (SectionsMemo.Count > 64) SectionsMemo.Clear();
      var (intro, sections) = SectionsMemo.GetOrAdd((markdown, splitLevel, groupLevel), key =>
      {
          var (i, s) = SplitIntoSectionsUncached(key.Markdown, key.SplitLevel, key.GroupLevel);
          return (i, s.ToArray());
      });
      return (intro, sections.ToList());
  }
  ```

  (`using System.Collections.Concurrent;`.) Check that `RulebookSection` is immutable (a record with positional/init-only members); if any caller mutates a returned section, copy each section instead of sharing it.
  3. Run `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~Rulebook` (all pass), re-run `measure-api-latency.sh depois-C-rulebook`, and commit: `perf(rulebook): memo do Markdown→seções (mesma saída, sem cache de dados)`.

  If the cost is elsewhere (database reads, serialization), change nothing and record the profile in the report.

- [ ] **Step 3: Baseline doc — "After C"**

Append to `docs/superpowers/specs/2026-10-16-versao-2-0-0-baseline.md`:

```markdown
## After C (deploy and load performance)

Measured on `<sha>` (release/2.0.0), same VM, one run each, with the deploy-based harness of `tools/bench/`
(the stack is deployed by the ref's own `deploy.sh`). "Before" is `3040ed0` measured with the same harness.

| Area | Metric | Before C (`3040ed0`) | After C |
|---|---|---|---|
| Load | first visit, bytes on the wire (KB) | … | … |
| Load | repeat visit, bytes on the wire (KB) | … | … |
| Load | first render, localhost, cold / warm (s) | … | … |
| Load | first render, 20 Mbit/s + 40 ms, cold / warm (s) | … | … |
| Deploy | full `deploy.sh deploy`, cold (s) | … | … |
| Deploy | build stage, cold (s) | … | … |
| Deploy | full `deploy.sh deploy`, one-line Api change (s) | … | … |
| Deploy | build stage, one-line Api change (s) | … | … |
| Client payload | `_framework` raw / SDK `.gz` / SDK `.br` (KB) | … | … |
| API latency | `/api/rulebook` p50 / p95 through nginx (s) | … | … |
| Images | `favicon.png` / `logo-grande.png` (KB) | 596 / 640 | … |
```

Fill every cell from `antes-C.txt` / `depois-C.txt` (and the image sizes from Task 4), plus one line per other latency endpoint if any moved by more than 20 %. Add one paragraph stating whether each success criterion of the spec was met, and the `/api/rulebook` outcome.

- [ ] **Step 4: H notes — "From sub-project C"**

Append to `docs/superpowers/specs/2026-10-17-versao-2-0-0-h-notas-do-cutover.md`:

```markdown
## From sub-project C (deploy and load performance)

- Preflight: `sudo docker version --format '{{.Server.Version}}'` on production must be ≥ 23 (BuildKit by default; `deploy/Dockerfile` uses `RUN --mount=type=cache`). Older: upgrade Docker before the window.
- The first 2.0.0 deploy on production builds with an empty build cache: expect the cold build time of the baseline doc's "After C" table, not the warm one, and size the window for it.
- `deploy/nginx/default.conf` changes with 2.0.0 and is picked up by the color switch's `nginx -s reload`; the `web` container is not recreated. It also serves 1.5.0's client correctly, so a rollback needs nothing here.
- After the cutover, through Cloudflare: `curl -sI -H 'Accept-Encoding: gzip' https://<host>/` and the same for `/_framework/blazor.boot.json`, one `/_framework/*.wasm` (name from `blazor.boot.json`) and `/css/app.css`: each shows `cache-control` as in Técnico's deploy section and the `.wasm`/`.css` show `content-encoding` (`gzip`, or `br` if Cloudflare re-encodes). A missing `cache-control` means a Cloudflare rule overrides it; check the panel.
```

- [ ] **Step 5: Full suite, then commit**

```bash
dotnet build --no-incremental -warnaserror
dotnet test
deploy/tests/run-bats.sh deploy/tests/gate.bats deploy/tests/fingerprint.bats deploy/tests/nginx.bats
python3 -m unittest deploy/tests/test_compare.py
git add docs/superpowers/specs/2026-10-16-versao-2-0-0-baseline.md docs/superpowers/specs/2026-10-17-versao-2-0-0-h-notas-do-cutover.md
git commit -m "docs: medições depois do C e notas do H"
git push
```

Expected: 0 warnings; Unit/Client/Integration all pass; bats and python pass.
