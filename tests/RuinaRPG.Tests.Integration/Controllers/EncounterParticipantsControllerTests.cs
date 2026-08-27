using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class EncounterParticipantsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public EncounterParticipantsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> CreateEncounterAsync(string gmToken, string campaignId, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/encounters", gmToken, new CreateEncounterRequest(nome)));
        return (await response.Content.ReadFromJsonAsync<EncounterResponse>())!.Id;
    }

    private async Task<string> CreateNpcSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    private static UpdateNpcSheetRequest ValidNpcUpdate(string nome, int vitalidadeAtual) => new(
        null, nome, "Humano", "Sinir", "Campeao", "Duelista", "Fogo", "Descrição de Teste",
        5, true, 750, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, vitalidadeAtual, 15, 8, 3, "Parcial", 100);

    private async Task UpdateNpcVitalidadeAsync(string gmToken, string npcId, string nome, int vitalidadeAtual) =>
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{npcId}", gmToken, ValidNpcUpdate(nome, vitalidadeAtual)));

    private async Task<string> CreateCharacterSheetAsync(string gmToken, string campaignId, string playerId)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await response.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }

    private static UpdateCharacterSheetRequest ValidCharacterUpdate(string nome, int vitalidadeAtual) => new(
        null, nome, "Humano", "Sinir", "Campeao", "Duelista", "Fogo", "Descrição de Teste",
        5, true, 750, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, vitalidadeAtual, 15, 8, 3, "Parcial", 100);

    private async Task UpdateCharacterVitalidadeAsync(string playerToken, string sheetId, string nome, int vitalidadeAtual) =>
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidCharacterUpdate(nome, vitalidadeAtual)));

    [Fact]
    public async Task AddParticipant_from_a_gm_owned_npc_copies_PV_once_and_it_becomes_independently_editable()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EpGm1", "ep1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Participante NPC");
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro NPC");
        var npcId = await CreateNpcSheetAsync(gmToken);
        await UpdateNpcVitalidadeAsync(gmToken, npcId, "Goblin Batedor", 30);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmToken,
            new AddParticipantRequest(null, npcId, null, 10)));

        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await addResponse.Content.ReadFromJsonAsync<EncounterParticipantResponse>();
        added!.PV.Should().Be(30);
        added.IsLiveSourced.Should().BeFalse();

        // Change the NPC's VitalidadeAtual after the fact — the participant's copy must not follow.
        await UpdateNpcVitalidadeAsync(gmToken, npcId, "Goblin Batedor", 5);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/encounters/{encounterId}/participants", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<EncounterParticipantResponse>>();
        list!.Should().ContainSingle().Which.PV.Should().Be(30);
    }

    [Fact]
    public async Task AddParticipant_from_a_character_sheet_reflects_PV_live()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EpGm2", "ep2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "EpPlayer2", "epplayer2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Participante Personagem");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro Personagem");
        var sheetId = await CreateCharacterSheetAsync(gmToken, campaignId, playerId);
        await UpdateCharacterVitalidadeAsync(playerToken, sheetId, "Vann Astrel", 25);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmToken,
            new AddParticipantRequest(sheetId, null, null, 8)));

        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await addResponse.Content.ReadFromJsonAsync<EncounterParticipantResponse>();
        added!.PV.Should().Be(25);
        added.IsLiveSourced.Should().BeTrue();

        // Change the sheet's VitalidadeAtual after the fact — the participant must reflect it live.
        await UpdateCharacterVitalidadeAsync(playerToken, sheetId, "Vann Astrel", 5);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/encounters/{encounterId}/participants", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<EncounterParticipantResponse>>();
        list!.Should().ContainSingle().Which.PV.Should().Be(5);
    }

    [Fact]
    public async Task AddParticipant_with_two_sources_set_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EpGm3", "ep3@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "EpPlayer3", "epplayer3@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Duas Origens");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro Duas Origens");
        var npcId = await CreateNpcSheetAsync(gmToken);
        var sheetId = await CreateCharacterSheetAsync(gmToken, campaignId, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmToken,
            new AddParticipantRequest(sheetId, npcId, null, 10)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_orders_participants_by_Iniciativa_descending()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EpGm4", "ep4@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Ordem");
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro Ordem");

        var npcLow = await CreateNpcSheetAsync(gmToken);
        await UpdateNpcVitalidadeAsync(gmToken, npcLow, "Baixa Iniciativa", 10);
        var npcHigh = await CreateNpcSheetAsync(gmToken);
        await UpdateNpcVitalidadeAsync(gmToken, npcHigh, "Alta Iniciativa", 10);
        var npcMid = await CreateNpcSheetAsync(gmToken);
        await UpdateNpcVitalidadeAsync(gmToken, npcMid, "Media Iniciativa", 10);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmToken, new AddParticipantRequest(null, npcLow, null, 5)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmToken, new AddParticipantRequest(null, npcHigh, null, 20)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmToken, new AddParticipantRequest(null, npcMid, null, 10)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/encounters/{encounterId}/participants", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<EncounterParticipantResponse>>();
        body!.Should().HaveCount(3);
        body!.Select(p => p.Nome).Should().ContainInOrder("Alta Iniciativa", "Media Iniciativa", "Baixa Iniciativa");
        body!.Select(p => p.Iniciativa).Should().ContainInOrder(20, 10, 5);
    }

    [Fact]
    public async Task Add_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EpGm5", "ep5@teste.com");
        var (_, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "EpPlayer5", "epplayer5@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Jogador Proibido");
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro Jogador Proibido");
        var npcId = await CreateNpcSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", playerToken,
            new AddParticipantRequest(null, npcId, null, 10)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
