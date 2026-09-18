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
