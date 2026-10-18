# Task 13 report
Status: DONE. Numbers, method and table are in the "After D" section of docs/superpowers/specs/2026-10-16-versao-2-0-0-baseline.md (source files: tools/bench/results/depois-d.txt, antes-d.txt).

Method: `.cs` blobs under the dirs read via git at 288fd0b and HEAD, concatenated, wc -l. Pre-D reproduces the spec's 15 813 / 40 139 exactly.

| Metric | Before | After | Target | Result |
|---|---|---|---|---|
| Controllers+Services lines | 15 813 | 13 191 | <= 11 500 | missed |
| Integration test lines | 40 139 | 34 312 | <= 34 000 | missed (312 over) |
| /api/rulebook p50 | 74.2 ms (antes-d; 69.8 in C's run) | 45.7 ms (42.5 in Task 2 run) | < 30 ms | missed |
| Integration wall time | 283.7 s / 1812 tests | 312.1 s / 2045 tests | no regression | regressed +10% (per test 0.157 -> 0.153 s) |
| build --no-incremental | 62.6 s | 63.4 s | no regression | met (0 warnings) |

Final checks: dotnet build 0 warnings; Unit 964, Client 936 (no flake), Integration 2045 all passed, 0 timeouts; verificar-baseline.sh 729dac9 exit 0; git diff ed7a447 Contracts/Client empty.
Docs: baseline spec (After D), D spec (note atop Divergências; entries unchanged), CLAUDE.md (Fichas/ + FichaStats line). Técnico.md names no per-kind controller classes: unchanged.
Concerns: three size/latency targets missed, wall time up because of more tests; not tuned.
