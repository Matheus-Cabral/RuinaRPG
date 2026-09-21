using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Items;
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

    private async Task<string> CreateArtefatoItemAsync(string gmToken, string tipoDeAlvo, string alvo, int valor)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Artefato", "Anel de Teste", 0.1m, 500, null, null, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, tipoDeAlvo, alvo, valor, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    private Task<HttpResponseMessage> AddArtifactAsync(string gmToken, string sheetId, string artifactItemId) =>
        _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/artifacts", gmToken, new AddNpcArtifactRequest(artifactItemId)));

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

    [Fact]
    public async Task An_equipped_Atributo_Artefato_is_summed_into_that_Atributos_Total()
    {
        // Requisitos - Ficha de NPCs R0005: equipping an Artefato targeting an Atributo now feeds
        // AttributeTotalCalculator.Total's Artefatos term for NPCs too (previously hardcoded 0).
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAttrGmArt1", "npcattrart1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Forca", gmToken,
            new UpdateNpcAttributeRequest(5, 3, false)));

        var artifactItemId = await CreateArtefatoItemAsync(gmToken, "Atributo", "Forca", 2);
        await AddArtifactAsync(gmToken, sheetId, artifactItemId);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/attributes", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcAttributeResponse>>();
        body!.Single(a => a.Atributo == "Forca").Total.Should().Be(8); // 5 + floor(3/2) + 2
    }

    [Fact]
    public async Task Budget_at_level_1_offers_9_points_and_reports_zero_spent()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAttrBud1", "npcattrbud1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/attributes/budget", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AttributePointBudgetResponse>();
        body!.PontosDisponiveis.Should().Be(9);
        body.GastoTotal.Should().Be(0);
    }

    [Fact]
    public async Task Budget_adds_the_Pontos_de_Atributo_of_every_level_up_to_the_sheets_Nivel()
    {
        // Tabela de Níveis: 9 (nível 1) + 1 (nível 2) + 1 (nível 6) + 2 (nível 10) = 13.
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAttrBud2", "npcattrbud2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/nivel", gmToken, 10));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/attributes/budget", gmToken));

        var body = await response.Content.ReadFromJsonAsync<AttributePointBudgetResponse>();
        body!.PontosDisponiveis.Should().Be(13);
    }

    [Fact]
    public async Task Budget_GastoTotal_is_the_sum_of_Gasto_across_attributes()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAttrBud3", "npcattrbud3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Forca", gmToken, new UpdateNpcAttributeRequest(3, 0, false)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Vigor", gmToken, new UpdateNpcAttributeRequest(4, 5, true)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/attributes/budget", gmToken));

        var body = await response.Content.ReadFromJsonAsync<AttributePointBudgetResponse>();
        body!.GastoTotal.Should().Be(7); // Bônus não entra no orçamento
    }

    [Fact]
    public async Task Update_above_the_budget_is_still_accepted_and_the_budget_reports_the_overspend()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAttrBud4", "npcattrbud4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken); // nível 1 = 9 pontos

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Forca", gmToken, new UpdateNpcAttributeRequest(50, 0, false)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/attributes/budget", gmToken));
        var body = await response.Content.ReadFromJsonAsync<AttributePointBudgetResponse>();
        body!.GastoTotal.Should().Be(50);
        body.PontosDisponiveis.Should().Be(9);
    }

    [Fact]
    public async Task Budget_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcAttrBudOwner5", "npcattrbudowner5@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcAttrBudOther5", "npcattrbudother5@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/attributes/budget", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
