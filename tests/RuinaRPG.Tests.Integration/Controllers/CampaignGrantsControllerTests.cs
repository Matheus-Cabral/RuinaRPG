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

        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        var sourceId = (await createResponse.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;

        var response = await GrantAsync(gmToken, campaignId, new GrantSheetRequest(playerId, "Creature", sourceId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<GrantSheetResponse>();
        body!.Tipo.Should().Be("Creature");

        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{body.SheetId}", gmToken));
        var sheet = await getResponse.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        sheet!.OwnerId.Should().Be(playerId);
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
}
