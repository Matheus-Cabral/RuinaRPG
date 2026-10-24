# tools/bench

Benchmark scripts for the 2.0.0 refactor. Every script takes `<label> [ref]` (label such as `baseline`, `2.0.0-A`, ...; `ref` defaults to `HEAD` and only matters to the stack scripts), prints `key: value` lines and appends them to `tools/bench/results/<label>.txt` (git-ignored). Run from anywhere: `bash tools/bench/<script> <label> [ref]`.

Run `measure-build-and-tests.sh` and `measure-integration-slices.sh` on an otherwise idle machine.

- `measure-code-size.sh` — lines of `.cs`/`.razor` per project (migrations excluded) plus migration count and size; seconds.
- `measure-build-and-tests.sh` — `dotnet build --no-incremental` time and warning count, then Unit/Client/Integration test time, summary and HttpClient-timeout count; about 20-30 minutes, dominated by the Integration suite.
- `measure-integration-slices.sh` — the Integration classes split into 6 slices by sorted class name, each run as one `dotnet test --no-build` process; same order of magnitude as the full Integration run. Needs a prior build.
- `measure-deploy.sh` — a full `deploy.sh deploy` of `<ref>`, cold (after `docker builder prune -af`, which wipes the host-wide build cache) and warm (one-line Api change committed only in the clone); the build stage is taken from the deploy log's timestamps. Several minutes.
- `measure-payload.sh` — publishes the Blazor client and measures `_framework` raw and gzip -9 size plus the SDK's own `.gz`/`.br` totals (publishes the working tree; takes no ref); about 1-2 minutes.
- `measure-api-latency.sh` — deploys `<ref>` into the bench stack, seeds via the API, times 50 requests per endpoint (p50/p95, any non-200 aborts), tears the stack down with its volumes; a few minutes.
- `measure-load.sh` — same bench stack, Playwright (Docker image) measures landing-page first render, cold and warm, with bytes on the wire, on localhost and on a throttled profile (20 Mbit/s, 40 ms); a few minutes.

The stack scripts deploy `<ref>` with that ref's own `deploy/deploy.sh` (in a throwaway shared clone) into the throwaway `ruinarpg-bench` compose project on `127.0.0.1:8088`, then remove it with its volumes and the images of throwaway commits. They never touch the user's own stack.
