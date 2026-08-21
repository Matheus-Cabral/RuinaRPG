using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CampaignsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CampaignsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest("Nome", "Desc"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_returns_201_with_the_campaign()
    {
        var token = await RegisterGmAndGetTokenAsync("CampGm1", "camp1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", token, new CreateCampaignRequest("A Ruína Aguarda", "Descrição")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CampaignResponse>();
        body!.Nome.Should().Be("A Ruína Aguarda");
    }

    [Fact]
    public async Task List_returns_only_campaigns_owned_by_the_authenticated_gm()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("CampGmA", "campgma@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("CampGmB", "campgmb@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", tokenA, new CreateCampaignRequest("Campanha A", "")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", tokenB, new CreateCampaignRequest("Campanha B", "")));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/campaigns", tokenA));

        var body = await response.Content.ReadFromJsonAsync<List<CampaignResponse>>();
        body!.Should().ContainSingle(c => c.Nome == "Campanha A");
    }

    private async Task<string> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();

        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        var meBody = await meResponse.Content.ReadFromJsonAsync<MeResponse>();
        return meBody!.Id;
    }

    private async Task<string> CreateCampaignAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        return (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
    }

    [Fact]
    public async Task AddMember_a_player_linked_to_the_caller_returns_204_and_they_appear_in_members()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CampMemberGm1", "campmember1@teste.com");
        var playerId = await RegisterJogadorLinkedToAsync(gmToken, "CampMemberPlayer1", "campmemberplayer1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Membro");

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        addResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/members", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CampaignMemberResponse>>();
        body!.Should().ContainSingle(m => m.Nickname == "CampMemberPlayer1");
    }

    [Fact]
    public async Task AddMember_a_player_not_linked_to_the_caller_returns_400()
    {
        var gmTokenA = await RegisterGmAndGetTokenAsync("CampMemberGmA", "campmembergma@teste.com");
        var gmTokenB = await RegisterGmAndGetTokenAsync("CampMemberGmB", "campmembergmb@teste.com");
        var playerOfB = await RegisterJogadorLinkedToAsync(gmTokenB, "CampMemberPlayerB", "campmemberplayerb@teste.com");
        var campaignOfA = await CreateCampaignAsync(gmTokenA, "Campanha de A");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignOfA}/members", gmTokenA, new AddCampaignMemberRequest(playerOfB)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddMember_to_a_campaign_owned_by_another_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CampMemberOwner", "campmemberowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CampMemberOther", "campmemberother@teste.com");
        var playerOfOther = await RegisterJogadorLinkedToAsync(gmTokenOther, "CampMemberPlayerOther", "campmemberplayerother@teste.com");
        var campaignOfOwner = await CreateCampaignAsync(gmTokenOwner, "Campanha do Dono");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignOfOwner}/members", gmTokenOther, new AddCampaignMemberRequest(playerOfOther)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
