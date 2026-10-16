namespace RuinaRPG.Tests.Integration;

// An empty database (no migrations) for classes that migrate it themselves - the CLI tests and the
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
