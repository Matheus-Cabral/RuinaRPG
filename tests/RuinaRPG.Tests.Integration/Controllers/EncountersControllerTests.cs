using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Encounters;

namespace RuinaRPG.Tests.Integration.Controllers;

public class EncountersControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public EncountersControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return tokens!.AccessToken;
    }

    private async Task<string> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return tokens!.AccessToken;
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> CreateCampaignAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        return (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
    }

    [Fact]
    public async Task Create_returns_201_starting_at_Round_1()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EncGm1", "enc1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Encontro");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/encounters", gmToken,
            new CreateEncounterRequest("Emboscada na Ponte")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<EncounterResponse>();
        body!.Nome.Should().Be("Emboscada na Ponte");
        body.CurrentRound.Should().Be(1);
        body.CurrentParticipantIndex.Should().Be(0);
    }

    [Fact]
    public async Task List_returns_only_encounters_for_campaigns_the_caller_owns()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("EncGmA", "enca@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("EncGmB", "encb@teste.com");
        var campaignA = await CreateCampaignAsync(tokenA, "Campanha A");
        var campaignB = await CreateCampaignAsync(tokenB, "Campanha B");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignA}/encounters", tokenA, new CreateEncounterRequest("Encontro A")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignB}/encounters", tokenB, new CreateEncounterRequest("Encontro B")));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignA}/encounters", tokenA));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<EncounterResponse>>();
        body!.Should().HaveCount(1);
        body.Should().ContainSingle(e => e.Nome == "Encontro A");
        body.Should().NotContain(e => e.Nome == "Encontro B");
    }

    [Fact]
    public async Task Create_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EncGmJog", "encgmjog@teste.com");
        var jogadorToken = await RegisterJogadorLinkedToAsync(gmToken, "EncJogador", "encjogador@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha do Jogador");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/encounters", jogadorToken,
            new CreateEncounterRequest("Não deveria funcionar")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_in_a_campaign_owned_by_another_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("EncOwner", "encowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("EncOther", "encother@teste.com");
        var campaignOfOwner = await CreateCampaignAsync(gmTokenOwner, "Campanha do Dono");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignOfOwner}/encounters", gmTokenOther,
            new CreateEncounterRequest("Invasão")));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
