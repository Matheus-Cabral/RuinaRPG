using Npgsql;

namespace RuinaRPG.Tests.Integration;

// Migrated and seeded once per test run (through the API's own DatabaseInitializer, via ApiFactory), then
// cloned by every ApiFixture - so no class pays for 169 migrations and seven seeders again.
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
