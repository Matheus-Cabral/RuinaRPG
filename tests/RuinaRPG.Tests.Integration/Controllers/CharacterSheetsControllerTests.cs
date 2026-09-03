using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterSheetsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CharacterSheetsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/campaigns/00000000-0000-0000-0000-000000000000/character-sheets", new CreateCharacterSheetRequest("00000000-0000-0000-0000-000000000000"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_for_a_campaign_member_returns_201_with_Nivel_1_and_no_other_field_set()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGm1", "sheet1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayer1", "sheetplayer1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Ficha");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));

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

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGm3", "sheet3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayer3", "sheetplayer3@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Jogador");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", playerToken, new CreateCharacterSheetRequest(playerId)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_for_a_campaign_owned_by_another_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("SheetGmCreateOwner", "sheetcreateowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("SheetGmCreateOther", "sheetcreateother@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmTokenOwner, "SheetPlayerCreateOwner", "sheetplayercreateowner@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha do Dono para Criação");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmTokenOwner, new AddCampaignMemberRequest(playerId)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmTokenOther, new CreateCharacterSheetRequest(playerId)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_with_a_malformed_OwnerId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmMalformed", "sheetmalformed@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha OwnerId Inválido");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest("not-a-guid")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_an_owned_sheet_returns_204()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmDelete1", "sheetdelete1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerDelete1", "sheetplayerdelete1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Delete");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        var sheetId = (await createResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_a_sheet_in_a_campaign_owned_by_another_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("SheetGmDeleteOwner", "sheetdeleteowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("SheetGmDeleteOther", "sheetdeleteother@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmTokenOwner, "SheetPlayerDeleteOwner", "sheetplayerdeleteowner@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha do Dono");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmTokenOwner, new AddCampaignMemberRequest(playerId)));
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmTokenOwner, new CreateCharacterSheetRequest(playerId)));
        var sheetId = (await createResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<string> CreateSheetForMemberAsync(string gmToken, string campaignId, string playerId)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await response.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }

    private static UpdateCharacterSheetRequest ValidUpdate() => new(
        null, "Vann Astrel", "Humano", "Sinir", "Campeao", "Duelista", "Fogo", "Marcado pela Ruína",
        // 749 XP is one below Nível 6's threshold (750) — Nível is derived now, and reaching a
        // threshold exactly already counts as that Nível, so 749 keeps this at Nível 5.
        true, 749, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100);

    [Fact]
    public async Task Get_returns_the_sheet()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmGet1", "sheetget1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerGet1", "sheetplayerget1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Get");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_by_an_unrelated_jogador_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmGetU1", "sheetgetu1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerGetU1", "sheetplayergetu1@teste.com");
        var (_, otherPlayerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerGetU1b", "sheetplayergetu1b@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Get Unrelated Jogador");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", otherPlayerToken));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_by_an_unrelated_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("SheetGmGetOwner", "sheetgetowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("SheetGmGetOther", "sheetgetother@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmTokenOwner, "SheetPlayerGetOwner", "sheetplayergetowner@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha Get Unrelated GM");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmTokenOwner, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmTokenOwner, campaignId, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_a_nonexistent_sheet_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmGetMissing", "sheetgetmissing@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{Guid.NewGuid()}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListForCampaign_returns_the_campaigns_sheets()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmList1", "sheetlist1@teste.com");
        var (playerId1, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerList1", "sheetplayerlist1@teste.com");
        var (playerId2, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerList2", "sheetplayerlist2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Lista de Fichas");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId1)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId2)));
        var sheetId1 = await CreateSheetForMemberAsync(gmToken, campaignId, playerId1);
        var sheetId2 = await CreateSheetForMemberAsync(gmToken, campaignId, playerId2);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/character-sheets", gmToken));

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

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/character-sheets", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_by_the_owner_returns_204_and_persists_every_field()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdate1", "sheetupdate1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdate1", "sheetplayerupdate1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // Campeão Nível 5 → Vida 24 (real Tabela de Vocação). Vigor total 3 → Vitalidade máximo
        // 3*2+24 = 30, exactly ValidUpdate()'s VitalidadeAtual, so the clamp added for "Atual não
        // pode exceder o máximo" (1.c) doesn't interfere with this test's real purpose (every
        // other field round-trips unchanged).
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Vigor", playerToken, new UpdateCharacterAttributeRequest(3, 0, false)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate()));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
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
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", gmToken, ValidUpdate()));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Update_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdate3", "sheetupdate3@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdate3", "sheetplayerupdate3@teste.com");
        var (_, otherPlayerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdate3b", "sheetplayerupdate3b@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update Unrelated");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", otherPlayerToken, ValidUpdate()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_with_a_Variante_that_does_not_belong_to_the_Linhagem_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdate4", "sheetupdate4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdate4", "sheetplayerupdate4@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update Invalid");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var invalid = ValidUpdate() with { Linhagem = "Humano", Variante = "Yavos" }; // Yavos belongs to Nephrytes
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EAPAtual_is_the_level_base_plus_the_Ambares_by_Rank()
    {
        // Ficha de Personagem 1.b: "Segue a tabela ... e é somado pelo resultado de Âmbares
        // Absorvidos". Nível 1 base = 0 (Tabelas de XP.../EAP). 2 Rank F (5 each) + 1 Rank C (120) = 130.
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmEap1", "sheeteap1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerEap1", "sheetplayereap1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha EAP");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // Nível is derived from ExperienciaAtual now — 0 XP computes to Nível 1 (real Tabela de XP).
        var update = ValidUpdate() with { ExperienciaAtual = 0, NucleosRankF = 2, NucleosRankC = 1 };
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
        var body = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.EAPAtual.Should().Be(130); // 0 (Nível 1 base) + 2*5 + 1*120
    }

    [Fact]
    public async Task Get_labels_Graduacao_as_Grau_for_Campeao_and_computes_it_from_EAP()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmGrad1", "sheetgrad1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerGrad1", "sheetplayergrad1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Graduacao");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // EAPAtual is computed, not settable — Nível 6 gives EAP = (6-1)*30 = 150 (EapCalculator,
        // 0 Âmbares) via the real EAP-por-Nível table. 1049 XP is one below Nível 7's threshold
        // (1050), keeping this at Nível 6 — Nível is derived, so ExperienciaAtual drives it here
        // instead of setting it directly.
        var update = ValidUpdate() with { Vocacao = "Campeao", ExperienciaAtual = 1049 };
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
        var body = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.GraduacaoLabel.Should().Be("Grau");
        body.EAPAtual.Should().Be(150);
        body.Graduacao.Should().Be(1); // 150 EAP >= the real Tabela's Grau 1 threshold (100)
    }

    [Fact]
    public async Task Get_labels_Graduacao_as_Circulo_for_a_magic_vocacao()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmGrad2", "sheetgrad2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerGrad2", "sheetplayergrad2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Graduacao 2");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var update = ValidUpdate() with { Vocacao = "Feiticeiro", PossuiCoracaoDeMana = false, ExperienciaAtual = 1049 };
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
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
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // Campeão Nível 1 → Vida 8, Arcana 4 (real Tabela de Vocação excerpt, same as
        // VocacaoProgressaoParserTests). This vocação is specifically chosen because it
        // exercises the accented-name lookup bug (Campeão/Caçador) fixed in this task.
        var update = ValidUpdate() with { Vocacao = "Campeao", ExperienciaAtual = 0 };
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

        // Vigor total = Gasto(5) + Bonus(0)/2 (sem maestria) = 5 → Vitalidade = 5*2 + 8 = 18.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Vigor", playerToken, new UpdateCharacterAttributeRequest(5, 0, false)));
        // Astúcia total = Gasto(3) + Bonus(0)/2 (sem maestria) = 3 → Foco = 3*2 + 4 = 10.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Astucia", playerToken, new UpdateCharacterAttributeRequest(3, 0, false)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));

        var body = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.VitalidadeMaximo.Should().Be(18);
        body.FocoMaximo.Should().Be(10);
        body.AdrenalinaMaximo.Should().Be(10); // 10 + Artefato bonus (não modelado ainda → 0)
        body.EstresseMaximo.Should().Be(10); // flat
    }

    [Fact]
    public async Task Update_clamps_every_Atual_resource_to_its_own_Maximo_instead_of_rejecting()
    {
        // Ficha de Personagem 1.c: "Atual não pode exceder o máximo" (Vitalidade/Foco/PA/Estresse).
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmClamp1", "sheetclamp1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerClamp1", "sheetplayerclamp1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Clamp");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // Campeão Nível 1 → Vitalidade máximo 18, Foco máximo 10 (see the test above); Adrenalina
        // and Estresse máximos (10 each) don't depend on attributes at all. Attributes are set
        // BEFORE the sheet PUT below, since clamping happens against the máximo at save time.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Vigor", playerToken, new UpdateCharacterAttributeRequest(5, 0, false)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Astucia", playerToken, new UpdateCharacterAttributeRequest(3, 0, false)));

        var update = ValidUpdate() with
        {
            Vocacao = "Campeao", ExperienciaAtual = 0,
            VitalidadeAtual = 999, FocoAtual = 999, AdrenalinaAtual = 999, EstresseAtual = 999
        };
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
        var body = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.VitalidadeAtual.Should().Be(18);
        body.FocoAtual.Should().Be(10);
        body.AdrenalinaAtual.Should().Be(10);
        body.EstresseAtual.Should().Be(10);
    }

    [Fact]
    public async Task LevelUpNotice_lists_bonus_text_for_every_level_gained_since_the_last_dismissal()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmLevelUp1", "sheetlevelup1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerLevelUp1", "sheetplayerlevelup1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha LevelUp");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);
        // 149 XP is one below Nível 3's threshold (150), keeping this at Nível 2 — Nível is
        // derived now, not settable directly.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate() with { ExperienciaAtual = 149 }));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/level-up-notice", playerToken));

        var body = await response.Content.ReadFromJsonAsync<LevelUpNoticeResponse>();
        body!.BonusTexts.Should().HaveCount(2); // Nível 1 and 2's bonus text, nothing dismissed yet
    }

    [Fact]
    public async Task Dismiss_stops_the_dismissed_levels_from_reappearing()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmLevelUp2", "sheetlevelup2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerLevelUp2", "sheetplayerlevelup2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha LevelUp 2");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate() with { ExperienciaAtual = 149 }));

        var dismissResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/dismiss-level-up-notice", playerToken));
        dismissResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var noticeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/level-up-notice", playerToken));
        var body = await noticeResponse.Content.ReadFromJsonAsync<LevelUpNoticeResponse>();
        body!.BonusTexts.Should().BeEmpty();
    }

    [Fact]
    public async Task Dismiss_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmLevelUp3", "sheetlevelup3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerLevelUp3", "sheetplayerlevelup3@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerLevelUp3b", "sheetplayerlevelup3b@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha LevelUp 3");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/dismiss-level-up-notice", otherToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_with_a_malformed_ImageId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdImg", "sheetupdimg@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdImg", "sheetplayerupdimg@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update ImageId Invalido");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var invalid = ValidUpdate() with { ImageId = "not-a-guid" };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_with_a_malformed_Cobertura_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdCob", "sheetupdcob@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdCob", "sheetplayerupdcob@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update Cobertura Invalida");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var invalid = ValidUpdate() with { Cobertura = "NaoExiste" };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_with_a_garbage_Vocacao_returns_400_instead_of_silently_saving_null()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdVoc", "sheetupdvoc@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdVoc", "sheetplayerupdvoc@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update Vocacao Invalida");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var invalid = ValidUpdate() with { Vocacao = "Xyz" };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_with_a_garbage_Afinidade_returns_400_instead_of_silently_saving_null()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdAfin", "sheetupdafin@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdAfin", "sheetplayerupdafin@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update Afinidade Invalida");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var invalid = ValidUpdate() with { Afinidade = "Xyz" };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_seeds_8_zeroed_attributes_and_39_zeroed_skills()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();

        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmSeed1", "sheetseed1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSeed1", "sheetplayerseed1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Seed");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var attributes = await db.CharacterAttributes.Where(a => a.CharacterSheetId == Guid.Parse(sheetId)).ToListAsync();
        var skills = await db.CharacterSkills.Where(s => s.CharacterSheetId == Guid.Parse(sheetId)).ToListAsync();

        attributes.Should().HaveCount(8);
        attributes.Should().OnlyContain(a => a.Gasto == 0 && a.Bonus == 0 && !a.TemMaestria);
        skills.Should().HaveCount(39);
        skills.Should().OnlyContain(s => s.Gasto == 0);
    }

    [Fact]
    public async Task SubAttributes_computes_from_attributes_and_arsenal()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmSub1", "sheetsub1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSub1", "sheetplayersub1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha SubAttr");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // Agilidade Gasto 4, no bônus/maestria/artefato → Total 4. Vigor same → Total 4.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Agilidade", playerToken,
            new UpdateCharacterAttributeRequest(4, 0, false)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Vigor", playerToken,
            new UpdateCharacterAttributeRequest(4, 0, false)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.Movimentacao.Should().Be(8); // (4*2) + 0 artefato - 0 sobrepeso (nothing carried yet)
    }

    [Fact]
    public async Task SubAttributes_includes_Bruto_Prontidao_Reflexos_and_Fortitude_from_their_skill_Modificador()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmSub4", "sheetsub4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSub4", "sheetplayersub4@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha SubAttr Bruto");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // Agilidade Gasto 4, Vigor Gasto 4, no bônus/maestria/artefato → Total 4 each.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Agilidade", playerToken,
            new UpdateCharacterAttributeRequest(4, 0, false)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Vigor", playerToken,
            new UpdateCharacterAttributeRequest(4, 0, false)));

        // Prontidao Gasto 9 -> Modificador 3, Reflexos Gasto 6 -> Modificador 2, Fortitude Gasto 3 -> Modificador 1.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Prontidao", playerToken,
            new UpdateCharacterSkillRequest(9, null)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Reflexos", playerToken,
            new UpdateCharacterSkillRequest(6, null)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Fortitude", playerToken,
            new UpdateCharacterSkillRequest(3, null)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.Iniciativa.Should().Be(7); // agilidade 4 + brutoProntidao 3 + 0 artefato
        body.EsquivaNatural.Should().Be(6); // agilidade 4 + brutoReflexos 2 + 0 artefatos - 0 penalidade
        body.DefesaNatural.Should().Be(5); // vigor 4 + brutoFortitude 1 + 0 escudo + 0 artefatos + 0 cobertura
    }

    private async Task<string> CreateArmaduraItemAsync(string gmToken, int rf, int rm)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new RuinaRPG.Contracts.Items.CreateItemRequest("Armadura", "Peitoral de Testes", 3m, 30, null, null, null, null, null, null, null, null, null, null, null, 12, "Medio", 5, rf, rm, "-1 Furtividade", 2, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Items.ItemResponse>())!.Id;
    }

    [Fact]
    public async Task SubAttributes_includes_equipped_armor_RF_and_RM()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmSub2", "sheetsub2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSub2", "sheetplayersub2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha SubAttr RF");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);
        var armorItemId = await CreateArmaduraItemAsync(gmToken, rf: 3, rm: 2);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/armor-slots/Capacete", playerToken,
            new UpdateCharacterArmorSlotRequest(armorItemId)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.ReducaoFisica.Should().Be(3);
        body.ReducaoMagica.Should().Be(2);
    }

    private async Task<string> CreateArtefatoItemAsync(string gmToken, string nome, string tipoDeAlvo, string alvo, int valor)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new RuinaRPG.Contracts.Items.CreateItemRequest("Artefato", nome, 0.1m, 500, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, tipoDeAlvo, alvo, valor)));
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
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var iniciativaArtifactId = await CreateArtefatoItemAsync(gmToken, "Amuleto da Presteza", "SubAtributo", "Iniciativa", 4);
        var reducaoFisicaArtifactId = await CreateArtefatoItemAsync(gmToken, "Bracelete de Ferro", "SubAtributo", "Redução Física", 2);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", playerToken, new AddCharacterArtifactRequest(iniciativaArtifactId)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", playerToken, new AddCharacterArtifactRequest(reducaoFisicaArtifactId)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.Iniciativa.Should().Be(4); // Agilidade(0) + Bruto Prontidão(0) + Artefato(4)
        body.ReducaoFisica.Should().Be(2); // Artefato(2) + Armadura(0)
        body.ReducaoMagica.Should().Be(0); // unaffected — different Alvo
    }

    [Fact]
    public async Task SubAttributes_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmSub3", "sheetsub3@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSub3", "sheetplayersub3@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSub3b", "sheetplayersub3b@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha SubAttr Forbidden");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", otherToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RacialAbility_is_null_before_a_Variante_is_chosen_and_populated_after()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmRacial1", "sheetracial1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerRacial1", "sheetplayerracial1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Racial");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var beforeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-ability", playerToken));
        (await beforeResponse.Content.ReadFromJsonAsync<RacialAbilityResponse>())!.Nome.Should().BeNull();

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate() with { Linhagem = "Nephrytes", Variante = "Yavos" }));

        var afterResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-ability", playerToken));
        (await afterResponse.Content.ReadFromJsonAsync<RacialAbilityResponse>())!.Nome.Should().Be("Racial (Sobre Voo)");
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
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignA}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignB}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetInA = await CreateSheetForMemberAsync(gmToken, campaignA, playerId);
        var sheetInB = await CreateSheetForMemberAsync(gmToken, campaignB, playerId);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetInA}", playerToken, ValidUpdate() with { Nome = "Vann de A" }));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/character-sheets/mine", playerToken));

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
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerAId)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerBId)));
        await CreateSheetForMemberAsync(gmToken, campaignId, playerBId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/character-sheets/mine", playerAToken));

        var body = await response.Content.ReadFromJsonAsync<List<MyCharacterSheetSummaryResponse>>();
        body!.Should().BeEmpty();
    }

    [Fact]
    public async Task ListMine_by_a_gm_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync(TestDataFaker.UniqueNickname(), TestDataFaker.UniqueEmail());

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/character-sheets/mine", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
