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
