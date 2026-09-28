using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>Habilidades Passivas nas Fichas de NPC e de Criatura, e sua cópia numa concessão.</summary>
public class NpcCreaturePassivasTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public NpcCreaturePassivasTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> CreateNpcSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    private async Task<string> CreateCreatureSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
    }

    private async Task<string> CreateCampaignAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        return (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
    }

    // Mesma chamada de GrantBlankNpcAsync (NpcSpellAbilitiesControllerTests), com a ficha de origem informada.
    private async Task<string> GrantNpcAsync(string gmToken, string campaignId, string playerId, string? sourceSheetId)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken,
            new GrantSheetRequest(playerId, "Npc", sourceSheetId)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;
    }

    private async Task PublishBankEntryAsync(string gmToken, string campaignId, string entryId)
    {
        var attach = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, entryId, null)));
        var attachmentId = (await attach.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/visibility", gmToken, true));
    }

    private async Task<string> CreatePassivaAsync(string gmToken, string nome, RequisitosDePassivaDto? requisitos)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
            new CreateSpellAbilityEntryRequest(nome, "Passiva", 0, "Descrição.", [], false, "Livre", requisitos)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!.Id;
    }

    [Fact]
    public async Task Npc_add_from_bank_checks_requisitos_and_from_scratch_passiva_is_refused()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPassGm1", "npcpassgm1@teste.com");
        var sheetId = await CreateNpcSheetAsync(gm);
        var passivaId = await CreatePassivaAsync(gm, "Nobreza", new RequisitosDePassivaDto(Vocacao: "Bruxo"));

        var add = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gm,
            new AddNpcSpellAbilityRequest(passivaId, null, null, null, null, null)));
        add.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await add.Content.ReadAsStringAsync()).Should().Contain("Requisitos não cumpridos: Vocação: Bruxo");

        var scratch = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gm,
            new AddNpcSpellAbilityRequest(null, "P", "Passiva", 0, "d", [])));
        scratch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await scratch.Content.ReadAsStringAsync()).Should().Contain("Passivas só são cadastradas no Banco de Magias e Habilidades.");
    }

    [Fact]
    public async Task Creature_ignores_personagem_only_requisitos_but_checks_nivel()
    {
        var gm = await RegisterGmAndGetTokenAsync("CrPassGm1", "crpassgm1@teste.com");
        var sheetId = await CreateCreatureSheetAsync(gm);
        var ignorada = await CreatePassivaAsync(gm, "De Bruxo", new RequisitosDePassivaDto(Vocacao: "Bruxo", Estrela: "Sadir", Atributos: [new("Instinto", 99)]));
        var alta = await CreatePassivaAsync(gm, "Alta", new RequisitosDePassivaDto(Nivel: 30));

        var aceita = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gm,
            new AddCreatureSpellAbilityRequest(ignorada, null, null, null, null, null)));
        aceita.StatusCode.Should().Be(HttpStatusCode.Created);
        (await aceita.Content.ReadFromJsonAsync<CreatureSpellAbilityResponse>())!.RequisitosPendentes.Should().BeEmpty();

        var bloqueada = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gm,
            new AddCreatureSpellAbilityRequest(alta, null, null, null, null, null)));
        bloqueada.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await bloqueada.Content.ReadAsStringAsync()).Should().Contain("Nível 30");

        var scratch = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gm,
            new AddCreatureSpellAbilityRequest(null, "P", "Passiva", 0, "d", [])));
        scratch.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var disponiveis = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/spell-abilities/passivas-disponiveis", gm)))
            .Content.ReadFromJsonAsync<List<PassivaDisponivelResponse>>();
        disponiveis!.Single(p => p.Entrada.Nome == "De Bruxo").Pendencias.Should().BeEmpty();
        disponiveis!.Single(p => p.Entrada.Nome == "Alta").Pendencias.Should().Equal("Nível 30");
    }

    [Fact]
    public async Task Granting_an_npc_copies_its_passivas_categoria_and_requisitos()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPassGm2", "npcpassgm2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, "NpcPassPl2", "npcpasspl2@teste.com");
        var campaignId = await CreateCampaignAsync(gm, "Campanha");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gm, new AddCampaignMemberRequest(playerId)));
        var sourceId = await CreateNpcSheetAsync(gm);
        var passivaId = await CreatePassivaAsync(gm, "Vigia", new RequisitosDePassivaDto(Nivel: 1));
        (await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sourceId}/spell-abilities", gm,
            new AddNpcSpellAbilityRequest(passivaId, null, null, null, null, null)))).StatusCode.Should().Be(HttpStatusCode.Created);

        var grantedId = await GrantNpcAsync(gm, campaignId, playerId, sourceId);

        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{grantedId}/spell-abilities", playerToken)))
            .Content.ReadFromJsonAsync<List<NpcSpellAbilityResponse>>();
        var copy = list!.Single(e => e.Nome == "Vigia");
        copy.Categoria.Should().Be("Livre");
        copy.Requisitos!.Nivel.Should().Be(1);
    }

    [Fact]
    public async Task Npc_passivas_disponiveis_for_the_granted_player_lists_only_the_campaign_public_ones()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPassGm3", "npcpassgm3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, "NpcPassPl3", "npcpasspl3@teste.com");
        var campaignId = await CreateCampaignAsync(gm, "Campanha");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gm, new AddCampaignMemberRequest(playerId)));
        var grantedId = await GrantNpcAsync(gm, campaignId, playerId, null);
        var publica = await CreatePassivaAsync(gm, "Pública", null);
        await CreatePassivaAsync(gm, "Privada", null);
        await PublishBankEntryAsync(gm, campaignId, publica);

        var url = $"/api/npc-sheets/{grantedId}/spell-abilities/passivas-disponiveis";
        var paraJogador = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, url, playerToken))).Content.ReadFromJsonAsync<List<PassivaDisponivelResponse>>();
        paraJogador!.Select(p => p.Entrada.Nome).Should().Equal("Pública");

        var paraGm = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, url, gm))).Content.ReadFromJsonAsync<List<PassivaDisponivelResponse>>();
        paraGm!.Select(p => p.Entrada.Nome).Should().BeEquivalentTo(["Pública", "Privada"]);
    }
}
