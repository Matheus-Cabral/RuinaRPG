using Npgsql;
using Testcontainers.PostgreSql;

namespace RuinaRPG.Tests.Integration;

// The one Postgres container of a test run. Booting one per test class overwhelmed this host's Docker
// daemon - see docs/superpowers/specs/2026-09-18-shared-postgres-testcontainer-design.md. Every fixture
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
