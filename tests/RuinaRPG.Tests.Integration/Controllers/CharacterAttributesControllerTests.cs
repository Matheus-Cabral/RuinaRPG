using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterAttributesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CharacterAttributesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        return ((await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
    }

    private async Task<string> SetUpSheetAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }

    [Fact]
    public async Task List_returns_8_attributes_all_zeroed()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttrGm1", "attr1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AttrPlayer1", "attrplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/attributes", playerToken));

        var body = await response.Content.ReadFromJsonAsync<List<CharacterAttributeResponse>>();
        body!.Should().HaveCount(8);
        body!.Should().OnlyContain(a => a.Total == 0);
    }

    [Fact]
    public async Task Update_an_attribute_by_the_owner_recomputes_Total()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttrGm2", "attr2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AttrPlayer2", "attrplayer2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Forca", playerToken,
            new UpdateCharacterAttributeRequest(5, 3, false)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/attributes", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterAttributeResponse>>();
        body!.Single(a => a.Atributo == "Forca").Total.Should().Be(6); // 5 + floor(3/2) + 0
    }

    [Fact]
    public async Task Update_an_attribute_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttrGm3", "attr3@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "AttrPlayer3", "attrplayer3@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "AttrPlayer3b", "attrplayer3b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Forca", otherToken,
            new UpdateCharacterAttributeRequest(5, 3, false)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttrGm4", "attr4@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "AttrPlayer4", "attrplayer4@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "AttrPlayer4b", "attrplayer4b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/attributes", otherToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
