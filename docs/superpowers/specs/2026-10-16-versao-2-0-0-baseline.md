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

## After B (migration squash)

Measured on `978348f` (release/2.0.0), same VM, one run.

| Area | Metric | After A | After B |
|---|---|---|---|
| Build | `dotnet build --no-incremental` (s) | 74.9 / 128.5 / 72.9 | 32.5 (0 warnings) |
| Tests | Integration, one process: time (s) | 453.1 / 432.9 / 432.5 | 289.6 |
| Tests | Integration, one process: result | 1806 passed | 1811 passed, 0 failed (of 1811) |
| Code size | migrations | 169 files, 9.6 MB | 3 files, 512K |
| Code size | test projects `.cs` lines | Unit 6695; Client 17334; Integration 40305 | Unit 6830; Client 17591; Integration 40102 |

Build: one run with warm NuGet/SDK caches, against multi-run values in the After A column; treat the gain as indicative.

`verificar-baseline.sh 729dac9`:

```
OK    schema: cadeia da 1.5.0 = baseline
OK    dump completo (migrations): cadeia da 1.5.0 = baseline
OK    dump completo (com seeders): 1.5.0 = 1.5.0 atualizada
OK    histórico: atualizado = novo
```

## After C (deploy and load performance)

Measured on `76868d1` (release/2.0.0), same VM, one run each, with the deploy-based harness of `tools/bench/`
(the stack is deployed by the ref's own `deploy.sh`). "Before" is `3040ed0` measured with the same harness.

| Area | Metric | Before C (`3040ed0`) | After C |
|---|---|---|---|
| Load | first visit, bytes on the wire (KB) | 14253 | 5834 |
| Load | repeat visit, bytes on the wire (KB) | 0 | 3 |
| Load | first render, localhost, cold / warm (s) | 3.33 / 1.33 | 2.32 / 1.34 |
| Load | first render, 20 Mbit/s + 40 ms, cold / warm (s) | 7.64 / 1.52 | 3.80 / 1.52 |
| Deploy | full `deploy.sh deploy`, cold (s) | 287.8 | 238.8 |
| Deploy | build stage, cold (s) | 233 | 192 |
| Deploy | full `deploy.sh deploy`, one-line Api change (s) | 211.3 | 137.7 |
| Deploy | build stage, one-line Api change (s) | 181 | 110 |
| Client payload | `_framework` raw / SDK `.gz` / SDK `.br` (KB) | 14192 / 5768 / 4084 | 14192 / 5768 / 4084 (unchanged; the gain is in what nginx sends) |
| API latency | `/api/rulebook` p50 / p95 through nginx (s) | 0.0718 / 0.0809 | 0.0698 / 0.0747 |
| Images | `favicon.png` / `logo-grande.png` (KB) | 593 / 638 | 533 / 588 |

Image sizes are KiB (bytes / 1024): `favicon.png` 607001 -> 545514 bytes, `logo-grande.png` 653705 -> 602119 bytes (the Before column of the original spec table, 596 / 640, used a different rounding). The other latency endpoints stayed under 20 %, except sub-10 ms ones within single-run noise, e.g. `/api/traits` 4.96 -> 6.09 ms.

Success criteria of the spec: first-visit transfer <= 7168 KB was **met** (5834 KB, -59 %). Warm build stage <= half of the before value (<= 90.5 s of 181 s) was **missed**: 110 s (-39 %); per the plan nothing was tuned to chase it. The cold full deploy still improved (287.8 -> 238.8 s) and the one-line-change deploy went from 211.3 to 137.7 s. `/api/rulebook`: p50 0.0698 s is above the 30 ms threshold, so it was profiled (temporary `Stopwatch` instrumentation, since reverted). Per request (warm, ~75 ms inside the test host) the Markdown step (`SplitIntoSections`, all documents) costs only ~12-15 ms; `BuildEquipagemAsync` alone costs ~45 ms, from per-kit database queries (an N+1 over `EquipmentKits`), not from Markdown. The Markdown memo of the plan therefore does not apply and no code was changed; the Equipagem queries are a candidate for a later round.

## After D (backend consolidation)

Measured on `76c7763` (release/2.0.0; differs from `57b4ba7` only by docs) with `tools/bench/` (`depois-d`), same VM, one run each. "Before D" is `288fd0b` (`antes-d`).

**Line-count method (same for both columns):** `git ls-files '<dir>/*.cs' | xargs cat | wc -l`, evaluated at `288fd0b` and at HEAD (by reading each tracked `.cs` blob with `git show`), for (a) `src/RuinaRPG.Api/Controllers` + `src/RuinaRPG.Api/Services` and (b) `tests/RuinaRPG.Tests.Integration`. The 15 813 / 40 139 in the D spec were computed slightly differently; re-measured with this method at `288fd0b` they come out the same (15 813 / 40 139), so before and after are comparable.

| Metric | Before D | After D | Target | Result |
|---|---|---|---|---|
| Lines in `Controllers/` + `Services/` | 15 813 | 13 191 (-16.6 %) | <= 11 500 | **missed** |
| Lines in `Tests.Integration/` | 40 139 | 34 312 (-14.5 %) | <= 34 000 | **missed** (312 lines over) |
| `/api/rulebook` p50 through nginx | 74.2 ms (`antes-d.txt`); 69.8 ms in C's run | 45.7 ms (42.5 ms in the Task 2 run, `depois-d-rulebook.txt`) | < 30 ms | **missed** |
| Integration wall time | 283.7 s (1812 tests) | 312.1 s (2045 tests) | no regression | **regressed** in total (+10 %), with +233 tests (+12.9 %); per test 0.157 -> 0.153 s |
| `dotnet build --no-incremental` | 62.6 s, 0 warnings | 63.4 s, 0 warnings | no regression | met |

Other numbers: whole `Api` project 16 318 -> 13 702 lines; Unit 964 -> 964 tests, Client 936 -> 936, Integration 1812 -> 2045 (tests per kind grew, none removed). HttpClient timeouts 0 in all three. The other API endpoints stayed within single-run noise of the before values (sub-10 ms ones +/- 1 ms; `/api/items` 18.9 -> 21.2 ms, `/api/creature-sheets/{id}` 13.1 -> 14.5 ms).

Targets were reported as measured, not tuned. The rulebook gain (74 -> 46 ms) comes from batching the Equipagem queries; where the rest goes was not profiled again in D (the profile under "After C" put the Markdown step at ~12-15 ms). The integration wall time grew because the suite now runs more cases (every shared case runs for each of the three kinds). Success criteria otherwise: build 0 warnings / 0 errors, whole suite green, `verificar-baseline.sh 729dac9` exit 0, `git diff ed7a447 -- src/RuinaRPG.Contracts src/RuinaRPG.Client` empty, route inventory and rulebook frozen output unchanged.

## After E (client consolidation)

"Before E" is `30a8fa4`, "after" is `0ebcecd` (release/2.0.0 HEAD). Both measured on the same VM; builds and client test runs in temporary `git worktree`s of each commit.

**Methods (same for both columns):** line counts read every tracked file of the set at the commit (`git ls-tree -r` + `git show`, `wc -l`): the three pages are `src/RuinaRPG.Client/Pages/FichaDe{Personagem,Npc,Criatura}.razor`; `Shared/Ficha/` is every tracked file in `src/RuinaRPG.Client/Shared/Ficha/`; client total is every `.razor` and `.cs` under `src/RuinaRPG.Client`; `Contracts` is every `.cs` under `src/RuinaRPG.Contracts`. Build: `dotnet build --no-incremental` of the whole solution, one run each. Tests: `dotnet test tests/RuinaRPG.Tests.Client --no-restore`, 3 runs each, nothing else running. `_framework`: `dotnet publish src/RuinaRPG.Client -c Release -o <tmp>` at each commit, size of `wwwroot/_framework`.

| Metric | Before E | After E | Reference | Result |
|---|---|---|---|---|
| Three sheet pages | 5 523 | 1 180 | - | -78.6 % |
| `Shared/Ficha/` | 0 | 3 421 | - | new |
| Pages + `Shared/Ficha/` | 5 523 | 4 601 (-16.7 %) | about 2 500-3 000 | **missed** (guess was too low) |
| Client `.razor` + `.cs` total | 19 986 | 19 064 (-4.6 %) | - | -922 lines |
| `Contracts` `.cs` | 1 251 | 1 105 (-11.7 %) | - | -146 lines |
| Client tests | 936 | 2 024 | - | +1 088 |
| Client test time (3 runs) | 23 / 23 / 24 s (wall 30.6 / 30.4 / 30.4 s) | 109 / 109 / 110 s (wall 116.1 / 116.1 / 116.5 s) | ~28 s (baseline table above: `Client (bUnit): time`, 27.6 / 28.7 s, 918 tests at `cc579e6`) | **regressed**: test time (`Duration`) about 4.7x (mean 23.3 -> 109.3 s), wall clock about 3.8x (mean 30.5 -> 116.2 s) |
| `dotnet build --no-incremental` | 55.7 s, 0 warnings | 29.5 s, 0 warnings | no meaningful change | not comparable (see below) |
| `_framework` (publish) | 24 635 507 B | 24 250 457 B (-1.6 %) | no meaningful change | met |

Other numbers: `_framework` without the `.gz`/`.br` copies 14 532 687 -> 14 276 687 B; `.br` files 4 182 448 -> 4 151 848 B. First after-run of the client suite had one timing failure (green in the other two runs and in two more later runs).

Notes. The pages-plus-shared number is reported as measured, not tuned: the three pages shrink by 4 343 lines, but the shared tabs carry 3 421 of them, and the tabs keep the per-kind capability branches the constraints allow. The client test count doubled because the E.1 safety net and the shared-tab tests are theories run once per kind and role; that is also why the suite takes longer: about 4.7x in test time (the `Duration` dotnet test reports, mean 23.3 -> 109.3 s) and about 3.8x in wall clock (whole `dotnet test` command, mean 30.5 -> 116.2 s); per test 0.025 s -> 0.054 s. The "Reference ~28 s" is the baseline table at the top of this doc (Client bUnit time 27.6 / 28.7 s at `cc579e6`, 918 tests), the number the E spec quoted. The build time is a single run each and the "before" run was the first build in a fresh worktree (cold restore and obj), so the 55.7 -> 29.5 s gap is not evidence of a speed-up. Download size did not change meaningfully, as expected. Whole `dotnet test` (all three projects) green: Unit 965 (964 after D, plus one test added in E Task 5: `ContractsDasFichasTests`), Client 2 024, Integration 2 045; when run in one command the Client project hit timing flakes under load from Integration running in parallel (7 failures, all green on rerun alone, twice).

After the final-review fix (2026-10-23, `ce41a46`): the client classes that assert real-time windows run in the non-parallel xunit collection "Tempo real" (`TempoRealCollection`), and `tests/RuinaRPG.Tests.Client/xunit.runner.json` caps the suite at `maxParallelThreads: 2` (the collection alone still gave 2 timing failures in 6 solo runs, both on bUnit's default 1 s `WaitForAssertion`). Client suite, 3 solo runs, nothing else running: 2 024 passed, 0 failed each; test time 3 m 15 s / 3 m 16 s / 3 m 15 s (about 195 s), wall 199.6 / 199.8 / 200.8 s. (With the collection alone: about 181 s, wall about 185 s.) That is about 1.8x the after-E test time above (109.3 s) and about 6.6x the before-E wall clock (30.5 s): the price of a green suite without retries.
