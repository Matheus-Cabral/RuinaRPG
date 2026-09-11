using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;

namespace RuinaRPG.Tests.Integration.Controllers;

public class AuthControllerMeTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public AuthControllerMeTests(PostgresFixture postgres) => _postgres = postgres;

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
    public async Task Me_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_with_a_valid_access_token_returns_the_caller_claims()
    {
        var register = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest("MeGm", "me@teste.com", "Senha!123", "Senha!123"));
        var tokens = await register.Content.ReadFromJsonAsync<AuthResponse>();

        using var message = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        body!.Nickname.Should().Be("MeGm");
        body.Role.Should().Be("GM");
        body.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Me_with_a_garbage_token_returns_401()
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");
        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_with_a_token_signed_by_a_different_key_returns_401()
    {
        var register = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest("MeForged", "me.forged@teste.com", "Senha!123", "Senha!123"));
        var tokens = await register.Content.ReadFromJsonAsync<AuthResponse>();

        using var message = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken + "tampered");
        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_reports_IsRulesAuditor_false_by_default()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulesAuditorGm1", "rulesauditorgm1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken));

        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        body!.IsRulesAuditor.Should().BeFalse();
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }
}
