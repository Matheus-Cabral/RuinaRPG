using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
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
}
