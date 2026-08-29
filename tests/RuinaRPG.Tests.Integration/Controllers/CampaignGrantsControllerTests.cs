using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CampaignGrantsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CampaignGrantsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
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
        return (meBody!.Id, tokens.AccessToken);
    }

    private async Task<string> CreateCampaignAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        return (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
    }

    private async Task AddMemberAsync(string gmToken, string campaignId, string playerId) =>
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));

    private async Task<string> CreateNpcSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    private async Task<NpcSheetResponse> GetNpcSheetAsync(string gmToken, string sheetId)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!;
    }

    private async Task<List<NpcAttributeResponse>> GetNpcAttributesAsync(string gmToken, string sheetId)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/attributes", gmToken));
        return (await response.Content.ReadFromJsonAsync<List<NpcAttributeResponse>>())!;
    }

    private async Task SetNpcAttributeGastoAsync(string gmToken, string sheetId, string atributo, int gasto) =>
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/{atributo}", gmToken,
            new UpdateNpcAttributeRequest(gasto, 0, false)));

    private async Task<HttpResponseMessage> GrantAsync(string gmToken, string campaignId, GrantSheetRequest request) =>
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken, request));

    private async Task<string> CreateCreatureSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
    }

    private async Task<CreatureSheetResponse> GetCreatureSheetAsync(string gmToken, string sheetId)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmToken));
        return (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!;
    }

    private async Task<List<CreatureAttributeResponse>> GetCreatureAttributesAsync(string gmToken, string sheetId)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/attributes", gmToken));
        return (await response.Content.ReadFromJsonAsync<List<CreatureAttributeResponse>>())!;
    }

    private async Task SetCreatureAttributeGastoAsync(string gmToken, string sheetId, string atributo, int gasto) =>
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/attributes/{atributo}", gmToken,
            new UpdateCreatureAttributeRequest(gasto, 0, false)));

    [Fact]
    public async Task Grant_blank_creates_a_new_owned_Npc_sheet()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("GrantGm1", "grant1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "GrantPlayer1", "grantplayer1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Grant Blank");
        await AddMemberAsync(gmToken, campaignId, playerId);

        var response = await GrantAsync(gmToken, campaignId, new GrantSheetRequest(playerId, "Npc", null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<GrantSheetResponse>();
        body!.Tipo.Should().Be("Npc");

        var sheet = await GetNpcSheetAsync(gmToken, body.SheetId);
        sheet.OwnerId.Should().Be(playerId);
        sheet.Nome.Should().BeNull();
        sheet.Nivel.Should().Be(1);
        sheet.EAPAtual.Should().Be(0);
    }

    [Fact]
    public async Task Grant_from_an_existing_Npc_deep_copies_attributes_and_stays_independent()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("GrantGm2", "grant2@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "GrantPlayer2", "grantplayer2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Grant Cópia");
        await AddMemberAsync(gmToken, campaignId, playerId);

        var sourceId = await CreateNpcSheetAsync(gmToken);
        await SetNpcAttributeGastoAsync(gmToken, sourceId, "Forca", 5);

        var response = await GrantAsync(gmToken, campaignId, new GrantSheetRequest(playerId, "Npc", sourceId));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<GrantSheetResponse>();

        var copiedAttributes = await GetNpcAttributesAsync(gmToken, body!.SheetId);
        copiedAttributes.Single(a => a.Atributo == "Forca").Gasto.Should().Be(5);

        // Mutate the ORIGINAL after the copy — the granted copy must stay independent.
        await SetNpcAttributeGastoAsync(gmToken, sourceId, "Forca", 9);

        var copiedAttributesAfter = await GetNpcAttributesAsync(gmToken, body.SheetId);
        copiedAttributesAfter.Single(a => a.Atributo == "Forca").Gasto.Should().Be(5);
    }

    [Fact]
    public async Task Grant_to_a_non_member_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("GrantGm3", "grant3@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "GrantPlayer3", "grantplayer3@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Grant Não Membro");
        // deliberately not adding playerId as a member

        var response = await GrantAsync(gmToken, campaignId, new GrantSheetRequest(playerId, "Npc", null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Grant_with_an_invalid_Tipo_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("GrantGm4", "grant4@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "GrantPlayer4", "grantplayer4@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Grant Tipo Inválido");
        await AddMemberAsync(gmToken, campaignId, playerId);

        var response = await GrantAsync(gmToken, campaignId, new GrantSheetRequest(playerId, "Vespa", null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Grant_from_an_existing_Creature_deep_copies_attributes_and_stays_independent()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("GrantGm5", "grant5@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "GrantPlayer5", "grantplayer5@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Grant Criatura");
        await AddMemberAsync(gmToken, campaignId, playerId);

        var sourceId = await CreateCreatureSheetAsync(gmToken);
        await SetCreatureAttributeGastoAsync(gmToken, sourceId, "Forca", 5);

        var response = await GrantAsync(gmToken, campaignId, new GrantSheetRequest(playerId, "Creature", sourceId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<GrantSheetResponse>();
        body!.Tipo.Should().Be("Creature");

        var sheet = await GetCreatureSheetAsync(gmToken, body.SheetId);
        sheet.OwnerId.Should().Be(playerId);

        var copiedAttributes = await GetCreatureAttributesAsync(gmToken, body.SheetId);
        copiedAttributes.Single(a => a.Atributo == "Forca").Gasto.Should().Be(5);

        // Mutate the ORIGINAL after the copy — the granted copy must stay independent.
        await SetCreatureAttributeGastoAsync(gmToken, sourceId, "Forca", 9);

        var copiedAttributesAfter = await GetCreatureAttributesAsync(gmToken, body.SheetId);
        copiedAttributesAfter.Single(a => a.Atributo == "Forca").Gasto.Should().Be(5);
    }

    [Fact]
    public async Task Grant_with_a_malformed_PlayerId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("GrantGm7", "grant7@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Grant PlayerId Malformado");

        var response = await GrantAsync(gmToken, campaignId, new GrantSheetRequest("not-a-guid", "Npc", null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Grant_with_a_malformed_SourceSheetId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("GrantGm8", "grant8@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "GrantPlayer8", "grantplayer8@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Grant SourceSheetId Malformado");
        await AddMemberAsync(gmToken, campaignId, playerId);

        var response = await GrantAsync(gmToken, campaignId, new GrantSheetRequest(playerId, "Npc", "not-a-guid"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Grant_a_copy_of_another_gms_Npc_sheet_returns_400()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("GrantGmOwner9", "grantowner9@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("GrantGmOther9", "grantother9@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmTokenOther, "GrantPlayer9", "grantplayer9@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOther, "Campanha Grant NPC Alheio");
        await AddMemberAsync(gmTokenOther, campaignId, playerId);

        var sourceId = await CreateNpcSheetAsync(gmTokenOwner);

        var response = await GrantAsync(gmTokenOther, campaignId, new GrantSheetRequest(playerId, "Npc", sourceId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Grant_a_copy_of_another_gms_Creature_sheet_returns_400()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("GrantGmOwner10", "grantowner10@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("GrantGmOther10", "grantother10@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmTokenOther, "GrantPlayer10", "grantplayer10@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOther, "Campanha Grant Criatura Alheia");
        await AddMemberAsync(gmTokenOther, campaignId, playerId);

        var sourceId = await CreateCreatureSheetAsync(gmTokenOwner);

        var response = await GrantAsync(gmTokenOther, campaignId, new GrantSheetRequest(playerId, "Creature", sourceId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Grant_scopes_the_players_meusCompanheiros_to_the_granting_campaign_only()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("GrantGm6", "grant6@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "GrantPlayer6", "grantplayer6@teste.com");
        var campaignA = await CreateCampaignAsync(gmToken, "Campanha Grant A");
        var campaignB = await CreateCampaignAsync(gmToken, "Campanha Grant B");
        await AddMemberAsync(gmToken, campaignA, playerId);
        await AddMemberAsync(gmToken, campaignB, playerId);

        var response = await GrantAsync(gmToken, campaignA, new GrantSheetRequest(playerId, "Npc", null));
        var body = await response.Content.ReadFromJsonAsync<GrantSheetResponse>();

        var viewAResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignA}/player-view", playerToken));
        var viewA = await viewAResponse.Content.ReadFromJsonAsync<PlayerCampaignViewResponse>();
        viewA!.MeusCompanheiros.Should().ContainSingle(c => c.Id == body!.SheetId);

        var viewBResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignB}/player-view", playerToken));
        var viewB = await viewBResponse.Content.ReadFromJsonAsync<PlayerCampaignViewResponse>();
        viewB!.MeusCompanheiros.Should().NotContain(c => c.Id == body!.SheetId);
    }

    [Fact]
    public async Task List_returns_the_existing_Npc_and_Creature_grants_for_the_campaign()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("GrantGm11", "grant11@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "GrantPlayer11", "grantplayer11@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Grant Lista");
        await AddMemberAsync(gmToken, campaignId, playerId);

        var npcGrantResponse = await GrantAsync(gmToken, campaignId, new GrantSheetRequest(playerId, "Npc", null));
        var npcGrant = await npcGrantResponse.Content.ReadFromJsonAsync<GrantSheetResponse>();
        var creatureGrantResponse = await GrantAsync(gmToken, campaignId, new GrantSheetRequest(playerId, "Creature", null));
        var creatureGrant = await creatureGrantResponse.Content.ReadFromJsonAsync<GrantSheetResponse>();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/grants", gmToken));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<GrantSummaryResponse>>();

        body!.Should().HaveCount(2);
        body.Should().Contain(g => g.SheetId == npcGrant!.SheetId && g.Tipo == "Npc" && g.PlayerNickname == "GrantPlayer11");
        body.Should().Contain(g => g.SheetId == creatureGrant!.SheetId && g.Tipo == "Creature" && g.PlayerNickname == "GrantPlayer11");
    }

    [Fact]
    public async Task List_for_a_campaign_owned_by_another_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("GrantGmOwner12", "grantowner12@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("GrantGmOther12", "grantother12@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha Grant Lista Alheia");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/grants", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
