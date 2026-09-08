using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CampaignAttachmentsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CampaignAttachmentsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> CreateCampaignAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        return (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
    }

    private static CreateItemRequest MinimalItemGeral(string nome) =>
        new("ItemGeral", nome, 0.5m, 5, null, "Equipamentos de Aventura", "Uma corda resistente.",
            null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null,
            null, null, null, null, null);

    private async Task<string> CreateItemAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken, MinimalItemGeral(nome)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    private async Task<string> CreateBankEntryAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
            new CreateSpellAbilityEntryRequest(nome, "Magia", 1, "Descrição.", [])));
        return (await response.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!.Id;
    }

    private async Task<string> UploadImageAsync(string gmToken) => (await UploadImageWithUrlAsync(gmToken)).Id;

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

    private async Task<string> CreateItemWithImageAsync(string gmToken, string nome, string imageId)
    {
        var request = MinimalItemGeral(nome) with { ImageId = imageId };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken, request));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    private static RuinaRPG.Contracts.NpcSheets.UpdateNpcSheetRequest NpcUpdateWithImage(string? imageId) => new(
        imageId, "Sentinela da Ruína", "Humano", "Sinir", "Campeao", "Duelista", "Fogo", "Guardiã do Portal",
        5, true, 750, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100);

    private static RuinaRPG.Contracts.CreatureSheets.UpdateCreatureSheetRequest CreatureUpdateWithImage(string? imageId) => new(
        imageId, "Lobo das Ruínas", "Lobo", "Fisico", "Predador", "Terra",
        "F", 3, 200, 5, 12, 8, 10, "Parcial");

    private async Task<string> CreateNpcSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.NpcSheets.NpcSheetResponse>())!.Id;
    }

    private async Task<string> CreateCreatureSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.CreatureSheets.CreatureSheetResponse>())!.Id;
    }

    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<InviteCodeResponse>())!.Code;

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();

        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        var meBody = await meResponse.Content.ReadFromJsonAsync<MeResponse>();
        return (meBody!.Id, tokens.AccessToken);
    }

    private async Task AddMemberAsync(string gmToken, string campaignId, string playerId) =>
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));

    [Fact]
    public async Task Attach_an_item_defaults_to_private()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttGm1", "att1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Anexo");
        var itemId = await CreateItemAsync(gmToken, "Corda");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(itemId, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CampaignAttachmentResponse>();
        body!.Tipo.Should().Be("Item");
        body.Nome.Should().Be("Corda");
        body.IsPublic.Should().BeFalse();
    }

    [Fact]
    public async Task Attach_with_no_target_set_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttGm2", "att2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Sem Alvo");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Attach_with_two_targets_set_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttGm3", "att3@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Dois Alvos");
        var itemId = await CreateItemAsync(gmToken, "Corda");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(itemId, null, null, null, itemId)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Attach_with_a_malformed_ItemId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttGm6", "att6@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha ItemId Malformado");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest("not-a-guid", null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Attach_with_a_nonexistent_ItemId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttGm7", "att7@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha ItemId Inexistente");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(Guid.NewGuid().ToString(), null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ToggleVisibility_flips_IsPublic_for_an_item_attachment()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttGm4", "att4@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Toggle");
        var itemId = await CreateItemAsync(gmToken, "Corda");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(itemId, null, null, null, null)));
        var attachmentId = (await createResponse.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;

        var toggleResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/visibility", gmToken, true));
        toggleResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/attachments", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CampaignAttachmentResponse>>();
        body!.Should().ContainSingle(a => a.Id == attachmentId && a.IsPublic == true);
    }

    [Fact]
    public async Task Delete_removes_the_attachment_but_not_the_underlying_item()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttGm5", "att5@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Delete");
        var itemId = await CreateItemAsync(gmToken, "Corda");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(itemId, null, null, null, null)));
        var attachmentId = (await createResponse.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/campaigns/{campaignId}/attachments/{attachmentId}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/attachments", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CampaignAttachmentResponse>>();
        body!.Should().BeEmpty();

        var itemsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/items", gmToken));
        var items = await itemsResponse.Content.ReadFromJsonAsync<List<ItemResponse>>();
        items!.Should().ContainSingle(i => i.Id == itemId);
    }

    [Fact]
    public async Task Attach_by_a_different_gm_to_someone_elses_campaign_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("AttOwner", "attowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("AttOther", "attother@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha Alheia");
        var itemId = await CreateItemAsync(gmTokenOther, "Corda Alheia");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmTokenOther,
            new AttachToCampaignRequest(itemId, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Attach_an_npc_defaults_both_toggles_off()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttNpcGm1", "attnpc1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com NPC");
        var npcId = await CreateNpcSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, npcId, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CampaignAttachmentResponse>();
        body!.Tipo.Should().Be("NpcSheet");
        body.NpcNomePublico.Should().BeFalse();
        body.NpcImagemPublica.Should().BeFalse();
    }

    [Fact]
    public async Task NpcVisibility_can_toggle_Nome_and_Imagem_independently()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttNpcGm2", "attnpc2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Toggle NPC");
        var npcId = await CreateNpcSheetAsync(gmToken);
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, npcId, null, null, null)));
        var attachmentId = (await createResponse.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;

        // Toggle both on
        var toggleResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/npc-visibility", gmToken,
            new { nomePublico = true, imagemPublica = true }));
        toggleResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/attachments", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CampaignAttachmentResponse>>();
        var attachment = body!.Should().ContainSingle(a => a.Id == attachmentId).Subject;
        attachment.NpcNomePublico.Should().BeTrue();
        attachment.NpcImagemPublica.Should().BeTrue();

        // Toggle nome off, imagem on
        var toggleResponse2 = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/npc-visibility", gmToken,
            new { nomePublico = false, imagemPublica = true }));
        toggleResponse2.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse2 = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/attachments", gmToken));
        var body2 = await listResponse2.Content.ReadFromJsonAsync<List<CampaignAttachmentResponse>>();
        var attachment2 = body2!.Should().ContainSingle(a => a.Id == attachmentId).Subject;
        attachment2.NpcNomePublico.Should().BeFalse();
        attachment2.NpcImagemPublica.Should().BeTrue();
    }

    [Fact]
    public async Task NpcVisibility_on_an_item_attachment_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttNpcGm3", "attnpc3@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Item Bad Request");
        var itemId = await CreateItemAsync(gmToken, "Corda");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(itemId, null, null, null, null)));
        var attachmentId = (await createResponse.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;

        var toggleResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/npc-visibility", gmToken,
            new { nomePublico = true, imagemPublica = true }));

        toggleResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatureVisibility_can_toggle_Nome_and_Imagem_independently()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttCreatureGm1", "attcreature1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Toggle Creature");
        var creatureId = await CreateCreatureSheetAsync(gmToken);
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, creatureId, null, null)));
        var attachmentId = (await createResponse.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;

        // Toggle both on
        var toggleResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/creature-visibility", gmToken,
            new { nomePublico = true, imagemPublica = true }));
        toggleResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/attachments", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CampaignAttachmentResponse>>();
        var attachment = body!.Should().ContainSingle(a => a.Id == attachmentId).Subject;
        attachment.CreatureNomePublico.Should().BeTrue();
        attachment.CreatureImagemPublica.Should().BeTrue();

        // Toggle nome off, imagem on
        var toggleResponse2 = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/creature-visibility", gmToken,
            new { nomePublico = false, imagemPublica = true }));
        toggleResponse2.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse2 = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/attachments", gmToken));
        var body2 = await listResponse2.Content.ReadFromJsonAsync<List<CampaignAttachmentResponse>>();
        var attachment2 = body2!.Should().ContainSingle(a => a.Id == attachmentId).Subject;
        attachment2.CreatureNomePublico.Should().BeFalse();
        attachment2.CreatureImagemPublica.Should().BeTrue();
    }

    [Fact]
    public async Task Attach_a_npc_sheet_owned_by_another_gm_returns_400()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("AttNpcCrossOwner", "attnpccrossowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("AttNpcCrossOther", "attnpccrossother@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha Cross GM NPC");
        var npcOfOther = await CreateNpcSheetAsync(gmTokenOther);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmTokenOwner,
            new AttachToCampaignRequest(null, npcOfOther, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Attach_a_creature_sheet_owned_by_another_gm_returns_400()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("AttCreatureCrossOwner", "attcreaturecrossowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("AttCreatureCrossOther", "attcreaturecrossother@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha Cross GM Creature");
        var creatureOfOther = await CreateCreatureSheetAsync(gmTokenOther);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmTokenOwner,
            new AttachToCampaignRequest(null, null, creatureOfOther, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_excludes_grant_link_attachments_but_still_includes_genuine_npc_attachments()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttGrantListGm", "attgrantlist@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "AttGrantListPlayer", "attgrantlistplayer@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Grant Link");
        await AddMemberAsync(gmToken, campaignId, playerId);

        // Grant flow inserts its own campaign-link CampaignAttachment row for the granted sheet.
        var grantResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken,
            new GrantSheetRequest(playerId, "Npc", null)));
        grantResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // A genuine, GM-authored display attachment of an NPC the GM still owns (not granted).
        var displayNpcId = await CreateNpcSheetAsync(gmToken);
        var attachResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, displayNpcId, null, null, null)));
        attachResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var displayAttachmentId = (await attachResponse.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/attachments", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CampaignAttachmentResponse>>();

        body!.Should().ContainSingle(a => a.Id == displayAttachmentId);
        body!.Should().HaveCount(1);
    }

    [Fact]
    public async Task Attach_a_bank_entry_owned_by_another_gm_returns_400()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("AttBankCrossOwner", "attbankcrossowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("AttBankCrossOther", "attbankcrossother@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha Cross GM Banco");
        var bankEntryOfOther = await CreateBankEntryAsync(gmTokenOther, "Bola de Fogo Alheia");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmTokenOwner,
            new AttachToCampaignRequest(null, null, null, bankEntryOfOther, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Attach_an_image_owned_by_another_gm_returns_400()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("AttImgCrossOwner", "attimgcrossowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("AttImgCrossOther", "attimgcrossother@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha Cross GM Imagem");
        var imageOfOther = await UploadImageAsync(gmTokenOther);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmTokenOwner,
            new AttachToCampaignRequest(null, null, null, null, imageOfOther)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Attach_a_bank_entry_owned_by_the_caller_returns_201()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttBankOwn", "attbankown@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Banco Próprio");
        var bankEntryId = await CreateBankEntryAsync(gmToken, "Bola de Fogo");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, bankEntryId, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CampaignAttachmentResponse>();
        body!.Tipo.Should().Be("SpellAbilityBankEntry");
    }

    [Fact]
    public async Task Attach_an_image_owned_by_the_caller_returns_201()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttImgOwn", "attimgown@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Imagem Própria");
        var imageId = await UploadImageAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, null, imageId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CampaignAttachmentResponse>();
        body!.Tipo.Should().Be("Image");
    }

    [Fact]
    public async Task Attaching_an_item_with_an_image_surfaces_the_items_ImageUrl()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttItemImgGm", "attitemimg@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Item Com Imagem");
        var (imageId, imageUrl) = await UploadImageWithUrlAsync(gmToken);
        var itemId = await CreateItemWithImageAsync(gmToken, "Escudo Retumbante", imageId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(itemId, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CampaignAttachmentResponse>();
        body!.ImageUrl.Should().Be(imageUrl);
    }

    [Fact]
    public async Task An_item_attachment_without_an_image_has_a_null_ImageUrl()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttItemNoImgGm", "attitemnoimg@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Item Sem Imagem");
        var itemId = await CreateItemAsync(gmToken, "Corda");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(itemId, null, null, null, null)));

        var body = await response.Content.ReadFromJsonAsync<CampaignAttachmentResponse>();
        body!.ImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task Attaching_an_npc_sheet_with_an_image_surfaces_its_profile_ImageUrl()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttNpcImgGm", "attnpcimg@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha NPC Com Imagem");
        var (imageId, imageUrl) = await UploadImageWithUrlAsync(gmToken);
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        var npcId = (await createResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.NpcSheets.NpcSheetResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{npcId}", gmToken, NpcUpdateWithImage(imageId)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, npcId, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CampaignAttachmentResponse>();
        body!.ImageUrl.Should().Be(imageUrl);
    }

    [Fact]
    public async Task Attaching_a_creature_sheet_with_an_image_surfaces_its_profile_ImageUrl()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttCreatureImgGm", "attcreatureimg@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Criatura Com Imagem");
        var (imageId, imageUrl) = await UploadImageWithUrlAsync(gmToken);
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        var creatureId = (await createResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.CreatureSheets.CreatureSheetResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{creatureId}", gmToken, CreatureUpdateWithImage(imageId)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, creatureId, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CampaignAttachmentResponse>();
        body!.ImageUrl.Should().Be(imageUrl);
    }

    [Fact]
    public async Task Attaching_an_image_directly_surfaces_its_own_ImageUrl()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttImgSelfGm", "attimgself@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Imagem Direta");
        var (imageId, imageUrl) = await UploadImageWithUrlAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, null, imageId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CampaignAttachmentResponse>();
        body!.ImageUrl.Should().Be(imageUrl);
    }

    [Fact]
    public async Task A_bank_entry_attachment_has_no_ImageUrl()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttBankImgGm", "attbankimg@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Banco Sem Imagem");
        var bankEntryId = await CreateBankEntryAsync(gmToken, "Bola de Fogo");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, bankEntryId, null)));

        var body = await response.Content.ReadFromJsonAsync<CampaignAttachmentResponse>();
        body!.ImageUrl.Should().BeNull();
    }
}
