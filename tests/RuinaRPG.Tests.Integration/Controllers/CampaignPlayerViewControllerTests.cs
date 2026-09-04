using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CampaignPlayerViewControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CampaignPlayerViewControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private static CreateItemRequest MinimalItemGeral(string nome) =>
        new("ItemGeral", nome, 0.5m, 5, null, "Equipamentos de Aventura", "Uma corda resistente.",
            null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null,
            null, null, null, null);

    private async Task<string> CreateItemAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken, MinimalItemGeral(nome)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    private static UpdateNpcSheetRequest NpcUpdateWithNome(string nome) => new(
        null, nome, "Humano", "Sinir", "Campeao", "Duelista", "Fogo", "Guardiã do Portal",
        5, true, 750, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100);

    private async Task<string> CreateNpcSheetAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        var npcId = (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{npcId}", gmToken, NpcUpdateWithNome(nome)));
        return npcId;
    }

    private async Task<string> AttachAsync(string gmToken, string campaignId, AttachToCampaignRequest request)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken, request));
        return (await response.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;
    }

    private record Setup(string GmToken, string PlayerToken, string PlayerId, string CampaignId, string CharacterSheetId, string PublicItemId, string PrivateItemId, string NpcAttachmentId);

    private async Task<Setup> BuildSetupAsync(string suffix)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"PvGm{suffix}", $"pvgm{suffix}@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, $"PvPlayer{suffix}", $"pvplayer{suffix}@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, $"Campanha PV {suffix}");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));

        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        var characterSheetId = (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;

        var publicItemId = await CreateItemAsync(gmToken, $"Corda Pública {suffix}");
        var publicAttachmentId = await AttachAsync(gmToken, campaignId, new AttachToCampaignRequest(publicItemId, null, null, null, null));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{publicAttachmentId}/visibility", gmToken, true));

        var privateItemId = await CreateItemAsync(gmToken, $"Corda Privada {suffix}");
        await AttachAsync(gmToken, campaignId, new AttachToCampaignRequest(privateItemId, null, null, null, null));

        var npcId = await CreateNpcSheetAsync(gmToken, $"Fido {suffix}");
        var npcAttachmentId = await AttachAsync(gmToken, campaignId, new AttachToCampaignRequest(null, npcId, null, null, null));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{npcAttachmentId}/npc-visibility", gmToken,
            new { nomePublico = true, imagemPublica = false }));

        return new Setup(gmToken, playerToken, playerId, campaignId, characterSheetId, publicItemId, privateItemId, npcAttachmentId);
    }

    [Fact]
    public async Task PlayerView_includes_the_players_own_sheet()
    {
        var setup = await BuildSetupAsync("Own");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/player-view", setup.PlayerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PlayerCampaignViewResponse>();
        body!.MinhasFichas.Should().ContainSingle(s => s.Id == setup.CharacterSheetId && s.Nivel == 1);
    }

    [Fact]
    public async Task PlayerView_includes_only_the_public_item_not_the_private_one()
    {
        var setup = await BuildSetupAsync("Item");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/player-view", setup.PlayerToken));

        var body = await response.Content.ReadFromJsonAsync<PlayerCampaignViewResponse>();
        body!.AnexosPublicos.Should().ContainSingle(a => a.Tipo == "Item" && a.Nome == "Corda Pública Item");
        body.AnexosPublicos.Should().NotContain(a => a.Nome == "Corda Privada Item");
    }

    [Fact]
    public async Task PlayerView_Item_ImageUrl_uses_the_images_prefix()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("PvImgGm", "pvimggm@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "PvImgPlayer", "pvimgplayer@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha PV Img");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));

        var (imageId, imageUrl) = await UploadImageWithUrlAsync(gmToken);
        var itemRequest = MinimalItemGeral("Espelho") with { ImageId = imageId };
        var itemResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken, itemRequest));
        var itemId = (await itemResponse.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
        var attachmentId = await AttachAsync(gmToken, campaignId, new AttachToCampaignRequest(itemId, null, null, null, null));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/visibility", gmToken, true));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/player-view", playerToken));

        var body = await response.Content.ReadFromJsonAsync<PlayerCampaignViewResponse>();
        var entry = body!.AnexosPublicos.Should().ContainSingle(a => a.Id == attachmentId).Subject;
        entry.ImageUrl.Should().Be(imageUrl);
    }

    private async Task<(string Id, string Url)> UploadImageWithUrlAsync(string gmToken)
    {
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var response = await _client.SendAsync(message);
        var body = await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Images.ImageUploadResponse>();
        return (body!.Id, body.Url);
    }

    [Fact]
    public async Task PlayerView_for_an_npc_attachment_shows_only_the_toggled_public_fields()
    {
        var setup = await BuildSetupAsync("Npc");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/player-view", setup.PlayerToken));

        var body = await response.Content.ReadFromJsonAsync<PlayerCampaignViewResponse>();
        var npcEntry = body!.AnexosPublicos.Should().ContainSingle(a => a.Id == setup.NpcAttachmentId).Subject;
        npcEntry.Tipo.Should().Be("NpcSheet");
        npcEntry.Nome.Should().Be("Fido Npc");
        npcEntry.ImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task PlayerView_by_a_non_member_returns_403()
    {
        var setup = await BuildSetupAsync("NonMember");
        var (_, outsiderToken) = await RegisterJogadorLinkedToAsync(setup.GmToken, "PvOutsider", "pvoutsider@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/player-view", outsiderToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PlayerView_by_the_gm_also_succeeds_gm_is_always_allowed_to_see_it_too()
    {
        var setup = await BuildSetupAsync("Gm");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/player-view", setup.GmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PlayerView_excludes_a_fully_private_npc_attachment_entirely()
    {
        var setup = await BuildSetupAsync("PrivateNpc");
        // BuildSetupAsync's own NPC attachment already toggles NomePublico on, so build a second
        // one here and deliberately leave both toggles at their false default.
        var privateNpcId = await CreateNpcSheetAsync(setup.GmToken, "Fido Secreto PrivateNpc");
        var privateNpcAttachmentId = await AttachAsync(setup.GmToken, setup.CampaignId, new AttachToCampaignRequest(null, privateNpcId, null, null, null));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/player-view", setup.PlayerToken));

        var body = await response.Content.ReadFromJsonAsync<PlayerCampaignViewResponse>();
        // R0008: the whole entry must be absent, matching the code's
        // Where(a => a.NpcSheetId is not null && (a.NpcNomePublico || a.NpcImagemPublica)) filter —
        // not merely present with null fields.
        body!.AnexosPublicos.Should().NotContain(a => a.Id == privateNpcAttachmentId);
    }

    [Fact]
    public async Task ListSecretNotes_by_a_non_member_returns_403()
    {
        var setup = await BuildSetupAsync("SecretNonMember");
        var (_, outsiderToken) = await RegisterJogadorLinkedToAsync(setup.GmToken, "PvSecretOutsider", "pvsecretoutsider@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/secret-notes", outsiderToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListMine_returns_only_campaigns_the_player_is_a_member_of()
    {
        // R0009's own precondition: a player has no way to discover their campaign(s) otherwise —
        // GET api/campaigns is GM-only and lists a different thing (campaigns the caller GMs).
        var gmToken = await RegisterGmAndGetTokenAsync("MineGm1", "minegm1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MinePlayer1", "mineplayer1@teste.com");
        var memberCampaignId = await CreateCampaignAsync(gmToken, "Campanha Onde Sou Membro");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{memberCampaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        await CreateCampaignAsync(gmToken, "Campanha Onde Não Sou Membro"); // same GM, player never added

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/campaigns/mine", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<CampaignResponse>>();
        body!.Should().ContainSingle(c => c.Id == memberCampaignId && c.Nome == "Campanha Onde Sou Membro");
    }

    [Fact]
    public async Task ListMine_for_a_player_in_no_campaigns_returns_an_empty_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MineGm2", "minegm2@teste.com");
        var (_, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MinePlayer2", "mineplayer2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/campaigns/mine", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<CampaignResponse>>();
        body!.Should().BeEmpty();
    }
}
