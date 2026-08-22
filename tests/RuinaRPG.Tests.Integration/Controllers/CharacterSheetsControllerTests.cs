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
        5, true, 750, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100);

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
    public async Task Get_labels_Graduacao_as_Grau_for_Campeao_and_computes_it_from_EAP()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmGrad1", "sheetgrad1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerGrad1", "sheetplayergrad1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Graduacao");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var update = ValidUpdate() with { Vocacao = "Campeao", EAPAtual = 150 };
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
        var body = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.GraduacaoLabel.Should().Be("Grau");
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

        var update = ValidUpdate() with { Vocacao = "Feiticeiro", PossuiCoracaoDeMana = false, EAPAtual = 150 };
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
        var body = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.GraduacaoLabel.Should().Be("Círculo");
        body.Graduacao.Should().Be(0); // no coração de mana → always 0 regardless of EAP
    }

    [Fact]
    public async Task LevelUpNotice_lists_bonus_text_for_every_level_gained_since_the_last_dismissal()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmLevelUp1", "sheetlevelup1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerLevelUp1", "sheetplayerlevelup1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha LevelUp");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate() with { Nivel = 2 }));

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
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate() with { Nivel = 2 }));

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
}
