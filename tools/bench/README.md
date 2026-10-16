# tools/bench

Benchmark scripts for the 2.0.0 refactor. Every script takes a label as its first argument (`baseline`, `2.0.0-A`, ...), prints `key: value` lines and appends them to `tools/bench/results/<label>.txt` (git-ignored). Run from anywhere: `bash tools/bench/<script> <label>`.

Run `measure-build-and-tests.sh` and `measure-integration-slices.sh` on an otherwise idle machine.

- `measure-code-size.sh` — lines of `.cs`/`.razor` per project (migrations excluded) plus migration count and size; seconds.
- `measure-build-and-tests.sh` — `dotnet build --no-incremental` time and warning count, then Unit/Client/Integration test time, summary and HttpClient-timeout count; about 20-30 minutes, dominated by the Integration suite.
- `measure-integration-slices.sh` — the Integration classes split into 6 slices by sorted class name, each run as one `dotnet test --no-build` process; same order of magnitude as the full Integration run. Needs a prior build.
- `measure-deploy.sh` — cold (after `docker builder prune -f`) and warm (one-line Api change) `docker compose build`; a few minutes. Uses the `ruinarpg-bench` compose project. It runs `docker builder prune -f`, which wipes the host-wide build cache; `docker image rm $(docker images -q 'ruinarpg-bench-*')` reclaims the bench images afterwards.
- `measure-payload.sh` — publishes the Blazor client and measures `_framework` raw and gzip -9 size; about 1-2 minutes.
- `measure-api-latency.sh` — brings the `ruinarpg-bench` stack up on port 8088, seeds via the API, times 50 requests per endpoint (p50/p95), tears the stack down with its volumes; a few minutes.
- `measure-load.sh` — same bench stack, Playwright (Docker image) measures landing-page first render, cold and warm; a few minutes.

The stack-based scripts use `docker compose -p ruinarpg-bench` on port 8088 (see `compose.bench.yml` and `bench.env`) and never touch the user's own stack.
