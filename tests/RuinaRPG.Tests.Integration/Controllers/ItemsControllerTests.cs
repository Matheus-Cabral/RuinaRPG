using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Items;

namespace RuinaRPG.Tests.Integration.Controllers;

public class ItemsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public ItemsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> RegisterJogadorTokenAsync(string gmToken, string nickname, string email)
    {
        var codeMessage = new HttpRequestMessage(HttpMethod.Post, "/api/invite-codes");
        codeMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var codeResponse = await _client.SendAsync(codeMessage);
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private static CreateItemRequest MinimalItemGeral(string nome) =>
        new("ItemGeral", nome, 0.5m, 5, null, "Equipamentos de Aventura", "Uma corda resistente.",
            null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null,
            null, null, null, null);

    private static CreateItemRequest MinimalArma(string nome) =>
        new("Arma", nome, 1.5m, 50, null, "Espadas", null,
            "F", "UmaMao", "2D6", 3, "19", 2, "Cortante", null, 10,
            null, null, null, null, null, null,
            null, null, null, null);

    private static CreateItemRequest MinimalArmadura(string nome) =>
        new("Armadura", nome, 8m, 100, null, null, null,
            null, null, null, null, null, null, null, null, 15,
            "Pesada", 5, 2, 1, null, 12,
            null, null, null, null);

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/items", MinimalItemGeral("Corda"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_as_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ItemGm1", "item1@teste.com");
        var jogadorToken = await RegisterJogadorTokenAsync(gmToken, "ItemJogador1", "itemjogador1@teste.com");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(MinimalItemGeral("Corda")) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jogadorToken);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_an_ItemGeral_returns_201_with_only_ItemGeral_fields_set()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGm2", "item2@teste.com");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(MinimalItemGeral("Corda")) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ItemResponse>();
        body!.Tipo.Should().Be("ItemGeral");
        body.Nome.Should().Be("Corda");
        body.Subcategoria.Should().Be("Equipamentos de Aventura");
        body.Dano.Should().BeNull();
    }

    [Fact]
    public async Task Create_an_Arma_returns_201_with_arma_specific_fields_set()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGm3", "item3@teste.com");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(MinimalArma("Espada Curta")) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        var body = await response.Content.ReadFromJsonAsync<ItemResponse>();
        body!.Tipo.Should().Be("Arma");
        body.Tier.Should().Be("F");
        body.Dano.Should().Be(3);
        body.Subcategoria.Should().Be("Espadas");
    }

    [Fact]
    public async Task Create_an_Armadura_returns_201_with_DurabilidadeMaxima_echoed_back()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGm5", "item5@teste.com");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(MinimalArmadura("Peitoral de Placas")) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ItemResponse>();
        body!.Tipo.Should().Be("Armadura");
        body.DurabilidadeMaxima.Should().Be(15);
    }

    [Fact]
    public async Task Create_with_an_unknown_Tipo_returns_400()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGm4", "item4@teste.com");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(MinimalItemGeral("Corda") with { Tipo = "NaoExiste" }) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<HttpResponseMessage> PostItemAsync(string token, CreateItemRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(request) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(message);
    }

    [Fact]
    public async Task List_returns_only_items_created_by_the_authenticated_gm()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("ItemGmA", "itemgma@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("ItemGmB", "itemgmb@teste.com");
        await PostItemAsync(tokenA, MinimalItemGeral("Corda A"));
        await PostItemAsync(tokenB, MinimalItemGeral("Corda B"));

        var message = new HttpRequestMessage(HttpMethod.Get, "/api/items");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var response = await _client.SendAsync(message);

        var body = await response.Content.ReadFromJsonAsync<List<ItemResponse>>();
        body!.Should().ContainSingle(i => i.Nome == "Corda A");
    }

    [Fact]
    public async Task List_can_filter_by_Tipo()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGmFilter1", "itemfilter1@teste.com");
        await PostItemAsync(token, MinimalItemGeral("Corda"));
        await PostItemAsync(token, MinimalArma("Espada"));

        var message = new HttpRequestMessage(HttpMethod.Get, "/api/items?tipo=Arma");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(message);

        var body = await response.Content.ReadFromJsonAsync<List<ItemResponse>>();
        body!.Should().OnlyContain(i => i.Tipo == "Arma");
    }

    [Fact]
    public async Task List_can_filter_by_Subcategoria_and_Tier_together()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGmFilter2", "itemfilter2@teste.com");
        await PostItemAsync(token, MinimalArma("Espada Curta")); // Subcategoria "Espadas", Tier F

        var message = new HttpRequestMessage(HttpMethod.Get, "/api/items?subcategoria=Espadas&tier=F");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(message);

        var body = await response.Content.ReadFromJsonAsync<List<ItemResponse>>();
        body!.Should().ContainSingle(i => i.Nome == "Espada Curta");
    }

    [Fact]
    public async Task Update_an_owned_item_returns_204_and_the_change_is_visible_on_list()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGmUpdate1", "itemupdate1@teste.com");
        var createResponse = await PostItemAsync(token, MinimalItemGeral("Corda"));
        var itemId = (await createResponse.Content.ReadFromJsonAsync<ItemResponse>())!.Id;

        var update = new UpdateItemRequest("Corda Reforçada", 0.6m, 8, null, "Equipamentos de Aventura", "Mais resistente.",
            null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null,
            null, null, null, null);
        var message = new HttpRequestMessage(HttpMethod.Put, $"/api/items/{itemId}") { Content = JsonContent.Create(update) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listMessage = new HttpRequestMessage(HttpMethod.Get, "/api/items");
        listMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var listResponse = await _client.SendAsync(listMessage);
        var body = await listResponse.Content.ReadFromJsonAsync<List<ItemResponse>>();
        body!.Should().ContainSingle(i => i.Nome == "Corda Reforçada" && i.Preco == 8);
    }

    [Fact]
    public async Task Update_a_item_owned_by_another_gm_returns_404()
    {
        var tokenOwner = await RegisterGmAndGetTokenAsync("ItemGmUpdateOwner", "itemupdateowner@teste.com");
        var tokenOther = await RegisterGmAndGetTokenAsync("ItemGmUpdateOther", "itemupdateother@teste.com");
        var createResponse = await PostItemAsync(tokenOwner, MinimalItemGeral("Corda"));
        var itemId = (await createResponse.Content.ReadFromJsonAsync<ItemResponse>())!.Id;

        var update = new UpdateItemRequest("Hack", 0m, 0, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        var message = new HttpRequestMessage(HttpMethod.Put, $"/api/items/{itemId}") { Content = JsonContent.Create(update) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenOther);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_an_owned_item_returns_204_and_it_no_longer_appears_on_list()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGmDelete1", "itemdelete1@teste.com");
        var createResponse = await PostItemAsync(token, MinimalItemGeral("Corda"));
        var itemId = (await createResponse.Content.ReadFromJsonAsync<ItemResponse>())!.Id;

        var message = new HttpRequestMessage(HttpMethod.Delete, $"/api/items/{itemId}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listMessage = new HttpRequestMessage(HttpMethod.Get, "/api/items");
        listMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var listResponse = await _client.SendAsync(listMessage);
        var body = await listResponse.Content.ReadFromJsonAsync<List<ItemResponse>>();
        body!.Should().NotContain(i => i.Id == itemId);
    }

    [Fact]
    public async Task Delete_a_nonexistent_item_returns_404()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGmDelete2", "itemdelete2@teste.com");
        var message = new HttpRequestMessage(HttpMethod.Delete, $"/api/items/{Guid.NewGuid()}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
