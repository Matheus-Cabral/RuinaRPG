using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Items;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CreaturePossessionsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CreaturePossessionsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
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
    public async Task AddSpoil_computes_CustoTotal_from_the_items_Preco_and_Qtd()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreaturePossGm1", "creatureposs1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var itemId = await CreateItemGeralAsync(gmToken, "Corda", 1.5m, 5);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spoils", gmToken,
            new AddCreatureSpoilRequest(itemId, 3, 15)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreatureSpoilResponse>();
        body!.ItemId.Should().Be(itemId);
        body.Nome.Should().Be("Corda");
        body.Custo.Should().Be(5);
        body.Qtd.Should().Be(3);
        body.CustoTotal.Should().Be(15); // Preco (5) * Qtd (3)
        body.DT.Should().Be(15);
    }

    [Fact]
    public async Task ListSpoils_returns_the_added_spoil()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreaturePossGm2", "creatureposs2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var itemId = await CreateItemGeralAsync(gmToken, "Tocha", 0.5m, 2);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spoils", gmToken, new AddCreatureSpoilRequest(itemId, 2, 10)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/spoils", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<CreatureSpoilResponse>>();
        body!.Should().ContainSingle(i => i.ItemId == itemId);
    }

    [Fact]
    public async Task Delete_an_existing_spoil_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreaturePossGm3", "creatureposs3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var itemId = await CreateItemGeralAsync(gmToken, "Lanterna", 1m, 8);
        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spoils", gmToken, new AddCreatureSpoilRequest(itemId, 1, 12)));
        var spoilId = (await addResponse.Content.ReadFromJsonAsync<CreatureSpoilResponse>())!.Id;

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}/spoils/{spoilId}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/spoils", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureSpoilResponse>>();
        body!.Should().NotContain(i => i.Id == spoilId);
    }

    [Fact]
    public async Task AddArtifact_links_the_item_and_list_returns_it_with_live_catalog_fields()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreaturePossGm4", "creatureposs4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var artifactItemId = await CreateArtefatoItemAsync(gmToken, "Anel do Poder", "Atributo", "Vigor", 2);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/artifacts", gmToken,
            new AddCreatureArtifactRequest(artifactItemId)));
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/artifacts", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureArtifactResponse>>();
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
        var gmToken = await RegisterGmAndGetTokenAsync("CreaturePossGm5", "creatureposs5@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var artifactItemId = await CreateArtefatoItemAsync(gmToken, "Anel da Sorte", "Atributo", "Astucia", 1);
        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/artifacts", gmToken, new AddCreatureArtifactRequest(artifactItemId)));
        var artifactId = (await addResponse.Content.ReadFromJsonAsync<CreatureArtifactResponse>())!.Id;

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}/artifacts/{artifactId}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/artifacts", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureArtifactResponse>>();
        body!.Should().NotContain(a => a.Id == artifactId);
    }

    [Fact]
    public async Task AddArtifact_a_4th_of_the_same_TipoDeAlvo_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreaturePossGm6", "creatureposs6@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var artifact1 = await CreateArtefatoItemAsync(gmToken, "Anel 1", "Atributo", "Vigor", 1);
        var artifact2 = await CreateArtefatoItemAsync(gmToken, "Anel 2", "Atributo", "Forca", 1);
        var artifact3 = await CreateArtefatoItemAsync(gmToken, "Anel 3", "Atributo", "Agilidade", 1);
        var artifact4 = await CreateArtefatoItemAsync(gmToken, "Anel 4", "Atributo", "Astucia", 1);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/artifacts", gmToken, new AddCreatureArtifactRequest(artifact1)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/artifacts", gmToken, new AddCreatureArtifactRequest(artifact2)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/artifacts", gmToken, new AddCreatureArtifactRequest(artifact3)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/artifacts", gmToken, new AddCreatureArtifactRequest(artifact4)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Possession_actions_by_a_different_gm_return_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreaturePossGmOwner7", "creaturepossowner7@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreaturePossGmOther7", "creaturepossother7@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);
        var itemId = await CreateItemGeralAsync(gmTokenOwner, "Corda", 1m, 5);
        var artifactItemId = await CreateArtefatoItemAsync(gmTokenOwner, "Anel do Poder", "Atributo", "Vigor", 2);
        var addSpoil = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spoils", gmTokenOwner, new AddCreatureSpoilRequest(itemId, 1, 10)));
        var spoilId = (await addSpoil.Content.ReadFromJsonAsync<CreatureSpoilResponse>())!.Id;
        var addArtifact = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/artifacts", gmTokenOwner, new AddCreatureArtifactRequest(artifactItemId)));
        var artifactId = (await addArtifact.Content.ReadFromJsonAsync<CreatureArtifactResponse>())!.Id;

        var addSpoilResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spoils", gmTokenOther, new AddCreatureSpoilRequest(itemId, 1, 10)));
        addSpoilResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listSpoilsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/spoils", gmTokenOther));
        listSpoilsResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteSpoilResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}/spoils/{spoilId}", gmTokenOther));
        deleteSpoilResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var addArtifactResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/artifacts", gmTokenOther, new AddCreatureArtifactRequest(artifactItemId)));
        addArtifactResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listArtifactsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/artifacts", gmTokenOther));
        listArtifactsResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteArtifactResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}/artifacts/{artifactId}", gmTokenOther));
        deleteArtifactResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddSpoil_with_a_malformed_ItemId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreaturePossGm8", "creatureposs8@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spoils", gmToken,
            new AddCreatureSpoilRequest("not-a-guid", 1, 10)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddArtifact_with_a_malformed_ArtifactItemId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreaturePossGm9", "creatureposs9@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/artifacts", gmToken,
            new AddCreatureArtifactRequest("not-a-guid")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
