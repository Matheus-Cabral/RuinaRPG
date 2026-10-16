using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterSpellAbilitiesControllerTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private async Task<string> SetUpSheetAsync(string gmToken, string playerId) =>
        (await SetUpSheetInCampaignAsync(gmToken, playerId)).SheetId;

    private async Task<(string SheetId, string CampaignId)> SetUpSheetInCampaignAsync(string gmToken, string playerId)
    {
        var campaignResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return ((await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id, campaignId);
    }

    private static AddCharacterSpellAbilityRequest BolaDeFogoFromScratch() => new(
        null, "Bola de Fogo", "Magia", 3, "Uma explosão de fogo.",
        [new SpellAbilityEffectRequest("Dano", 4, 8), new SpellAbilityEffectRequest("Alcance", 2, 6)]);

    private async Task<List<SpellAbilityEntryResponse>> GetBankAsync(string gmToken)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/spell-ability-bank", gmToken));
        return (await response.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>())!;
    }

    [Fact]
    public async Task AddFromScratch_creates_the_sheet_entry_and_an_independent_bank_copy()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasGm1", "magias1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPlayer1", "magiasplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 1000);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken, BolaDeFogoFromScratch()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CharacterSpellAbilityResponse>();
        body!.Nome.Should().Be("Bola de Fogo");
        body.Tipo.Should().Be("Magia");
        body.Grau.Should().Be(3);
        body.GastoEmPI.Should().Be(14); // 8 + 6
        body.Custo.Should().Be(18); // ceil(14 * 1.25) = 18
        body.Efeitos.Should().HaveCount(2);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<CharacterSpellAbilityResponse>>();
        list!.Should().ContainSingle(e => e.Nome == "Bola de Fogo");

        var bank = await GetBankAsync(gmToken);
        bank.Should().ContainSingle(e => e.Nome == "Bola de Fogo" && e.Tipo == "Magia" && e.Grau == 3 && e.GastoEmPI == 14 && e.Custo == 18);
    }

    // AddFromScratch_editing_the_sheet_copy_afterward_does_not_change_the_bank_copy: this plan
    // doesn't build a PUT for spell abilities — Ficha de Personagem's 4.b describes add/remove,
    // not edit-in-place — so there is no endpoint to exercise for this case; intentionally omitted.

    [Fact]
    public async Task AddFromBankEntry_copies_every_field_and_reuses_the_bank_entry_without_duplicating_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasGm2", "magias2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPlayer2", "magiasplayer2@teste.com");
        var (sheetId, campaignId) = await SetUpSheetInCampaignAsync(gmToken, playerId);

        var bankCreateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
            new CreateSpellAbilityEntryRequest("Cura Leve", "Habilidade", 1, "Restaura um pouco de vida.",
                [new SpellAbilityEffectRequest("Dano", 2, 4), new SpellAbilityEffectRequest("Cura", null, 2)])));
        var bankEntry = await bankCreateResponse.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>();

        // O jogador só alcança entradas que o GM liberou como públicas na campanha da ficha.
        await PublishBankEntryAsync(gmToken, campaignId, bankEntry!.Id);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken,
            new AddCharacterSpellAbilityRequest(bankEntry!.Id, null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var sheetCopy = await response.Content.ReadFromJsonAsync<CharacterSpellAbilityResponse>();
        sheetCopy!.Nome.Should().Be(bankEntry.Nome);
        sheetCopy.Tipo.Should().Be(bankEntry.Tipo);
        sheetCopy.Grau.Should().Be(bankEntry.Grau);
        sheetCopy.Descricao.Should().Be(bankEntry.Descricao);
        sheetCopy.GastoEmPI.Should().Be(bankEntry.GastoEmPI);
        sheetCopy.Custo.Should().Be(bankEntry.Custo);
        sheetCopy.Efeitos.Should().BeEquivalentTo(bankEntry.Efeitos);

        var bank = await GetBankAsync(gmToken);
        bank.Should().ContainSingle().Which.Id.Should().Be(bankEntry.Id); // escolher do banco não gera uma segunda entrada
        (await SourceBankEntryIdAsync(sheetCopy.Id)).Should().Be(Guid.Parse(bankEntry.Id));
    }

    [Fact]
    public async Task Delete_removes_only_the_sheet_copy()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasGm3", "magias3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPlayer3", "magiasplayer3@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 1000);
        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken, BolaDeFogoFromScratch()));
        var entryId = (await addResponse.Content.ReadFromJsonAsync<CharacterSpellAbilityResponse>())!.Id;

        (await GetBankAsync(gmToken)).Should().ContainSingle();

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/spell-abilities/{entryId}", playerToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<CharacterSpellAbilityResponse>>();
        list!.Should().NotContain(e => e.Id == entryId);

        (await GetBankAsync(gmToken)).Should().ContainSingle(); // untouched — independent copies
    }

    [Fact]
    public async Task Add_without_either_SourceBankEntryId_or_from_scratch_fields_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasGm4", "magias4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPlayer4", "magiasplayer4@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken,
            new AddCharacterSpellAbilityRequest(null, null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Add_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasGm5", "magias5@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPlayer5", "magiasplayer5@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPlayer5b", "magiasplayer5b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", otherToken, BolaDeFogoFromScratch()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<(string SheetId, string CampaignId)> SetUpSheetWithCampaignAsync(string gmToken, string playerId)
    {
        var campaignResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        var sheetId = (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
        return (sheetId, campaignId);
    }

    [Fact]
    public async Task AddFromScratch_by_the_player_auto_attaches_the_bank_copy_as_public()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasAutoGm1", "magiasauto1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasAutoPlayer1", "magiasautoplayer1@teste.com");
        var (sheetId, campaignId) = await SetUpSheetWithCampaignAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 1000);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken, BolaDeFogoFromScratch()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var availableResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/available-spell-abilities", playerToken));
        var available = await availableResponse.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        available!.Should().ContainSingle(e => e.Nome == "Bola de Fogo");
    }

    [Fact]
    public async Task Add_from_scratch_with_an_effect_missing_a_prerequisite_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CharSpellEfeitoGm", "charspellefeito@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharSpellEfeitoPlayer", "charspellefeitoplayer@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken,
            new AddCharacterSpellAbilityRequest(null, "Cura Sem Dano", "Magia", 1, "Descrição.",
                new List<SpellAbilityEffectRequest> { new("Cura", null, 2) })));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddFromScratch_by_the_gm_does_not_auto_attach()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasAutoGm2", "magiasauto2@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasAutoPlayer2", "magiasautoplayer2@teste.com");
        var (sheetId, campaignId) = await SetUpSheetWithCampaignAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 1000);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var availableResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/available-spell-abilities", gmToken));
        var available = await availableResponse.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        available!.Should().NotContain(e => e.Nome == "Bola de Fogo");
    }

    private async Task<string> CreateBankEntryAsync(string gmToken, string nome)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
            new CreateSpellAbilityEntryRequest(nome, "Magia", 1, "Descrição.", [])));
        return (await response.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!.Id;
    }

    private async Task PublishBankEntryAsync(string gmToken, string campaignId, string entryId)
    {
        var attach = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new RuinaRPG.Contracts.Campaigns.AttachToCampaignRequest(null, null, null, entryId, null)));
        var attachmentId = (await attach.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Campaigns.CampaignAttachmentResponse>())!.Id;
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/visibility", gmToken, true));
    }

    // Segurança: uma entrada do Banco de Magias é copiada para a ficha só se for do GM da ficha e, para um
    // jogador, só se o GM a anexou como pública à campanha da ficha (Requisitos - Ficha de Personagem R0003).
    [Fact]
    public async Task AddFromBankEntry_by_a_player_requires_the_entry_to_be_public_in_the_campaign()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SecMagCharPGm", "SecMagCharPgm@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SecMagCharPPl", "SecMagCharPpl@teste.com");
        var (sheetId, campaignId) = await SetUpSheetInCampaignAsync(gmToken, playerId);
        var entryId = await CreateBankEntryAsync(gmToken, "Magia Reservada");

        var antes = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken,
            new AddCharacterSpellAbilityRequest(entryId, null, null, null, null, null)));
        antes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await PublishBankEntryAsync(gmToken, campaignId, entryId);

        var depois = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken,
            new AddCharacterSpellAbilityRequest(entryId, null, null, null, null, null)));
        depois.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddFromBankEntry_never_accepts_another_gms_entry_not_even_for_the_gm()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SecMagCharGGm", "SecMagCharGgm@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SecMagCharGPl", "SecMagCharGpl@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var otherGmToken = await RegisterGmAndGetTokenAsync("SecMagCharOtherGm", "secmagcharothergm@teste.com");
        var foreignEntryId = await CreateBankEntryAsync(otherGmToken, "Magia do Outro GM");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", gmToken,
            new AddCharacterSpellAbilityRequest(foreignEntryId, null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Guid?> SourceBankEntryIdAsync(string sheetSpellAbilityId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        return await db.CharacterSpellAbilities.Where(e => e.Id == Guid.Parse(sheetSpellAbilityId)).Select(e => e.SourceBankEntryId).SingleAsync();
    }

    [Fact]
    public async Task AddFromScratch_links_the_sheet_entry_to_the_bank_entry_it_created()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasGmLink", "magiasgmlink@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPlayerLink", "magiasplayerlink@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 1000);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));
        var sheetCopy = await response.Content.ReadFromJsonAsync<CharacterSpellAbilityResponse>();

        var bankEntry = (await GetBankAsync(gmToken)).Should().ContainSingle().Subject;
        (await SourceBankEntryIdAsync(sheetCopy!.Id)).Should().Be(Guid.Parse(bankEntry.Id));
        bankEntry.DeCriatura.Should().Be(false);
    }

    [Fact]
    public async Task An_entry_built_from_scratch_is_flagged_CriadaNaFicha_and_one_from_the_bank_is_not()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasFlag1", "magiasflag1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasFlagP1", "magiasflagp1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 100);

        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));
        var bankId = (await GetBankAsync(gmToken)).Single(e => e.Nome == "Bola de Fogo").Id;
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", gmToken,
            new AddCharacterSpellAbilityRequest(bankId, null, null, null, null, null)));

        var list = (await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/spell-abilities", gmToken)))
            .Content.ReadFromJsonAsync<List<CharacterSpellAbilityResponse>>())!;
        list.Should().HaveCount(2);
        list.Count(e => e.CriadaNaFicha).Should().Be(1);
    }

    private async Task SetPiAsync(string sheetId, int pi)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        (await db.CharacterSheets.FindAsync(Guid.Parse(sheetId)))!.PontosDeIgnicaoAtual = pi;
        await db.SaveChangesAsync();
    }

    private static UpdateSheetSpellAbilityRequest BolaDeFogoEditada() => new(
        "Bola de Fogo Maior", "Magia", 3, "Explosão ampliada.",
        [new SpellAbilityEffectRequest("Dano", 4, 8)]);

    private async Task<CharacterSpellAbilityResponse> AddAsync(string token, string sheetId, AddCharacterSpellAbilityRequest request) =>
        (await (await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", token, request)))
            .Content.ReadFromJsonAsync<CharacterSpellAbilityResponse>())!;

    [Fact]
    public async Task Update_by_the_owner_changes_a_from_scratch_entry_and_leaves_the_bank_copy_alone()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasEdit1", "magiasedit1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasEditP1", "magiaseditp1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 100);
        var criada = await AddAsync(playerToken, sheetId, BolaDeFogoFromScratch());

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/spell-abilities/{criada.Id}", playerToken, BolaDeFogoEditada()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CharacterSpellAbilityResponse>();
        body!.Nome.Should().Be("Bola de Fogo Maior");
        body.Descricao.Should().Be("Explosão ampliada.");
        body.GastoEmPI.Should().Be(8);
        body.Custo.Should().Be(10); // ceil(8 * 1.25)
        body.Efeitos.Should().ContainSingle(e => e.EfeitoNome == "Dano");
        body.CriadaNaFicha.Should().BeTrue();
        (await GetBankAsync(gmToken)).Should().ContainSingle(e => e.Nome == "Bola de Fogo" && e.GastoEmPI == 14);
    }

    [Fact]
    public async Task Update_refuses_an_entry_that_came_from_the_bank()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasEdit2", "magiasedit2@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasEditP2", "magiaseditp2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 100);
        await AddAsync(gmToken, sheetId, BolaDeFogoFromScratch());
        var bankId = (await GetBankAsync(gmToken)).Single(e => e.Nome == "Bola de Fogo").Id;
        var doBanco = await AddAsync(gmToken, sheetId, new AddCharacterSpellAbilityRequest(bankId, null, null, null, null, null));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/spell-abilities/{doBanco.Id}", gmToken, BolaDeFogoEditada()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Só Magias e Habilidades montadas na ficha podem ser editadas.");
    }

    [Fact]
    public async Task Update_validates_like_a_from_scratch_add()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasEdit3", "magiasedit3@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasEditP3", "magiaseditp3@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 100);
        var criada = await AddAsync(gmToken, sheetId, BolaDeFogoFromScratch());
        var url = $"/api/character-sheets/{sheetId}/spell-abilities/{criada.Id}";

        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken, BolaDeFogoEditada() with { Tipo = "Passiva" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken, BolaDeFogoEditada() with { Tipo = "Inexistente" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken,
            BolaDeFogoEditada() with { Efeitos = [new SpellAbilityEffectRequest("Efeito Que Não Existe", 1, 1)] }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_by_someone_who_cannot_edit_the_sheet_is_refused_and_an_unknown_id_is_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasEdit4", "magiasedit4@teste.com");
        var outroGm = await RegisterGmAndGetTokenAsync("MagiasEdit4b", "magiasedit4b@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasEditP4", "magiaseditp4@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 100);
        var criada = await AddAsync(gmToken, sheetId, BolaDeFogoFromScratch());

        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/spell-abilities/{criada.Id}", outroGm, BolaDeFogoEditada())))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/spell-abilities/{Guid.NewGuid()}", gmToken, BolaDeFogoEditada())))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<int> GetPiAsync(string sheetId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        return (await db.CharacterSheets.AsNoTracking().SingleAsync(s => s.Id == Guid.Parse(sheetId))).PontosDeIgnicaoAtual;
    }

    [Fact]
    public async Task Add_is_refused_when_the_Gasto_em_PI_exceeds_the_balance_even_for_the_gm()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasPi1", "magiaspi1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPiP1", "magiaspip1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 13);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Pontos de Ignição insuficientes: custa 14 e a ficha tem 13.");
        (await GetPiAsync(sheetId)).Should().Be(13);
        (await GetBankAsync(gmToken)).Should().NotContain(e => e.Nome == "Bola de Fogo", "uma criação recusada não deixa cópia no banco");
    }

    [Fact]
    public async Task Add_with_exactly_enough_debits_the_balance_to_zero_and_Delete_refunds_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasPi2", "magiaspi2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPiP2", "magiaspip2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 14);

        var criada = await AddAsync(playerToken, sheetId, BolaDeFogoFromScratch());
        (await GetPiAsync(sheetId)).Should().Be(0);

        (await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/spell-abilities/{criada.Id}", playerToken)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetPiAsync(sheetId)).Should().Be(14);
    }

    [Fact]
    public async Task Add_from_the_bank_also_checks_and_debits()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasPi3", "magiaspi3@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPiP3", "magiaspip3@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 20);
        await AddAsync(gmToken, sheetId, BolaDeFogoFromScratch()); // 20 - 14 = 6
        var bankId = (await GetBankAsync(gmToken)).Single(e => e.Nome == "Bola de Fogo").Id;

        var recusada = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", gmToken,
            new AddCharacterSpellAbilityRequest(bankId, null, null, null, null, null)));
        recusada.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await SetPiAsync(sheetId, 14);
        var aceita = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", gmToken,
            new AddCharacterSpellAbilityRequest(bankId, null, null, null, null, null)));
        aceita.StatusCode.Should().Be(HttpStatusCode.Created);
        (await GetPiAsync(sheetId)).Should().Be(0);
    }

    [Fact]
    public async Task Update_settles_only_the_difference_in_both_directions()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasPi4", "magiaspi4@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPiP4", "magiaspip4@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 14);
        var criada = await AddAsync(gmToken, sheetId, BolaDeFogoFromScratch()); // gasto 14, saldo 0
        var url = $"/api/character-sheets/{sheetId}/spell-abilities/{criada.Id}";

        // Só o nome muda (diferença 0): aceito mesmo com saldo zero.
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken,
            new UpdateSheetSpellAbilityRequest("Outro Nome", "Magia", 3, "Uma explosão de fogo.",
                [new SpellAbilityEffectRequest("Dano", 4, 8), new SpellAbilityEffectRequest("Alcance", 2, 6)])))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetPiAsync(sheetId)).Should().Be(0);

        // Custo cai de 14 para 8: devolve 6.
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken, BolaDeFogoEditada()))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetPiAsync(sheetId)).Should().Be(6);

        // Custo sobe de 8 para 14: precisa de 6, tem 6.
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken,
            new UpdateSheetSpellAbilityRequest("Bola de Fogo", "Magia", 3, "x",
                [new SpellAbilityEffectRequest("Dano", 4, 8), new SpellAbilityEffectRequest("Alcance", 2, 6)])))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetPiAsync(sheetId)).Should().Be(0);

        // Voltar a 8 e tentar subir para 14 com saldo 5: recusado, nada muda.
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken, BolaDeFogoEditada()))).EnsureSuccessStatusCode();
        await SetPiAsync(sheetId, 5);
        var recusada = await Client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken,
            new UpdateSheetSpellAbilityRequest("Bola de Fogo", "Magia", 3, "x",
                [new SpellAbilityEffectRequest("Dano", 4, 8), new SpellAbilityEffectRequest("Alcance", 2, 6)])));
        recusada.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await recusada.Content.ReadAsStringAsync()).Should().Contain("Pontos de Ignição insuficientes: custa 6 e a ficha tem 5.");
        (await GetPiAsync(sheetId)).Should().Be(5);
    }

    [Fact]
    public async Task Deleting_an_entry_that_was_never_debited_still_refunds_its_Gasto_em_PI()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasPi5", "magiaspi5@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPiP5", "magiaspip5@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var legadaId = Guid.NewGuid();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            db.CharacterSpellAbilities.Add(new RuinaRPG.Infrastructure.CharacterSheets.CharacterSpellAbility
            {
                Id = legadaId, CharacterSheetId = Guid.Parse(sheetId), Nome = "Antiga", Tipo = RuinaRPG.Domain.SpellsAndAbilities.SpellAbilityTipo.Magia,
                Grau = 1, GastoEmPI = 9, Custo = 12, Descricao = "anterior à 1.5.0",
            });
            await db.SaveChangesAsync();
        }
        await SetPiAsync(sheetId, 2);

        (await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/spell-abilities/{legadaId}", gmToken))).EnsureSuccessStatusCode();

        (await GetPiAsync(sheetId)).Should().Be(11);
    }

    [Fact]
    public async Task Update_refuses_a_from_scratch_entry_whose_stored_Tipo_does_not_consume_PI()
    {
        // Uma Racial montada na ficha é CriadaNaFicha mas nunca foi debitada: convertê-la em Magia por
        // aqui custaria quase nada e a remoção devolveria o gasto inteiro.
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasFix1", "magiasfix1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasFixP1", "magiasfixp1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 100);
        var racial = await AddAsync(gmToken, sheetId, BolaDeFogoFromScratch() with { Tipo = "Racial" });
        var saldo = await GetPiAsync(sheetId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/spell-abilities/{racial.Id}", gmToken, BolaDeFogoEditada()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Só Magias e Habilidades montadas na ficha podem ser editadas.");
        var list = (await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/spell-abilities", gmToken)))
            .Content.ReadFromJsonAsync<List<CharacterSpellAbilityResponse>>())!;
        list.Should().ContainSingle(e => e.Id == racial.Id && e.Nome == "Bola de Fogo" && e.Tipo == "Racial" && e.GastoEmPI == 14 && e.Efeitos.Count == 2);
        (await GetPiAsync(sheetId)).Should().Be(saldo);
    }

    [Fact]
    public async Task A_stale_sheet_form_cannot_erase_a_debit()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasFix2", "magiasfix2@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasFixP2", "magiasfixp2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 14);
        await AddAsync(gmToken, sheetId, BolaDeFogoFromScratch());
        (await GetPiAsync(sheetId)).Should().Be(0);

        // O formulário da ficha, aberto antes do débito, ainda traz 14 no campo de PI.
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", gmToken,
            new UpdateCharacterSheetRequest(null, "Vann Astrel", "Humano", "Sinir", "Campeao", "Duelista", null, null,
                true, 0, 0, 0, 0, 0, 0, 0, 0, 0, 14, 0, 0, 0, 0, 0, "Nenhuma", 0, 0, null, null, 0, null, null, 0)))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetPiAsync(sheetId)).Should().Be(0);
    }
}
