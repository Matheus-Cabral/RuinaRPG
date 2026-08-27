using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.NpcSheets;

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
            null, null, null, null);

    private async Task<string> CreateItemAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken, MinimalItemGeral(nome)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

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
}
