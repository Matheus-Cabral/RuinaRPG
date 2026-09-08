using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class NpcPossessionsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public NpcPossessionsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> CreateItemGeralAsync(string gmToken, string nome, decimal peso, int preco)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("ItemGeral", nome, peso, preco, null, "Diversos", "Um item qualquer", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    private async Task<string> CreateArtefatoItemAsync(string gmToken, string nome, string tipoDeAlvo, string alvo, int valor)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Artefato", nome, 0.1m, 500, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, tipoDeAlvo, alvo, valor, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    [Fact]
    public async Task AddInventoryItem_links_the_item_and_computes_total_as_peso_times_qtd()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcPossGm1", "npcposs1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var itemId = await CreateItemGeralAsync(gmToken, "Corda", 1.5m, 5);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/inventory", gmToken,
            new AddNpcInventoryItemRequest(itemId, 3)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<NpcInventoryItemResponse>();
        body!.ItemId.Should().Be(itemId);
        body.Nome.Should().Be("Corda");
        body.Qtd.Should().Be(3);
        body.Total.Should().Be(4.5m);
    }

    [Fact]
    public async Task ListInventory_returns_the_added_item()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcPossGm2", "npcposs2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var itemId = await CreateItemGeralAsync(gmToken, "Tocha", 0.5m, 2);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/inventory", gmToken, new AddNpcInventoryItemRequest(itemId, 2)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/inventory", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<NpcInventoryItemResponse>>();
        body!.Should().ContainSingle(i => i.ItemId == itemId);
    }

    [Fact]
    public async Task Delete_an_existing_inventory_item_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcPossGm3", "npcposs3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var itemId = await CreateItemGeralAsync(gmToken, "Lanterna", 1m, 8);
        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/inventory", gmToken, new AddNpcInventoryItemRequest(itemId, 1)));
        var inventoryItemId = (await addResponse.Content.ReadFromJsonAsync<NpcInventoryItemResponse>())!.Id;

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/npc-sheets/{sheetId}/inventory/{inventoryItemId}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/inventory", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcInventoryItemResponse>>();
        body!.Should().NotContain(i => i.Id == inventoryItemId);
    }

    [Fact]
    public async Task AddArtifact_links_the_item_and_list_returns_it_with_live_catalog_fields()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcPossGm4", "npcposs4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var artifactItemId = await CreateArtefatoItemAsync(gmToken, "Anel do Poder", "Atributo", "Vigor", 2);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/artifacts", gmToken,
            new AddNpcArtifactRequest(artifactItemId)));
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/artifacts", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcArtifactResponse>>();
        body!.Should().HaveCount(1);
        var artifact = body!.Single();
        artifact.ArtifactItemId.Should().Be(artifactItemId);
        artifact.Nome.Should().Be("Anel do Poder");
        artifact.TipoDeAlvo.Should().Be("Atributo");
        artifact.Alvo.Should().Be("Vigor");
        artifact.Valor.Should().Be(2);
    }

    [Fact]
    public async Task Delete_an_existing_artifact_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcPossGm5", "npcposs5@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var artifactItemId = await CreateArtefatoItemAsync(gmToken, "Anel da Sorte", "Atributo", "Astucia", 1);
        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/artifacts", gmToken, new AddNpcArtifactRequest(artifactItemId)));
        var artifactId = (await addResponse.Content.ReadFromJsonAsync<NpcArtifactResponse>())!.Id;

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/npc-sheets/{sheetId}/artifacts/{artifactId}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/artifacts", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcArtifactResponse>>();
        body!.Should().NotContain(a => a.Id == artifactId);
    }

    [Fact]
    public async Task AddArtifact_a_4th_of_the_same_TipoDeAlvo_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcPossGm6", "npcposs6@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var artifact1 = await CreateArtefatoItemAsync(gmToken, "Anel 1", "Atributo", "Vigor", 1);
        var artifact2 = await CreateArtefatoItemAsync(gmToken, "Anel 2", "Atributo", "Forca", 1);
        var artifact3 = await CreateArtefatoItemAsync(gmToken, "Anel 3", "Atributo", "Agilidade", 1);
        var artifact4 = await CreateArtefatoItemAsync(gmToken, "Anel 4", "Atributo", "Astucia", 1);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/artifacts", gmToken, new AddNpcArtifactRequest(artifact1)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/artifacts", gmToken, new AddNpcArtifactRequest(artifact2)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/artifacts", gmToken, new AddNpcArtifactRequest(artifact3)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/artifacts", gmToken, new AddNpcArtifactRequest(artifact4)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Possession_actions_by_a_different_gm_return_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcPossGmOwner7", "npcpossowner7@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcPossGmOther7", "npcpossother7@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);
        var itemId = await CreateItemGeralAsync(gmTokenOwner, "Corda", 1m, 5);
        var artifactItemId = await CreateArtefatoItemAsync(gmTokenOwner, "Anel do Poder", "Atributo", "Vigor", 2);
        var addInventory = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/inventory", gmTokenOwner, new AddNpcInventoryItemRequest(itemId, 1)));
        var inventoryItemId = (await addInventory.Content.ReadFromJsonAsync<NpcInventoryItemResponse>())!.Id;
        var addArtifact = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/artifacts", gmTokenOwner, new AddNpcArtifactRequest(artifactItemId)));
        var artifactId = (await addArtifact.Content.ReadFromJsonAsync<NpcArtifactResponse>())!.Id;

        var addInventoryResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/inventory", gmTokenOther, new AddNpcInventoryItemRequest(itemId, 1)));
        addInventoryResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listInventoryResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/inventory", gmTokenOther));
        listInventoryResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteInventoryResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/npc-sheets/{sheetId}/inventory/{inventoryItemId}", gmTokenOther));
        deleteInventoryResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var addArtifactResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/artifacts", gmTokenOther, new AddNpcArtifactRequest(artifactItemId)));
        addArtifactResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listArtifactsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/artifacts", gmTokenOther));
        listArtifactsResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteArtifactResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/npc-sheets/{sheetId}/artifacts/{artifactId}", gmTokenOther));
        deleteArtifactResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddInventoryItem_with_a_malformed_ItemId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcPossGm8", "npcposs8@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/inventory", gmToken,
            new AddNpcInventoryItemRequest("not-a-guid", 1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddArtifact_with_a_malformed_ArtifactItemId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcPossGm9", "npcposs9@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/artifacts", gmToken,
            new AddNpcArtifactRequest("not-a-guid")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
