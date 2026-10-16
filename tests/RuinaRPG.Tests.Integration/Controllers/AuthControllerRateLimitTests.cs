using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using RuinaRPG.Contracts.Auth;

namespace RuinaRPG.Tests.Integration.Controllers;

public class AuthControllerRateLimitTests : IClassFixture<BlankDatabaseFixture>, IAsyncLifetime
{
    private readonly BlankDatabaseFixture _postgres;
    private ApiFactory _factory = null!;
    private WebApplicationFactory<Program> _productionLimitFactory = null!;
    private HttpClient _client = null!;

    public AuthControllerRateLimitTests(BlankDatabaseFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        // Production's 10 logins/min (Técnico R0006); ApiFactory raises it for every other class.
        _productionLimitFactory = _factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:LoginPermitLimit", "10"));
        _client = _productionLimitFactory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _productionLimitFactory.DisposeAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Testing_environment_does_not_throttle_logins()
    {
        using var client = _factory.CreateClient();
        var request = new LoginRequest("NoThrottleTest", "WrongPassword!");
        HttpResponseMessage? lastResponse = null;

        for (var i = 0; i < 15; i++)
            lastResponse = await client.PostAsJsonAsync("/api/auth/login", request);

        lastResponse!.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
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
