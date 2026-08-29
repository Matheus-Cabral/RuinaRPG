using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Items;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterPossessionsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CharacterPossessionsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> SetUpSheetAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }

    private async Task<string> CreateItemGeralAsync(string gmToken, string nome, decimal peso, int preco)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("ItemGeral", nome, peso, preco, null, "Diversos", "Um item qualquer", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    private async Task<string> CreateArtefatoItemAsync(string gmToken, string nome, string tipoDeAlvo, string alvo, int valor)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Artefato", nome, 0.1m, 500, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, tipoDeAlvo, alvo, valor)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    private async Task<string> CreateArmaItemAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Arma", nome, 1.5m, 50, null, "Espadas", null, "F", "UmaMao", "2D6", 3, "19", 2, "Cortante", null, 20, null, null, null, null, null, null, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    [Fact]
    public async Task AddInventoryItem_links_the_item_and_computes_total_as_peso_times_qtd()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("PossGm1", "poss1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "PossPlayer1", "possplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var itemId = await CreateItemGeralAsync(gmToken, "Corda", 1.5m, 5);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/inventory", playerToken,
            new AddCharacterInventoryItemRequest(itemId, 3)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CharacterInventoryItemResponse>();
        body!.ItemId.Should().Be(itemId);
        body.Nome.Should().Be("Corda");
        body.Qtd.Should().Be(3);
        body.Total.Should().Be(4.5m);
    }

    [Fact]
    public async Task ListInventory_returns_the_added_item()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("PossGm2", "poss2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "PossPlayer2", "possplayer2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var itemId = await CreateItemGeralAsync(gmToken, "Tocha", 0.5m, 2);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/inventory", playerToken, new AddCharacterInventoryItemRequest(itemId, 2)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/inventory", playerToken));

        var body = await response.Content.ReadFromJsonAsync<List<CharacterInventoryItemResponse>>();
        body!.Should().ContainSingle(i => i.ItemId == itemId);
    }

    [Fact]
    public async Task Delete_an_existing_inventory_item_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("PossGm3", "poss3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "PossPlayer3", "possplayer3@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var itemId = await CreateItemGeralAsync(gmToken, "Lanterna", 1m, 8);
        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/inventory", playerToken, new AddCharacterInventoryItemRequest(itemId, 1)));
        var inventoryItemId = (await addResponse.Content.ReadFromJsonAsync<CharacterInventoryItemResponse>())!.Id;

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/inventory/{inventoryItemId}", playerToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/inventory", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterInventoryItemResponse>>();
        body!.Should().NotContain(i => i.Id == inventoryItemId);
    }

    [Fact]
    public async Task AddArtifact_links_the_item_and_list_returns_it_with_live_catalog_fields()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("PossGm4", "poss4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "PossPlayer4", "possplayer4@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var artifactItemId = await CreateArtefatoItemAsync(gmToken, "Anel do Poder", "Atributo", "Vigor", 2);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", playerToken,
            new AddCharacterArtifactRequest(artifactItemId)));
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/artifacts", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterArtifactResponse>>();
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
        var gmToken = await RegisterGmAndGetTokenAsync("PossGm5", "poss5@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "PossPlayer5", "possplayer5@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var artifactItemId = await CreateArtefatoItemAsync(gmToken, "Anel da Sorte", "Atributo", "Astucia", 1);
        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", playerToken, new AddCharacterArtifactRequest(artifactItemId)));
        var artifactId = (await addResponse.Content.ReadFromJsonAsync<CharacterArtifactResponse>())!.Id;

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/artifacts/{artifactId}", playerToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/artifacts", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterArtifactResponse>>();
        body!.Should().NotContain(a => a.Id == artifactId);
    }

    [Fact]
    public async Task AddArtifact_a_4th_of_the_same_TipoDeAlvo_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("PossGm6", "poss6@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "PossPlayer6", "possplayer6@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var artifact1 = await CreateArtefatoItemAsync(gmToken, "Anel 1", "Atributo", "Vigor", 1);
        var artifact2 = await CreateArtefatoItemAsync(gmToken, "Anel 2", "Atributo", "Forca", 1);
        var artifact3 = await CreateArtefatoItemAsync(gmToken, "Anel 3", "Atributo", "Agilidade", 1);
        var artifact4 = await CreateArtefatoItemAsync(gmToken, "Anel 4", "Atributo", "Astucia", 1);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", playerToken, new AddCharacterArtifactRequest(artifact1)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", playerToken, new AddCharacterArtifactRequest(artifact2)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", playerToken, new AddCharacterArtifactRequest(artifact3)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", playerToken, new AddCharacterArtifactRequest(artifact4)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Possession_actions_by_an_unrelated_jogador_return_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("PossGm7", "poss7@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "PossPlayer7", "possplayer7@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "PossPlayer7b", "possplayer7b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var itemId = await CreateItemGeralAsync(gmToken, "Corda", 1m, 5);
        var artifactItemId = await CreateArtefatoItemAsync(gmToken, "Anel do Poder", "Atributo", "Vigor", 2);
        var addInventory = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/inventory", playerToken, new AddCharacterInventoryItemRequest(itemId, 1)));
        var inventoryItemId = (await addInventory.Content.ReadFromJsonAsync<CharacterInventoryItemResponse>())!.Id;
        var addArtifact = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", playerToken, new AddCharacterArtifactRequest(artifactItemId)));
        var artifactId = (await addArtifact.Content.ReadFromJsonAsync<CharacterArtifactResponse>())!.Id;

        var addInventoryResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/inventory", otherToken, new AddCharacterInventoryItemRequest(itemId, 1)));
        addInventoryResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var listInventoryResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/inventory", otherToken));
        listInventoryResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var deleteInventoryResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/inventory/{inventoryItemId}", otherToken));
        deleteInventoryResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var addArtifactResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", otherToken, new AddCharacterArtifactRequest(artifactItemId)));
        addArtifactResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var listArtifactsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/artifacts", otherToken));
        listArtifactsResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var deleteArtifactResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/artifacts/{artifactId}", otherToken));
        deleteArtifactResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddInventoryItem_rejects_a_catalog_id_that_is_not_an_ItemGeral()
    {
        // Ficha de Personagem 5.a: "Armas, Armaduras e Escudos não aparecem aqui" — a Weapon
        // catalog id must not be addable as a plain inventory row (would double-count its Peso).
        var gmToken = await RegisterGmAndGetTokenAsync("PossGm10", "poss10@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "PossPlayer10", "possplayer10@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var armaItemId = await CreateArmaItemAsync(gmToken, "Espada Longa");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/inventory", playerToken,
            new AddCharacterInventoryItemRequest(armaItemId, 1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddInventoryItem_with_a_malformed_ItemId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("PossGm8", "poss8@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "PossPlayer8", "possplayer8@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/inventory", playerToken,
            new AddCharacterInventoryItemRequest("not-a-guid", 1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddArtifact_with_a_malformed_ArtifactItemId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("PossGm9", "poss9@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "PossPlayer9", "possplayer9@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", playerToken,
            new AddCharacterArtifactRequest("not-a-guid")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
