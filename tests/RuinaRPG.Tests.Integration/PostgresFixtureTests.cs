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

            // The bounded pool size is the invariant that must not silently regress as more test
            // classes are added later - see
            // .superpowers/sdd/2026-09-18-shared-postgres-testcontainer/final-review-fix-brief.md.
            firstConnection.MaxPoolSize.Should().Be(8);
            secondConnection.MaxPoolSize.Should().Be(8);

            // The checks above only compare connection-string text; they never actually open a
            // connection. That's how a 757/777-failure full-suite collapse slipped past this test
            // undetected. Prove the databases are real, reachable, and actually isolated from each
            // other by opening real connections and exercising real data.
            await using (var firstDb = new NpgsqlConnection(first.ConnectionString))
            await using (var secondDb = new NpgsqlConnection(second.ConnectionString))
            {
                await firstDb.OpenAsync();
                await secondDb.OpenAsync();

                await using (var firstCurrentDb = new NpgsqlCommand("SELECT current_database()", firstDb))
                {
                    var name = (string)(await firstCurrentDb.ExecuteScalarAsync())!;
                    name.Should().Be(firstConnection.Database);
                }

                await using (var secondCurrentDb = new NpgsqlCommand("SELECT current_database()", secondDb))
                {
                    var name = (string)(await secondCurrentDb.ExecuteScalarAsync())!;
                    name.Should().Be(secondConnection.Database);
                }

                await using (var createTable = new NpgsqlCommand("CREATE TABLE isolation_probe (id int)", firstDb))
                {
                    await createTable.ExecuteNonQueryAsync();
                }

                await using (var insertRow = new NpgsqlCommand("INSERT INTO isolation_probe (id) VALUES (1)", firstDb))
                {
                    await insertRow.ExecuteNonQueryAsync();
                }

                // The table created on `first`'s database must not be visible on `second`'s -
                // this is what actually proves data isolation between the two fixtures.
                await using var queryOnSecond = new NpgsqlCommand(
                    "SELECT to_regclass('public.isolation_probe')::text", secondDb);
                var result = await queryOnSecond.ExecuteScalarAsync();
                (result is DBNull || result is null).Should().BeTrue(
                    "the table created on the first fixture's database should not exist on the second's");
            }
        }
        finally
        {
            await first.DisposeAsync();
            await second.DisposeAsync();
        }
    }
}
