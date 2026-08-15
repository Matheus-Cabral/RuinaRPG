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

    [Fact]
    public async Task Login_rate_limit_buckets_are_partitioned_by_the_forwarded_client_ip()
    {
        // Behind nginx every request reaches Kestrel from the nginx container's own
        // address, so partitioning on Connection.RemoteIpAddress alone collapses every
        // client into one shared bucket (Técnico R0006 requires the limit to be per
        // IP/usuário). The forwarded-headers middleware must restore the real client IP.
        var request = new LoginRequest("ForwardedForTest", "WrongPassword!");

        for (var i = 0; i < 10; i++)
        {
            var response = await PostLoginAsync(request, forwardedFor: "1.1.1.1");
            response.StatusCode.Should().NotBe(
                HttpStatusCode.TooManyRequests,
                "the first 10 requests from 1.1.1.1 are within its own permit limit");
        }

        var otherClientResponse = await PostLoginAsync(request, forwardedFor: "2.2.2.2");

        otherClientResponse.StatusCode.Should().NotBe(
            HttpStatusCode.TooManyRequests,
            "2.2.2.2 must get its own bucket instead of inheriting 1.1.1.1's exhausted one");
    }

    private async Task<HttpResponseMessage> PostLoginAsync(LoginRequest request, string forwardedFor)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-Forwarded-For", forwardedFor);

        return await _client.SendAsync(message);
    }
}
