using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Runes;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterRunesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CharacterRunesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> SetUpSheetAsync(string gmToken, string playerId) =>
        (await SetUpSheetInCampaignAsync(gmToken, playerId)).SheetId;

    private async Task<(string SheetId, string CampaignId)> SetUpSheetInCampaignAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return ((await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id, campaignId);
    }

    private async Task<string> CreateRuneEntryAsync(string gmToken, string nome, string descricao, int grau)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", gmToken, new CreateRuneBankEntryRequest(nome, descricao, grau)));
        return (await response.Content.ReadFromJsonAsync<RuneBankEntryResponse>())!.Id;
    }

    private async Task PublishRuneToCampaignAsync(string gmToken, string campaignId, string runeEntryId)
    {
        var attach = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, null, null, runeEntryId)));
        var attachmentId = (await attach.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/visibility", gmToken, true));
    }

    private async Task<List<RuneBankEntryResponse>> BankOfAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rune-bank", gmToken));
        return (await response.Content.ReadFromJsonAsync<List<RuneBankEntryResponse>>())!;
    }

    private async Task<List<RuneBankEntryResponse>> PublicRunesAsync(string token, string campaignId)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/available-runes", token));
        return (await response.Content.ReadFromJsonAsync<List<RuneBankEntryResponse>>())!;
    }

    [Fact]
    public async Task Add_a_valid_rune_returns_201()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneGm1", "rune1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RunePlayer1", "runeplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest("Runa do Fogo", "Queima o alvo.", 1)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/runes", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterRuneResponse>>();
        body!.Should().ContainSingle(r => r.Nome == "Runa do Fogo" && r.Descricao == "Queima o alvo." && r.Grau == 1);
    }

    [Fact]
    public async Task List_returns_the_runes_on_the_sheet()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneGm2", "rune2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RunePlayer2", "runeplayer2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest("Runa da Terra", "Endurece a pele.", 2)));

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/runes", playerToken));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterRuneResponse>>();
        body!.Should().ContainSingle(r => r.Nome == "Runa da Terra" && r.Descricao == "Endurece a pele." && r.Grau == 2);
    }

    [Fact]
    public async Task Delete_an_existing_rune_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneGm3", "rune3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RunePlayer3", "runeplayer3@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest("Runa da Água", "Cura ferimentos leves.", 3)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterRuneResponse>();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/runes/{added!.Id}", playerToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/runes", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterRuneResponse>>();
        body!.Should().NotContain(r => r.Id == added.Id);
    }

    [Fact]
    public async Task Add_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneGm4", "rune4@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "RunePlayer4", "runeplayer4@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "RunePlayer4b", "runeplayer4b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", otherToken,
            new AddCharacterRuneRequest("Runa do Vento", "Aumenta velocidade.", 1)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_rune_on_the_sheet_is_read_only_there_is_no_update_endpoint()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneGm5", "rune5@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RunePlayer5", "runeplayer5@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest("Runa do Fogo", "Queima o alvo.", 1)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterRuneResponse>();

        var update = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/runes/{added!.Id}", playerToken,
            new { Nome = "Runa do Gelo", Descricao = "Congela.", Grau = 2 }));

        update.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task A_rune_added_by_the_gm_lands_in_the_bank_but_is_not_published_to_the_campaign()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneBkGm1", "runebk1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RuneBkPlayer1", "runebkplayer1@teste.com");
        var (sheetId, campaignId) = await SetUpSheetInCampaignAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", gmToken,
            new AddCharacterRuneRequest("Runa do Fogo", "Queima o alvo.", 2)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await BankOfAsync(gmToken)).Should().ContainSingle(e => e.Nome == "Runa do Fogo" && e.Descricao == "Queima o alvo." && e.Grau == 2);
        (await PublicRunesAsync(playerToken, campaignId)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_rune_added_by_the_player_lands_in_the_bank_and_is_published_to_the_campaign()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneBkGm2", "runebk2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RuneBkPlayer2", "runebkplayer2@teste.com");
        var (sheetId, campaignId) = await SetUpSheetInCampaignAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest("Runa da Água", "Cura ferimentos leves.", 1)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await BankOfAsync(gmToken)).Should().ContainSingle(e => e.Nome == "Runa da Água");
        (await PublicRunesAsync(playerToken, campaignId)).Should().ContainSingle(e => e.Nome == "Runa da Água" && e.Grau == 1);
    }

    [Fact]
    public async Task A_rune_picked_from_the_bank_copies_its_fields_and_makes_a_new_independent_bank_copy()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneBkGm3", "runebk3@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "RuneBkPlayer3", "runebkplayer3@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gmToken, playerId);
        var entryId = await CreateRuneEntryAsync(gmToken, "Runa da Luz", "Ilumina a área.", 3);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", gmToken,
            new AddCharacterRuneRequest(null, null, null, entryId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CharacterRuneResponse>();
        created!.Nome.Should().Be("Runa da Luz");
        created.Descricao.Should().Be("Ilumina a área.");
        created.Grau.Should().Be(3);
        (await BankOfAsync(gmToken)).Count(e => e.Nome == "Runa da Luz").Should().Be(2); // a original + a cópia (R0001)
    }

    [Fact]
    public async Task A_player_can_only_pick_bank_entries_the_gm_published_to_the_campaign()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneBkGm4", "runebk4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RuneBkPlayer4", "runebkplayer4@teste.com");
        var (sheetId, campaignId) = await SetUpSheetInCampaignAsync(gmToken, playerId);
        var entryId = await CreateRuneEntryAsync(gmToken, "Runa Reservada", "Só depois de liberada.", 1);

        var antes = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest(null, null, null, entryId)));
        antes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await PublishRuneToCampaignAsync(gmToken, campaignId, entryId);

        var depois = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest(null, null, null, entryId)));
        depois.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Adding_with_both_paths_or_with_neither_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneBkGm5", "runebk5@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "RuneBkPlayer5", "runebkplayer5@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gmToken, playerId);
        var entryId = await CreateRuneEntryAsync(gmToken, "Runa", "Desc.", 1);

        var both = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", gmToken,
            new AddCharacterRuneRequest("Runa", "Desc.", 1, entryId)));
        var neither = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", gmToken,
            new AddCharacterRuneRequest(null, null, null)));
        var partial = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", gmToken,
            new AddCharacterRuneRequest("Só o nome", null, null)));

        both.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        neither.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        partial.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task Picking_a_malformed_or_unknown_bank_entry_returns_400(string entryId)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"RuneBkGm6{entryId.Length}", $"runebk6{entryId.Length}@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, $"RuneBkPlayer6{entryId.Length}", $"runebkplayer6{entryId.Length}@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", gmToken,
            new AddCharacterRuneRequest(null, null, null, entryId)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Picking_another_gms_bank_entry_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneBkGm7", "runebk7@teste.com");
        var otherGmToken = await RegisterGmAndGetTokenAsync("RuneBkGm7b", "runebk7b@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "RuneBkPlayer7", "runebkplayer7@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gmToken, playerId);
        var foreignEntry = await CreateRuneEntryAsync(otherGmToken, "Runa Alheia", "Do outro GM.", 1);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", gmToken,
            new AddCharacterRuneRequest(null, null, null, foreignEntry)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- Imagem opcional da Runa ----

    private async Task<(string Id, string Url)> UploadImageAsync(string token, string? campaignId = null)
    {
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");
        if (campaignId is not null)
            content.Add(new StringContent(campaignId), "campaignId");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(message);
        var body = await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Images.ImageUploadResponse>();
        return (body!.Id, body.Url);
    }

    private async Task PublishImageToCampaignAsync(string gmToken, string campaignId, string imageId)
    {
        var attach = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, null, imageId, null)));
        var attachmentId = (await attach.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/visibility", gmToken, true));
    }

    private async Task<List<CharacterRuneResponse>> RunesOfAsync(string sheetId, string token)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/runes", token));
        return (await response.Content.ReadFromJsonAsync<List<CharacterRuneResponse>>())!;
    }

    [Fact]
    public async Task A_rune_from_scratch_with_the_callers_own_image_returns_201_and_the_list_has_the_ImageUrl()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneImgGm1", "runeimg1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RuneImgPlayer1", "runeimgplayer1@teste.com");
        var (sheetId, campaignId) = await SetUpSheetInCampaignAsync(gmToken, playerId);
        var (imageId, imageUrl) = await UploadImageAsync(playerToken, campaignId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest("Runa Ilustrada", "Tem imagem.", 1, null, imageId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<CharacterRuneResponse>())!.ImageUrl.Should().Be(imageUrl);
        (await RunesOfAsync(sheetId, playerToken)).Should().ContainSingle(r => r.Nome == "Runa Ilustrada" && r.ImageUrl == imageUrl);
    }

    [Fact]
    public async Task A_rune_without_image_has_a_null_ImageUrl()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneImgGm2", "runeimg2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RuneImgPlayer2", "runeimgplayer2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest("Runa Nua", "Sem imagem.", 1, null, "")));

        (await RunesOfAsync(sheetId, playerToken)).Should().ContainSingle(r => r.Nome == "Runa Nua" && r.ImageUrl == null);
    }

    [Fact]
    public async Task The_automatic_bank_copy_carries_the_image()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneImgGm3", "runeimg3@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "RuneImgPlayer3", "runeimgplayer3@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gmToken, playerId);
        var (imageId, imageUrl) = await UploadImageAsync(gmToken);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", gmToken,
            new AddCharacterRuneRequest("Runa do GM", "Com imagem.", 2, null, imageId)));

        (await BankOfAsync(gmToken)).Should().ContainSingle(e => e.Nome == "Runa do GM" && e.ImageId == imageId && e.ImageUrl == imageUrl);
    }

    [Fact]
    public async Task A_jogador_can_use_an_image_the_gm_published_in_the_campaign()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneImgGm4", "runeimg4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RuneImgPlayer4", "runeimgplayer4@teste.com");
        var (sheetId, campaignId) = await SetUpSheetInCampaignAsync(gmToken, playerId);
        var (imageId, imageUrl) = await UploadImageAsync(gmToken);
        await PublishImageToCampaignAsync(gmToken, campaignId, imageId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest("Runa Liberada", "Imagem do GM.", 1, null, imageId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await RunesOfAsync(sheetId, playerToken)).Should().ContainSingle(r => r.ImageUrl == imageUrl);
    }

    [Fact]
    public async Task A_jogador_cannot_use_a_non_public_image_of_the_gm()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneImgGm5", "runeimg5@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RuneImgPlayer5", "runeimgplayer5@teste.com");
        var (sheetId, campaignId) = await SetUpSheetInCampaignAsync(gmToken, playerId);
        var (naoAnexada, _) = await UploadImageAsync(gmToken);
        var (privada, _) = await UploadImageAsync(gmToken);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, null, privada, null))); // anexada, mas privada

        var semAnexo = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest("Runa", "X.", 1, null, naoAnexada)));
        var comAnexoPrivado = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest("Runa", "X.", 1, null, privada)));

        semAnexo.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        comAnexoPrivado.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RunesOfAsync(sheetId, playerToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_public_image_of_another_campaign_is_not_usable()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneImgGm6", "runeimg6@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RuneImgPlayer6", "runeimgplayer6@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gmToken, playerId);
        var (_, otherCampaignId) = await SetUpSheetInCampaignAsync(gmToken, playerId);
        var (imageId, _) = await UploadImageAsync(gmToken);
        await PublishImageToCampaignAsync(gmToken, otherCampaignId, imageId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest("Runa", "X.", 1, null, imageId)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task A_malformed_or_unknown_image_id_returns_400(string imageId)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"RuneImgGm7{imageId.Length}", $"runeimg7{imageId.Length}@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, $"RuneImgPlayer7{imageId.Length}", $"runeimgplayer7{imageId.Length}@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", gmToken,
            new AddCharacterRuneRequest("Runa", "X.", 1, null, imageId)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_rune_picked_from_the_bank_inherits_the_entrys_image_on_the_rune_and_on_the_new_bank_copy()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneImgGm8", "runeimg8@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "RuneImgPlayer8", "runeimgplayer8@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gmToken, playerId);
        var (imageId, imageUrl) = await UploadImageAsync(gmToken);
        var entryResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", gmToken,
            new CreateRuneBankEntryRequest("Runa Herdeira", "Herda a imagem.", 1, imageId)));
        var entry = (await entryResponse.Content.ReadFromJsonAsync<RuneBankEntryResponse>())!;

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", gmToken,
            new AddCharacterRuneRequest(null, null, null, entry.Id)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<CharacterRuneResponse>())!.ImageUrl.Should().Be(imageUrl);
        (await RunesOfAsync(sheetId, gmToken)).Should().ContainSingle(r => r.Nome == "Runa Herdeira" && r.ImageUrl == imageUrl);
        (await BankOfAsync(gmToken)).Where(e => e.Nome == "Runa Herdeira").Should().HaveCount(2).And.OnlyContain(e => e.ImageId == imageId);
    }

    [Fact]
    public async Task Picking_from_the_bank_together_with_an_image_id_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneImgGm9", "runeimg9@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "RuneImgPlayer9", "runeimgplayer9@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gmToken, playerId);
        var (imageId, _) = await UploadImageAsync(gmToken);
        var entryId = await CreateRuneEntryAsync(gmToken, "Runa", "Desc.", 1);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", gmToken,
            new AddCharacterRuneRequest(null, null, null, entryId, imageId)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
