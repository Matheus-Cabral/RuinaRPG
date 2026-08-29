using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

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

    private async Task<string> CreateCreatureSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.CreatureSheets.CreatureSheetResponse>())!.Id;
    }

    private static RuinaRPG.Contracts.CreatureSheets.UpdateCreatureSheetRequest ValidCreatureUpdate(string nome, int vitalidadeAtual) => new(
        null, nome, "Lobo", "Fisico", "Predador", "Terra", "F", 3, 200, 5, vitalidadeAtual, 8, 10, "Parcial");

    private async Task UpdateCreatureVitalidadeAsync(string gmToken, string creatureId, string nome, int vitalidadeAtual) =>
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{creatureId}", gmToken, ValidCreatureUpdate(nome, vitalidadeAtual)));

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
        // Vitalidade não pode exceder o Máximo (Ficha de Personagem 1.c) — Campeão Nível 5 → Vida
        // 24 (real Tabela de Vocação); Vigor 1 pushes o máximo pra 26, folga suficiente pro 25 pedido.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Vigor", playerToken, new UpdateCharacterAttributeRequest(1, 0, false)));
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
    public async Task AddParticipant_from_a_gm_owned_creature_copies_PV_once_and_it_becomes_independently_editable()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EpGm12", "ep12@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Participante Criatura");
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro Criatura");
        var creatureId = await CreateCreatureSheetAsync(gmToken);
        await UpdateCreatureVitalidadeAsync(gmToken, creatureId, "Lobo das Ruínas", 30);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmToken,
            new AddParticipantRequest(null, null, creatureId, 10)));

        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await addResponse.Content.ReadFromJsonAsync<EncounterParticipantResponse>();
        added!.PV.Should().Be(30);
        added.IsLiveSourced.Should().BeFalse();

        // Change the creature's VitalidadeAtual after the fact — the participant's copy must not follow.
        await UpdateCreatureVitalidadeAsync(gmToken, creatureId, "Lobo das Ruínas", 5);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/encounters/{encounterId}/participants", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<EncounterParticipantResponse>>();
        list!.Should().ContainSingle().Which.PV.Should().Be(30);
    }

    [Fact]
    public async Task AddParticipant_with_a_CreatureSheet_owned_by_a_different_gm_returns_400()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("EpGm13Owner", "ep13owner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("EpGm13Other", "ep13other@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOther, "Campanha Criatura Alheia");
        var encounterId = await CreateEncounterAsync(gmTokenOther, campaignId, "Encontro Criatura Alheia");
        var creatureId = await CreateCreatureSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmTokenOther,
            new AddParticipantRequest(null, null, creatureId, 10)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddParticipant_with_an_NpcSheet_granted_in_a_different_campaign_returns_400()
    {
        // Épico 5 item 1 of the gap audit: same-GM cross-campaign leak — a granted NPC/Criatura
        // is campaign-scoped via its grant-link CampaignAttachment row, not just by GmId. The GM
        // owns both campaigns here (same-tenant, no cross-account leak), but the encounter in
        // campaign B must not be able to pull in a companion granted only in campaign A.
        var gmToken = await RegisterGmAndGetTokenAsync("EpGm14", "ep14@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "EpPlayer14", "epplayer14@teste.com");
        var campaignA = await CreateCampaignAsync(gmToken, "Campanha A Vazamento");
        var campaignB = await CreateCampaignAsync(gmToken, "Campanha B Vazamento");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignA}/members", gmToken, new AddCampaignMemberRequest(playerId)));

        var grantResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignA}/grants", gmToken,
            new GrantSheetRequest(playerId, "Npc", null)));
        var npcId = (await grantResponse.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;

        var encounterInB = await CreateEncounterAsync(gmToken, campaignB, "Encontro Vazamento B");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterInB}/participants", gmToken,
            new AddParticipantRequest(null, npcId, null, 10)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddParticipant_with_a_CreatureSheet_granted_in_a_different_campaign_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EpGm15", "ep15@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "EpPlayer15", "epplayer15@teste.com");
        var campaignA = await CreateCampaignAsync(gmToken, "Campanha A Vazamento Criatura");
        var campaignB = await CreateCampaignAsync(gmToken, "Campanha B Vazamento Criatura");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignA}/members", gmToken, new AddCampaignMemberRequest(playerId)));

        var grantResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignA}/grants", gmToken,
            new GrantSheetRequest(playerId, "Creature", null)));
        var creatureId = (await grantResponse.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;

        var encounterInB = await CreateEncounterAsync(gmToken, campaignB, "Encontro Vazamento Criatura B");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterInB}/participants", gmToken,
            new AddParticipantRequest(null, null, creatureId, 10)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    [Fact]
    public async Task AddParticipant_with_a_CharacterSheet_from_a_different_campaign_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EpGm6", "ep6@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "EpPlayer6", "epplayer6@teste.com");
        var campaignA = await CreateCampaignAsync(gmToken, "Campanha A Sheet");
        var campaignB = await CreateCampaignAsync(gmToken, "Campanha B Encontro");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignA}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateCharacterSheetAsync(gmToken, campaignA, playerId);
        // Encounter lives in campaignB — sheetId belongs to campaignA.
        var encounterId = await CreateEncounterAsync(gmToken, campaignB, "Encontro B");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmToken,
            new AddParticipantRequest(sheetId, null, null, 10)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddParticipant_with_an_NpcSheet_owned_by_a_different_gm_returns_400()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("EpGmOwner7", "ep7owner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("EpGmOther7", "ep7other@teste.com");
        var npcId = await CreateNpcSheetAsync(gmTokenOwner);
        var campaignId = await CreateCampaignAsync(gmTokenOther, "Campanha do Outro GM");
        var encounterId = await CreateEncounterAsync(gmTokenOther, campaignId, "Encontro do Outro GM");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmTokenOther,
            new AddParticipantRequest(null, npcId, null, 10)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddParticipant_on_an_encounter_owned_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("EpGmOwner11", "ep11owner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("EpGmOther11", "ep11other@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha do Dono do Encontro");
        var encounterId = await CreateEncounterAsync(gmTokenOwner, campaignId, "Encontro do Dono");
        var npcId = await CreateNpcSheetAsync(gmTokenOther);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmTokenOther,
            new AddParticipantRequest(null, npcId, null, 10)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_a_non_live_sourced_participant_changes_PV_and_condicoes()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EpGm8", "ep8@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update NPC");
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro Update NPC");
        var npcId = await CreateNpcSheetAsync(gmToken);
        await UpdateNpcVitalidadeAsync(gmToken, npcId, "Goblin Editável", 30);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmToken,
            new AddParticipantRequest(null, npcId, null, 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<EncounterParticipantResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/encounters/{encounterId}/participants/{added!.Id}", gmToken,
            new UpdateParticipantRequest(15, 12, 5, 2, 1, new List<string> { "Enfraquecido", "Sangrando" })));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/encounters/{encounterId}/participants", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<EncounterParticipantResponse>>();
        var updated = list!.Should().ContainSingle().Which;
        updated.Iniciativa.Should().Be(15);
        updated.PV.Should().Be(12);
        updated.AcoesRestantes.Should().Be(1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var condicoes = await db.EncounterParticipantConditions
            .Where(c => c.EncounterParticipantId == Guid.Parse(added.Id))
            .Select(c => c.Texto)
            .ToListAsync();
        condicoes.Should().BeEquivalentTo(new[] { "Enfraquecido", "Sangrando" });
    }

    [Fact]
    public async Task Update_PV_on_a_live_sourced_participant_returns_400()
    {
        // R0003: PV/PF/PA are read-only on the Encounter screen for a live-sourced participant.
        var gmToken = await RegisterGmAndGetTokenAsync("EpGm9", "ep9@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "EpPlayer9", "epplayer9@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update Live");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro Update Live");
        var sheetId = await CreateCharacterSheetAsync(gmToken, campaignId, playerId);
        await UpdateCharacterVitalidadeAsync(playerToken, sheetId, "Vann Editável", 25);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmToken,
            new AddParticipantRequest(sheetId, null, null, 8)));
        var added = await addResponse.Content.ReadFromJsonAsync<EncounterParticipantResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/encounters/{encounterId}/participants/{added!.Id}", gmToken,
            new UpdateParticipantRequest(8, 5, null, null, 3, new List<string>())));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_returns_the_participants_current_Condicoes()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EpGm10", "ep10@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Condições");
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro Condições");
        var npcId = await CreateNpcSheetAsync(gmToken);
        await UpdateNpcVitalidadeAsync(gmToken, npcId, "Goblin Envenenado", 30);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmToken,
            new AddParticipantRequest(null, npcId, null, 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<EncounterParticipantResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/encounters/{encounterId}/participants/{added!.Id}", gmToken,
            new UpdateParticipantRequest(10, 12, 5, 2, 3, new List<string> { "Envenenado", "Enraizado" })));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/encounters/{encounterId}/participants", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<EncounterParticipantResponse>>();
        list!.Should().ContainSingle().Which.Condicoes.Should().BeEquivalentTo(new[] { "Envenenado", "Enraizado" });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public async Task Update_with_an_AcoesRestantes_outside_0_to_3_returns_400(int acoesRestantes)
    {
        var suffix = acoesRestantes < 0 ? "Neg" : acoesRestantes.ToString();
        var gmToken = await RegisterGmAndGetTokenAsync($"EpGm11{suffix}", $"ep11{suffix}@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha AcoesRestantes Inválido");
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro AcoesRestantes Inválido");
        var npcId = await CreateNpcSheetAsync(gmToken);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmToken,
            new AddParticipantRequest(null, npcId, null, 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<EncounterParticipantResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/encounters/{encounterId}/participants/{added!.Id}", gmToken,
            new UpdateParticipantRequest(10, null, null, null, acoesRestantes, new List<string>())));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
