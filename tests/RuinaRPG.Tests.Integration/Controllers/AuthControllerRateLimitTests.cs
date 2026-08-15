using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;

namespace RuinaRPG.Tests.Integration.Controllers;

public class AuthControllerRateLimitTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public AuthControllerRateLimitTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task Login_returns_429_after_exceeding_the_rate_limit()
    {
        var request = new LoginRequest("RateLimitTest", "WrongPassword!");
        HttpResponseMessage? lastResponse = null;

        for (var i = 0; i < 15; i++)
            lastResponse = await _client.PostAsJsonAsync("/api/auth/login", request);

        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
