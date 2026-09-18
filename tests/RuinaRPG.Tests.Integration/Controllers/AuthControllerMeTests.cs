using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Domain;

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

    [Fact]
    public async Task Me_reports_MustChangePassword_false_by_default()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MustChangePwGm1", "mustchangepwgm1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken));

        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        body!.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task Me_reports_PendingChangelogVersion_for_a_GM_who_never_dismissed_anything()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ChangelogGm1", "changeloggm1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken));

        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        body!.PendingChangelogVersion.Should().Be(AppVersionInfo.Current);
    }

    [Fact]
    public async Task Me_reports_no_PendingChangelogVersion_for_a_Jogador()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ChangelogGm2", "changeloggm2@teste.com");
        var (_, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "ChangelogPlayer2", "changelogplayer2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", playerToken));

        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        body!.PendingChangelogVersion.Should().BeNull();
    }

    [Fact]
    public async Task DismissChangelog_makes_PendingChangelogVersion_null_for_that_user_only()
    {
        var gmToken1 = await RegisterGmAndGetTokenAsync("ChangelogGm3", "changeloggm3@teste.com");
        var gmToken2 = await RegisterGmAndGetTokenAsync("ChangelogGm4", "changeloggm4@teste.com");

        var dismissResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/auth/dismiss-changelog", gmToken1));
        dismissResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meAfterDismiss = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken1));
        (await meAfterDismiss.Content.ReadFromJsonAsync<MeResponse>())!.PendingChangelogVersion.Should().BeNull();

        // The second GM never called dismiss — still pending. Confirms the dismissal is per-user,
        // not a global flag.
        var meOther = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken2));
        (await meOther.Content.ReadFromJsonAsync<MeResponse>())!.PendingChangelogVersion.Should().Be(AppVersionInfo.Current);
    }

    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", tokens!.AccessToken));
        return ((await me.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
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
