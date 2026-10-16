# Versão 2.0.0 — 0+A: Baseline and test infrastructure — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Measure today's build/test/deploy/load numbers, then make the integration suite boot one API host per test class over a pre-migrated, pre-seeded template database instead of one host + migrate + seed per test.

**Architecture:** Benchmark scripts in `tools/bench/` run against a throwaway compose project (`ruinarpg-bench`, port 8088), never the user's stack. In the API, the duplicated migrate+seed block of `Program.cs` becomes `DatabaseInitializer`. In the tests, a shared Postgres container hosts one template database (initialized once per run through `DatabaseInitializer`), and each test class clones it (`CREATE DATABASE … TEMPLATE`) inside an `ApiFixture` that owns a single `ApiFactory` running in the `Testing` environment. Duplicated helpers move to `ApiTestBase`.

**Tech Stack:** .NET 8, xUnit 2.4, `Microsoft.AspNetCore.Mvc.Testing`, Testcontainers.PostgreSql 3.10, Npgsql/EF Core 8, bash + curl + jq, Docker Compose v5, Playwright (Python Docker image) for load timing.

**Spec:** `docs/superpowers/specs/2026-10-16-versao-2-0-0-0-a-testes-design.md` (umbrella: `docs/superpowers/specs/2026-10-16-versao-2-0-0-roadmap-design.md`).

**Refinements to the spec decided while planning** (no user decision changed):
- The high login limit for tests is set with `builder.UseSetting("RateLimiting:LoginPermitLimit", "100000")` in `ApiFactory` instead of an `appsettings.Testing.json`, so no test-only file ships inside the production image.
- Load timing measures the landing page (`/`) cold and warm. The Ficha de Personagem needs an authenticated browser session, and WASM first render is dominated by the runtime download, which `/` already captures; Ficha timing is deferred to sub-project C if C needs it.
- Template clones are serialized by a `SemaphoreSlim(1)` (cheap, and avoids Postgres "source database is being accessed by other users" races).
- During migration of the test classes, `ApiFactory` keeps a transitional `initializeDatabase` flag (default `true`) so unconverted classes keep working; Task 9 removes it.

## Global Constraints

- `dotnet build` ends with **0 warnings, 0 errors**; check with `dotnet build --no-incremental` before each commit that touches C#.
- TDD (Técnico R0011): failing test first for every unit of behavior.
- No production behavior change: Development and Production startup do exactly what they do today (same seeders, same order, same log lines).
- Benchmarks never touch the user's compose project or its volumes: always `docker compose -p ruinarpg-bench`.
- Before any full integration run: `df -h /` must show **≥ 4 GB free**; if not, `docker builder prune -f` first (safe for live containers/volumes).
- Integration failures that are `HttpClient.Timeout of 100 seconds` are the known flake: rerun **one test per process** (`dotnet test --no-build --filter "FullyQualifiedName~<Class>.<Method>"`) before calling them real.
- Commit messages in Portuguese, conventional prefix (`feat:`, `fix:`, `test:`, `chore:`, `docs:`), ending with the line `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Push after each task (`git push`).
- Do not delete or weaken any existing test assertion. Test count only changes by the new infrastructure tests.

## Review Focus

1. **A test that relied on a fresh host per test** (in-memory state such as rate-limit buckets, SignalR connections, or a `WithWebHostBuilder` override) now shares the class host — it must still pass when run alone *and* inside its full class run. Covered by running each converted batch as whole classes (Tasks 6–8).
2. **Template contents drifting from Development startup** (a seeder added to `Program.cs` later but not to the template) — impossible by construction because both call `DatabaseInitializer`; pinned by `DatabaseInitializerTests` (Task 2) and `TemplateDatabaseTests` (Task 4).
3. **Concurrent class start-up cloning the template** while another clone is running — pinned by `ApiFixtureTests.Two_fixtures_initialized_concurrently_get_independent_databases` (Task 4).
4. **The 429 tests silently passing with the raised limit** — `AuthControllerRateLimitTests` must still see 429 at the production limit of 10, and a new test proves the Testing default does not throttle (Task 3).
5. **A blank-database test class accidentally converted to `ApiFixture`** (it would then fail on "relation already exists" or test the wrong migration) — Task 4 renames those classes to `BlankDatabaseFixture` explicitly and Tasks 6–8 only touch classes that construct `new ApiFactory(_postgres.ConnectionString)`.

---

## File Structure

| File | Responsibility |
|---|---|
| `tools/bench/lib.sh` | Shared bash helpers: timing, result file, bench compose wrapper |
| `tools/bench/compose.bench.yml` | Compose override: nginx on port 8088 |
| `tools/bench/bench.env` | Non-secret env for the bench stack (Development mode) |
| `tools/bench/measure-code-size.sh` | Lines per project, `Migrations/` size |
| `tools/bench/measure-build-and-tests.sh` | Build + three test projects, timings and counts |
| `tools/bench/measure-integration-slices.sh` | Integration suite in 6 slices |
| `tools/bench/measure-deploy.sh` | Cold/warm `docker compose build` of the bench project |
| `tools/bench/measure-payload.sh` | Published `_framework` size raw/gzip, file count |
| `tools/bench/measure-api-latency.sh` | Seeds via API, p50/p95 of 10 GET endpoints |
| `tools/bench/measure-load.sh` + `tools/bench/load_time.py` | Landing-page first render, cold and warm |
| `tools/bench/README.md` | How to run each script |
| `docs/superpowers/specs/2026-10-16-versao-2-0-0-baseline.md` | Results table (baseline + 2.0.0 column) |
| `src/RuinaRPG.Api/DatabaseInitializer.cs` | Migrate + every seeder/backfill, in today's order |
| `src/RuinaRPG.Api/Program.cs` | Calls `DatabaseInitializer`; configurable login limit |
| `tests/RuinaRPG.Tests.Integration/SharedPostgres.cs` | The one Testcontainers Postgres + admin helpers (extracted from `PostgresFixture`) |
| `tests/RuinaRPG.Tests.Integration/BlankDatabaseFixture.cs` | Renamed `PostgresFixture`: empty DB per class |
| `tests/RuinaRPG.Tests.Integration/TemplateDatabase.cs` | Creates + initializes the template once per run |
| `tests/RuinaRPG.Tests.Integration/ApiFixture.cs` | Clone of the template + one `ApiFactory` + `HttpClient` per class |
| `tests/RuinaRPG.Tests.Integration/ApiTestBase.cs` | Base class with the shared helpers |
| `tests/RuinaRPG.Tests.Integration/ApiFactory.cs` | `Testing` environment, no `BuildServiceProvider`/`Migrate` |

---

### Task 1: Benchmark scripts and baseline measurement (sub-project 0)

No product code changes, so no TDD; each script is verified by running it.

**Files:**
- Create: everything under `tools/bench/` listed above
- Create: `docs/superpowers/specs/2026-10-16-versao-2-0-0-baseline.md`
- Modify: `.gitignore` (add `tools/bench/results/`)

**Interfaces:**
- Produces: scripts that each print a `key: value` block to stdout and append it to `tools/bench/results/<label>.txt`; first argument is always the label (`baseline` now, `2.0.0-A` later).

- [ ] **Step 1: Shared helpers**

`tools/bench/lib.sh`:

```bash
#!/usr/bin/env bash
# Shared helpers for tools/bench/*. Source it; do not run it.
set -euo pipefail

BENCH_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BENCH_DIR="$BENCH_ROOT/tools/bench"
RESULTS_DIR="$BENCH_DIR/results"
mkdir -p "$RESULTS_DIR"

LABEL="${1:?usage: $0 <label>}"
RESULT_FILE="$RESULTS_DIR/$LABEL.txt"

# record <key> <value> — prints and appends one result line.
record() {
  echo "$1: $2" | tee -a "$RESULT_FILE"
}

# timed <command...> — runs the command, sets ELAPSED (seconds, 1 decimal) and keeps its exit code in STATUS.
timed() {
  local start end
  start=$(date +%s.%N)
  set +e
  "$@"
  STATUS=$?
  set -e
  end=$(date +%s.%N)
  ELAPSED=$(echo "$end - $start" | bc -l | xargs printf '%.1f')
}

# bench_compose <args...> — the throwaway bench stack; never the user's own project.
bench_compose() {
  docker compose -p ruinarpg-bench --env-file "$BENCH_DIR/bench.env" \
    -f "$BENCH_ROOT/docker-compose.yml" -f "$BENCH_DIR/compose.bench.yml" "$@"
}

require_free_disk_gb() {
  local free_gb
  free_gb=$(df --output=avail -BG / | tail -1 | tr -dc '0-9')
  if (( free_gb < $1 )); then
    echo "Only ${free_gb} GB free on /; need $1 GB. Run 'docker builder prune -f' first." >&2
    exit 1
  fi
}
```

`tools/bench/compose.bench.yml`:

```yaml
# Bench override: the bench stack listens on 8088 so it never collides with the user's stack on 80.
services:
  nginx:
    ports: !override
      - "8088:80"
```

`tools/bench/bench.env` (non-secret, bench only):

```bash
ASPNETCORE_ENVIRONMENT=Development
POSTGRES_USER=bench
POSTGRES_PASSWORD=bench
JWT_SIGNING_KEY=bench-signing-key-that-is-at-least-32-bytes-long
JWT_ISSUER=RuinaRPG
JWT_AUDIENCE=RuinaRPG
JWT_ACCESS_TOKEN_MINUTES=60
JWT_REFRESH_TOKEN_DAYS=30
CORS_ALLOWED_ORIGINS=http://localhost:8088
IMG_MAX_SIZE_MB=10
INVITE_CODE_EXPIRATION_HOURS=48
```

Add `tools/bench/results/` to `.gitignore`. Run `which bc` — install nothing; if `bc` is missing, replace the `bc` line with `ELAPSED=$(awk "BEGIN{printf \"%.1f\", $end-$start}")`.

- [ ] **Step 2: Code size**

`tools/bench/measure-code-size.sh`:

```bash
#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
cd "$BENCH_ROOT"
for p in src/* tests/*; do
  [[ -d "$p" ]] || continue
  cs=$(find "$p" -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -not -path '*/Migrations/*' -print0 | xargs -0 cat 2>/dev/null | wc -l)
  razor=$(find "$p" -name '*.razor' -not -path '*/obj/*' -print0 | xargs -0 cat 2>/dev/null | wc -l)
  record "lines.$(basename "$p")" "cs=$cs razor=$razor"
done
record "migrations.files" "$(ls src/RuinaRPG.Infrastructure/Persistence/Migrations | wc -l)"
record "migrations.size" "$(du -sh src/RuinaRPG.Infrastructure/Persistence/Migrations | cut -f1)"
```

Run: `bash tools/bench/measure-code-size.sh baseline` → expect one `lines.*` line per project.

- [ ] **Step 3: Build and tests**

`tools/bench/measure-build-and-tests.sh`:

```bash
#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
cd "$BENCH_ROOT"
require_free_disk_gb 4

timed dotnet build --no-incremental -v q -clp:NoSummary > "$RESULTS_DIR/$LABEL-build.log" 2>&1
record "build.seconds" "$ELAPSED"
record "build.warnings" "$(grep -c ' warning ' "$RESULTS_DIR/$LABEL-build.log" || true)"

for project in Unit Client Integration; do
  log="$RESULTS_DIR/$LABEL-test-$project.log"
  timed dotnet test "tests/RuinaRPG.Tests.$project" --no-build > "$log" 2>&1
  record "test.$project.seconds" "$ELAPSED"
  record "test.$project.summary" "$(grep -E '(Passed|Failed)!' "$log" | tail -1 | sed 's/  */ /g')"
  record "test.$project.timeouts" "$(grep -c 'HttpClient.Timeout' "$log" || true)"
done
```

Run: `bash tools/bench/measure-build-and-tests.sh baseline` (takes a while). Expected: `build.warnings: 0`, three summaries.

- [ ] **Step 4: Integration in six slices**

`tools/bench/measure-integration-slices.sh` — splits the integration test classes into 6 groups by sorted fully-qualified class name and runs each group as one `dotnet test --no-build` process:

```bash
#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
cd "$BENCH_ROOT"
require_free_disk_gb 4
project=tests/RuinaRPG.Tests.Integration

mapfile -t classes < <(dotnet test "$project" --no-build --list-tests 2>/dev/null \
  | grep -E '^\s+RuinaRPG\.' | sed -E 's/^\s+//; s/\.[^.]+$//; s/\(.*$//' | sort -u)
total=${#classes[@]}
per=$(( (total + 5) / 6 ))
grand=0
for slice in 0 1 2 3 4 5; do
  chunk=("${classes[@]:$((slice * per)):per}")
  (( ${#chunk[@]} )) || continue
  filter=$(printf 'FullyQualifiedName~%s.|' "${chunk[@]}"); filter=${filter%|}
  log="$RESULTS_DIR/$LABEL-slice-$slice.log"
  timed dotnet test "$project" --no-build --filter "$filter" > "$log" 2>&1
  grand=$(echo "$grand + $ELAPSED" | bc -l)
  record "slice.$slice.seconds" "$ELAPSED"
  record "slice.$slice.summary" "$(grep -E '(Passed|Failed)!' "$log" | tail -1 | sed 's/  */ /g')"
done
record "slices.total.seconds" "$grand"
```

Note: `--list-tests` prints `Class.Method` (Theories may include argument text); the `sed` keeps the class part. Verify by checking that the six slice summaries add up to the full run's total test count from Step 3; if not, fix the parsing before recording.

- [ ] **Step 5: Deploy build times**

`tools/bench/measure-deploy.sh`:

```bash
#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
cd "$BENCH_ROOT"
require_free_disk_gb 6

docker builder prune -f > /dev/null
timed bench_compose build > "$RESULTS_DIR/$LABEL-deploy-cold.log" 2>&1
record "deploy.build.cold.seconds" "$ELAPSED"

# Warm: a one-line, comment-only change in an Api source file, then rebuild; reverted afterwards.
touched=src/RuinaRPG.Api/Program.cs
echo "// bench-touch" >> "$touched"
timed bench_compose build > "$RESULTS_DIR/$LABEL-deploy-warm.log" 2>&1
git checkout -- "$touched"
record "deploy.build.warm.seconds" "$ELAPSED"
```

Run it. Expected: two timings; `git status` clean afterwards.

- [ ] **Step 6: Client payload**

`tools/bench/measure-payload.sh`:

```bash
#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
cd "$BENCH_ROOT"
out=$(mktemp -d)
trap 'rm -rf "$out"' EXIT
dotnet publish src/RuinaRPG.Client/RuinaRPG.Client.csproj -c Release -o "$out" -v q > /dev/null
fw="$out/wwwroot/_framework"
# Exclude the .gz/.br siblings the SDK already emits; measure the raw files and their gzip -9 size.
mapfile -t files < <(find "$fw" -type f ! -name '*.gz' ! -name '*.br')
raw=0; gz=0
for f in "${files[@]}"; do
  raw=$(( raw + $(stat -c %s "$f") ))
  gz=$(( gz + $(gzip -9 -c "$f" | wc -c) ))
done
record "payload.framework.files" "${#files[@]}"
record "payload.framework.raw.kb" "$(( raw / 1024 ))"
record "payload.framework.gzip.kb" "$(( gz / 1024 ))"
record "payload.served.encoding" "nginx serves raw files today (no gzip/brotli configured)"
```

- [ ] **Step 7: API latency**

`tools/bench/measure-api-latency.sh` — brings the bench stack up, seeds through the public API, times 50 requests per endpoint, tears the stack down (volumes included, it is the bench project):

```bash
#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
cd "$BENCH_ROOT"
base=http://localhost:8088

bench_compose up -d --build > /dev/null
trap 'bench_compose down --volumes > /dev/null' EXIT
for _ in $(seq 1 60); do curl -fs "$base/api/health" > /dev/null && break; sleep 2; done

json() { curl -fs -H 'Content-Type: application/json' "$@"; }
token=$(json -X POST "$base/api/auth/register/gm" \
  -d '{"nickname":"BenchGm","email":"bench@bench.local","senha":"Senha!123","confirmacaoSenha":"Senha!123"}' | jq -r .accessToken)
auth=(-H "Authorization: Bearer $token")
campaign=$(json "${auth[@]}" -X POST "$base/api/campaigns" -d '{"nome":"Bench","descricao":""}' | jq -r .id)
npc=$(curl -fs "${auth[@]}" -X POST "$base/api/npc-sheets" | jq -r .id)
creature=$(curl -fs "${auth[@]}" -X POST "$base/api/creature-sheets" | jq -r .id)

endpoints=(
  "/api/auth/me" "/api/campaigns" "/api/campaigns/$campaign/members" "/api/npc-sheets/$npc"
  "/api/creature-sheets/$creature" "/api/items" "/api/spell-ability-bank" "/api/traits"
  "/api/rulebook" "/api/compendio/search?q=fogo"
)
for e in "${endpoints[@]}"; do
  curl -fs -o /dev/null "${auth[@]}" "$base$e"   # warm-up, also fails fast on a wrong route
  times=$(for _ in $(seq 1 50); do curl -s -o /dev/null -w '%{time_total}\n' "${auth[@]}" "$base$e"; done | sort -n)
  p50=$(sed -n 25p <<< "$times"); p95=$(sed -n 48p <<< "$times")
  name=$(sed -E 's#/[0-9a-f-]{36}#/{id}#g; s#\?.*##' <<< "$e")
  record "latency.$name" "p50=${p50}s p95=${p95}s"
done
```

If any route 404s during warm-up (`curl -f` exits non-zero and the script stops), fix the route by reading the controller's `[Route]`/`[HttpGet]` and rerun — do not drop the endpoint.

- [ ] **Step 8: Load time**

`tools/bench/load_time.py`:

```python
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
        page.wait_for_selector("svg.loading-progress", state="detached", timeout=120_000)
        print(f"load.{label}.seconds: {time.perf_counter() - start:.2f}")
    browser.close()
```

`tools/bench/measure-load.sh`:

```bash
#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
cd "$BENCH_ROOT"
bench_compose up -d --build > /dev/null
trap 'bench_compose down --volumes > /dev/null' EXIT
for _ in $(seq 1 60); do curl -fs http://localhost:8088/api/health > /dev/null && break; sleep 2; done
docker run --rm --network host -v "$BENCH_DIR:/bench:ro" mcr.microsoft.com/playwright/python:v1.48.0-jammy \
  bash -c "pip install -q playwright==1.48.0 && python /bench/load_time.py http://localhost:8088/" \
  | grep '^load\.' | while IFS=': ' read -r k v; do record "$k" "$v"; done
```

- [ ] **Step 9: README and baseline document**

`tools/bench/README.md`: one line per script (what it measures, how long it takes, that it uses the `ruinarpg-bench` project on port 8088 and never the user's stack), plus "run `measure-build-and-tests.sh` and `measure-integration-slices.sh` on an otherwise idle machine".

Run all seven scripts with label `baseline` (2× for every timing script; keep both runs). Then write `docs/superpowers/specs/2026-10-16-versao-2-0-0-baseline.md`:

```markdown
# Versão 2.0.0 — Baseline measurements

Measured on `<commit>` (code identical to `main` 729dac9), this VM (4 cores, 15 GB RAM), <date>.
Scripts: `tools/bench/`. Each timing run twice; both values shown.

| Area | Metric | Baseline (run 1 / run 2) | After A | 2.0.0 |
|---|---|---|---|---|
| Build | `dotnet build --no-incremental` | … | | |
| … one row per recorded key … |
```

Fill every row from `tools/bench/results/baseline.txt`; leave the "After A" and "2.0.0" columns empty.

- [ ] **Step 10: Commit**

```bash
git add tools/bench .gitignore docs/superpowers/specs/2026-10-16-versao-2-0-0-baseline.md
git commit -m "chore: scripts de benchmark e medição baseline da 2.0.0

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push
```

---

### Task 2: `DatabaseInitializer`

**Files:**
- Create: `src/RuinaRPG.Api/DatabaseInitializer.cs`
- Modify: `src/RuinaRPG.Api/Program.cs` (the `--migrate` block, lines ~226-266, and the `IsDevelopment()` block, lines ~312-350)
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/DatabaseInitializerTests.cs`

**Interfaces:**
- Produces: `public static class RuinaRPG.Api.DatabaseInitializer { public static Task InitializeAsync(IServiceProvider services, ILogger logger); }` — `services` is a root provider; the method creates its own scope and needs `RuinaRpgDbContext`, `LivroDeRegrasEfeitosSync` and `IRulesDataProvider` registered.

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RuinaRPG.Api;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class DatabaseInitializerTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public DatabaseInitializerTests(PostgresFixture postgres) => _postgres = postgres;

    private ServiceProvider BuildServices() => new ServiceCollection()
        .AddDbContext<RuinaRpgDbContext>(o => o.UseNpgsql(_postgres.ConnectionString))
        .AddScoped<LivroDeRegrasEfeitosSync>()
        .AddSingleton<IRulesDataProvider, RulesDataProvider>()
        .BuildServiceProvider();

    private async Task<Dictionary<string, int>> CountSeededTablesAsync(ServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        return new Dictionary<string, int>
        {
            ["Traits"] = await db.Traits.CountAsync(),
            ["Historicos"] = await db.Historicos.CountAsync(),
            ["Efeitos"] = await db.Efeitos.CountAsync(),
            ["EquipmentKits"] = await db.EquipmentKits.CountAsync(),
            ["DurabilidadesPorRank"] = await db.DurabilidadesPorRank.CountAsync(),
            ["TabelaDeAfinidades"] = await db.TabelaDeAfinidades.CountAsync(),
            ["NiveisProgressao"] = await db.NiveisProgressao.CountAsync(),
        };
    }

    [Fact]
    public async Task Initializing_a_blank_database_migrates_it_and_runs_every_seeder()
    {
        await using var services = BuildServices();

        await DatabaseInitializer.InitializeAsync(services, NullLogger.Instance);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await CountSeededTablesAsync(services)).Values.Should().OnlyContain(count => count > 0);
    }

    [Fact]
    public async Task Initializing_twice_is_idempotent()
    {
        await using var services = BuildServices();
        await DatabaseInitializer.InitializeAsync(services, NullLogger.Instance);
        var first = await CountSeededTablesAsync(services);

        await DatabaseInitializer.InitializeAsync(services, NullLogger.Instance);

        (await CountSeededTablesAsync(services)).Should().Equal(first);
    }
}
```

The `DbSet` names above are guesses from the seeder names: open `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs` and use the real property names for the tables each seeder fills (`TraitSeeder`, `HistoricoSeeder`, `EfeitoSeeder`, `EquipmentKitSeeder`, `DurabilidadePorRankSeeder`, `TabelaDeAfinidadesSeeder`, `TabelaDeNiveisSeeder`). Both tests share the class database, which is fine: the second test runs the initializer on an already-initialized DB, which is exactly what idempotency means.

- [ ] **Step 2: Run it and verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~DatabaseInitializerTests"`
Expected: build error `The name 'DatabaseInitializer' does not exist`.

- [ ] **Step 3: Implement**

`src/RuinaRPG.Api/DatabaseInitializer.cs` — the body is today's `--migrate` block moved verbatim, with `migrate*` prefixes dropped, `app.Logger` → `logger`, and `IRulesDataProvider` taken from DI (Development already did that; `--migrate` used `new RulesDataProvider()`, which is the same singleton type):

```csharp
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api;

/// Applies pending migrations and runs every seeder/backfill, in order. The one place that knows the
/// sequence: `--migrate` (Production, Técnico R0009), Development startup and the integration-test
/// template database all call it.
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        await db.Database.MigrateAsync();

        var traitSeed = await TraitSeeder.SeedAsync(db, RulesDataProvider.ReadResource("Caracteristicas.md"));
        logger.LogInformation("Trait seed: {InsertedCount} new row(s) inserted, {UpdatedCount} existing row(s) updated", traitSeed.Inserted, traitSeed.Updated);

        var historicoSeed = await HistoricoSeeder.SeedAsync(db, RulesDataProvider.ReadResource("Historico.md"));
        logger.LogInformation("Historico seed: {InsertedCount} new row(s) inserted", historicoSeed.Inserted);

        await EfeitoSeeder.SeedAsync(db);
        logger.LogInformation("Efeito seed: catalog synced");

        var livroEfeitos = await scope.ServiceProvider.GetRequiredService<LivroDeRegrasEfeitosSync>().SincronizarFaltantesAsync();
        logger.LogInformation("Livro de Regras (Graus & Círculos): {InsertedCount} Efeito block(s) added", livroEfeitos);

        var catalogSeed = await DefaultCatalogSeeder.SeedMissingAsync(db);
        logger.LogInformation("Default catalog seed: {SeededGmCount} GM(s) with an empty catalog seeded", catalogSeed);

        var kitSeed = await EquipmentKitSeeder.SeedAsync(db);
        logger.LogInformation("EquipmentKit seed: {InsertedCount} new kit(s) inserted", kitSeed);

        var fixedItemBackfill = await EquipmentKitFixedItemBackfill.RunAsync(db);
        logger.LogInformation("EquipmentKit fixed-item backfill: {CreatedCount} fixed item(s) created", fixedItemBackfill);

        var durabilidadeSeed = await DurabilidadePorRankSeeder.SeedAsync(db, RulesDataProvider.ReadResource("Tabela de Durabilidade por Rank.md"));
        logger.LogInformation("DurabilidadePorRank seed: {InsertedCount} new row(s) inserted", durabilidadeSeed);

        var afinidadesSeed = await TabelaDeAfinidadesSeeder.SeedAsync(db, RulesDataProvider.ReadResource("Tabela de Afinidades.md"));
        logger.LogInformation("TabelaDeAfinidades seed: {InsertedCount} new row(s) inserted", afinidadesSeed);

        var rules = scope.ServiceProvider.GetRequiredService<IRulesDataProvider>();
        var niveisSeed = await TabelaDeNiveisSeeder.SeedAsync(db, RulesDataProvider.ReadResource("Tabela de Níveis.md"), rules.XpPorNivel, rules.EapPorNivel);
        logger.LogInformation("TabelaDeNiveis seed: {InsertedCount} new row(s)/column(s) inserted", niveisSeed);
    }
}
```

Fix the `using`s to whatever namespaces the seeders actually live in (copy them from `Program.cs`'s header). In `Program.cs`, replace the whole `--migrate` block body with:

```csharp
if (args.Contains("--migrate"))
{
    await DatabaseInitializer.InitializeAsync(app.Services, app.Logger);
    return;
}
```

and the whole `IsDevelopment()` seeding block with:

```csharp
if (app.Environment.IsDevelopment())
    await DatabaseInitializer.InitializeAsync(app.Services, app.Logger);
```

Remove `using`s in `Program.cs` that become unused (the build will warn about none — C# does not warn on unused usings by default — but remove them anyway if nothing else in the file uses them).

- [ ] **Step 4: Verify**

Run: `dotnet build --no-incremental` → 0 warnings. Then `dotnet test tests/RuinaRPG.Tests.Integration --no-build --filter "FullyQualifiedName~DatabaseInitializerTests|FullyQualifiedName~TabelaDeNiveisSeederTests|FullyQualifiedName~RulesAuditorCliTests|FullyQualifiedName~GmPasswordResetCliTests|FullyQualifiedName~TraitsControllerTests"` → all pass.

- [ ] **Step 5: Commit and push**

```bash
git add src/RuinaRPG.Api tests/RuinaRPG.Tests.Integration/Persistence/DatabaseInitializerTests.cs
git commit -m "refactor: DatabaseInitializer substitui os dois blocos duplicados de migrate + seed do Program.cs

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push
```

---

### Task 3: `Testing` environment and configurable login limit

**Files:**
- Modify: `src/RuinaRPG.Api/Program.cs:181-192` (rate limiter)
- Modify: `tests/RuinaRPG.Tests.Integration/ApiFactory.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/AuthControllerRateLimitTests.cs`

**Interfaces:**
- Consumes: `DatabaseInitializer.InitializeAsync(IServiceProvider, ILogger)` (Task 2).
- Produces: `public class ApiFactory(string connectionString, bool initializeDatabase = true) : WebApplicationFactory<Program>` — environment `Testing`; when `initializeDatabase` is true, runs `DatabaseInitializer` once right after the host starts (transitional; Task 9 removes the parameter). Config key `RateLimiting:LoginPermitLimit` (int, default 10).

- [ ] **Step 1: Write the failing test** — in `AuthControllerRateLimitTests`, make the two existing tests run against the production limit, then add a test for the Testing default.

The two existing tests assert production's limit of 10, and `ApiFactory` is about to raise the limit for every class. So `InitializeAsync` builds the client from a derived host with the production value (this class keeps a fresh host per test on purpose — rate-limit buckets are per host):

```csharp
    private ApiFactory _factory = null!;
    private WebApplicationFactory<Program> _productionLimitFactory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        // Production's 10 logins/min (Técnico R0006); ApiFactory raises it for every other class.
        _productionLimitFactory = _factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:LoginPermitLimit", "10"));
        _client = _productionLimitFactory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _productionLimitFactory.DisposeAsync();
        await _factory.DisposeAsync();
    }
```

(Add `using Microsoft.AspNetCore.Mvc.Testing;` and `using Microsoft.AspNetCore.Hosting;`.) This class is **not** converted to `ApiFixture` in Tasks 6–8 — exclude it from the batch lists. Then add the new test, using the plain `_factory`:

```csharp
    [Fact]
    public async Task Testing_environment_does_not_throttle_logins()
    {
        using var client = _factory.CreateClient();
        var request = new LoginRequest("NoThrottleTest", "WrongPassword!");
        HttpResponseMessage? lastResponse = null;

        for (var i = 0; i < 15; i++)
            lastResponse = await client.PostAsJsonAsync("/api/auth/login", request);

        lastResponse!.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }
```

- [ ] **Step 2: Run and verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~AuthControllerRateLimitTests"`
Expected: `Testing_environment_does_not_throttle_logins` FAILS (429), the other two pass.

- [ ] **Step 3: Implement**

`Program.cs` rate limiter:

```csharp
// Production and Development keep 10/min (Técnico R0006); the integration tests raise it, since one
// host now serves a whole test class.
var loginPermitLimit = builder.Configuration.GetValue("RateLimiting:LoginPermitLimit", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = loginPermitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});
```

`ApiFactory.cs` (whole file):

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using RuinaRPG.Api;
using RuinaRPG.Infrastructure.Auth;
using RuinaRPG.Infrastructure.Images;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SheetLogs;

namespace RuinaRPG.Tests.Integration;

// "Testing" is neither Development nor Production: Program.cs skips its startup migrate/seed, so the
// database must already be initialized (a clone of the template, see ApiFixture) — or, while
// initializeDatabase is true, this factory initializes it once after the host starts.
public class ApiFactory(string connectionString, bool initializeDatabase = true) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // One host serves a whole test class; production's 10 logins/min would throttle it.
        builder.UseSetting("RateLimiting:LoginPermitLimit", "100000");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RuinaRpgDbContext>>();
            // Mesmo registro do Program.cs, só com outra connection string: sem o interceptor aqui, o log de
            // ficha não seria gravado em nenhum teste.
            services.AddDbContext<RuinaRpgDbContext>((sp, options) => options
                .UseNpgsql(connectionString)
                .AddInterceptors(sp.GetRequiredService<SheetLogInterceptor>()));

            // Jwt options are env-var-only in prod (docker-compose.yml's Jwt__* mappings); supply
            // non-secret test values, as JwtTokenServiceTests does.
            services.Configure<JwtOptions>(options =>
            {
                options.SigningKey = "integration-test-signing-key-needs-32-bytes-minimum";
                options.Issuer = "RuinaRPG.Tests";
                options.Audience = "RuinaRPG.Tests";
            });

            // Program.cs falls back to "/images", a container-only path; use a temp directory.
            services.Configure<ImageStorageOptions>(options =>
            {
                options.ImagesPath = Path.Combine(Path.GetTempPath(), "ruinarpg-test-images");
                options.MaxSizeMb = 10;
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        if (initializeDatabase)
            DatabaseInitializer.InitializeAsync(host.Services, NullLogger.Instance).GetAwaiter().GetResult();
        return host;
    }
}
```

`Swagger` is only mapped in Development; check `grep -rn "swagger" tests/` returns nothing before relying on that.

- [ ] **Step 4: Verify**

`dotnet build --no-incremental` → 0 warnings. Run `AuthControllerRateLimitTests` → 3 pass. Then run one slice that covers most patterns:
`dotnet test tests/RuinaRPG.Tests.Integration --no-build --filter "FullyQualifiedName~.Hubs.|FullyQualifiedName~.SheetLogs.|FullyQualifiedName~CharacterSheetsControllerTests|FullyQualifiedName~TabelaDeNiveisSeederTests|FullyQualifiedName~CampaignsControllerTests"` → all pass (timeouts: rerun singly per Global Constraints).

- [ ] **Step 5: Commit and push**

```bash
git add src/RuinaRPG.Api/Program.cs tests/RuinaRPG.Tests.Integration
git commit -m "test: ambiente Testing nos testes de integração e limite de login configurável

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push
```

---

### Task 4: Shared Postgres, template database, `ApiFixture`, `BlankDatabaseFixture`

**Files:**
- Create: `tests/RuinaRPG.Tests.Integration/SharedPostgres.cs`
- Create: `tests/RuinaRPG.Tests.Integration/TemplateDatabase.cs`
- Create: `tests/RuinaRPG.Tests.Integration/ApiFixture.cs`
- Rename: `PostgresFixture.cs` → `BlankDatabaseFixture.cs` (class too), `PostgresFixtureTests.cs` → `BlankDatabaseFixtureTests.cs`; update all 158 `IClassFixture<PostgresFixture>`/constructor references by `sed`
- Test: `tests/RuinaRPG.Tests.Integration/ApiFixtureTests.cs`, `tests/RuinaRPG.Tests.Integration/TemplateDatabaseTests.cs`

**Interfaces:**
- Consumes: `ApiFactory(string connectionString, bool initializeDatabase = true)` (Task 3).
- Produces:
  - `static class SharedPostgres { Task<string> ServerConnectionStringAsync(); Task ExecuteAdminAsync(string sql); string DatabaseConnectionString(string server, string database); Task DropDatabaseAsync(string database, string? connectionString); }`
  - `static class TemplateDatabase { const string Name = "ruinarpg_template"; Task EnsureCreatedAsync(); }`
  - `class ApiFixture : IAsyncLifetime { string ConnectionString; ApiFactory Factory; HttpClient Client; }`
  - `class BlankDatabaseFixture : IAsyncLifetime { string ConnectionString; }` (behavior identical to today's `PostgresFixture`).

- [ ] **Step 1: Mechanical rename first (no behavior change)**

```bash
cd tests/RuinaRPG.Tests.Integration
git mv PostgresFixture.cs BlankDatabaseFixture.cs
git mv PostgresFixtureTests.cs BlankDatabaseFixtureTests.cs
grep -rl "PostgresFixture" --include=*.cs . | xargs sed -i 's/\bPostgresFixture\b/BlankDatabaseFixture/g; s/\bPostgresFixtureTests\b/BlankDatabaseFixtureTests/g'
cd ../..
dotnet build --no-incremental
```

Expected: 0 warnings. Commit: `chore: PostgresFixture vira BlankDatabaseFixture`.

- [ ] **Step 2: Write the failing tests**

`TemplateDatabaseTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration;

public class TemplateDatabaseTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _fixture;

    public TemplateDatabaseTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A_clone_has_every_migration_and_the_seeded_rules_data()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();

        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await db.Traits.CountAsync()).Should().BeGreaterThan(0);
        (await db.NiveisProgressao.CountAsync()).Should().BeGreaterThan(0);
    }
}
```

(Use the same real `DbSet` names chosen in Task 2.)

`ApiFixtureTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using Npgsql;

namespace RuinaRPG.Tests.Integration;

public class ApiFixtureTests
{
    [Fact]
    public async Task Two_fixtures_initialized_concurrently_get_independent_databases()
    {
        var first = new ApiFixture();
        var second = new ApiFixture();
        try
        {
            await Task.WhenAll(first.InitializeAsync(), second.InitializeAsync());

            new NpgsqlConnectionStringBuilder(first.ConnectionString).Database
                .Should().NotBe(new NpgsqlConnectionStringBuilder(second.ConnectionString).Database);
            new NpgsqlConnectionStringBuilder(first.ConnectionString).MaxPoolSize.Should().Be(8);

            await using (var a = new NpgsqlConnection(first.ConnectionString))
            {
                await a.OpenAsync();
                await new NpgsqlCommand("CREATE TABLE only_in_first (id int)", a).ExecuteNonQueryAsync();
            }
            await using (var b = new NpgsqlConnection(second.ConnectionString))
            {
                await b.OpenAsync();
                var exists = await new NpgsqlCommand(
                    "SELECT to_regclass('only_in_first') IS NOT NULL", b).ExecuteScalarAsync();
                exists.Should().Be(false);
            }
        }
        finally
        {
            await first.DisposeAsync();
            await second.DisposeAsync();
        }
    }

    [Fact]
    public async Task The_client_reaches_the_api_backed_by_the_clone()
    {
        var fixture = new ApiFixture();
        try
        {
            await fixture.InitializeAsync();
            var response = await fixture.Client.GetAsync("/api/health");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var rulebook = await fixture.Client.GetAsync("/api/rulebook");
            rulebook.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }
}
```

- [ ] **Step 3: Run and verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~ApiFixtureTests|FullyQualifiedName~TemplateDatabaseTests"` → build error `ApiFixture` not found.

- [ ] **Step 4: Implement**

`SharedPostgres.cs` — move the container `Lazy` and the admin-connection logic out of `BlankDatabaseFixture`, keeping its comments (they record the 2026-09-18 lessons):

```csharp
using Npgsql;
using Testcontainers.PostgreSql;

namespace RuinaRPG.Tests.Integration;

// The one Postgres container of a test run. Booting one per test class overwhelmed this host's Docker
// daemon — see docs/superpowers/specs/2026-09-18-shared-postgres-testcontainer-design.md. Every fixture
// gets its own database inside it instead.
public static class SharedPostgres
{
    private static readonly Lazy<Task<PostgreSqlContainer>> Container = new(async () =>
    {
        var container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("ruinarpg")
            .WithUsername("test")
            .WithPassword("test")
            .WithCommand("-c", "max_connections=500")
            .Build();
        await container.StartAsync();
        return container;
    });

    public static async Task<string> ServerConnectionStringAsync() => (await Container.Value).GetConnectionString();

    // Admin connections must not be pooled: a pooled connection returned by `await using` stays open
    // against the shared server's connection budget for the rest of the run.
    public static async Task ExecuteAdminAsync(string sql)
    {
        var admin = new NpgsqlConnectionStringBuilder(await ServerConnectionStringAsync()) { Pooling = false }.ConnectionString;
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    // Each class's pool is bounded so ~150 classes can't each open Npgsql's default 100 connections.
    public static string DatabaseConnectionString(string server, string database) =>
        new NpgsqlConnectionStringBuilder(server) { Database = database, MaxPoolSize = 8, MinPoolSize = 0 }.ConnectionString;

    public static async Task DropDatabaseAsync(string database, string? connectionString)
    {
        if (connectionString is not null)
            NpgsqlConnection.ClearPool(new NpgsqlConnection(connectionString));
        await ExecuteAdminAsync($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)");
    }
}
```

`BlankDatabaseFixture.cs` becomes:

```csharp
namespace RuinaRPG.Tests.Integration;

// An empty database (no migrations) for classes that migrate it themselves — the CLI tests and the
// migration tests. API tests use ApiFixture instead.
public class BlankDatabaseFixture : IAsyncLifetime
{
    private readonly string _databaseName = $"test_{Guid.NewGuid():N}";

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await SharedPostgres.ExecuteAdminAsync($"CREATE DATABASE \"{_databaseName}\"");
        ConnectionString = SharedPostgres.DatabaseConnectionString(await SharedPostgres.ServerConnectionStringAsync(), _databaseName);
    }

    public Task DisposeAsync() => SharedPostgres.DropDatabaseAsync(_databaseName, ConnectionString);
}
```

`BlankDatabaseFixtureTests` must keep passing unchanged (it asserts `MaxPoolSize == 8` and isolation).

`TemplateDatabase.cs`:

```csharp
using Npgsql;

namespace RuinaRPG.Tests.Integration;

// Migrated and seeded once per test run (through the API's own DatabaseInitializer, via ApiFactory), then
// cloned by every ApiFixture — so no class pays for 169 migrations and seven seeders again.
public static class TemplateDatabase
{
    public const string Name = "ruinarpg_template";

    private static readonly Lazy<Task> Created = new(CreateAsync);
    private static readonly SemaphoreSlim CloneLock = new(1, 1);

    public static Task EnsureCreatedAsync() => Created.Value;

    public static async Task CloneAsync(string databaseName)
    {
        await EnsureCreatedAsync();
        // A template can't be copied while anyone, including another CREATE DATABASE, is using it.
        await CloneLock.WaitAsync();
        try
        {
            await SharedPostgres.ExecuteAdminAsync($"CREATE DATABASE \"{databaseName}\" TEMPLATE \"{Name}\"");
        }
        finally
        {
            CloneLock.Release();
        }
    }

    private static async Task CreateAsync()
    {
        await SharedPostgres.ExecuteAdminAsync($"CREATE DATABASE \"{Name}\"");
        var connectionString = SharedPostgres.DatabaseConnectionString(await SharedPostgres.ServerConnectionStringAsync(), Name);

        await using (var factory = new ApiFactory(connectionString, initializeDatabase: true))
            _ = factory.Services; // starting the host runs DatabaseInitializer

        NpgsqlConnection.ClearPool(new NpgsqlConnection(connectionString));
        await SharedPostgres.ExecuteAdminAsync($"ALTER DATABASE \"{Name}\" WITH IS_TEMPLATE true");
    }
}
```

`ApiFixture.cs`:

```csharp
namespace RuinaRPG.Tests.Integration;

// One clone of the template database and one API host for a whole test class. Tests in a class already
// shared one database; now they also share the host.
public class ApiFixture : IAsyncLifetime
{
    private readonly string _databaseName = $"test_{Guid.NewGuid():N}";

    public string ConnectionString { get; private set; } = null!;
    public ApiFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await TemplateDatabase.CloneAsync(_databaseName);
        ConnectionString = SharedPostgres.DatabaseConnectionString(await SharedPostgres.ServerConnectionStringAsync(), _databaseName);
        Factory = new ApiFactory(ConnectionString, initializeDatabase: false);
        Client = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (Factory is not null)
            await Factory.DisposeAsync();
        await SharedPostgres.DropDatabaseAsync(_databaseName, ConnectionString);
    }
}
```

- [ ] **Step 5: Verify**

`dotnet build --no-incremental` → 0 warnings. Run `ApiFixtureTests|TemplateDatabaseTests|BlankDatabaseFixtureTests|DatabaseInitializerTests|.Persistence.` → all pass.

- [ ] **Step 6: Commit and push**

```bash
git add tests/RuinaRPG.Tests.Integration
git commit -m "test: banco-template migrado uma vez por execução e ApiFixture com um host por classe

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push
```

---

### Task 5: `ApiTestBase` with the shared helpers

**Files:**
- Create: `tests/RuinaRPG.Tests.Integration/ApiTestBase.cs`
- Test: `tests/RuinaRPG.Tests.Integration/ApiTestBaseTests.cs`

**Interfaces:**
- Consumes: `ApiFixture` (Task 4).
- Produces (all `protected`, on `public abstract class ApiTestBase : IClassFixture<ApiFixture>`):
  - ctor `ApiTestBase(ApiFixture fixture)`; properties `ApiFixture Fixture`, `ApiFactory Factory`, `HttpClient Client`
  - `static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)`
  - `Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)`
  - `Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)`
  - `Task GrantRulesAuditorAsync(string email)`
  - `Task<string> CreateCampaignAsync(string gmToken, string nome)`

These five are the helpers with ≥ 14 identical copies (79, 69, 40, 17, 16 copies). Others stay where they are.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Net;
using FluentAssertions;

namespace RuinaRPG.Tests.Integration;

public class ApiTestBaseTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Helpers_register_a_gm_a_linked_player_and_a_campaign()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("BaseGm", "basegm@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "BasePlayer", "baseplayer@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Base");
        await GrantRulesAuditorAsync("basegm@teste.com");

        playerId.Should().NotBeNullOrEmpty();
        campaignId.Should().NotBeNullOrEmpty();
        var me = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", playerToken));
        me.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

- [ ] **Step 2: Run, expect build failure** (`ApiTestBase` not found).

- [ ] **Step 3: Implement** — bodies copied from the most common existing copies (e.g. `Hubs/EncounterHubTests.cs:42-74`):

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration;

// Base for API test classes: the class's ApiFixture plus the helpers that used to be copied into
// dozens of classes. Helpers used by only a few classes stay in those classes.
public abstract class ApiTestBase(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    protected ApiFixture Fixture { get; } = fixture;
    protected ApiFactory Factory => Fixture.Factory;
    protected HttpClient Client => Fixture.Client;

    protected static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    protected async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    protected async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<InviteCodeResponse>())!.Code;

        var response = await Client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;

        var me = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", tokens.AccessToken));
        return ((await me.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
    }

    protected async Task GrantRulesAuditorAsync(string email)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
    }

    protected async Task<string> CreateCampaignAsync(string gmToken, string nome)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        return (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
    }
}
```

Check the `InviteCodeResponse`/`RegisterJogadorRequest` namespaces with `grep -rn "record InviteCodeResponse\|record RegisterJogadorRequest" src/RuinaRPG.Contracts`.

- [ ] **Step 4: Verify** — build 0 warnings; `ApiTestBaseTests` passes.

- [ ] **Step 5: Commit and push** — `test: ApiTestBase com os helpers repetidos nas classes de teste`.

---

### Tasks 6, 7, 8: Convert the API test classes (three batches)

The ~101 classes containing `_factory = new ApiFactory(_postgres.ConnectionString);` — after Task 4's rename the text is `_factory = new ApiFactory(_postgres.ConnectionString);` with `_postgres` typed `BlankDatabaseFixture`. List them sorted:

```bash
grep -rl "_factory = new ApiFactory(_postgres.ConnectionString);" --include=*.cs tests/RuinaRPG.Tests.Integration | sort
```

- **Task 6:** entries 1–34 of that list (starts at `Condicoes/…`, ends around `Controllers/Creature…`).
- **Task 7:** entries 35–68.
- **Task 8:** entries 69–end.

Record the exact file list of the batch in the task's first commit message body, so the reviewer can check completeness.

**Interfaces:**
- Consumes: `ApiTestBase` and its members (Task 5), `ApiFixture` (Task 4).

Each batch is a refactor of tests, so the "failing test" step is replaced by "the batch passes before and after":

- [ ] **Step 1: Run the batch before converting, record the result**

Build a filter from the batch's class names (`FullyQualifiedName~.<ClassName>.` joined by `|`) and run `dotnet test tests/RuinaRPG.Tests.Integration --no-build --filter "<filter>"`. Record passed/failed counts (rerun timeouts singly). This is the reference.

- [ ] **Step 2: Convert each class** with this exact shape. Before:

```csharp
public class XControllerTests : IClassFixture<BlankDatabaseFixture>, IAsyncLifetime
{
    private readonly BlankDatabaseFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public XControllerTests(BlankDatabaseFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }
```

After:

```csharp
public class XControllerTests(ApiFixture fixture) : ApiTestBase(fixture)
{
```

Then in the class body:
- `_client` → `Client`, `_factory` → `Factory`, `_postgres.ConnectionString` → `Fixture.ConnectionString`.
- Delete local copies of `AuthedRequest`, `RegisterGmAndGetTokenAsync`, `RegisterJogadorLinkedToAsync`, `GrantRulesAuditorAsync`, `CreateCampaignAsync` **only when their signature and behavior are identical** to `ApiTestBase`'s (whitespace, fully-qualified type names and `static` don't matter).
- A local helper with the **same name but different signature or behavior** (e.g. `RegisterJogadorLinkedToAsync(string gmToken, string nickname)` returning `(Id, Token)`, or `GrantRulesAuditorAsync` returning `Task<string>`) is kept and **renamed** with a suffix describing the difference (e.g. `RegisterJogadorLinkedWithDefaultEmailAsync`), updating its call sites — never use `new` to hide a base member (CS0108 must not appear).
- If a class has other `IAsyncLifetime` work besides creating the factory, keep `IAsyncLifetime` and the extra work.
- If a class also used `IClassFixture` for something else, keep that fixture.
- Remove `using`s that become unused only if trivially obvious; do not hunt.

- [ ] **Step 3: Build** — `dotnet build --no-incremental` → 0 warnings, 0 errors.

- [ ] **Step 4: Run the batch again** with the same filter. Expected: same passed count as Step 1. Any new failure that is not a 100 s timeout means the test depended on a fresh host: run it alone (`--filter "FullyQualifiedName~<Class>.<Method>"`). If it passes alone but fails in the class run, find the shared in-memory state and either give that test its own host (`await using var factory = Factory.WithWebHostBuilder(_ => { }); using var client = factory.CreateClient();`) or make it use unique data; note the case in the commit message.

- [ ] **Step 5: Commit and push** — `test: classes de integração <lote N> usam ApiFixture/ApiTestBase` with the file list in the body.

---

### Task 9: Remove the transitional flag; fix the stragglers

**Files:**
- Modify: `tests/RuinaRPG.Tests.Integration/ApiFactory.cs`, `TemplateDatabase.cs`, `ApiFixture.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Rules/TabelaDeNiveisSeederTests.cs:90-99` (`Api_host_starts_with_the_table_seeded` builds `new ApiFactory(_fixture.ConnectionString)` on a blank DB)
- Modify: any remaining `new ApiFactory(` call sites

**Interfaces:**
- Produces: final `public class ApiFactory(string connectionString) : WebApplicationFactory<Program>` with no `CreateHost` override; `TemplateDatabase` initializes explicitly.

- [ ] **Step 1: Inventory** — `grep -rn "new ApiFactory(" tests/` must show only `ApiFixture.cs`, `TemplateDatabase.cs`, `TabelaDeNiveisSeederTests.cs` and `AuthControllerRateLimitTests.cs` (kept on purpose, see Task 3; it needs no change here because `ApiFactory`'s second parameter had a default — just drop nothing from its call). Anything else is a missed class: convert it as in Tasks 6–8.

- [ ] **Step 2: Rewrite the straggler test so it still asserts its intent** — "a host started on a database initialized the way production does it serves the seeded table":

```csharp
    [Fact]
    public async Task Api_host_starts_with_the_table_seeded()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        await DatabaseInitializer.InitializeAsync(factory.Services, NullLogger.Instance);
        using var scope = factory.Services.CreateScope();

        var tabela = await scope.ServiceProvider.GetRequiredService<ITabelaDeNiveis>().ObterAsync();

        tabela.UltimoNivel.Should().Be(50);
    }
```

- [ ] **Step 3: Remove the flag** — delete the `initializeDatabase` parameter and the `CreateHost` override from `ApiFactory`; in `ApiFixture` use `new ApiFactory(ConnectionString)`; in `TemplateDatabase.CreateAsync`:

```csharp
        await using (var factory = new ApiFactory(connectionString))
            await DatabaseInitializer.InitializeAsync(factory.Services, NullLogger.Instance);
```

Update the `ApiFactory` class comment accordingly (drop the "while initializeDatabase is true" clause).

- [ ] **Step 4: Verify** — build 0 warnings (`--no-incremental`); run `TabelaDeNiveisSeederTests|ApiFixtureTests|TemplateDatabaseTests|AuthControllerRateLimitTests` → pass. Then the full integration suite **in six slices** (`bash tools/bench/measure-integration-slices.sh after-A-check`) → every slice passes (timeouts rerun singly).

- [ ] **Step 5: Commit and push** — `test: ApiFactory sem inicialização implícita do banco`.

---

### Task 10: The "Regeneração Bestial" collision

**Files:**
- Modify: whichever of `Controllers/CampaignGrantsControllerTests.cs`, `Controllers/CreatureExclusiveTraitsControllerTests.cs`, `Controllers/CreatureAffectionsAndTraitsControllerTests.cs` the reproduction implicates

- [ ] **Step 1: Reproduce** — run the three classes together 5 times:
`for i in 1 2 3 4 5; do dotnet test tests/RuinaRPG.Tests.Integration --no-build --filter "FullyQualifiedName~CampaignGrantsControllerTests|FullyQualifiedName~CreatureExclusiveTraitsControllerTests|FullyQualifiedName~CreatureAffectionsAndTraitsControllerTests" | grep -E "Passed!|Failed!"; done`
Look for `Grant_from_an_existing_Creature_deep_copies_traits_from_both_catalogs` failing fast with "Positivas contain 1 item instead of 2".

- [ ] **Step 2a: If it never fails in 5 runs** — the per-class database (already in place) plus the per-class host removed the collision path. Still make the inserted name unique in the three classes (`$"Regeneração Bestial {Guid.NewGuid():N}"[..40]` or whatever length limit the `Nome` column has — check `RuinaRpgDbContext`) only if the test asserts by name lookup within its own database; otherwise leave the tests untouched and record "not reproducible after A" in the commit/notes. Do not change assertions.

- [ ] **Step 2b: If it fails** — the failing run shows which state is shared. Since each class has its own database, the shared state must be inside a single class (two tests of `CampaignGrantsControllerTests` inserting the same trait into its one DB). Make the trait name unique per test in that class, keeping every assertion's meaning; rerun Step 1 → 5/5 pass.

- [ ] **Step 3: Commit and push** — `fix(test): nome único para a característica exclusiva "Regeneração Bestial"` (or a `docs:` note in the baseline doc if 2a with no code change).

---

### Task 11: Measure A and record

**Files:**
- Modify: `docs/superpowers/specs/2026-10-16-versao-2-0-0-baseline.md`
- Modify: `/home/pro/.claude/projects/-home-pro-RuinaRPG/memory/ruina-integration-test-flake.md` (controller only — not a subagent task)

- [ ] **Step 1: Three full runs** — `df -h /` ≥ 4 GB, then `bash tools/bench/measure-build-and-tests.sh after-A-1`, `after-A-2`, `after-A-3`. Also `bash tools/bench/measure-integration-slices.sh after-A` once and `bash tools/bench/measure-code-size.sh after-A`.

- [ ] **Step 2: Compare** — the set of tests passing must equal the baseline's plus the new infrastructure tests (`DatabaseInitializerTests` ×2, `ApiFixtureTests` ×2, `TemplateDatabaseTests` ×1, `ApiTestBaseTests` ×1, `Testing_environment_does_not_throttle_logins` ×1). Compute it from the `Passed` counts: `after = baseline + 7`. If it differs, find the missing/extra tests with `--list-tests` diffs before going further.

- [ ] **Step 3: Fill the "After A" column** of the baseline doc for: build, each test project's time, integration timeouts per run (all three), slices total, and `lines.RuinaRPG.Tests.Integration`. Add a short "A — result" paragraph: speed-up factor (baseline integration seconds ÷ after-A median), timeout counts, whether the ≥ 3× target was met. If timeouts remain, note them with the new numbers (spec: documented, not chased in A).

- [ ] **Step 4: Commit and push** — `docs: medições após a infraestrutura de testes (2.0.0 A)`.

---

## Self-review notes

- Spec coverage: 0 → Task 1 (all nine measurement rows; Ficha load deferred, stated in Refinements). A.1 → Task 2. A.2 → Task 3. A.3/A.4 → Task 4. A.5 → Task 5. A.6 → Tasks 6–8 (+9 for stragglers). A.7 → Task 10. Testing section → tests in Tasks 2–5. Success criteria → Task 11.
- Names used consistently: `DatabaseInitializer.InitializeAsync(IServiceProvider, ILogger)`, `ApiFactory(string[, bool])`, `SharedPostgres.*`, `TemplateDatabase.{Name, EnsureCreatedAsync, CloneAsync}`, `ApiFixture.{ConnectionString, Factory, Client}`, `ApiTestBase.{Fixture, Factory, Client, AuthedRequest, RegisterGmAndGetTokenAsync, RegisterJogadorLinkedToAsync, GrantRulesAuditorAsync, CreateCampaignAsync}`.
