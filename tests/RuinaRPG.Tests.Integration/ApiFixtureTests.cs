using System.Net;
using FluentAssertions;
using Npgsql;

namespace RuinaRPG.Tests.Integration;

public class ApiFixtureTests
{
    [Fact]
    public async Task Two_fixtures_initialized_concurrently_get_independent_databases()
    {
        var first = new ApiFixture();
        var second = new ApiFixture();
        try
        {
            await Task.WhenAll(first.InitializeAsync(), second.InitializeAsync());

            new NpgsqlConnectionStringBuilder(first.ConnectionString).Database
                .Should().NotBe(new NpgsqlConnectionStringBuilder(second.ConnectionString).Database);
            new NpgsqlConnectionStringBuilder(first.ConnectionString).MaxPoolSize.Should().Be(8);

            await using (var a = new NpgsqlConnection(first.ConnectionString))
            {
                await a.OpenAsync();
                await new NpgsqlCommand("CREATE TABLE only_in_first (id int)", a).ExecuteNonQueryAsync();
            }
            await using (var b = new NpgsqlConnection(second.ConnectionString))
            {
                await b.OpenAsync();
                var exists = await new NpgsqlCommand(
                    "SELECT to_regclass('only_in_first') IS NOT NULL", b).ExecuteScalarAsync();
                exists.Should().Be(false);
            }
        }
        finally
        {
            await first.DisposeAsync();
            await second.DisposeAsync();
        }
    }

    [Fact]
    public async Task The_client_reaches_the_api_backed_by_the_clone()
    {
        var fixture = new ApiFixture();
        try
        {
            await fixture.InitializeAsync();
            var response = await fixture.Client.GetAsync("/api/health");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var rulebook = await fixture.Client.GetAsync("/api/rulebook");
            rulebook.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }
}
