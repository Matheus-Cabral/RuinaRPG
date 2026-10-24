using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Infrastructure.Rules.Niveis;

using RuinaRPG.Tests.Integration.Controllers.Fichas;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterSheetsControllerTests(ApiFixture fixture) : FichaDePersonagemOuNpcTestes<TipoDeTestePersonagem>(fixture)
{
    protected override object AtualizacaoValida() => ValidUpdate();

    protected override Task DefinirXpAsync(FichaDeTeste f, int xp) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{f.SheetId}", f.Token, ValidUpdate() with { ExperienciaAtual = xp }));

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await Client.PostAsJsonAsync("/api/campaigns/00000000-0000-0000-0000-000000000000/character-sheets", new CreateCharacterSheetRequest("00000000-0000-0000-0000-000000000000"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_for_a_campaign_member_returns_201_with_Nivel_1_and_no_other_field_set()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGm1", "sheet1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayer1", "sheetplayer1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Ficha");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.OwnerId.Should().Be(playerId);
        body.Nivel.Should().Be(1);
        body.Nome.Should().BeNull();
    }

    [Fact]
    public async Task Create_for_a_non_member_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGm2", "sheet2@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayer2", "sheetplayer2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha sem Membro");
        // note: playerId is NOT added as a member of this campaign

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGm3", "sheet3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayer3", "sheetplayer3@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Jogador");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", playerToken, new CreateCharacterSheetRequest(playerId)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_for_a_campaign_owned_by_another_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("SheetGmCreateOwner", "sheetcreateowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("SheetGmCreateOther", "sheetcreateother@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmTokenOwner, "SheetPlayerCreateOwner", "sheetplayercreateowner@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha do Dono para Criação");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmTokenOwner, new AddCampaignMemberRequest(playerId)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmTokenOther, new CreateCharacterSheetRequest(playerId)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_with_a_malformed_OwnerId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmMalformed", "sheetmalformed@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha OwnerId Inválido");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest("not-a-guid")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_an_owned_sheet_returns_204()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmDelete1", "sheetdelete1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerDelete1", "sheetplayerdelete1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Delete");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var createResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        var sheetId = (await createResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_a_sheet_in_a_campaign_owned_by_another_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("SheetGmDeleteOwner", "sheetdeleteowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("SheetGmDeleteOther", "sheetdeleteother@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmTokenOwner, "SheetPlayerDeleteOwner", "sheetplayerdeleteowner@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha do Dono");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmTokenOwner, new AddCampaignMemberRequest(playerId)));
        var createResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmTokenOwner, new CreateCharacterSheetRequest(playerId)));
        var sheetId = (await createResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<string> CreateSheetForMemberAsync(string gmToken, string campaignId, string playerId)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await response.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }

    private static UpdateCharacterSheetRequest ValidUpdate() => new(
        null, "Vann Astrel", "Humano", "Sinir", "Campeao", "Duelista", null, "Marcado pela Ruína",
        // 749 XP is one below Nível 6's threshold (750) — Nível is derived now, and reaching a
        // threshold exactly already counts as that Nível, so 749 keeps this at Nível 5.
        true, 749, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100, 0, null, null, 0, null, null, 0);

    [Fact]
    public async Task Get_returns_the_sheet()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmGet1", "sheetget1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerGet1", "sheetplayerget1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Get");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_by_an_unrelated_jogador_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmGetU1", "sheetgetu1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerGetU1", "sheetplayergetu1@teste.com");
        var (_, otherPlayerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerGetU1b", "sheetplayergetu1b@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Get Unrelated Jogador");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", otherPlayerToken));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_by_an_unrelated_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("SheetGmGetOwner", "sheetgetowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("SheetGmGetOther", "sheetgetother@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmTokenOwner, "SheetPlayerGetOwner", "sheetplayergetowner@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha Get Unrelated GM");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmTokenOwner, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmTokenOwner, campaignId, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListForCampaign_returns_the_campaigns_sheets()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmList1", "sheetlist1@teste.com");
        var (playerId1, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerList1", "sheetplayerlist1@teste.com");
        var (playerId2, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerList2", "sheetplayerlist2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Lista de Fichas");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId1)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId2)));
        var sheetId1 = await CreateSheetForMemberAsync(gmToken, campaignId, playerId1);
        var sheetId2 = await CreateSheetForMemberAsync(gmToken, campaignId, playerId2);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/character-sheets", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<CharacterSheetResponse>>();
        body!.Select(s => s.Id).Should().BeEquivalentTo(new[] { sheetId1, sheetId2 });
    }

    [Fact]
    public async Task ListForCampaign_for_a_campaign_owned_by_another_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("SheetGmListOwner", "sheetlistowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("SheetGmListOther", "sheetlistother@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha Lista de Outro GM");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/character-sheets", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_by_the_owner_returns_204_and_persists_every_field()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdate1", "sheetupdate1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdate1", "sheetplayerupdate1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // Campeão Nível 5 → Vida 24 (real Tabela de Vocação). Vigor total 3 → Vitalidade máximo
        // 3*2+24 = 30, exactly ValidUpdate()'s VitalidadeAtual, so the clamp added for "Atual não
        // pode exceder o máximo" (1.c) doesn't interfere with this test's real purpose (every
        // other field round-trips unchanged).
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Vigor", playerToken, new UpdateAttributeRequest(3, 0, false)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate()));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
        var body = await getResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.Nome.Should().Be("Vann Astrel");
        body.Linhagem.Should().Be("Humano");
        body.Variante.Should().Be("Sinir");
        body.Nivel.Should().Be(5);
        body.VitalidadeAtual.Should().Be(30);
    }

    [Fact]
    public async Task Update_by_the_campaigns_gm_returns_204()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdate2", "sheetupdate2@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdate2", "sheetplayerupdate2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update GM");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", gmToken, ValidUpdate()));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Update_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdate3", "sheetupdate3@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdate3", "sheetplayerupdate3@teste.com");
        var (_, otherPlayerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdate3b", "sheetplayerupdate3b@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update Unrelated");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", otherPlayerToken, ValidUpdate()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EAPAtual_is_the_level_base_plus_the_Ambares_by_Rank()
    {
        // Ficha de Personagem 1.b: "Segue a tabela ... e é somado pelo resultado de Âmbares
        // Absorvidos". Nível 1 base = 0 (Tabelas de XP.../EAP). 2 Rank F (5 each) + 1 Rank C (120) = 130.
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmEap1", "sheeteap1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerEap1", "sheetplayereap1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha EAP");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // Nível is derived from ExperienciaAtual now — 0 XP computes to Nível 1 (real Tabela de XP).
        var update = ValidUpdate() with { ExperienciaAtual = 0, NucleosRankF = 2, NucleosRankC = 1 };
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
        var body = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.EAPAtual.Should().Be(130); // 0 (Nível 1 base) + 2*5 + 1*120
    }

    [Fact]
    public async Task Get_labels_Graduacao_as_Grau_for_Campeao_and_computes_it_from_EAP()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmGrad1", "sheetgrad1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerGrad1", "sheetplayergrad1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Graduacao");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // EAPAtual is computed, not settable — Nível 6 gives EAP = (6-1)*30 = 150 (EapCalculator,
        // 0 Âmbares) via the real EAP-por-Nível table. 1049 XP is one below Nível 7's threshold
        // (1050), keeping this at Nível 6 — Nível is derived, so ExperienciaAtual drives it here
        // instead of setting it directly.
        var update = ValidUpdate() with { Vocacao = "Campeao", ExperienciaAtual = 1049 };
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
        var body = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.GraduacaoLabel.Should().Be("Grau");
        body.EAPAtual.Should().Be(150);
        body.Graduacao.Should().Be(1); // 150 EAP >= the real Tabela's Grau 1 threshold (100)
    }

    [Fact]
    public async Task Get_gives_a_fresh_Campeao_at_zero_XP_Grau_1()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmGrad3", "sheetgrad3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerGrad3", "sheetplayergrad3@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Graduacao 3");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var update = ValidUpdate() with { Vocacao = "Campeao", ExperienciaAtual = 0 };
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
        var body = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.EAPAtual.Should().Be(0);
        body.Graduacao.Should().Be(1);
    }

    [Fact]
    public async Task Get_labels_Graduacao_as_Circulo_for_a_magic_vocacao()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmGrad2", "sheetgrad2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerGrad2", "sheetplayergrad2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Graduacao 2");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var update = ValidUpdate() with { Vocacao = "Feiticeiro", PossuiCoracaoDeMana = false, ExperienciaAtual = 1049 };
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
        var body = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.GraduacaoLabel.Should().Be("Círculo");
        body.Graduacao.Should().Be(0); // no coração de mana → always 0 regardless of EAP
    }

    [Fact]
    public async Task Get_computes_vitalidade_and_foco_maximo_from_vigor_astucia_and_vocacao()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmMax1", "sheetmax1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerMax1", "sheetplayermax1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Maximo");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // Campeão Nível 1 → Vida 8, Arcana 4 (real Tabela de Vocação excerpt, same as
        // VocacaoProgressaoParserTests). This vocação is specifically chosen because it
        // exercises the accented-name lookup bug (Campeão/Caçador) fixed in this task.
        var update = ValidUpdate() with { Vocacao = "Campeao", ExperienciaAtual = 0 };
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

        // Vigor total = Gasto(5) + Bonus(0)/2 (sem maestria) = 5 → Vitalidade = 5*2 + 8 = 18.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Vigor", playerToken, new UpdateAttributeRequest(5, 0, false)));
        // Astúcia total = Gasto(3) + Bonus(0)/2 (sem maestria) = 3 → Foco = 3*2 + 4 = 10.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Astucia", playerToken, new UpdateAttributeRequest(3, 0, false)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));

        var body = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.VitalidadeMaximo.Should().Be(18);
        body.FocoMaximo.Should().Be(10);
        body.AdrenalinaMaximo.Should().Be(10); // 10 + Artefato bonus (nenhum equipado aqui → 0; ver o teste dedicado abaixo)
        body.EstresseMaximo.Should().Be(10); // flat
    }

    [Fact]
    public async Task Update_clamps_every_Atual_resource_to_its_own_Maximo_instead_of_rejecting()
    {
        // Ficha de Personagem 1.c: "Atual não pode exceder o máximo" (Vitalidade/Foco/PA/Estresse).
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmClamp1", "sheetclamp1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerClamp1", "sheetplayerclamp1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Clamp");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // Campeão Nível 1 → Vitalidade máximo 18, Foco máximo 10 (see the test above); Adrenalina
        // and Estresse máximos (10 each) don't depend on attributes at all. Attributes are set
        // BEFORE the sheet PUT below, since clamping happens against the máximo at save time.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Vigor", playerToken, new UpdateAttributeRequest(5, 0, false)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Astucia", playerToken, new UpdateAttributeRequest(3, 0, false)));

        var update = ValidUpdate() with
        {
            Vocacao = "Campeao", ExperienciaAtual = 0,
            VitalidadeAtual = 999, FocoAtual = 999, AdrenalinaAtual = 999, EstresseAtual = 999
        };
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
        var body = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.VitalidadeAtual.Should().Be(18);
        body.FocoAtual.Should().Be(10);
        body.AdrenalinaAtual.Should().Be(10);
        body.EstresseAtual.Should().Be(10);
    }

    [Fact]
    public async Task Dismiss_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmLevelUp3", "sheetlevelup3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerLevelUp3", "sheetplayerlevelup3@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerLevelUp3b", "sheetplayerlevelup3b@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha LevelUp 3");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/dismiss-level-up-notice", otherToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_with_a_malformed_ImageId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdImg", "sheetupdimg@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdImg", "sheetplayerupdimg@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update ImageId Invalido");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var invalid = ValidUpdate() with { ImageId = "not-a-guid" };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_with_a_malformed_Cobertura_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdCob", "sheetupdcob@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdCob", "sheetplayerupdcob@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update Cobertura Invalida");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var invalid = ValidUpdate() with { Cobertura = "NaoExiste" };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_with_a_garbage_Vocacao_returns_400_instead_of_silently_saving_null()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdVoc", "sheetupdvoc@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdVoc", "sheetplayerupdvoc@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update Vocacao Invalida");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var invalid = ValidUpdate() with { Vocacao = "Xyz" };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_with_a_garbage_Afinidade_returns_400_instead_of_silently_saving_null()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdAfin", "sheetupdafin@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdAfin", "sheetplayerupdafin@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update Afinidade Invalida");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var invalid = ValidUpdate() with { Afinidade = "Xyz" };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public async Task Update_with_SinaAtual_outside_0_to_3_returns_400(int sinaAtual)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"SheetGmUpdSina{sinaAtual}", $"sheetupdsina{sinaAtual}@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, $"SheetPlayerUpdSina{sinaAtual}", $"sheetplayerupdsina{sinaAtual}@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, $"Campanha Update Sina Invalida {sinaAtual}");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var invalid = ValidUpdate() with { SinaAtual = sinaAtual };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_persists_Estrela_and_SinaAtual()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdEst2", "sheetupdest2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdEst2", "sheetplayerupdest2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update Estrela Valida");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var update = ValidUpdate() with { Estrela = "Vaelen", SinaAtual = 2 };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
        var body = await getResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.Estrela.Should().Be("Vaelen");
        body.SinaAtual.Should().Be(2);
    }

    [Fact]
    public async Task GetSheet_surfaces_the_sheet_s_real_EquipmentKitId_once_one_is_set()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitRealGm", "equipkitreal@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "EquipKitRealPlayer", "equipkitrealplayer@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Equip Kit Real");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        Guid kitId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            kitId = (await db.EquipmentKits.FirstAsync()).Id;
            var sheet = await db.CharacterSheets.FindAsync(Guid.Parse(sheetId));
            sheet!.EquipmentKitId = kitId;
            await db.SaveChangesAsync();
        }

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", gmToken));
        var body = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.EquipmentKitId.Should().Be(kitId.ToString());
    }

    [Fact]
    public async Task Update_allows_an_Afinidade_liberada_pela_Vocacao_atual()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmAfin2", "sheetafin2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerAfin2", "sheetplayerafin2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Afinidade 2");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // Feiticeiro libera Dobra+Transmutação — Gelo é de Transmutação.
        var update = ValidUpdate() with { Vocacao = "Feiticeiro", Afinidade = "Gelo" };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Update_keeps_an_old_Afinidade_that_no_longer_fits_a_new_Vocacao_when_resubmitted_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmAfin3", "sheetafin3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerAfin3", "sheetplayerafin3@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Afinidade 3");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // Feiticeiro libera Gelo (Transmutação).
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken,
            ValidUpdate() with { Vocacao = "Feiticeiro", Afinidade = "Gelo" }));

        // Troca pra Adepto (não libera Transmutação) reenviando a MESMA Afinidade — não deve ser bloqueado.
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken,
            ValidUpdate() with { Vocacao = "Adepto", Afinidade = "Gelo" }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var getResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
        var body = await getResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.Afinidade.Should().Be("Gelo");
    }

    [Fact]
    public async Task Create_seeds_8_zeroed_attributes_and_39_zeroed_skills()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();

        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmSeed1", "sheetseed1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSeed1", "sheetplayerseed1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Seed");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var attributes = await db.CharacterAttributes.Where(a => a.SheetId == Guid.Parse(sheetId)).ToListAsync();
        var skills = await db.CharacterSkills.Where(s => s.SheetId == Guid.Parse(sheetId)).ToListAsync();

        attributes.Should().HaveCount(8);
        attributes.Should().OnlyContain(a => a.Gasto == 0 && a.Bonus == 0 && !a.TemMaestria);
        skills.Should().HaveCount(39);
        skills.Should().OnlyContain(s => s.Gasto == 0);
    }

    [Fact]
    public async Task SubAttributes_includes_the_Historico_bonus_on_Prontidao_and_Reflexos()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmSubHist1", "sheetsubhist1@teste.com");
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == "SHEETSUBHIST1@TESTE.COM");
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();

        var historicoResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Teste de Bônus Sub-Atributo", "Descrição de teste.", "Prontidao", "Reflexos")));
        var historico = await historicoResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSubHist1", "sheetplayersubhist1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha SubAttr Historico");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // Cobertura is pinned to "Nenhuma" (ValidUpdate()'s default is "Parcial", which would add
        // +5 to DefesaNatural via SubAttributes' cobertura term and break the "0 cobertura" assertion below).
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken,
            ValidUpdate() with { HistoricoId = historico!.Id, Cobertura = "Nenhuma" }));

        // Agilidade Gasto 4, Vigor Gasto 4, no bônus/maestria/artefato → Total 4 each.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Agilidade", playerToken,
            new UpdateAttributeRequest(4, 0, false)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Vigor", playerToken,
            new UpdateAttributeRequest(4, 0, false)));

        // Prontidao Gasto 9, Histórico bonus 6 counts as Pericia points -> Modificador (9+6)/3 = 5.
        // Reflexos Gasto 6, Histórico bonus 3 -> Modificador (6+3)/3 = 3.
        // Fortitude Gasto 3 -> Modificador 1, untouched by this Histórico.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Prontidao", playerToken,
            new UpdateSkillRequest(9, null)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Reflexos", playerToken,
            new UpdateSkillRequest(6, null)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Fortitude", playerToken,
            new UpdateSkillRequest(3, null)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.Iniciativa.Should().Be(9); // agilidade 4 + brutoProntidao ((9+6)/3=5) + 0 artefato
        body.EsquivaNatural.Should().Be(7); // agilidade 4 + brutoReflexos ((6+3)/3=3) + 0 artefatos - 0 penalidade
        body.DefesaNatural.Should().Be(5); // vigor 4 + brutoFortitude 1 (no bonus) + 0 escudo + 0 artefatos + 0 cobertura
    }

    private async Task<string> CreateArtefatoItemAsync(string gmToken, string nome, string tipoDeAlvo, string alvo, int valor)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new RuinaRPG.Contracts.Items.CreateItemRequest("Artefato", nome, 0.1m, 500, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, tipoDeAlvo, alvo, valor, null)));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Items.ItemResponse>())!.Id;
    }

    [Fact]
    public async Task SubAttributes_sums_Artefatos_targeting_SubAtributo_terms()
    {
        // Formulas.md: every Sub-Atributo formula has an "Artefato(s)" term — Requisitos - Ficha de
        // Personagem 2.b — sourced from equipped Artefatos whose TipoDeAlvo=SubAtributo and Alvo
        // matches the term's canonical name (SubAtributoAlvo).
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmSub5", "sheetsub5@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSub5", "sheetplayersub5@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha SubAttr Artefato");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var iniciativaArtifactId = await CreateArtefatoItemAsync(gmToken, "Amuleto da Presteza", "SubAtributo", "Iniciativa", 4);
        var reducaoFisicaArtifactId = await CreateArtefatoItemAsync(gmToken, "Bracelete de Ferro", "SubAtributo", "Redução Física", 2);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", playerToken, new AddArtifactRequest(iniciativaArtifactId)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", playerToken, new AddArtifactRequest(reducaoFisicaArtifactId)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.Iniciativa.Should().Be(4); // Agilidade(0) + Bruto Prontidão(0) + Artefato(4)
        body.ReducaoFisica.Should().Be(2); // Artefato(2) + Armadura(0)
        body.ReducaoMagica.Should().Be(0); // unaffected — different Alvo
    }

    private async Task<(string SheetId, string PlayerToken, string CampaignId, string GmToken)> NewSheetWithForcaVigorAsync(string tag, int forcaVigor)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"SheetGmBc{tag}", $"sheetbc{tag}@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, $"SheetPlayerBc{tag}", $"sheetplayerbc{tag}@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, $"Campanha Bc {tag}");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Forca", playerToken, new UpdateAttributeRequest(forcaVigor, 0, false)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Vigor", playerToken, new UpdateAttributeRequest(forcaVigor, 0, false)));
        return (sheetId, playerToken, campaignId, gmToken);
    }

    private async Task SetBonusDeCargaAsync(string gmToken, string campaignId, decimal bonus)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}", gmToken, new UpdateCampaignRequest($"Campanha Bc", "", null, bonus)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<SubAttributesResponse> GetSubAttributesAsync(string sheetId, string token)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<SubAttributesResponse>())!;
    }

    [Fact]
    public async Task SubAttributes_campaign_BonusDeCarga_is_added_to_PesoMaximo_and_reported()
    {
        var (sheetId, playerToken, campaignId, gmToken) = await NewSheetWithForcaVigorAsync("1", 4);
        (await GetSubAttributesAsync(sheetId, playerToken)).PesoMaximo.Should().Be(4m); // floor((4+4)/2)

        await SetBonusDeCargaAsync(gmToken, campaignId, 10m);

        var body = await GetSubAttributesAsync(sheetId, playerToken);
        body.PesoMaximo.Should().Be(14m);
        body.BonusDeCargaDaCampanha.Should().Be(10m);
    }

    [Fact]
    public async Task SubAttributes_negative_campaign_BonusDeCarga_lowers_PesoMaximo_but_never_below_zero()
    {
        var (sheetId, playerToken, campaignId, gmToken) = await NewSheetWithForcaVigorAsync("2", 4);

        await SetBonusDeCargaAsync(gmToken, campaignId, -3m);
        var lowered = await GetSubAttributesAsync(sheetId, playerToken);
        lowered.PesoMaximo.Should().Be(1m);
        lowered.BonusDeCargaDaCampanha.Should().Be(-3m);

        await SetBonusDeCargaAsync(gmToken, campaignId, -50m);
        (await GetSubAttributesAsync(sheetId, playerToken)).PesoMaximo.Should().Be(0m);
    }

    [Fact]
    public async Task SubAttributes_Movimentacao_overweight_penalty_follows_the_campaign_BonusDeCarga()
    {
        var (sheetId, playerToken, campaignId, gmToken) = await NewSheetWithForcaVigorAsync("3", 2);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Agilidade", playerToken, new UpdateAttributeRequest(4, 0, false)));
        var itemId = await CreateItemGeralAsync(gmToken, "Pedra", peso: 4m, capacidadeExtra: null);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/inventory", playerToken, new AddInventoryItemRequest(itemId, 1)));

        var overweight = await GetSubAttributesAsync(sheetId, playerToken); // max 2, carrying 4 -> sobrepeso 2
        overweight.Movimentacao.Should().Be(6); // 4*2 - 2 sobrepeso

        await SetBonusDeCargaAsync(gmToken, campaignId, 2m); // max 4 -> no penalty

        var withBonus = await GetSubAttributesAsync(sheetId, playerToken);
        withBonus.PesoMaximo.Should().Be(4m);
        withBonus.Movimentacao.Should().Be(8);
    }

    [Fact]
    public async Task SubAttributes_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmSub3", "sheetsub3@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSub3", "sheetplayersub3@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSub3b", "sheetplayersub3b@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha SubAttr Forbidden");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", otherToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SubAttributes_computes_EficienciaElemental_and_DanoElemental_from_the_matching_Afinidade_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmSub7", "sheetsub7@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSub7", "sheetplayersub7@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha SubAttr Elemental");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // Adepto libera Dobra (Fogo) — Afinidade principal = Fogo.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken,
            ValidUpdate() with { Vocacao = "Adepto", Afinidade = "Fogo", AfinidadeAdicional = 10 }));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddAffinityRequest("Fogo", 7, null, null, null, null)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.EficienciaElemental.Should().Be(4); // 7 no elemento: linha 7 da Tabela de Afinidades
        body.DanoElemental.Should().Be(3);
    }

    [Fact]
    public async Task SubAttributes_follow_an_edit_made_by_the_Auditor_to_the_Tabela_de_Afinidades()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmSub7b", "sheetsub7b@teste.com");
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            var user = await db.Users.SingleAsync(u => u.NormalizedEmail == "SHEETSUB7B@TESTE.COM");
            user.IsRulesAuditor = true;
            await db.SaveChangesAsync();
        }
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSub7b", "sheetplayersub7b@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha SubAttr Tabela Editada");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken,
            ValidUpdate() with { Vocacao = "Adepto", Afinidade = "Fogo", AfinidadeAdicional = 10 }));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddAffinityRequest("Fogo", 7, null, null, null, null)));
        var linhas = (await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/tabela-de-afinidades", gmToken)))
            .Content.ReadFromJsonAsync<List<LinhaDaTabelaDeAfinidadesResponse>>())!;
        var linha7 = linhas.Single(l => l.Afinidade == 7);

        try
        {
            var put = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/tabela-de-afinidades/{linha7.Id}", gmToken,
                new SalvarLinhaDaTabelaDeAfinidadesRequest(7, 40, 30)));
            put.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken));

            var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
            body!.EficienciaElemental.Should().Be(40);
            body.DanoElemental.Should().Be(30);
        }
        finally
        {
            await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/tabela-de-afinidades/{linha7.Id}", gmToken,
                new SalvarLinhaDaTabelaDeAfinidadesRequest(7, linha7.Eficiencia, linha7.Dano)));
        }
    }

    [Fact]
    public async Task SubAttributes_EficienciaElemental_and_DanoElemental_are_0_when_no_row_matches_Afinidade()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmSub8", "sheetsub8@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSub8", "sheetplayersub8@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha SubAttr Elemental Zero");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.EficienciaElemental.Should().Be(0);
        body.DanoElemental.Should().Be(0);
    }

    [Fact]
    public async Task RacialAbility_uses_the_GMs_override_instead_of_the_default_when_one_exists()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmRacial2", "sheetracial2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerRacial2", "sheetplayerracial2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Racial 2");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate() with { Linhagem = "Nephrytes", Variante = "Yavos" }));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-abilities/Yavos", gmToken, new RuinaRPG.Contracts.CharacterSheets.UpdateRacialAbilityRequest("Racial (Override)", "Texto do GM.")));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-ability", playerToken));

        var body = await response.Content.ReadFromJsonAsync<RacialAbilityResponse>();
        body!.Nome.Should().Be("Racial (Override)");
        body.Descricao.Should().Be("Texto do GM.");
    }

    [Fact]
    public async Task RacialAbility_resolves_the_Arca_for_a_Humano_sheet_that_has_rolled()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmRacial3", "sheetracial3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerRacial3", "sheetplayerracial3@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Racial 3");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/7", gmToken, new RuinaRPG.Contracts.CharacterSheets.UpdateArcaEntryRequest("A Chama Eterna", "Resistência ao fogo por 1 cena.")));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate() with { Linhagem = "Humano", Variante = "Sinir", ArcaRolada = 7 }));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-ability", playerToken));

        var body = await response.Content.ReadFromJsonAsync<RacialAbilityResponse>();
        body!.ArcaRolada.Should().Be(7);
        body.ArcaNome.Should().Be("A Chama Eterna");
        body.ArcaDescricao.Should().Be("Resistência ao fogo por 1 cena.");
    }

    [Fact]
    public async Task RacialAbility_lists_only_Arca_evolucoes_unlocked_by_the_character_level()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmArcaEvo1", "sheetarcaevo1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerArcaEvo1", "sheetplayerarcaevo1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Arca Evo 1");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/7", gmToken, new UpdateArcaEntryRequest("A Chama Eterna", "Resistência ao fogo.")));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/7/evolucoes", gmToken, new ArcaEvolucaoRequest(1, "nível um")));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/7/evolucoes", gmToken, new ArcaEvolucaoRequest(2, "nível dois")));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate() with { Linhagem = "Humano", Variante = "Sinir", ArcaRolada = 7, ExperienciaAtual = 0 }));

        var body = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-ability", playerToken)))
            .Content.ReadFromJsonAsync<RacialAbilityResponse>();

        body!.ArcaEvolucoes.Select(e => e.Descricao).Should().Equal("nível um");

        // XP 50 is the Nível 2 threshold (NivelCalculator) — the level-2 evolução unlocks after this save.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate() with { Linhagem = "Humano", Variante = "Sinir", ArcaRolada = 7, ExperienciaAtual = 50 }));
        var atLevel2 = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-ability", playerToken)))
            .Content.ReadFromJsonAsync<RacialAbilityResponse>();

        atLevel2!.ArcaEvolucoes.Select(e => e.Descricao).Should().Equal("nível um", "nível dois");
    }

    [Fact]
    public async Task RacialAbility_reports_a_null_Arca_when_the_GM_hasnt_registered_that_roll()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmRacial4", "sheetracial4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerRacial4", "sheetplayerracial4@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Racial 4");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate() with { Linhagem = "Humano", Variante = "Laonir", ArcaRolada = 12 }));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-ability", playerToken));

        var body = await response.Content.ReadFromJsonAsync<RacialAbilityResponse>();
        body!.ArcaRolada.Should().Be(12);
        body.ArcaNome.Should().BeNull();
        body.ArcaDescricao.Should().BeNull();
    }

    private async Task<(string GmToken, string PlayerToken, string SheetId)> CreateArcaSheetAsync(string nickname)
    {
        var gmToken = await RegisterGmAndGetTokenAsync(nickname + "Gm", nickname + "gm@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, nickname + "Pl", nickname + "pl@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha " + nickname);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);
        return (gmToken, playerToken, sheetId);
    }

    private Task<HttpResponseMessage> PutArcaAsync(string token, string sheetId, int arca, int vitalidadeAtual = 0) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", token,
            ValidUpdate() with { Linhagem = "Humano", Variante = "Sinir", ArcaRolada = arca, VitalidadeAtual = vitalidadeAtual }));

    [Fact]
    public async Task Update_accepts_ArcaRolada_20_and_rejects_21_under_the_default_die()
    {
        var (_, playerToken, sheetId) = await CreateArcaSheetAsync("ArcaDadoSheetA");

        (await PutArcaAsync(playerToken, sheetId, 20)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var rejected = await PutArcaAsync(playerToken, sheetId, 21);
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await rejected.Content.ReadAsStringAsync()).Should().Contain("ArcaRolada deve estar entre 1 e 20.");
    }

    [Fact]
    public async Task Update_rejects_ArcaRolada_7_under_D6()
    {
        var (gmToken, playerToken, sheetId) = await CreateArcaSheetAsync("ArcaDadoSheetB");
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/dado", gmToken, new ArcaDadoRequest(6)));

        var response = await PutArcaAsync(playerToken, sheetId, 7);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ArcaRolada deve estar entre 1 e 6.");
    }

    [Fact]
    public async Task A_stored_ArcaRolada_above_a_shrunken_die_is_reported_out_of_the_table_and_does_not_block_saving_other_fields()
    {
        var (gmToken, playerToken, sheetId) = await CreateArcaSheetAsync("ArcaDadoSheetC");
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/15", gmToken, new UpdateArcaEntryRequest("Arca Alta", "Descrição.")));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/15/evolucoes", gmToken, new ArcaEvolucaoRequest(1, "evo")));
        (await PutArcaAsync(playerToken, sheetId, 15)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/dado", gmToken, new ArcaDadoRequest(12)));

        var body = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-ability", playerToken)))
            .Content.ReadFromJsonAsync<RacialAbilityResponse>();
        body!.ArcaDado.Should().Be(12);
        body.ArcaForaDaTabela.Should().BeTrue();
        body.ArcaRolada.Should().Be(15);
        body.ArcaNome.Should().BeNull();
        body.ArcaDescricao.Should().BeNull();
        body.ArcaEvolucoes.Should().BeEmpty();

        (await PutArcaAsync(playerToken, sheetId, 15, vitalidadeAtual: 1)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PutArcaAsync(playerToken, sheetId, 14)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PutArcaAsync(playerToken, sheetId, 12)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-ability", playerToken)))
            .Content.ReadFromJsonAsync<RacialAbilityResponse>();
        after!.ArcaForaDaTabela.Should().BeFalse();
    }

    [Fact]
    public async Task RacialAbility_default_text_follows_the_GMs_die()
    {
        var (gmToken, playerToken, sheetId) = await CreateArcaSheetAsync("ArcaDadoSheetD");
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/dado", gmToken, new ArcaDadoRequest(10)));
        await PutArcaAsync(playerToken, sheetId, 3);

        var body = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-ability", playerToken)))
            .Content.ReadFromJsonAsync<RacialAbilityResponse>();

        body!.Descricao.Should().Be("Role 1d10 na tabela de Arcas.");
        body.ArcaDado.Should().Be(10);
        body.ArcaForaDaTabela.Should().BeFalse();
    }

    [Fact]
    public async Task ListMine_returns_the_players_own_sheets_across_every_campaign_with_the_campaigns_name()
    {
        // Painel do Jogador (canvas "01 - Visão Geral"): a cross-campaign list, unlike
        // ListForCampaign above which is scoped to one campaign and GM-only.
        var gmToken = await RegisterGmAndGetTokenAsync(TestDataFaker.UniqueNickname(), TestDataFaker.UniqueEmail());
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, TestDataFaker.UniqueNickname(), TestDataFaker.UniqueEmail());
        var campaignA = await CreateCampaignAsync(gmToken, "Campanha A do Painel");
        var campaignB = await CreateCampaignAsync(gmToken, "Campanha B do Painel");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignA}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignB}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetInA = await CreateSheetForMemberAsync(gmToken, campaignA, playerId);
        var sheetInB = await CreateSheetForMemberAsync(gmToken, campaignB, playerId);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetInA}", playerToken, ValidUpdate() with { Nome = "Vann de A" }));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/character-sheets/mine", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<MyCharacterSheetSummaryResponse>>();
        body!.Should().HaveCount(2);
        body.Should().ContainSingle(s => s.Id == sheetInA && s.Nome == "Vann de A" && s.CampanhaNome == "Campanha A do Painel");
        body.Should().ContainSingle(s => s.Id == sheetInB && s.CampanhaNome == "Campanha B do Painel");
    }

    [Fact]
    public async Task ListMine_does_not_include_another_players_sheets()
    {
        var gmToken = await RegisterGmAndGetTokenAsync(TestDataFaker.UniqueNickname(), TestDataFaker.UniqueEmail());
        var (playerAId, playerAToken) = await RegisterJogadorLinkedToAsync(gmToken, TestDataFaker.UniqueNickname(), TestDataFaker.UniqueEmail());
        var (playerBId, _) = await RegisterJogadorLinkedToAsync(gmToken, TestDataFaker.UniqueNickname(), TestDataFaker.UniqueEmail());
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Compartilhada");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerAId)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerBId)));
        await CreateSheetForMemberAsync(gmToken, campaignId, playerBId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/character-sheets/mine", playerAToken));

        var body = await response.Content.ReadFromJsonAsync<List<MyCharacterSheetSummaryResponse>>();
        body!.Should().BeEmpty();
    }

    [Fact]
    public async Task ListMine_by_a_gm_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync(TestDataFaker.UniqueNickname(), TestDataFaker.UniqueEmail());

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/character-sheets/mine", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // Alma e Vida são Caminhos, não Afinidades — Requisitos - Ficha de Personagem 1.a.
    [Theory]
    [InlineData("Alma")]
    [InlineData("Vida")]
    public async Task Update_rejects_a_new_Afinidade_of_Alma_or_Vida(string afinidade)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"CaminhoAfinGmACharacterSheets{afinidade}", $"caminhoafinACharacterSheets{afinidade}@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, $"CaminhoAfinPACharacterSheets{afinidade}", $"caminhoafinpACharacterSheets{afinidade}@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, $"Campanha Caminho ACharacterSheets{afinidade}");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken,
            ValidUpdate() with { Vocacao = "Adepto", Afinidade = afinidade }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_keeps_a_legacy_Afinidade_of_Vida_when_resubmitted_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CaminhoAfinGmBCharacterSheets", "caminhoafinBCharacterSheets@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CaminhoAfinPBCharacterSheets", "caminhoafinpBCharacterSheets@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Caminho BCharacterSheets");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            var entity = await db.CharacterSheets.SingleAsync(s => s.Id == Guid.Parse(sheetId));
            entity.Afinidade = RuinaRPG.Domain.CharacterSheets.AfinidadeElemental.Vida;
            await db.SaveChangesAsync();
        }

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken,
            ValidUpdate() with { Vocacao = "Adepto", Afinidade = "Vida" }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<(string GmToken, string PlayerToken, string SheetId)> CreateOwnedSheetAsync(string suffix)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"HistGm{suffix}", $"histgm{suffix}@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, $"HistPlayer{suffix}", $"histplayer{suffix}@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, $"Campanha História {suffix}");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);
        return (gmToken, playerToken, sheetId);
    }

    private async Task<CharacterSheetResponse> GetSheetAsync(string token, string sheetId) =>
        (await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", token)))
            .Content.ReadFromJsonAsync<CharacterSheetResponse>())!;

    private async Task<int> PiAcumuladoAsync(int nivel)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var tabela = await scope.ServiceProvider.GetRequiredService<ITabelaDeNiveis>().ObterAsync();
        return tabela.Acumulado(ChavesDeNivel.PontosDeIgnicao, nivel);
    }

    private Task<HttpResponseMessage> PutSheetAsync(string token, string sheetId, UpdateCharacterSheetRequest request) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", token, request));

    [Fact]
    public async Task A_new_sheet_starts_with_the_level_1_Pontos_de_Ignicao()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("PiNovo");

        (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual.Should().Be(await PiAcumuladoAsync(1));
    }

    // O PUT genérico não grava o saldo vindo do formulário (1.b): o valor de partida destes testes entra
    // pelo endpoint próprio do campo.
    private Task<HttpResponseMessage> PutPontosDeIgnicaoAsync(string token, string sheetId, int pontos) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/pontos-de-ignicao", token, pontos));

    [Fact]
    public async Task Levelling_up_adds_the_gained_levels_Pontos_de_Ignicao_to_Atual()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("PiSobe");
        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { ExperienciaAtual = 0 })).EnsureSuccessStatusCode();
        (await PutPontosDeIgnicaoAsync(playerToken, sheetId, 7)).EnsureSuccessStatusCode();

        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { ExperienciaAtual = 749, PontosDeIgnicaoAtual = 7 })).EnsureSuccessStatusCode();

        var sheet = await GetSheetAsync(playerToken, sheetId);
        sheet.Nivel.Should().Be(5);
        sheet.PontosDeIgnicaoAtual.Should().Be(7 + await PiAcumuladoAsync(5) - await PiAcumuladoAsync(1));
    }

    [Fact]
    public async Task Saving_again_at_the_same_level_does_not_add_the_points_twice()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("PiMesmo");
        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { ExperienciaAtual = 749 })).EnsureSuccessStatusCode();
        (await PutPontosDeIgnicaoAsync(playerToken, sheetId, 7)).EnsureSuccessStatusCode();

        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { ExperienciaAtual = 749, PontosDeIgnicaoAtual = 7 })).EnsureSuccessStatusCode();

        (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual.Should().Be(7);
    }

    [Fact]
    public async Task Losing_levels_subtracts_the_same_points_and_never_goes_below_zero()
    {
        // ValidUpdate() carries a manual bonus of 40; pinned to 0 so only the level moves Atual.
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("PiDesce");
        var delta = await PiAcumuladoAsync(5) - await PiAcumuladoAsync(1);
        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { ExperienciaAtual = 749, PontosDeIgnicaoBonusManual = 0 })).EnsureSuccessStatusCode();
        (await PutPontosDeIgnicaoAsync(playerToken, sheetId, delta + 3)).EnsureSuccessStatusCode();

        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { ExperienciaAtual = 0, PontosDeIgnicaoBonusManual = 0 })).EnsureSuccessStatusCode();
        (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual.Should().Be(3); // (Δ + 3) − Δ

        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { ExperienciaAtual = 749, PontosDeIgnicaoBonusManual = 0 })).EnsureSuccessStatusCode();
        (await PutPontosDeIgnicaoAsync(playerToken, sheetId, 2)).EnsureSuccessStatusCode();
        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { ExperienciaAtual = 0, PontosDeIgnicaoBonusManual = 0 })).EnsureSuccessStatusCode();
        (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual.Should().Be(0);
    }

    [Fact]
    public async Task A_manual_bonus_delta_reaches_Atual_as_well_as_the_total()
    {
        var (gmToken, _, sheetId) = await CreateOwnedSheetAsync("PiBonus");
        (await PutSheetAsync(gmToken, sheetId, ValidUpdate() with { ExperienciaAtual = 0, PontosDeIgnicaoBonusManual = 0 })).EnsureSuccessStatusCode();
        (await PutPontosDeIgnicaoAsync(gmToken, sheetId, 10)).EnsureSuccessStatusCode();

        (await PutSheetAsync(gmToken, sheetId, ValidUpdate() with { ExperienciaAtual = 0, PontosDeIgnicaoAtual = 10, PontosDeIgnicaoBonusManual = 5 })).EnsureSuccessStatusCode();

        var sheet = await GetSheetAsync(gmToken, sheetId);
        sheet.PontosDeIgnicaoAtual.Should().Be(15);
        sheet.PontosDeIgnicaoBonusManual.Should().Be(5);
        sheet.PontosDeIgnicaoTotal.Should().Be(await PiAcumuladoAsync(1) + 5);
    }

    [Fact]
    public async Task The_generic_update_ignores_the_PontosDeIgnicaoAtual_of_the_request()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("PiIgnora");
        (await PutSheetAsync(playerToken, sheetId, ValidUpdate())).EnsureSuccessStatusCode();
        var guardado = (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual;

        // Mesmo nível e mesmo bônus manual: só o saldo do formulário difere.
        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { PontosDeIgnicaoAtual = guardado + 123 })).EnsureSuccessStatusCode();

        (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual.Should().Be(guardado);
    }

    [Fact]
    public async Task A_stale_form_after_a_level_up_applies_the_level_delta_to_the_stored_balance()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("PiVelho");
        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { ExperienciaAtual = 749, PontosDeIgnicaoBonusManual = 0 })).EnsureSuccessStatusCode();
        var noNivel5 = (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual;

        // Um formulário aberto antes da subida: ainda no nível 1 e com um saldo que não vale mais.
        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { ExperienciaAtual = 0, PontosDeIgnicaoBonusManual = 0, PontosDeIgnicaoAtual = 999 })).EnsureSuccessStatusCode();

        (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual
            .Should().Be(noNivel5 - (await PiAcumuladoAsync(5) - await PiAcumuladoAsync(1)));
    }

    [Fact]
    public async Task The_pontos_de_ignicao_endpoint_sets_the_balance_for_the_owning_player_and_the_gm()
    {
        var (gmToken, playerToken, sheetId) = await CreateOwnedSheetAsync("PiCampo");

        (await PutPontosDeIgnicaoAsync(playerToken, sheetId, 23)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual.Should().Be(23);

        (await PutPontosDeIgnicaoAsync(gmToken, sheetId, 0)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual.Should().Be(0);
    }

    [Fact]
    public async Task The_pontos_de_ignicao_endpoint_refuses_a_negative_value()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("PiNegativo");
        var antes = (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual;

        var response = await PutPontosDeIgnicaoAsync(playerToken, sheetId, -1);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Os Pontos de Ignição não podem ser negativos.");
        (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual.Should().Be(antes);
    }

    [Fact]
    public async Task The_pontos_de_ignicao_endpoint_refuses_an_unrelated_jogador_and_an_unknown_sheet()
    {
        var (gmToken, playerToken, sheetId) = await CreateOwnedSheetAsync("PiEstranho");
        var (_, otherPlayerToken) = await RegisterJogadorLinkedToAsync(gmToken, "HistPlayerPiEstranhoB", "histplayerpiestranhob@teste.com");
        var antes = (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual;

        (await PutPontosDeIgnicaoAsync(otherPlayerToken, sheetId, 50)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await PutPontosDeIgnicaoAsync(gmToken, Guid.NewGuid().ToString(), 50)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual.Should().Be(antes);
    }

    [Fact]
    public async Task UpdateHistoria_by_the_owner_persists_sanitized_html_and_Get_returns_it()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("C1");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/historia", playerToken,
            new UpdateHistoriaRequest("<h2>Origem</h2><p style=\"color: red\">Nasceu em Alkeria</p><script>alert(1)</script>")));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var sheet = await GetSheetAsync(playerToken, sheetId);
        sheet.Historia.Should().Contain("<h2>Origem</h2>").And.Contain("Nasceu em Alkeria").And.Contain("color: rgba(255, 0, 0, 1)").And.NotContain("script");
    }

    [Fact]
    public async Task UpdateHistoria_by_the_campaign_gm_is_allowed()
    {
        var (gmToken, _, sheetId) = await CreateOwnedSheetAsync("C2");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/historia", gmToken,
            new UpdateHistoriaRequest("<p>Nota do mestre</p>")));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateHistoria_by_an_unrelated_jogador_returns_403()
    {
        var (gmToken, _, sheetId) = await CreateOwnedSheetAsync("C3");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "HistPlayerC3b", "histplayerc3b@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/historia", otherToken,
            new UpdateHistoriaRequest("<p>invasão</p>")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateHistoria_on_a_nonexistent_sheet_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistGmC4", "histgmc4@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{Guid.NewGuid()}/historia", gmToken,
            new UpdateHistoriaRequest("<p>x</p>")));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateHistoria_accepts_exactly_200000_characters_and_rejects_200001()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("C5");
        var noLimite = "<p>" + new string('a', 200_000 - 7) + "</p>";

        var ok = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/historia", playerToken, new UpdateHistoriaRequest(noLimite)));
        var tooLong = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/historia", playerToken, new UpdateHistoriaRequest(noLimite + "a")));

        noLimite.Length.Should().Be(200_000);
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);
        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await tooLong.Content.ReadAsStringAsync()).Should().Contain("A História pode ter no máximo 200.000 caracteres.");
    }

    [Fact]
    public async Task UpdateHistoria_rejects_input_that_grows_past_200000_characters_when_sanitized()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("C8");
        // The sanitizer rewrites "red" as "rgba(255, 0, 0, 1)", so each paragraph grows by ~60%: as many
        // repetitions as fit in 200,000 raw chars sanitize to well over 200,000.
        const string paragrafo = "<p style=\"color: red\">x</p>";
        var historia = string.Concat(Enumerable.Repeat(paragrafo, 200_000 / paragrafo.Length));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/historia", playerToken, new UpdateHistoriaRequest(historia)));

        historia.Length.Should().BeLessThanOrEqualTo(200_000);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("A História pode ter no máximo 200.000 caracteres.");
    }

    [Fact]
    public async Task UpdateHistoria_with_only_empty_markup_stores_null()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("C6");
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/historia", playerToken, new UpdateHistoriaRequest("<p>algo</p>")));

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/historia", playerToken, new UpdateHistoriaRequest("<p><br></p>")));

        (await GetSheetAsync(playerToken, sheetId)).Historia.Should().BeNull();
    }

    [Fact]
    public async Task The_general_sheet_Update_leaves_Historia_untouched()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("C7");
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/historia", playerToken, new UpdateHistoriaRequest("<p>Minha história</p>")));

        var update = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate()));

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetSheetAsync(playerToken, sheetId)).Historia.Should().Contain("Minha história");
    }
}
