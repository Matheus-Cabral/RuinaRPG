# Versão 2.0.0 — Baseline measurements

Measured on `cc579e6` (code identical to `main` 729dac9), this VM (4 cores, 15 GB RAM), 2026-10-16.
Scripts: `tools/bench/`. Each timing run twice; both values shown (run 1 / run 2). Raw output: `tools/bench/results/baseline.txt` (git-ignored).

| Area | Metric | Baseline (run 1 / run 2) | After A | 2.0.0 |
|---|---|---|---|---|
| Build | `dotnet build --no-incremental` (s) | 73.0 / 98.2 | 74.9 / 128.5 / 72.9 | |
| Build | warnings | 0 / 0 | 0 / 0 / 0 | |
| Tests | Unit: time (s) | 7.0 / 7.1 | 7.1 / 7.1 / 7.7 | |
| Tests | Unit: result | 951 passed, 0 failed / 951 passed, 0 failed | 951 passed, 0 failed (3 runs) | |
| Tests | Client (bUnit): time (s) | 27.6 / 28.7 | 27.9 / 29.1 / 29.5 | |
| Tests | Client: result | 918 passed, 0 failed / 918 passed, 0 failed | 918 passed, 0 failed (3 runs) | |
| Tests | Integration, one process: time (s) | 1200.8 / 1231.2 | 453.1 / 432.9 / 432.5 | |
| Tests | Integration, one process: result | 1719 passed, 80 failed / 1720 passed, 79 failed (of 1799) | 1806 passed, 1 failed (of 1807; cleanup race, passes alone) / 1806 passed, 0 failed / 1806 passed, 0 failed | |
| Tests | Integration, one process: `HttpClient.Timeout` failures | 80 / 79 | 0 / 0 / 0 | |
| Tests | Integration, six slices: slice 0 (s) | 285.6 / 296.9 | 139.8 | |
| Tests | Integration, six slices: slice 1 (s) | 289.5 / 301.7 | 120.4 | |
| Tests | Integration, six slices: slice 2 (s) | 253.9 / 260.1 | 94.9 | |
| Tests | Integration, six slices: slice 3 (s) | 172.4 / 170.9 | 80.9 | |
| Tests | Integration, six slices: slice 4 (s) | 107.4 / 107.4 | 109.9 | |
| Tests | Integration, six slices: slice 5 (s) | 130.6 / 128.6 | 110.1 | |
| Tests | Integration, six slices: total (s) | 1239.4 / 1265.6 | 656.0 | |
| Tests | Integration, six slices: result | 397+529+499+230+38+106 = 1799 passed, 0 failed / same | 381+550+499+231+40+105 = 1806 passed, 0 failed | |
| Deploy | `docker compose build`, cold (s) | 303.0 / 207.5 | | |
| Deploy | `docker compose build`, warm, one Api line changed (s) | 144.4 / 142.4 | | |
| Client payload | `_framework` files | 84 | | |
| Client payload | `_framework` raw (KB) | 14180 | | |
| Client payload | `_framework` gzip -9 (KB) | 5103 | | |
| Client payload | encoding served today | none (nginx has no gzip/brotli configured) | | |
| Load time | landing first render, cold (s) | 2.25 / 2.72 | | |
| Load time | landing first render, warm reload (s) | 1.31 / 1.32 | | |
| API latency | `/api/auth/me` p50 / p95 (s) | 0.0043 / 0.0076 ; 0.0045 / 0.0069 | | |
| API latency | `/api/campaigns` | 0.0038 / 0.0051 ; 0.0039 / 0.0056 | | |
| API latency | `/api/campaigns/{id}/members` | 0.0053 / 0.0084 ; 0.0056 / 0.0070 | | |
| API latency | `/api/npc-sheets/{id}` | 0.0154 / 0.0176 ; 0.0170 / 0.0194 | | |
| API latency | `/api/creature-sheets/{id}` | 0.0106 / 0.0125 ; 0.0120 / 0.0146 | | |
| API latency | `/api/items` | 0.0137 / 0.0155 ; 0.0145 / 0.0191 | | |
| API latency | `/api/spell-ability-bank` | 0.0043 / 0.0052 ; 0.0046 / 0.0058 | | |
| API latency | `/api/traits` | 0.0040 / 0.0048 ; 0.0039 / 0.0048 | | |
| API latency | `/api/rulebook` | 0.0564 / 0.0631 ; 0.0517 / 0.0565 | | |
| API latency | `/api/compendio/search?q=fogo` | 0.0040 / 0.0054 ; 0.0039 / 0.0051 | | |
| Code size | lines `.cs` / `.razor` (migrations excluded) | Api 16330 / 0; Client 1583 / 18240; Contracts 1248 / 0; Domain 3880 / 0; Infrastructure 5585 / 0 | Api 16312 / 0; Client 1583 / 18240; Contracts 1248 / 0; Domain 3880 / 0; Infrastructure 5585 / 0 | |
| Code size | test projects `.cs` lines | Unit 6695; Client 17334; Integration 43635 | Unit 6695; Client 17334; Integration 40305 | |
| Code size | migrations | 169 files, 9.6 MB | 169 files, 9.6 MB | |

Notes:

- Integration total is 1799 tests (the ~1677 estimate was stale). The full single-process run shows ~4.4% `HttpClient.Timeout` failures (80 and 79 tests), while the same tests split into six `--filter` slices all pass with zero failures in both runs. Failure counts are recorded, not fixed.
- Slice 4 is only 38 tests but takes ~107 s wall-clock (process and container startup dominate).
- API latency is a single-user, localhost, Development-mode stack measured with 50 sequential `curl` requests after one warm-up; each cell lists `p50 / p95` for run 1 ; run 2.
- Load time is measured with Playwright (Chromium, `mcr.microsoft.com/playwright/python:v1.48.0-jammy`) from the same host over localhost, so it excludes real network transfer time of the ~14 MB payload.
- Cold deploy build run 1 (303 s) vs run 2 (207.5 s): both ran after `docker builder prune -f`; the difference is probably pulled base layers and NuGet/network variance.

## A — result

Measured on `46d6bc3` (release/2.0.0), same VM, three full runs plus one slice run. The integration suite now takes 453.1 / 432.9 / 432.5 s in one process (median 432.9 s) against 1200.8 / 1231.2 s before: a speed-up of about 2.8x (1216 s average baseline / 432.9 s), so the 3x target was **not** met for the single-process run; it came close. The six-slice total fell from 1239-1266 s to 656.0 s (about 1.9x), because slices are dominated by per-process startup. `HttpClient.Timeout` failures went from 80 / 79 to 0 / 0 / 0 in all three runs, so the timeout flake is gone in these measurements (one run per condition is not proof it can never recur). Test count is 1806 = 1799 + 7 new infrastructure tests, in the single-process runs and in the slices. Build, Unit and Client are unchanged within noise; Integration test code shrank from 43635 to 40305 lines.

One non-timeout failure appeared in run 1: `TabelaDeNiveisConfigMigrationTests` reported a class-cleanup error (`DROP DATABASE ... WITH (FORCE)` -> Postgres 55006 "being accessed by other users"). It passed when rerun alone, and runs 2 and 3 were clean; it is a rare race in the class-database teardown, recorded and not chased in A.

The "Regeneração Bestial" name collision seen before A is not reproducible after A: 5/5 runs of the three classes involved pass.
