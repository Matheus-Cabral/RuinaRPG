using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Contracts.Runes;

namespace RuinaRPG.Tests.Integration.Controllers;

public class NpcRunesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public NpcRunesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> CreateSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
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

    private async Task<(string SheetId, string CampaignId)> GrantBlankNpcAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var grantResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken, new GrantSheetRequest(playerId, "Npc", null)));
        return ((await grantResponse.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId, campaignId);
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
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneGm1", "npcrune1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", gmToken,
            new AddNpcRuneRequest("Runa do Fogo", "Queima o alvo.", 1)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/runes", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcRuneResponse>>();
        body!.Should().ContainSingle(r => r.Nome == "Runa do Fogo" && r.Descricao == "Queima o alvo." && r.Grau == 1);
    }

    [Fact]
    public async Task List_returns_the_runes_on_the_sheet()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneGm2", "npcrune2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", gmToken,
            new AddNpcRuneRequest("Runa da Terra", "Endurece a pele.", 2)));

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/runes", gmToken));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcRuneResponse>>();
        body!.Should().ContainSingle(r => r.Nome == "Runa da Terra" && r.Descricao == "Endurece a pele." && r.Grau == 2);
    }

    [Fact]
    public async Task Delete_an_existing_rune_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneGm3", "npcrune3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", gmToken,
            new AddNpcRuneRequest("Runa da Água", "Cura ferimentos leves.", 3)));
        var added = await addResponse.Content.ReadFromJsonAsync<NpcRuneResponse>();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/npc-sheets/{sheetId}/runes/{added!.Id}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/runes", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcRuneResponse>>();
        body!.Should().NotContain(r => r.Id == added.Id);
    }

    [Fact]
    public async Task Add_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcRuneGmOwner4", "npcruneowner4@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcRuneGmOther4", "npcruneother4@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", gmTokenOther,
            new AddNpcRuneRequest("Runa do Vento", "Aumenta velocidade.", 1)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_rune_added_by_the_gm_lands_in_the_bank()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneBkGm1", "npcrunebk1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", gmToken,
            new AddNpcRuneRequest("Runa do Fogo", "Queima o alvo.", 2)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await BankOfAsync(gmToken)).Should().ContainSingle(e => e.Nome == "Runa do Fogo" && e.Grau == 2);
    }

    [Fact]
    public async Task A_rune_added_by_the_player_a_npc_was_granted_to_is_also_published_to_the_campaign()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneBkGm2", "npcrunebk2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "NpcRuneBkPlayer2", "npcrunebkplayer2@teste.com");
        var (sheetId, campaignId) = await GrantBlankNpcAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", playerToken,
            new AddNpcRuneRequest("Runa da Terra", "Endurece a pele.", 1)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await BankOfAsync(gmToken)).Should().ContainSingle(e => e.Nome == "Runa da Terra");
        (await PublicRunesAsync(playerToken, campaignId)).Should().ContainSingle(e => e.Nome == "Runa da Terra");
    }

    [Fact]
    public async Task A_rune_picked_from_the_bank_copies_its_fields()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneBkGm3", "npcrunebk3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var entryId = await CreateRuneEntryAsync(gmToken, "Runa da Luz", "Ilumina a área.", 3);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", gmToken,
            new AddNpcRuneRequest(null, null, null, entryId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<NpcRuneResponse>();
        created!.Nome.Should().Be("Runa da Luz");
        created.Descricao.Should().Be("Ilumina a área.");
        created.Grau.Should().Be(3);
        (await BankOfAsync(gmToken)).Count(e => e.Nome == "Runa da Luz").Should().Be(2);
    }

    [Fact]
    public async Task A_player_can_only_pick_bank_entries_the_gm_published_to_the_campaign()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneBkGm4", "npcrunebk4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "NpcRuneBkPlayer4", "npcrunebkplayer4@teste.com");
        var (sheetId, campaignId) = await GrantBlankNpcAsync(gmToken, playerId);
        var entryId = await CreateRuneEntryAsync(gmToken, "Runa Reservada", "Só depois de liberada.", 1);

        var antes = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", playerToken,
            new AddNpcRuneRequest(null, null, null, entryId)));
        antes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await PublishRuneToCampaignAsync(gmToken, campaignId, entryId);

        var depois = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", playerToken,
            new AddNpcRuneRequest(null, null, null, entryId)));
        depois.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Adding_with_both_paths_or_with_neither_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneBkGm5", "npcrunebk5@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var entryId = await CreateRuneEntryAsync(gmToken, "Runa", "Desc.", 1);

        var both = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", gmToken,
            new AddNpcRuneRequest("Runa", "Desc.", 1, entryId)));
        var neither = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", gmToken,
            new AddNpcRuneRequest(null, null, null)));

        both.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        neither.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task Picking_a_malformed_or_unknown_bank_entry_returns_400(string entryId)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"NpcRuneBkGm6{entryId.Length}", $"npcrunebk6{entryId.Length}@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", gmToken,
            new AddNpcRuneRequest(null, null, null, entryId)));

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

    private async Task<List<NpcRuneResponse>> RunesOfAsync(string sheetId, string token)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/runes", token));
        return (await response.Content.ReadFromJsonAsync<List<NpcRuneResponse>>())!;
    }

    [Fact]
    public async Task A_rune_from_scratch_with_the_callers_own_image_returns_201_and_the_list_has_the_ImageUrl()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneImgGm1", "npcruneimg1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "NpcRuneImgPlayer1", "npcruneimgplayer1@teste.com");
        var (sheetId, campaignId) = await GrantBlankNpcAsync(gmToken, playerId);
        var (imageId, imageUrl) = await UploadImageAsync(playerToken, campaignId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", playerToken,
            new AddNpcRuneRequest("Runa Ilustrada", "Tem imagem.", 1, null, imageId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<NpcRuneResponse>())!.ImageUrl.Should().Be(imageUrl);
        (await RunesOfAsync(sheetId, playerToken)).Should().ContainSingle(r => r.Nome == "Runa Ilustrada" && r.ImageUrl == imageUrl);
    }

    [Fact]
    public async Task The_automatic_bank_copy_carries_the_image()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneImgGm2", "npcruneimg2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var (imageId, imageUrl) = await UploadImageAsync(gmToken);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", gmToken,
            new AddNpcRuneRequest("Runa do GM", "Com imagem.", 2, null, imageId)));

        (await BankOfAsync(gmToken)).Should().ContainSingle(e => e.Nome == "Runa do GM" && e.ImageId == imageId && e.ImageUrl == imageUrl);
    }

    [Fact]
    public async Task A_jogador_can_use_an_image_the_gm_published_in_the_campaign()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneImgGm3", "npcruneimg3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "NpcRuneImgPlayer3", "npcruneimgplayer3@teste.com");
        var (sheetId, campaignId) = await GrantBlankNpcAsync(gmToken, playerId);
        var (imageId, imageUrl) = await UploadImageAsync(gmToken);
        await PublishImageToCampaignAsync(gmToken, campaignId, imageId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", playerToken,
            new AddNpcRuneRequest("Runa Liberada", "Imagem do GM.", 1, null, imageId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await RunesOfAsync(sheetId, playerToken)).Should().ContainSingle(r => r.ImageUrl == imageUrl);
    }

    [Fact]
    public async Task A_jogador_cannot_use_a_non_public_image_of_the_gm()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneImgGm4", "npcruneimg4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "NpcRuneImgPlayer4", "npcruneimgplayer4@teste.com");
        var (sheetId, campaignId) = await GrantBlankNpcAsync(gmToken, playerId);
        var (naoAnexada, _) = await UploadImageAsync(gmToken);
        var (privada, _) = await UploadImageAsync(gmToken);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, null, privada, null))); // anexada, mas privada

        var semAnexo = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", playerToken,
            new AddNpcRuneRequest("Runa", "X.", 1, null, naoAnexada)));
        var comAnexoPrivado = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", playerToken,
            new AddNpcRuneRequest("Runa", "X.", 1, null, privada)));

        semAnexo.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        comAnexoPrivado.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RunesOfAsync(sheetId, playerToken)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task A_malformed_or_unknown_image_id_returns_400(string imageId)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"NpcRuneImgGm5{imageId.Length}", $"npcruneimg5{imageId.Length}@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", gmToken,
            new AddNpcRuneRequest("Runa", "X.", 1, null, imageId)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_rune_picked_from_the_bank_inherits_the_entrys_image_on_the_rune_and_on_the_new_bank_copy()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneImgGm6", "npcruneimg6@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var (imageId, imageUrl) = await UploadImageAsync(gmToken);
        var entryResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", gmToken,
            new CreateRuneBankEntryRequest("Runa Herdeira", "Herda a imagem.", 1, imageId)));
        var entry = (await entryResponse.Content.ReadFromJsonAsync<RuneBankEntryResponse>())!;

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", gmToken,
            new AddNpcRuneRequest(null, null, null, entry.Id)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<NpcRuneResponse>())!.ImageUrl.Should().Be(imageUrl);
        (await RunesOfAsync(sheetId, gmToken)).Should().ContainSingle(r => r.Nome == "Runa Herdeira" && r.ImageUrl == imageUrl);
        (await BankOfAsync(gmToken)).Where(e => e.Nome == "Runa Herdeira").Should().HaveCount(2).And.OnlyContain(e => e.ImageId == imageId);
    }

    [Fact]
    public async Task Picking_from_the_bank_together_with_an_image_id_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneImgGm7", "npcruneimg7@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var (imageId, _) = await UploadImageAsync(gmToken);
        var entryId = await CreateRuneEntryAsync(gmToken, "Runa", "Desc.", 1);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", gmToken,
            new AddNpcRuneRequest(null, null, null, entryId, imageId)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
