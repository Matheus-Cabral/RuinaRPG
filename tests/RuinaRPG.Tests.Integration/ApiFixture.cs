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
        Factory = new ApiFactory(ConnectionString);
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
