# Versão 2.0.0 — Spec C: Deploy and load performance — Design

**Status:** design approved by user 2026-10-18 (in chat, section by section). Version 2.0.0, branch
`release/2.0.0`. Detailed spec of sub-project **C** of the roadmap (`2026-10-16-versao-2-0-0-roadmap-design.md`).
It refines the roadmap, with the user's approval: Brotli at the origin, `InvariantGlobalization`, AOT and
lazy-loading are out (see "Out of scope"); lossless recompression of the startup images is in.

## Overview

Same behavior for the end user (same pixels, same pt-BR formatting, same features). C makes three things
faster:

1. **Load:** nginx serves the client compressed and with explicit `Cache-Control`, so the first visit
   transfers about 6.5 MB instead of about 15.9 MB, a repeat visit transfers almost nothing, and no
   browser or Cloudflare edge serves stale JS/CSS after a deploy.
2. **Deploy:** one shared multi-stage Dockerfile restores with a cached layer and compiles the solution
   once per deploy instead of three times.
3. **API:** re-measured through nginx after compression; code changes only where a measurement still
   shows a problem.

## Findings that drive C

Measured on this VM, 2026-10-18, on `062f0ac` (see also `2026-10-16-versao-2-0-0-baseline.md`):

1. **Client payload:** Release publish already trims. `_framework` holds 76 `.wasm`, 3 ICU `.dat`, 4 `.js`
   and `blazor.boot.json`: 14.3 MB raw. The publish already writes `.gz` (5.9 MB) and `.br` (4.2 MB) next
   to each `_framework` file, but nginx serves neither (no `gzip`, no `gzip_static`). Outside `_framework`
   nothing is precompressed: `MudBlazor.min.css` (612 KB), `jodit.min.js` (716 KB), `jodit.min.css`,
   `MudBlazor.min.js`, `app.css`, `theme.css`, `RuinaRPG.Client.styles.css` — about 1.6 MB of text.
2. **Fingerprints:** only `_framework/dotnet.native.<ver>.<hash>.js` and `dotnet.runtime.<ver>.<hash>.js`
   carry a content hash in the name. Everything else keeps its name across releases.
3. **Cache headers:** only `index.html` and `blazor.boot.json` send `Cache-Control: no-cache`. Everything
   else sends none, so browsers and Cloudflare cache heuristically. Production sits behind a Cloudflare
   Tunnel with **default settings** (user, 2026-10-18): Cloudflare caches `.js`/`.css`/images at the edge
   by extension and, without `Cache-Control`, for a default TTL, so a deploy can be followed by stale
   `blazor.webassembly.js`/`MudBlazor.min.js`/`app.css` for hours. The hop from the house to Cloudflare
   carries whatever nginx sends.
4. **nginx image:** `nginx:1.27-alpine` is built `--with-http_gzip_static_module` and has no Brotli module.
   `deploy.sh`'s `reload_web` runs `nginx -t -q && nginx -s reload` on every color switch, which re-reads
   the bind-mounted `default.conf`.
5. **Uploads:** `DiskImageFileStore.SaveAsync` names every upload `<Guid.NewGuid()><ext>` and never
   overwrites; `/images/` is served by nginx without authentication (the GUID is the protection).
6. **Startup images:** the landing page loads `favicon.png` (596 KB) and `branding/logo-grande.png`
   (640 KB).
7. **Builds:** `src/RuinaRPG.Api/Dockerfile`, `infra/client/Dockerfile` and `tools/RuinaRPG.Smoke/Dockerfile`
   each `COPY . .` before `dotnet publish`, so any change invalidates restore, and each compiles
   Domain/Contracts (and, for Api and Smoke, Infrastructure) on its own. `docker compose build`: 207–303 s
   cold, about 143 s after a one-line Api change. `deploy.sh build_images` builds the three in sequence
   (`build_image <image> <dockerfile> <sha> [args]`, skipped when the tag exists). Docker on this VM:
   29.5.3, buildx 0.34.1.
8. **API latency** (baseline, 50 sequential requests, localhost, Development): every measured endpoint
   is under 20 ms p50 except `/api/rulebook` (about 56 ms), which returns all 7 rendered rulebook
   documents as HTML inside JSON.
9. **Bench:** `measure-load.sh` measures first render over localhost only, so transfer size is invisible
   to it; `measure-deploy.sh` measures only `docker compose build`, so there is no full-deploy number.

## Design

### C.1 nginx (`deploy/nginx/default.conf`)

**Compression.**

- `gzip_static on` in the client locations: nginx sends `<file>.gz` when it exists and the request
  accepts gzip (all of `_framework`).
- `gzip on`, `gzip_comp_level 5`, `gzip_min_length 1024`, `gzip_types text/css application/javascript
  application/json image/svg+xml text/plain`, `gzip_proxied any`: on-the-fly compression for the text
  files without a `.gz` and for `/api/` JSON.
- `/hubs/` gets `gzip off` (SignalR negotiates its own transport).
- `/api/auth/` gets `gzip off`: its responses carry tokens next to request-derived data, the shape
  BREACH attacks compression on (refinement found while planning).
- `gzip_vary on`: `Vary: Accept-Encoding` on every compressible response, so Cloudflare keeps the
  compressed and plain variants apart.
- A request without `Accept-Encoding: gzip` gets the plain file, as today.
- `.wasm` is served as `application/wasm` (the stock `mime.types` maps it; the header test pins it).

**Cache headers** — every client and image response carries an explicit `Cache-Control`:

| Path | `Cache-Control` |
|---|---|
| `/index.html`, `/_framework/blazor.boot.json`, and the `try_files` fallback to `index.html` | `no-cache` |
| `/_framework/dotnet.*.<10-char hash>.js` | `public, max-age=31536000, immutable` |
| every other client file (`.wasm`, `.dat`, CSS, JS, fonts, images, audio, `appsettings.json`) | `no-cache` |
| `/images/` (uploads) | `private, max-age=31536000, immutable` |

`no-cache` means "revalidate": nginx answers the revalidation with `304` (ETag/Last-Modified, on by
default), so a repeat visit costs headers only. A new release lives in a new `releases/<sha>` directory,
so its files get new mtimes and ETags. Blazor also keeps the `.wasm`/`.dat` resources in its own Cache
Storage keyed by the integrity hash in `blazor.boot.json`. `private` on uploads keeps them out of the
Cloudflare edge, so a deleted image is not left reachable there; the browser still caches it.

`/api/` responses keep whatever headers the API sends; nginx only compresses them.

**Rollout:** the file is bind-mounted into the long-lived `web` container; the first 2.0.0 deploy's
color switch reloads it. No container is recreated. A rollback to 1.5.0 keeps the new `default.conf`
(the rollback does not check out files), which is compatible with 1.5.0's client and API.

### C.2 Shared Dockerfile (`deploy/Dockerfile`)

The three Dockerfiles are replaced by one multi-stage file:

- `restore` (`mcr.microsoft.com/dotnet/sdk:8.0`): copies only the
  `.csproj` files of `src/*` and `tools/RuinaRPG.Smoke` (no test projects) to their paths, then runs
  `dotnet restore` for `src/RuinaRPG.Api`, `src/RuinaRPG.Client` and `tools/RuinaRPG.Smoke`. The restored
  packages stay **in the layer** (reused until a `.csproj` changes); a BuildKit cache mount on NuGet's
  **http-cache** (`/root/.local/share/NuGet/http-cache`) saves the downloads when one does. Refinement
  found while planning: with the packages folder itself on a cache mount, a partial prune could leave the
  restore layer cached and the packages gone, and the `--no-restore` build would fail.
- `build` (from `restore`, `ARG GIT_SHA=dev`): copies `src/`, `tools/RuinaRPG.Smoke/` and `Docs/Sistema RPG/`
  (embedded resources of Infrastructure), then in **one** `RUN` (same cache mount) publishes the three
  projects with `-c Release --no-restore -p:SourceRevisionId=$GIT_SHA` to `/out/api`, `/out/client` and
  `/out/smoke` (no cache mount: the packages come from the `restore` layer). MSBuild compiles each shared project once and reuses it for the next publish.
- `api` (`mcr.microsoft.com/dotnet/aspnet:8.0`): `/out/api` in `/app`, `EXPOSE 8080`,
  `ENTRYPOINT ["dotnet", "RuinaRPG.Api.dll"]` — same as today.
- `client` (`alpine:3.20`): `/out/client/wwwroot` in `/app/wwwroot` — same as today.
- `smoke` (`mcr.microsoft.com/dotnet/aspnet:8.0`): `/out/smoke` in `/app`,
  `ENTRYPOINT ["dotnet", "RuinaRPG.Smoke.dll"]` — same as today.

If the exact `COPY` list misses a file a project needs at build time, the build fails loudly; the plan
derives the list from the projects (`.csproj` references, embedded resources) and the equivalence check
below proves nothing went missing.

`deploy.sh`: `build_image` keeps its contract (skip when `<image>:<sha>` exists; tags unchanged) and gains
the target: `build_images` builds `ruinarpg-api`, `ruinarpg-client` and `ruinarpg-smoke` from
`deploy/Dockerfile` with `--target api|client|smoke` and `--build-arg GIT_SHA=<sha>` for all three (the
`build` stage is shared, so its args must match for the cache to hit). BuildKit reuses the `build` stage
for the second and third image. Nothing else in `deploy.sh`, `clone.sh` or the workflows names the old
Dockerfiles except what the plan finds with `grep` and updates.

Requirement: BuildKit (default since Docker 23). Production's Docker version is an H preflight item.

### C.3 Startup images

`favicon.png` and `branding/logo-grande.png` are recompressed losslessly with `oxipng` (run once, from a
container, result committed). The script that does it decodes original and result and fails unless every
pixel (RGBA) is identical. Other images are not touched.

### C.4 Measurement (`tools/bench/`)

The stack-based bench scripts still use the pre-F layout (`compose.bench.yml` overrides an `nginx` service
that no longer exists), so they are rebuilt first around `deploy.sh`: a shared clone of the repo at a
given ref, deployed with `RUINA_PROJECT=ruinarpg-bench` on `127.0.0.1:8088`, torn down with its volumes.
Because the ref is a parameter, "before" (`3040ed0`) and "after" are measured with the same harness.
`compose.bench.yml` is removed.

- `measure-load.sh`: per page, besides today's localhost first render, records **bytes transferred**
  (sum of the Playwright response sizes, with `Accept-Encoding` as the browser sends it) and **first
  render on a throttled network** (Chromium network emulation: 20 Mbit/s down, 40 ms latency), cold and
  warm. If the bench stack does not serve the client through `deploy/nginx/default.conf`, the plan fixes
  that first, so the change is measured where it happens.
- `measure-deploy.sh`: besides cold/warm `compose build`, runs a **full `deploy.sh deploy`** in the
  `ruinarpg-bench` project: one deploy from nothing and one after a one-line Api change, total time and
  per stage (from the `deploy.sh` log timestamps). It runs before and after C.2, since no full-deploy
  baseline exists.
- `measure-payload.sh`: also records the `.gz` and `.br` totals.
- `measure-api-latency.sh`: re-run after C.1, through nginx.

### C.5 API

No code change up front. After C.1, an endpoint is fixed in C only if it is still **above 30 ms p50**
through nginx and a profile shows the cause (N+1 queries, missing `AsNoTracking`, work repeated per
request). The one candidate today is `/api/rulebook`. Correction found while planning: `RulebookRenderer`
is not a pure function of embedded resources — it reads the Rules Auditor's Markdown overrides and the
live Traits, Historicos, EquipmentKits and level tables on every request — so its documents must **not**
be cached across requests. The only cache allowed is a memo of the pure Markdown-to-sections step
(`SplitIntoSections`, keyed by its full input, bounded), which cannot change any output. If the profile
shows the cost elsewhere (database reads, serialization, size), nothing changes in C.

## Tests

TDD: each test below is written and seen failing before the change it covers.

- **nginx headers** (new, runs with the pipeline tests in `deploy/tests/` and therefore in CI): starts
  `nginx:1.27-alpine` with the real `default.conf`, a fake webroot (`index.html`, `_framework/x.wasm`
  with `x.wasm.gz`, `_framework/blazor.boot.json`, `_framework/dotnet.runtime.8.0.0.abcdefghij.js`, a CSS
  file over 1 KB, an SPA route), an `/images/` file, and a stub upstream answering JSON over 1 KB on
  `/api/` and on `/hubs/`. Asserts per row of the C.1 table: `Cache-Control`; `Content-Encoding: gzip`
  and `Vary: Accept-Encoding` with `Accept-Encoding: gzip`; plain body without it; `.wasm` content type;
  `/hubs/` never compressed; the SPA fallback is `index.html` with `no-cache`. Uses its own container
  names and network and removes them.
- **`deploy.sh` bats:** a unit test (sourcing `deploy.sh` with a `docker` shell function, like the
  existing `select_prunable` test) asserts `-f deploy/Dockerfile` with `--target api|client|smoke` and the
  same `GIT_SHA` for all three, and that an existing tag still skips the build.
- **Image equivalence** (one-off script under `tools/bench/` or `tools/`, run during C, not in CI): builds
  the three images from the old Dockerfiles (parent commit) and from `deploy/Dockerfile` with the same
  `GIT_SHA`, exports each image's filesystem, and compares file lists and contents; any byte difference
  is listed and must be explained (none expected beyond nondeterministic build metadata) before C is
  done.
- **Images:** the pixel check in C.3.
- **Suite:** Unit, Client and Integration pass; `dotnet build` 0 warnings; bats and `test_compare.py` pass.

## Docs

- `Requisitos - Técnico.md`: a paragraph in the deploy section on the nginx policy (compression, cache
  per file type, uploads private) and the shared `deploy/Dockerfile`.
- `CLAUDE.md`: `deploy/Dockerfile` replaces the mentions of `infra/client/` and the Api Dockerfile;
  README likewise if it names them.
- Baseline doc: an "After C" section with the measurements of C.4 before and after.
- H notes, section "From sub-project C": preflight that production's Docker has BuildKit (≥ 23); the
  first 2.0.0 deploy on production builds with an empty cache (cold time); after the cutover,
  `curl -sI -H 'Accept-Encoding: gzip'` on the public URL for `index.html`, a `.wasm` and `app.css`
  confirms `Content-Encoding` and `Cache-Control` through Cloudflare.

## Success criteria

- First visit to the landing page transfers **≤ 7 MB** (about 15.9 MB before); throttled first render
  recorded before and after.
- Image build after a one-line Api change **at least 2× faster** than before (about 143 s); full
  `deploy.sh deploy` recorded before and after.
- Every client and upload response carries the `Cache-Control` of the C.1 table (header test).
- The three images' content is equivalent before and after C.2 (equivalence script).
- Startup images pixel-identical; no visible change for the user.
- `/api/rulebook` re-measured through nginx; code changed only under the C.5 rule.
- Suite green, 0 warnings.

## Out of scope

- Brotli at the origin (needs a custom `web` image and a recreate of the long-lived container; on default
  settings Cloudflare can serve Brotli to browsers itself).
- Brotli decoding in JS on the client.
- `InvariantGlobalization` (changes pt-BR formatting), AOT (bigger download, slower build), lazy-loading
  of assemblies (no measurement justifies it).
- Recompressing images other than `favicon.png` and `logo-grande.png`.
- Any change to the `web` container image, to `docker-compose.yml`'s services beyond what C.2 needs, or
  to the API's responses.
