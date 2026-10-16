using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>Habilidades Passivas na Ficha de Personagem: só do Banco, bloqueio por requisitos e aviso.</summary>
public class CharacterPassivasTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private async Task<(string SheetId, string CampaignId)> SetUpSheetInCampaignAsync(string gmToken, string playerId)
    {
        var campaignResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return ((await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id, campaignId);
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

    private Task<HttpResponseMessage> AddFromBankAsync(string token, string sheetId, string entryId) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", token,
            new AddCharacterSpellAbilityRequest(entryId, null, null, null, null, null)));

    private Task SetForcaAsync(string token, string sheetId, int gasto) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Forca", token, new UpdateCharacterAttributeRequest(gasto, 0, false)));

    [Fact]
    public async Task Building_a_passiva_from_scratch_on_a_sheet_is_refused()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassGm1", "passgm1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gm, "PassPl1", "passpl1@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gm, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", gm,
            new AddCharacterSpellAbilityRequest(null, "P", "Passiva", 0, "d", [])));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Passivas só são cadastradas no Banco de Magias e Habilidades.");
    }

    [Fact]
    public async Task Unmet_requisitos_block_the_gm_and_the_player_alike()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassGm2", "passgm2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, "PassPl2", "passpl2@teste.com");
        var (sheetId, campaignId) = await SetUpSheetInCampaignAsync(gm, playerId);
        var passivaId = await CreatePassivaAsync(gm, "Força Bruta", new RequisitosDePassivaDto(Atributos: [new("Forca", 4)]));
        await PublishBankEntryAsync(gm, campaignId, passivaId);

        foreach (var token in new[] { gm, playerToken })
        {
            var response = await AddFromBankAsync(token, sheetId, passivaId);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("Requisitos não cumpridos: Força ≥ 4");
        }
    }

    [Fact]
    public async Task Met_requisitos_copy_categoria_and_requisitos_and_a_later_change_shows_the_warning()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassGm3", "passgm3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, "PassPl3", "passpl3@teste.com");
        var (sheetId, campaignId) = await SetUpSheetInCampaignAsync(gm, playerId);
        var passivaId = await CreatePassivaAsync(gm, "Força Bruta", new RequisitosDePassivaDto(Atributos: [new("Forca", 4)]));
        await PublishBankEntryAsync(gm, campaignId, passivaId);
        await SetForcaAsync(playerToken, sheetId, 5);

        var response = await AddFromBankAsync(playerToken, sheetId, passivaId);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<CharacterSpellAbilityResponse>())!;
        created.Tipo.Should().Be("Passiva");
        created.Categoria.Should().Be("Livre");
        created.RequisitosPendentes.Should().BeEmpty();

        await SetForcaAsync(playerToken, sheetId, 2);
        var list = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken)))
            .Content.ReadFromJsonAsync<List<CharacterSpellAbilityResponse>>();
        list!.Single(e => e.Id == created.Id).RequisitosPendentes.Should().Equal("Força ≥ 4");
    }

    [Fact]
    public async Task A_passiva_without_requisitos_is_always_addable()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassGm4", "passgm4@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gm, "PassPl4", "passpl4@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gm, playerId);
        var passivaId = await CreatePassivaAsync(gm, "Livre", null);

        (await AddFromBankAsync(gm, sheetId, passivaId)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_player_never_sees_the_requisitos_of_a_passiva_that_is_not_public()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassGm5", "passgm5@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, "PassPl5", "passpl5@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gm, playerId);
        var passivaId = await CreatePassivaAsync(gm, "Secreta", new RequisitosDePassivaDto(Nivel: 20));

        var response = await AddFromBankAsync(playerToken, sheetId, passivaId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("Entrada do banco não encontrada.");
        text.Should().NotContain("Nível");
    }

    [Fact]
    public async Task Passivas_disponiveis_lists_reachable_passivas_with_their_pendencias()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassGm6", "passgm6@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, "PassPl6", "passpl6@teste.com");
        var (sheetId, campaignId) = await SetUpSheetInCampaignAsync(gm, playerId);
        var publica = await CreatePassivaAsync(gm, "Pública", new RequisitosDePassivaDto(Nivel: 20));
        await CreatePassivaAsync(gm, "Privada", null);
        await PublishBankEntryAsync(gm, campaignId, publica);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gm,
            new CreateSpellAbilityEntryRequest("Uma Magia", "Magia", 1, "d", [])));

        var url = $"/api/character-sheets/{sheetId}/spell-abilities/passivas-disponiveis";
        var paraGm = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, url, gm))).Content.ReadFromJsonAsync<List<PassivaDisponivelResponse>>();
        paraGm!.Select(p => p.Entrada.Nome).Should().BeEquivalentTo(["Pública", "Privada"]);
        paraGm!.Single(p => p.Entrada.Nome == "Pública").Pendencias.Should().Equal("Nível 20");

        var paraJogador = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, url, playerToken))).Content.ReadFromJsonAsync<List<PassivaDisponivelResponse>>();
        paraJogador!.Select(p => p.Entrada.Nome).Should().Equal("Pública");
    }

    [Fact]
    public async Task Adding_the_same_passiva_twice_is_refused()
    {
        // Finding 3 of the final whole-branch review: unlike a Magia/Habilidade comum, a Passiva
        // is a scalar on/off per bank entry — the sheet must not end up with two independent copies
        // of the same one just because the player (or GM) clicked "Adicionar" twice.
        var gm = await RegisterGmAndGetTokenAsync("PassGm8", "passgm8@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gm, "PassPl8", "passpl8@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gm, playerId);
        var passivaId = await CreatePassivaAsync(gm, "Pele de Pedra", null);

        (await AddFromBankAsync(gm, sheetId, passivaId)).StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await AddFromBankAsync(gm, sheetId, passivaId);

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await second.Content.ReadAsStringAsync()).Should().Contain("Esta Passiva já está na ficha.");
    }

    [Fact]
    public async Task A_historico_requisito_is_checked_and_named()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassGm7", "passgm7@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gm, "PassPl7", "passpl7@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gm, playerId);
        var historicos = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/historicos", gm))).Content.ReadFromJsonAsync<List<HistoricoResponse>>();
        var historico = historicos!.First();
        var passivaId = await CreatePassivaAsync(gm, "De Berço", new RequisitosDePassivaDto(HistoricoId: historico.Id));

        var response = await AddFromBankAsync(gm, sheetId, passivaId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain($"Histórico: {historico.Nome}");
    }

    [Fact]
    public async Task A_passiva_does_not_touch_the_Pontos_de_Ignicao_balance_even_at_zero()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassGmPi", "passgmpi@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gm, "PassPlPi", "passplpi@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gm, playerId);
        var passivaId = await CreatePassivaAsync(gm, "Sem PI", null);
        await SetPiAsync(sheetId, 0);

        var added = await AddFromBankAsync(gm, sheetId, passivaId);
        added.StatusCode.Should().Be(HttpStatusCode.Created);
        (await GetPiAsync(sheetId)).Should().Be(0);

        var criada = (await added.Content.ReadFromJsonAsync<CharacterSpellAbilityResponse>())!;
        (await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/spell-abilities/{criada.Id}", gm)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetPiAsync(sheetId)).Should().Be(0);
    }

    private async Task SetPiAsync(string sheetId, int pi)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        (await db.CharacterSheets.FindAsync(Guid.Parse(sheetId)))!.PontosDeIgnicaoAtual = pi;
        await db.SaveChangesAsync();
    }

    private async Task<int> GetPiAsync(string sheetId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        return (await db.CharacterSheets.AsNoTracking().SingleAsync(s => s.Id == Guid.Parse(sheetId))).PontosDeIgnicaoAtual;
    }
}
