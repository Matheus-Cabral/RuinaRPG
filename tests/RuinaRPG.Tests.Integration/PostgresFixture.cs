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
            .WithCommand("-c", "max_connections=500")
            .Build();
        await container.StartAsync();
        return container;
    });

    private readonly string _databaseName = $"test_{Guid.NewGuid():N}";

    // Admin connections (CREATE DATABASE / DROP DATABASE) must not be pooled: `await using` on a
    // pooled NpgsqlConnection returns it to Npgsql's pool instead of physically closing it, and
    // nothing prunes that pool mid-run. With ~91 test classes initializing concurrently, that piled
    // nearly all of them into the shared server's connection budget at once (see
    // .superpowers/sdd/2026-09-18-shared-postgres-testcontainer/final-review-fix-brief.md).
    private string? _adminConnectionString;

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var container = await SharedContainer.Value;
        var serverConnectionString = container.GetConnectionString();
        _adminConnectionString = new NpgsqlConnectionStringBuilder(serverConnectionString) { Pooling = false }.ConnectionString;

        await using (var admin = new NpgsqlConnection(_adminConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        // Bound each class's own pool explicitly - without this, every one of the 91 classes could
        // each open up to Npgsql's default MaxPoolSize=100 connections to the shared server, with no
        // coordination between them, regardless of how high max_connections is raised above.
        ConnectionString = new NpgsqlConnectionStringBuilder(serverConnectionString)
        {
            Database = _databaseName,
            MaxPoolSize = 8,
            MinPoolSize = 0,
        }.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        if (_adminConnectionString is null) return;

        // Release this class's own pooled connections immediately rather than waiting for
        // WITH (FORCE) to forcibly kill the backends. ConnectionString may still be unset if
        // InitializeAsync failed between opening the admin connection and reaching this point.
        if (ConnectionString is not null)
        {
            NpgsqlConnection.ClearPool(new NpgsqlConnection(ConnectionString));
        }

        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE \"{_databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}
