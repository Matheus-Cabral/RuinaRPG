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
public class NpcCreaturePassivasTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private async Task<string> CreateNpcSheetAsync(string gmToken)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    private async Task<string> CreateCreatureSheetAsync(string gmToken)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
    }

    // Mesma chamada de GrantBlankNpcAsync (NpcSpellAbilitiesControllerTests), com a ficha de origem informada.
    private async Task<string> GrantNpcAsync(string gmToken, string campaignId, string playerId, string? sourceSheetId)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken,
            new GrantSheetRequest(playerId, "Npc", sourceSheetId)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;
    }

    private async Task PublishBankEntryAsync(string gmToken, string campaignId, string entryId)
    {
        var attach = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, entryId, null)));
        var attachmentId = (await attach.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/visibility", gmToken, true));
    }

    private async Task<string> CreatePassivaAsync(string gmToken, string nome, RequisitosDePassivaDto? requisitos)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
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

        var add = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gm,
            new AddNpcSpellAbilityRequest(passivaId, null, null, null, null, null)));
        add.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await add.Content.ReadAsStringAsync()).Should().Contain("Requisitos não cumpridos: Vocação: Bruxo");

        var scratch = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gm,
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

        var aceita = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gm,
            new AddCreatureSpellAbilityRequest(ignorada, null, null, null, null, null)));
        aceita.StatusCode.Should().Be(HttpStatusCode.Created);
        (await aceita.Content.ReadFromJsonAsync<CreatureSpellAbilityResponse>())!.RequisitosPendentes.Should().BeEmpty();

        var bloqueada = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gm,
            new AddCreatureSpellAbilityRequest(alta, null, null, null, null, null)));
        bloqueada.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await bloqueada.Content.ReadAsStringAsync()).Should().Contain("Nível 30");

        var scratch = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gm,
            new AddCreatureSpellAbilityRequest(null, "P", "Passiva", 0, "d", [])));
        scratch.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var disponiveis = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/spell-abilities/passivas-disponiveis", gm)))
            .Content.ReadFromJsonAsync<List<PassivaDisponivelResponse>>();
        disponiveis!.Single(p => p.Entrada.Nome == "De Bruxo").Pendencias.Should().BeEmpty();
        disponiveis!.Single(p => p.Entrada.Nome == "Alta").Pendencias.Should().Equal("Nível 30");
    }

    [Fact]
    public async Task Npc_adding_the_same_passiva_twice_is_refused()
    {
        // Finding 3 of the final whole-branch review: same rule as the Ficha de Personagem — a
        // Passiva is a scalar on/off per bank entry, so a second Add of the same bank entry on the
        // same sheet must be refused rather than creating a duplicate copy.
        var gm = await RegisterGmAndGetTokenAsync("NpcPassGm5", "npcpassgm5@teste.com");
        var sheetId = await CreateNpcSheetAsync(gm);
        var passivaId = await CreatePassivaAsync(gm, "Vigilante", null);

        var first = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gm,
            new AddNpcSpellAbilityRequest(passivaId, null, null, null, null, null)));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gm,
            new AddNpcSpellAbilityRequest(passivaId, null, null, null, null, null)));
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await second.Content.ReadAsStringAsync()).Should().Contain("Esta Passiva já está na ficha.");
    }

    [Fact]
    public async Task Creature_adding_the_same_passiva_twice_is_refused()
    {
        var gm = await RegisterGmAndGetTokenAsync("CrPassGm2", "crpassgm2@teste.com");
        var sheetId = await CreateCreatureSheetAsync(gm);
        var passivaId = await CreatePassivaAsync(gm, "Casco Duro", null);

        var first = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gm,
            new AddCreatureSpellAbilityRequest(passivaId, null, null, null, null, null)));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gm,
            new AddCreatureSpellAbilityRequest(passivaId, null, null, null, null, null)));
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await second.Content.ReadAsStringAsync()).Should().Contain("Esta Passiva já está na ficha.");
    }

    [Fact]
    public async Task Granting_an_npc_copies_its_passivas_categoria_and_requisitos()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPassGm2", "npcpassgm2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, "NpcPassPl2", "npcpasspl2@teste.com");
        var campaignId = await CreateCampaignAsync(gm, "Campanha");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gm, new AddCampaignMemberRequest(playerId)));
        var sourceId = await CreateNpcSheetAsync(gm);
        var passivaId = await CreatePassivaAsync(gm, "Vigia", new RequisitosDePassivaDto(Nivel: 1));
        (await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sourceId}/spell-abilities", gm,
            new AddNpcSpellAbilityRequest(passivaId, null, null, null, null, null)))).StatusCode.Should().Be(HttpStatusCode.Created);

        var grantedId = await GrantNpcAsync(gm, campaignId, playerId, sourceId);

        var list = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{grantedId}/spell-abilities", playerToken)))
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
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gm, new AddCampaignMemberRequest(playerId)));
        var grantedId = await GrantNpcAsync(gm, campaignId, playerId, null);
        var publica = await CreatePassivaAsync(gm, "Pública", null);
        await CreatePassivaAsync(gm, "Privada", null);
        await PublishBankEntryAsync(gm, campaignId, publica);

        var url = $"/api/npc-sheets/{grantedId}/spell-abilities/passivas-disponiveis";
        var paraJogador = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, url, playerToken))).Content.ReadFromJsonAsync<List<PassivaDisponivelResponse>>();
        paraJogador!.Select(p => p.Entrada.Nome).Should().Equal("Pública");

        var paraGm = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, url, gm))).Content.ReadFromJsonAsync<List<PassivaDisponivelResponse>>();
        paraGm!.Select(p => p.Entrada.Nome).Should().BeEquivalentTo(["Pública", "Privada"]);
    }

    [Fact]
    public async Task Granting_an_npc_whose_passiva_left_the_bank_recreates_it_with_categoria_and_requisitos()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPassGm4", "npcpassgm4@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gm, "NpcPassPl4", "npcpasspl4@teste.com");
        var campaignId = await CreateCampaignAsync(gm, "Campanha");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gm, new AddCampaignMemberRequest(playerId)));
        var sourceId = await CreateNpcSheetAsync(gm);
        var create = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gm,
            new CreateSpellAbilityEntryRequest("Sentinela", "Passiva", 0, "Descrição.", [], false, "Vocacional", new RequisitosDePassivaDto(Nivel: 1))));
        var passivaId = (await create.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!.Id;
        (await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sourceId}/spell-abilities", gm,
            new AddNpcSpellAbilityRequest(passivaId, null, null, null, null, null)))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/spell-ability-bank/{passivaId}", gm))).IsSuccessStatusCode.Should().BeTrue();

        await GrantNpcAsync(gm, campaignId, playerId, sourceId);

        var bank = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/spell-ability-bank?tipo=Passiva", gm)))
            .Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        var recriada = bank!.Single(e => e.Nome == "Sentinela");
        recriada.Id.Should().NotBe(passivaId);
        recriada.Categoria.Should().Be("Vocacional");
        recriada.Requisitos!.Nivel.Should().Be(1);
    }
}
