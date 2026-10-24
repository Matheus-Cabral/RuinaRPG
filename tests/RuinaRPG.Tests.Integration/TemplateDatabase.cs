using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using RuinaRPG.Api;

namespace RuinaRPG.Tests.Integration;

// Migrated and seeded once per test run (through the API's own DatabaseInitializer, via ApiFactory), then
// cloned by every ApiFixture - so no class pays for the migrations and seeders again.
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
            await SharedPostgres.RetryOn55006Async(
                () => SharedPostgres.ExecuteAdminAsync($"CREATE DATABASE \"{databaseName}\" TEMPLATE \"{Name}\""),
                $"clone {databaseName}",
                rethrowOnFinalFailure: true);
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

        await using (var factory = new ApiFactory(connectionString))
            await DatabaseInitializer.InitializeAsync(factory.Services, NullLogger.Instance);

        NpgsqlConnection.ClearPool(new NpgsqlConnection(connectionString));
        await SharedPostgres.ExecuteAdminAsync($"ALTER DATABASE \"{Name}\" WITH IS_TEMPLATE true");
        // Nothing may ever hold a connection on the template, or CREATE DATABASE ... TEMPLATE fails.
        await SharedPostgres.ExecuteAdminAsync($"ALTER DATABASE \"{Name}\" WITH ALLOW_CONNECTIONS false");
    }
}
