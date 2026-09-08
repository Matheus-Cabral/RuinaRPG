using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class NpcAttributesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public NpcAttributesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> CreateSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
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

    private async Task<string> GrantBlankNpcAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var grantResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken, new GrantSheetRequest(playerId, "Npc", null)));
        return (await grantResponse.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;
    }

    [Fact]
    public async Task List_returns_8_attributes_all_zeroed()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAttrGm1", "npcattr1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/attributes", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<NpcAttributeResponse>>();
        body!.Should().HaveCount(8);
        body!.Should().OnlyContain(a => a.Total == 0);
    }

    [Fact]
    public async Task Update_an_attribute_by_the_owning_gm_recomputes_Total()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAttrGm2", "npcattr2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Forca", gmToken,
            new UpdateNpcAttributeRequest(5, 3, false)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/attributes", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcAttributeResponse>>();
        body!.Single(a => a.Atributo == "Forca").Total.Should().Be(6); // 5 + floor(3/2) + 0
    }

    [Fact]
    public async Task List_returns_attributes_in_canonical_order_and_stays_stable_after_an_update()
    {
        // Same canonical order as CharacterAttributesController's equivalent test
        // (AttributeDisplayOrder, Domain) — NPC uses the identical Atributo enum.
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAttrGm9", "npcattr9@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var beforeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/attributes", gmToken));
        var before = (await beforeResponse.Content.ReadFromJsonAsync<List<NpcAttributeResponse>>())!;
        before.Select(a => a.Atributo).Should().Equal(
            "Forca", "Vigor", "Agilidade", "Destreza", "Astucia", "Instinto", "Influencia", "Vontade");

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Vontade", gmToken,
            new UpdateNpcAttributeRequest(3, 0, false)));

        var afterResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/attributes", gmToken));
        var after = (await afterResponse.Content.ReadFromJsonAsync<List<NpcAttributeResponse>>())!;
        after.Select(a => a.Atributo).Should().Equal(before.Select(a => a.Atributo));
    }

    [Fact]
    public async Task Update_an_attribute_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcAttrGmOwner3", "npcattrowner3@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcAttrGmOther3", "npcattrother3@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Forca", gmTokenOther,
            new UpdateNpcAttributeRequest(5, 3, false)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcAttrGmOwner4", "npcattrowner4@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcAttrGmOther4", "npcattrother4@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/attributes", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_an_attribute_by_the_player_the_sheet_was_granted_to_recomputes_Total()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAttrGmGrant1", "npcattrgmgrant1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "NpcAttrJogadorGrant1", "npcattrjogadorgrant1@teste.com");
        var sheetId = await GrantBlankNpcAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Forca", playerToken,
            new UpdateNpcAttributeRequest(5, 3, false)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/attributes", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcAttributeResponse>>();
        body!.Single(a => a.Atributo == "Forca").Total.Should().Be(6); // 5 + floor(3/2)
    }
}
