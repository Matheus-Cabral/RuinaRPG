# Shared Postgres Testcontainer for Integration Tests — Design

## Problem

`tests/RuinaRPG.Tests.Integration` has 91 test classes, each declaring
`IClassFixture<PostgresFixture>`. xUnit's `IClassFixture<T>` creates a fresh
instance of `T` per test class, and `PostgresFixture` (`tests/RuinaRPG.Tests.Integration/PostgresFixture.cs`)
builds and starts a brand-new `postgres:16-alpine` Testcontainers container
in that instance's `InitializeAsync()`. The result: a full run of the suite
boots 91 separate real Postgres containers over its lifetime, with up to 4
starting concurrently (xUnit's default parallelism, matched to this host's
4 CPU cores).

On this host (4 cores, shared with the live production Docker stack), that
container-start concurrency overwhelms the Docker daemon: `StartContainerAsync`
calls start timing out with `TaskCanceledException`, cascading into mass
test failures unrelated to actual test assertions. This was diagnosed by
running the full suite, confirming 100% of failures were the identical
`TaskCanceledException → OperationCanceledException → socket cancellation`
chain (zero assertion failures), and confirming RAM was never the bottleneck
(no OOM kills, `free -h` stayed comfortable) — the real constraint is core
count / Docker daemon throughput under container-start churn, compounded by
disk pressure from 91 containers' worth of image/volume layers.

This isn't just a "this VM is small" problem: as more integration tests get
added over time, the container count (and therefore the load) grows
linearly with the number of test classes, regardless of hardware — a
structural problem in the test architecture, not just a resource limit to
raise.

## Goal

Cut the number of Postgres **containers** the suite starts from 91 to 1,
without changing:
- the isolation guarantee each test class currently gets (a fresh, empty,
  fully-migrated schema, invisible to every other class)
- the current parallelism (test classes still run concurrently across
  cores — no re-serialization of the suite)
- the public contract test classes depend on (`IClassFixture<PostgresFixture>`,
  `PostgresFixture.ConnectionString`) — **zero changes to any of the 91 test
  class files**

## Design

### One shared container, one database per class

`PostgresFixture` is rewritten internally. Its public shape
(`IAsyncLifetime`, `ConnectionString`) does not change.

- A `static readonly Lazy<Task<PostgreSqlContainer>>` on `PostgresFixture`
  (or a small internal static holder type) holds a single
  `postgres:16-alpine` container for the entire test assembly's run.
  `Lazy<Task<T>>` makes "start exactly once, even under concurrent access"
  free: if several class fixtures call into it at once during suite
  startup, only the first actually invokes `StartAsync()`; the rest await
  the same in-flight `Task`.
- Each `PostgresFixture` instance's `InitializeAsync()`:
  1. Awaits the shared container (starting it on first access).
  2. Opens an `NpgsqlConnection` to that server's default `postgres`
     maintenance database and runs `CREATE DATABASE test_{sanitized guid}`.
  3. Builds and exposes a connection string pointing at that new database
     (same server host/port/credentials as the shared container, different
     `Database=` value).
- `ApiFactory.ConfigureWebHost`'s existing `db.Database.Migrate()` call is
  untouched — it runs against the fresh per-class database exactly as it
  does today against the fresh per-class container.
- `DisposeAsync()` runs `DROP DATABASE test_{guid} WITH (FORCE)` against the
  shared server to reclaim the database (`WITH (FORCE)` terminates any
  lingering connections first; supported since Postgres 13, and this
  project targets `postgres:16-alpine`).

### Isolation is unaffected

Each class still gets a genuinely separate, empty, fully-migrated database
— the same guarantee as today's separate containers, just scoped at the
database level instead of the container level. A single Postgres server
handles many concurrent lightweight connections/databases without issue;
the expensive part being eliminated is *booting 91 containers*, not
*running several classes' queries concurrently against one server*.

### Container teardown

Testcontainers already runs a "Ryuk" reaper sidecar by default, which kills
every container it started when the test host process exits. This already
backs today's 91-container cleanup and requires no configuration — the
single shared container gets the same safety net for free. No explicit
assembly-level teardown hook is needed.

### `RulesAuditorCliTests`

This class already uses `IClassFixture<PostgresFixture>` directly (rather
than going through `ApiFactory`) — it needs no special handling; it gets
the same shared-container/per-class-database treatment as the other 90
classes automatically, since the change lives entirely inside
`PostgresFixture`.

## Non-goals

- No change to xUnit parallelism settings (`maxParallelThreads` or
  similar) — the design's whole point is that the current parallelism
  becomes safe once container-starts are no longer the concurrent
  bottleneck.
- No change to any of the 91 test class files, `ApiFactory.cs`, or the
  migration/DbContext wiring inside it.
- No new NuGet package. `Npgsql` is already a transitive dependency via
  `Microsoft.EntityFrameworkCore.Design`/`Npgsql.EntityFrameworkCore.PostgreSQL`
  in this project (used to build/run the `RuinaRpgDbContext`), so raw
  `CREATE DATABASE`/`DROP DATABASE` execution needs no new reference.

## Testing / verification

- Existing tests are the verification: every one of the 91 classes'
  existing test bodies already assumes a fresh, isolated, empty-then-
  migrated database — nothing about their assertions changes. If isolation
  regresses (e.g., a `CREATE DATABASE` collision, or a class reading data
  seeded by a different class), an existing test will fail with a data
  assertion mismatch, distinguishable from today's infra-level
  `TaskCanceledException` failures.
- New test: `PostgresFixture` itself should get a small test confirming
  two instances created back-to-back share the same underlying container
  (e.g., same `_container` reference or same mapped port) but expose
  different connection strings/databases — this is the one piece of new
  behavior with no existing coverage.
- Primary success signal: run the full `RuinaRPG.Tests.Integration` suite
  (previously reliably failing en masse from container-start timeouts) and
  confirm a clean pass, with `docker ps` showing exactly one Postgres
  container for the whole run instead of dozens.
