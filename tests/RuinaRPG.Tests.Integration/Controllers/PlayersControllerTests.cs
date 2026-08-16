using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Contracts.Players;

namespace RuinaRPG.Tests.Integration.Controllers;

public class PlayersControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public PlayersControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return message;
    }

    private async Task<string> RegisterGmAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return tokens!.AccessToken;
    }

    private async Task RegisterJogadorAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<InviteCodeResponse>())!.Code;

        await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
    }

    [Fact]
    public async Task Search_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/players");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Search_with_no_query_returns_all_players_linked_to_the_gm()
    {
        var gmToken = await RegisterGmAsync("PlayersGm1", "playersgm1@teste.com");
        await RegisterJogadorAsync(gmToken, "PlayerOne", "playerone@teste.com");
        await RegisterJogadorAsync(gmToken, "PlayerTwo", "playertwo@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/players", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<PlayerSearchResultResponse>>();
        body!.Select(p => p.Nickname).Should().BeEquivalentTo("PlayerOne", "PlayerTwo");
    }

    [Fact]
    public async Task Search_excludes_players_linked_to_a_different_gm()
    {
        var gmTokenA = await RegisterGmAsync("PlayersGmA", "playersgma@teste.com");
        var gmTokenB = await RegisterGmAsync("PlayersGmB", "playersgmb@teste.com");
        await RegisterJogadorAsync(gmTokenA, "PlayerA", "playera@teste.com");
        await RegisterJogadorAsync(gmTokenB, "PlayerB", "playerb@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/players", gmTokenA));

        var body = await response.Content.ReadFromJsonAsync<List<PlayerSearchResultResponse>>();
        body!.Select(p => p.Nickname).Should().BeEquivalentTo("PlayerA");
    }

    [Fact]
    public async Task Search_by_partial_nickname_is_case_insensitive()
    {
        var gmToken = await RegisterGmAsync("PlayersGm2", "playersgm2@teste.com");
        await RegisterJogadorAsync(gmToken, "FindableNickname", "findable@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/players?q=findable", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<PlayerSearchResultResponse>>();
        body!.Should().ContainSingle(p => p.Nickname == "FindableNickname");
    }

    [Fact]
    public async Task Search_by_partial_email_matches()
    {
        var gmToken = await RegisterGmAsync("PlayersGm3", "playersgm3@teste.com");
        await RegisterJogadorAsync(gmToken, "EmailMatch", "unique-mailbox@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/players?q=unique-mailbox", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<PlayerSearchResultResponse>>();
        body!.Should().ContainSingle(p => p.Nickname == "EmailMatch");
    }
}
