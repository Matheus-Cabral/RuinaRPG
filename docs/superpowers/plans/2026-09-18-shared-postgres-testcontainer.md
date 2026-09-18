# Shared Postgres Testcontainer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cut the integration test suite's Postgres container count from 91 (one per test class) to 1 (shared for the whole assembly run), without changing test-class isolation, current parallelism, or any of the 91 test class files.

**Architecture:** `PostgresFixture` keeps its exact public shape (`IAsyncLifetime`, `ConnectionString`) but its internals change: a `static readonly Lazy<Task<PostgreSqlContainer>>` starts one shared `postgres:16-alpine` container the first time any test class needs it (subsequent accesses await the same in-flight `Task`, giving thread-safe start-once semantics for free). Each fixture instance then runs `CREATE DATABASE` against that shared server for its own uniquely-named database, migrates and uses that database exactly as before, and runs `DROP DATABASE ... WITH (FORCE)` on dispose.

**Tech Stack:** .NET 8, xUnit 2.4.2, `Testcontainers.PostgreSql` 3.10.0, Npgsql (already a transitive dependency of `tests/RuinaRPG.Tests.Integration` via `RuinaRPG.Api` → `RuinaRPG.Infrastructure` → `Npgsql.EntityFrameworkCore.PostgreSQL`, confirmed by reading `src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj:12` and `src/RuinaRPG.Api/RuinaRPG.Api.csproj`'s `ProjectReference`s — no new package reference needed).

**Spec:** docs/superpowers/specs/2026-09-18-shared-postgres-testcontainer-design.md

## Global Constraints

- `PostgresFixture`'s public contract does not change: it still implements `IAsyncLifetime`, still exposes `public string ConnectionString { get; }`, and still has a public parameterless constructor usable by `IClassFixture<PostgresFixture>`.
- **Zero changes to any of the 91 existing test class files** that declare `IClassFixture<PostgresFixture>` (this includes `tests/RuinaRPG.Tests.Integration/RulesAuditorCliTests.cs` and every file under `tests/RuinaRPG.Tests.Integration/Controllers/`) or to `tests/RuinaRPG.Tests.Integration/ApiFactory.cs`.
- No new NuGet package reference in `tests/RuinaRPG.Tests.Integration/RuinaRPG.Tests.Integration.csproj`.
- No xUnit parallelism configuration changes (no `xunit.runner.json`, no `[CollectionDefinition]` additions) — the whole point is that today's parallelism becomes safe once container-starts stop being the concurrent bottleneck.
- Each test class must still get a genuinely separate, empty, fully-migrated database — the same isolation guarantee it has today, just scoped at the database level instead of the container level.

---

### Task 1: Shared container + per-class database in PostgresFixture

**Files:**
- Modify: `tests/RuinaRPG.Tests.Integration/PostgresFixture.cs`
- Create: `tests/RuinaRPG.Tests.Integration/PostgresFixtureTests.cs`

**Interfaces:**
- Consumes: `Testcontainers.PostgreSql.PostgreSqlBuilder`/`PostgreSqlContainer` (already used in the current file — same `.WithImage("postgres:16-alpine").WithDatabase("ruinarpg").WithUsername("test").WithPassword("test")` configuration); `Npgsql.NpgsqlConnection`, `Npgsql.NpgsqlCommand`, `Npgsql.NpgsqlConnectionStringBuilder` (transitively available, see Tech Stack above — add `using Npgsql;` to both files).
- Produces: `PostgresFixture.ConnectionString` (`string`, unchanged signature) — every one of the 91 existing `IClassFixture<PostgresFixture>` consumers keeps working against this exact property with no changes on their end. `PostgresFixture` still has an implicit public parameterless constructor (unchanged — no explicit constructor is declared, same as today).

- [ ] **Step 1: Write the failing test**

Create `tests/RuinaRPG.Tests.Integration/PostgresFixtureTests.cs`:

```csharp
using FluentAssertions;
using Npgsql;

namespace RuinaRPG.Tests.Integration;

public class PostgresFixtureTests
{
    [Fact]
    public async Task Two_fixtures_share_one_container_but_get_separate_databases()
    {
        var first = new PostgresFixture();
        var second = new PostgresFixture();
        try
        {
            await first.InitializeAsync();
            await second.InitializeAsync();

            var firstConnection = new NpgsqlConnectionStringBuilder(first.ConnectionString);
            var secondConnection = new NpgsqlConnectionStringBuilder(second.ConnectionString);

            // Same underlying server (same shared container) ...
            firstConnection.Host.Should().Be(secondConnection.Host);
            firstConnection.Port.Should().Be(secondConnection.Port);
            // ... but each fixture instance owns its own isolated database.
            firstConnection.Database.Should().NotBe(secondConnection.Database);
        }
        finally
        {
            await first.DisposeAsync();
            await second.DisposeAsync();
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~PostgresFixtureTests"`
Expected: FAIL. Under the current implementation, `first` and `second` each start their own container on a
different, randomly-assigned host port, so `firstConnection.Port.Should().Be(secondConnection.Port)` fails
(two different ports).

- [ ] **Step 3: Rewrite PostgresFixture.cs**

Replace the full contents of `tests/RuinaRPG.Tests.Integration/PostgresFixture.cs` with:

```csharp
using Npgsql;
using Testcontainers.PostgreSql;

namespace RuinaRPG.Tests.Integration;

public class PostgresFixture : IAsyncLifetime
{
    // Booting a fresh Postgres container per test class (91 of them) overwhelms this host's Docker
    // daemon under xUnit's default parallelism (up to 4 concurrent container starts, on a 4-core
    // host also running the live production stack) - see
    // docs/superpowers/specs/2026-09-18-shared-postgres-testcontainer-design.md. One container is
    // shared for the whole assembly's test run; each fixture instance below gets its own database
    // within it instead of its own container - a CREATE DATABASE is orders of magnitude cheaper
    // than a full container lifecycle, and keeps the same per-class isolation guarantee.
    private static readonly Lazy<Task<PostgreSqlContainer>> SharedContainer = new(async () =>
    {
        var container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("ruinarpg")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
        await container.StartAsync();
        return container;
    });

    private readonly string _databaseName = $"test_{Guid.NewGuid():N}";
    private string _serverConnectionString = null!;

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var container = await SharedContainer.Value;
        _serverConnectionString = container.GetConnectionString();

        await using (var admin = new NpgsqlConnection(_serverConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        ConnectionString = new NpgsqlConnectionStringBuilder(_serverConnectionString) { Database = _databaseName }.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        await using var admin = new NpgsqlConnection(_serverConnectionString);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE \"{_databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}
```

- [ ] **Step 4: Run the new test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~PostgresFixtureTests"`
Expected: PASS.

- [ ] **Step 5: Verify a representative slice of existing consumers still pass unchanged**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RulesAuditorCliTests|FullyQualifiedName~AuthControllerMeTests|FullyQualifiedName~PostgresFixtureTests"`
Expected: PASS — all of `RulesAuditorCliTests` (6 tests, uses `PostgresFixture` directly without
`ApiFactory`), `AuthControllerMeTests` (8 tests, uses `PostgresFixture` + `ApiFactory` together, the
common pattern shared by every controller test class), and the new `PostgresFixtureTests` pass
together, with these classes running as separate xUnit test classes (so still exercising
cross-class parallel access against the one shared container).

Also run: `docker ps --filter "ancestor=postgres:16-alpine" --format "{{.ID}}"` immediately after the
above command completes but before its containers are torn down (or check the test log for
Testcontainers' own container-start log lines) to confirm exactly one `postgres:16-alpine`
container was used for that whole run, not three.

- [ ] **Step 6: Commit**

```bash
git add tests/RuinaRPG.Tests.Integration/PostgresFixture.cs tests/RuinaRPG.Tests.Integration/PostgresFixtureTests.cs
git commit -m "perf: share one Postgres Testcontainer across all integration test classes

Each of the 91 test classes using IClassFixture<PostgresFixture> used to boot its own
container; on this host that overwhelmed the Docker daemon under xUnit's default
parallelism. Now one container is shared for the whole run and each class gets its own
database within it instead - same isolation, same parallelism, a fraction of the load."
```

(Use the repository's standard commit-attribution trailer if one is configured for this session.)

---

## Verification note (not a plan task — informational for whoever runs this plan)

After Task 1 lands, the real-world payoff is best confirmed by running the **full**
`RuinaRPG.Tests.Integration` suite (previously reliably failing en masse from container-start
timeouts on this host) and confirming a clean pass with `docker ps` showing a single Postgres
container for the entire run. This is expensive (the full suite takes several minutes) and isn't a
bite-sized TDD step, so it belongs in this plan's final whole-branch review / the
`finishing-a-development-branch` test-verification step, not as a Task 1 step.
