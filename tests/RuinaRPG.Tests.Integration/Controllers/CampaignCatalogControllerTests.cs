using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Images;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CampaignCatalogControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CampaignCatalogControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> CreateCampaignAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        return (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
    }

    private static CreateItemRequest MinimalItem(string nome, string tipo = "ItemGeral") =>
        new(tipo, nome, 0.5m, 5, null, "Equipamentos de Aventura", "Descrição.",
            null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null,
            null, null, null, null, null);

    private async Task<string> CreateItemAsync(string gmToken, string nome, string tipo = "ItemGeral")
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken, MinimalItem(nome, tipo)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    private async Task<string> CreateBankEntryAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
            new CreateSpellAbilityEntryRequest(nome, "Magia", 1, "Descrição.", [])));
        return (await response.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!.Id;
    }

    private async Task<string> UploadImageAsync(string gmToken)
    {
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var response = await _client.SendAsync(message);
        return (await response.Content.ReadFromJsonAsync<ImageUploadResponse>())!.Id;
    }

    private async Task<string> AttachAndPublishAsync(string gmToken, string campaignId, AttachToCampaignRequest request)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken, request));
        var attachmentId = (await response.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/visibility", gmToken, true));
        return attachmentId;
    }

    private record Setup(string GmToken, string PlayerToken, string CampaignId);

    private async Task<Setup> BuildMemberSetupAsync(string suffix)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"CatGm{suffix}", $"catgm{suffix}@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, $"CatPlayer{suffix}", $"catplayer{suffix}@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, $"Campanha Catalogo {suffix}");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        return new Setup(gmToken, playerToken, campaignId);
    }

    [Fact]
    public async Task AvailableItems_returns_only_publicly_attached_items()
    {
        var setup = await BuildMemberSetupAsync("Items1");
        var publicItemId = await CreateItemAsync(setup.GmToken, "Corda Pública");
        await AttachAndPublishAsync(setup.GmToken, setup.CampaignId, new AttachToCampaignRequest(publicItemId, null, null, null, null));
        var privateItemId = await CreateItemAsync(setup.GmToken, "Corda Privada");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{setup.CampaignId}/attachments", setup.GmToken,
            new AttachToCampaignRequest(privateItemId, null, null, null, null))); // attached but never made public

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/available-items", setup.PlayerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<ItemResponse>>();
        body!.Should().ContainSingle(i => i.Id == publicItemId);
        body.Should().NotContain(i => i.Id == privateItemId);
    }

    [Fact]
    public async Task AvailableItems_tipo_filter_matches_the_ItemsController_behavior()
    {
        var setup = await BuildMemberSetupAsync("Items2");
        var weaponId = await CreateItemAsync(setup.GmToken, "Espada Longa", "Arma");
        await AttachAndPublishAsync(setup.GmToken, setup.CampaignId, new AttachToCampaignRequest(weaponId, null, null, null, null));
        var generalId = await CreateItemAsync(setup.GmToken, "Corda", "ItemGeral");
        await AttachAndPublishAsync(setup.GmToken, setup.CampaignId, new AttachToCampaignRequest(generalId, null, null, null, null));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/available-items?tipo=Arma", setup.PlayerToken));

        var body = await response.Content.ReadFromJsonAsync<List<ItemResponse>>();
        body!.Should().ContainSingle(i => i.Id == weaponId);
        body.Should().NotContain(i => i.Id == generalId);
    }

    [Fact]
    public async Task AvailableItems_by_a_non_member_returns_403()
    {
        var setup = await BuildMemberSetupAsync("Items3");
        var (_, outsiderToken) = await RegisterJogadorLinkedToAsync(setup.GmToken, "CatOutsiderItems3", "catoutsideritems3@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/available-items", outsiderToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AvailableSpellAbilities_returns_only_publicly_attached_entries()
    {
        var setup = await BuildMemberSetupAsync("Spells1");
        var publicEntryId = await CreateBankEntryAsync(setup.GmToken, "Bola de Fogo");
        await AttachAndPublishAsync(setup.GmToken, setup.CampaignId, new AttachToCampaignRequest(null, null, null, publicEntryId, null));
        var privateEntryId = await CreateBankEntryAsync(setup.GmToken, "Segredo do GM");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{setup.CampaignId}/attachments", setup.GmToken,
            new AttachToCampaignRequest(null, null, null, privateEntryId, null)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/available-spell-abilities", setup.PlayerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        body!.Should().ContainSingle(e => e.Id == publicEntryId);
        body.Should().NotContain(e => e.Id == privateEntryId);
    }

    [Fact]
    public async Task AvailableImages_returns_only_publicly_attached_images()
    {
        var setup = await BuildMemberSetupAsync("Images1");
        var publicImageId = await UploadImageAsync(setup.GmToken);
        await AttachAndPublishAsync(setup.GmToken, setup.CampaignId, new AttachToCampaignRequest(null, null, null, null, publicImageId));
        var privateImageId = await UploadImageAsync(setup.GmToken);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{setup.CampaignId}/attachments", setup.GmToken,
            new AttachToCampaignRequest(null, null, null, null, privateImageId)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/available-images", setup.PlayerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<ImageSummaryResponse>>();
        body!.Should().ContainSingle(i => i.Id == publicImageId);
        body.Should().NotContain(i => i.Id == privateImageId);
    }

    [Fact]
    public async Task AvailableImages_for_a_nonexistent_campaign_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CatNotFoundGm", "catnotfoundgm@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{Guid.NewGuid()}/available-images", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
