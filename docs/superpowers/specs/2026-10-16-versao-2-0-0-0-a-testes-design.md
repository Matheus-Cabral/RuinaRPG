# Versão 2.0.0 — Spec 0+A: Baseline and test infrastructure — Design

**Status:** design approved by user 2026-10-16 (in chat). Version 2.0.0, branch `release/2.0.0`.
First detailed spec of the 2.0.0 roadmap (`2026-10-16-versao-2-0-0-roadmap-design.md`), covering
sub-projects **0** (baseline measurement) and **A** (test infrastructure).

## Overview

**0** records how slow things are today, so every later sub-project can show a measured gain.
**A** makes the integration test suite much faster and more reliable without changing what any test
asserts. No production behavior changes.

## Findings that drive A

Integration tests (`tests/RuinaRPG.Tests.Integration`, ~1677 `[Fact]`/`[Theory]` in 158 classes using
`IClassFixture<PostgresFixture>`) pay a large fixed cost **per test**, not per class:

1. Each test class builds `new ApiFactory(...)` in `InitializeAsync`, and xUnit creates a new class
   instance **per test** — so every test boots its own ASP.NET Core host.
2. `ApiFactory.ConfigureWebHost` calls `services.BuildServiceProvider()` and `Database.Migrate()` — per
   test, over a chain of 169 migrations.
3. `WebApplicationFactory` defaults to the `Development` environment, so `Program.cs` runs its startup
   `Migrate()` **and seven seeders** (parsing embedded Markdown) — again per test.
4. Hundreds of hosts booting concurrently match the known residual flake (~4% `HttpClient.Timeout` at the
   TestServer layer, with growing monitor-lock contention; see memory/flake writeup in
   `2026-09-18-shared-postgres-testcontainer-design.md`).
5. Each test class re-declares the same helpers (`AuthedRequest`, `RegisterGmAndGetTokenAsync`,
   `CreateSheetAsync`, ...).

Constraints found while checking the design:

- The `login` rate-limiter policy allows 10 requests/min per IP. Today every test gets a fresh limiter;
  with one host per class a class could exceed it and get 429.
- Some classes (the CLI tests and `Persistence/*MigrationTests`, `DefaultCatalogSeederTests`,
  `ConcorrenciaDoEncontroTests`) run `Migrate()` themselves, some to intermediate migrations, so they need
  a **blank** database, not a migrated clone.
- `WithWebHostBuilder` is used in 3 files (`NotificationHubTests`, `CondicoesAlteradasTests`,
  `SheetLogRecordingTests`) to swap services for a single test.
- `Program.cs` has the migrate + seed sequence twice, identical: once for `--migrate`, once for
  `IsDevelopment()`.

## 0 — Baseline measurement

Run on `origin/main` (`729dac9`) on this VM, with `df -h /` showing ≥ 4 GB free and no other heavy load.
Each timing taken 2× and both recorded. Results go to
`docs/superpowers/specs/2026-10-16-versao-2-0-0-baseline.md`, a table per area with a "2.0.0" column
filled in as sub-projects land.

| Area | Measurement |
|---|---|
| Build | `dotnet build --no-incremental` wall time; warnings count |
| Unit tests | `dotnet test tests/RuinaRPG.Tests.Unit --no-build` wall time, pass/fail |
| Client tests | same for `tests/RuinaRPG.Tests.Client` |
| Integration tests | one full `--no-build` run: wall time, passed/failed/timeouts; plus the six-slice run from the flake memory |
| Code size | lines of `.cs`/`.razor` per project (excluding `obj/`, `Migrations/`), size of `Migrations/` |
| Deploy | `docker compose build` cold (after `docker builder prune -f`) and warm (after touching one `.cs` in `Api`) |
| Client payload | size of published `wwwroot/_framework` raw and gzip'd; file count |
| Load | time to first render of `/` and of a Ficha de Personagem, via a headless browser against a local stack (cold cache) |
| API latency | p50/p95 of the ten most-used GET endpoints over a seeded local stack (simple scripted loop) |

Rules: nothing in 0 changes code. The measuring scripts live in `tools/bench/` (committed, so 2.0.0's
column is measured the same way).

## A — Design

### A.1 `DatabaseInitializer` (Api)

The duplicated migrate + seed sequence in `Program.cs` becomes
`DatabaseInitializer.InitializeAsync(IServiceProvider services, ILogger logger)` (Migrate, then every
seeder/backfill in today's order, same log lines). `Program.cs` calls it from `--migrate` and from the
`IsDevelopment()` block. Behavior in Development and Production is unchanged; ~40 lines removed.

### A.2 `Testing` environment

`ApiFactory` sets `builder.UseEnvironment("Testing")`. Consequences:

- `IsDevelopment()` is false → no startup migrate/seed and no Swagger in tests (no test exercises Swagger;
  verified while planning).
- `appsettings.Testing.json` (Api) carries what tests need from configuration: logging levels as today's
  Development file, and `RateLimiting:LoginPermitLimit` set high.
- The `login` policy reads `PermitLimit` from `RateLimiting:LoginPermitLimit` (default 10, so Production
  and Development are unchanged). Any existing test that asserts the 429 behavior keeps passing by
  overriding the option back to 10 on its own factory.

### A.3 Template database (Tests.Integration)

`TemplateDatabase`: a static `Lazy<Task<string>>`, created once per test run inside the shared
Testcontainers Postgres:

1. `CREATE DATABASE ruinarpg_template`;
2. build a service provider from the API's own registrations (via an `ApiFactory` pointed at the template)
   and run `DatabaseInitializer.InitializeAsync` — so the template holds exactly what Development startup
   would produce;
3. clear pools to the template (a template must have no open connections to be cloned) and mark it
   `IS_TEMPLATE true`.

### A.4 Fixtures

- `ApiFixture : IAsyncLifetime` (replaces `PostgresFixture` for API tests): `CREATE DATABASE test_<guid>
  TEMPLATE ruinarpg_template`; creates **one** `ApiFactory` for the class; exposes `Factory`, `Client`
  (a default `HttpClient`) and `ConnectionString`. Dispose: dispose factory, clear pool, `DROP DATABASE
  ... WITH (FORCE)` as today. Admin connections stay unpooled and per-class `MaxPoolSize` stays bounded
  (both lessons from the 2026-09-18 spec).
- `BlankDatabaseFixture`: today's `PostgresFixture` behavior (empty database, no host), for the classes
  that migrate by themselves. Renamed so intent is explicit.
- Concurrent template creation is serialized by the `Lazy`; clones run concurrently (Postgres serializes
  `CREATE DATABASE ... TEMPLATE` itself; if that proves a bottleneck the plan may add a small semaphore).

### A.5 `ApiTestBase`

An abstract base for API test classes (`IClassFixture<ApiFixture>`) holding the helpers that are copied
across classes today: authed request building, register/login GM and Player returning tokens, create
sheet of each kind, create campaign, join via invite. Helpers that exist in only one class stay there.
The plan inventories the duplicated helpers first and only moves those with ≥ 3 copies and identical
behavior.

### A.6 Migration of the 158 classes

Mechanical and per class: replace `IClassFixture<PostgresFixture>` + per-test `ApiFactory` with
`ApiTestBase`/`ApiFixture` (or `BlankDatabaseFixture`), delete the now-redundant helpers. Tests inside a
class already shared one database, so **data isolation is unchanged**; what changes is that host-level
in-memory state (caches, `IMemoryCache`, SignalR groups, rate-limit counters) now also persists across a
class's tests. Any test that turns out to depend on a fresh host gets its own factory via
`WithWebHostBuilder`, noted in the plan.

### A.7 Known deterministic collision

`CampaignGrantsControllerTests`, `CreatureExclusiveTraitsControllerTests` and
`CreatureAffectionsAndTraitsControllerTests` all insert a `CreatureExclusiveTrait` named "Regeneração
Bestial" (memory note, 2026-09-21). It was observed even with one database per class, so the exact
collision path is unknown; the plan reproduces it first, then fixes it with unique names per test.

## Testing (TDD)

A changes test infrastructure, so its own tests are:

- `DatabaseInitializerTests` (written first): on a blank DB, initializing yields the same row counts per
  seeded table as today's Development startup; running it twice is idempotent.
- `TemplateDatabaseTests`: the clone has the full migration history and the seeded rows; two clones are
  independent (a write in one is not seen by the other).
- `ApiFixtureTests` replaces `PostgresFixtureTests`: a request through `Client` hits the cloned DB.
- Rate-limit test: with the default option 10, the 11th login in a minute returns 429 (existing test, if
  any, otherwise added).
- The whole existing suite is the regression test: same set of passing tests as the baseline run.

## Success criteria

- Same tests pass as in the baseline (no test deleted or weakened in A; test count only changes by the
  new infrastructure tests).
- Integration suite wall time **significantly** lower than the baseline (target ≥ 3× faster; the actual
  number goes into the baseline doc).
- Timeouts: measured over 3 consecutive full runs and recorded. Goal is zero; if not zero, the residual is
  documented with the new data rather than chased inside A.
- `dotnet build` still 0 warnings / 0 errors (checked with `--no-incremental`).

## Out of scope

- Unit and client (bUnit) test speed beyond what 0 measures — they are not the bottleneck; revisit only if
  0 shows otherwise.
- Consolidating Character/NPC/Creature test triplicates into theories — that belongs to D.
- Removing `Persistence/*MigrationTests` — that belongs to B.
