using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;

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
            null, null, null, null, null, null, null,
            null, null, null, null,
            null, null, null, null, null);

    private static CreateItemRequest MinimalArma(string nome) =>
        new("Arma", nome, 1.5m, 50, null, "Espadas", "Uma lâmina curta e leve.",
            "F", "UmaMao", "2D6", 3, "19", 2, "Cortante",
            null, null, null, null,
            null, null, null, null, null);

    private static CreateItemRequest MinimalArmadura(string nome) =>
        new("Armadura", nome, 8m, 100, null, null, "Placas forjadas em aço temperado.",
            "D", null, null, null, null, null, null,
            "Pesada", 5, 2, 1,
            null, null, null, null, null);

    private static CreateItemRequest MinimalEscudo(string nome) =>
        new("Escudo", nome, 4m, 60, null, null, "Um pequeno broquel de madeira.",
            "F", null, null, null, null, null, null,
            "Leve", null, null, null,
            3, null, null, null, null);

    private static CreateItemRequest MinimalArtefato(string nome) =>
        new("Artefato", nome, 0.2m, 200, null, null, "Um anel gravado com runas antigas.",
            null, null, null, null, null, null, null,
            null, null, null, null,
            null, "Atributo", "Força", 2, null);

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
        body.Rank.Should().Be("F");
        body.Dano.Should().Be(3);
        body.Subcategoria.Should().Be("Espadas");
        body.Descricao.Should().Be("Uma lâmina curta e leve.");
    }

    [Fact]
    public async Task Create_an_Armadura_returns_201_with_DurabilidadeMaxima_resolved_from_its_Rank()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGm5", "item5@teste.com");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(MinimalArmadura("Peitoral de Placas")) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ItemResponse>();
        body!.Tipo.Should().Be("Armadura");
        body.DurabilidadeMaxima.Should().Be(80); // Rank D, Tabela de Durabilidade por Rank
        body.Descricao.Should().Be("Placas forjadas em aço temperado.");
    }

    [Fact]
    public async Task Create_an_Escudo_returns_201_with_escudo_specific_fields_set()
    {
        // Regression guard: a field silently dropped in the Create/Update switch branches has
        // already caused two real bugs in this plan (Armadura's DurabilidadeMaxima, Item's
        // ImageId) — Escudo had zero end-to-end coverage before this test.
        var token = await RegisterGmAndGetTokenAsync("ItemGmEscudo1", "itemescudo1@teste.com");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(MinimalEscudo("Broquel")) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ItemResponse>();
        body!.Tipo.Should().Be("Escudo");
        body.Categoria.Should().Be("Leve");
        body.BonusDefesa.Should().Be(3);
        body.DurabilidadeMaxima.Should().Be(20); // Rank F
        body.Descricao.Should().Be("Um pequeno broquel de madeira.");
    }

    [Fact]
    public async Task Create_an_Artefato_returns_201_with_artefato_specific_fields_set()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGmArtefato1", "itemartefato1@teste.com");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(MinimalArtefato("Anel do Vigor")) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ItemResponse>();
        body!.Tipo.Should().Be("Artefato");
        body.TipoDeAlvo.Should().Be("Atributo");
        body.Alvo.Should().Be("Força");
        body.Valor.Should().Be(2);
        body.Descricao.Should().Be("Um anel gravado com runas antigas.");
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
    public async Task List_as_a_jogador_returns_their_own_gms_catalog_only()
    {
        // A player needs to browse the Catálogo to pick something for their own sheet — Create/
        // Update/Delete stay GM-only, but List doesn't.
        var gmTokenA = await RegisterGmAndGetTokenAsync("ItemGmJogadorA", "itemgmjogadora@teste.com");
        var gmTokenB = await RegisterGmAndGetTokenAsync("ItemGmJogadorB", "itemgmjogadorb@teste.com");
        var jogadorTokenA = await RegisterJogadorTokenAsync(gmTokenA, "ItemJogadorScopeA", "itemjogadorscopea@teste.com");
        await PostItemAsync(gmTokenA, MinimalItemGeral("Corda de A"));
        await PostItemAsync(gmTokenB, MinimalItemGeral("Corda de B"));

        var message = new HttpRequestMessage(HttpMethod.Get, "/api/items");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jogadorTokenA);
        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<ItemResponse>>();
        body!.Should().ContainSingle(i => i.Nome == "Corda de A");
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

        var message = new HttpRequestMessage(HttpMethod.Get, "/api/items?subcategoria=Espadas&rank=F");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(message);

        var body = await response.Content.ReadFromJsonAsync<List<ItemResponse>>();
        body!.Should().ContainSingle(i => i.Nome == "Espada Curta");
    }

    [Fact]
    public async Task List_can_filter_by_partial_Nome_case_insensitively()
    {
        // "Cerimonial" doesn't appear in any DefaultCatalogItems name (unlike "Espada", which the
        // new GM's seeded catalog already has plenty of — see DefaultCatalogSeeder), so filtering
        // by it stays a clean single-match test regardless of the GM's starting catalog.
        var token = await RegisterGmAndGetTokenAsync("ItemGmFilter3", "itemfilter3@teste.com");
        await PostItemAsync(token, MinimalItemGeral("Corda Resistente"));
        await PostItemAsync(token, MinimalArma("Espada Cerimonial"));

        var message = new HttpRequestMessage(HttpMethod.Get, "/api/items?nome=cerimonial");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(message);

        var body = await response.Content.ReadFromJsonAsync<List<ItemResponse>>();
        body!.Should().ContainSingle(i => i.Nome == "Espada Cerimonial");
    }

    [Fact]
    public async Task Update_an_owned_item_returns_204_and_the_change_is_visible_on_list()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGmUpdate1", "itemupdate1@teste.com");
        var createResponse = await PostItemAsync(token, MinimalItemGeral("Corda"));
        var itemId = (await createResponse.Content.ReadFromJsonAsync<ItemResponse>())!.Id;

        var update = new UpdateItemRequest("Corda Reforçada", 0.6m, 8, null, "Equipamentos de Aventura", "Mais resistente.",
            null, null, null, null, null, null, null,
            null, null, null, null,
            null, null, null, null, null);
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
    public async Task Create_an_ItemGeral_with_CapacidadeExtra_returns_it_in_the_response()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGmCapExtra1", "itemcapextra1@teste.com");

        var request = new CreateItemRequest("ItemGeral", "Mochila de Couro", 1m, 40, null, "Equipamentos de Aventura", "Uma mochila resistente.",
            null, null, null, null, null, null, null,
            null, null, null, null,
            null, null, null, null, 10m);
        var response = await PostItemAsync(token, request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ItemResponse>();
        body!.CapacidadeExtra.Should().Be(10m);
    }

    [Fact]
    public async Task Create_an_Arma_ignores_CapacidadeExtra_even_if_sent()
    {
        // CapacidadeExtra only makes sense for ItemGeral — sending it for another Tipo must be
        // silently ignored, matching how every other Tipo-specific field on this shared request
        // already behaves for a Tipo it doesn't apply to.
        var token = await RegisterGmAndGetTokenAsync("ItemGmCapExtra2", "itemcapextra2@teste.com");

        var request = new CreateItemRequest("Arma", "Espada Estranha", 1.5m, 50, null, "Espadas", null,
            "F", "UmaMao", "2D6", 3, "19", 2, "Cortante",
            null, null, null, null,
            null, null, null, null, 10m);
        var response = await PostItemAsync(token, request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ItemResponse>();
        body!.CapacidadeExtra.Should().BeNull();
    }

    [Fact]
    public async Task Update_an_ItemGeral_changes_its_CapacidadeExtra()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGmCapExtra3", "itemcapextra3@teste.com");
        var createResponse = await PostItemAsync(token, MinimalItemGeral("Mochila"));
        var itemId = (await createResponse.Content.ReadFromJsonAsync<ItemResponse>())!.Id;

        var update = new UpdateItemRequest("Mochila", 1m, 40, null, "Equipamentos de Aventura", "Uma mochila resistente.",
            null, null, null, null, null, null, null,
            null, null, null, null,
            null, null, null, null, 8m);
        var message = new HttpRequestMessage(HttpMethod.Put, $"/api/items/{itemId}") { Content = JsonContent.Create(update) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var listMessage = new HttpRequestMessage(HttpMethod.Get, "/api/items");
        listMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var listResponse = await _client.SendAsync(listMessage);
        var body = await listResponse.Content.ReadFromJsonAsync<List<ItemResponse>>();
        body!.Single(i => i.Id == itemId).CapacidadeExtra.Should().Be(8m);
    }

    [Fact]
    public async Task Update_an_Arma_changes_its_Descricao()
    {
        // Descricao lives on the shared Item base now, not just ItemGeral — this guards the other
        // 4 Tipos (Arma here as the representative) against a regression back to ItemGeral-only.
        var token = await RegisterGmAndGetTokenAsync("ItemGmDescricao1", "itemdescricao1@teste.com");
        var createResponse = await PostItemAsync(token, MinimalArma("Espada Curta"));
        var itemId = (await createResponse.Content.ReadFromJsonAsync<ItemResponse>())!.Id;

        var update = new UpdateItemRequest("Espada Curta", 1.5m, 50, null, "Espadas", "Agora com o fio recém-afiado.",
            "F", "UmaMao", "2D6", 3, "19", 2, "Cortante",
            null, null, null, null,
            null, null, null, null, null);
        var message = new HttpRequestMessage(HttpMethod.Put, $"/api/items/{itemId}") { Content = JsonContent.Create(update) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var listMessage = new HttpRequestMessage(HttpMethod.Get, "/api/items");
        listMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var listResponse = await _client.SendAsync(listMessage);
        var body = await listResponse.Content.ReadFromJsonAsync<List<ItemResponse>>();
        body!.Single(i => i.Id == itemId).Descricao.Should().Be("Agora com o fio recém-afiado.");
    }

    [Fact]
    public async Task Update_a_item_owned_by_another_gm_returns_404()
    {
        var tokenOwner = await RegisterGmAndGetTokenAsync("ItemGmUpdateOwner", "itemupdateowner@teste.com");
        var tokenOther = await RegisterGmAndGetTokenAsync("ItemGmUpdateOther", "itemupdateother@teste.com");
        var createResponse = await PostItemAsync(tokenOwner, MinimalItemGeral("Corda"));
        var itemId = (await createResponse.Content.ReadFromJsonAsync<ItemResponse>())!.Id;

        var update = new UpdateItemRequest("Hack", 0m, 0, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
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

    private static MultipartFormDataContent BuildImageUpload(string fileName = "test.png")
    {
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    private async Task<string> UploadImageAsync(string token)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildImageUpload() };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(message);
        var body = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
        return body!.Id;
    }

    [Fact]
    public async Task Create_with_a_negative_Preco_returns_400()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGmNegPreco", "itemnegpreco@teste.com");
        var request = MinimalItemGeral("Corda") with { Preco = -1 };
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(request) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_a_negative_Peso_returns_400()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGmNegPeso", "itemnegpeso@teste.com");
        var request = MinimalItemGeral("Corda") with { Peso = -0.5m };
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(request) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_an_empty_string_ImageId_is_treated_as_no_image()
    {
        // CatalogoItemForm.razor's "— nenhuma —" <option value=""> posts ImageId="" rather than
        // null — this must be treated as "no image", not throw on Guid.Parse("").
        var token = await RegisterGmAndGetTokenAsync("ItemGmEmptyImg", "itememptyimg@teste.com");
        var request = MinimalItemGeral("Corda") with { ImageId = "" };
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(request) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ItemResponse>();
        body!.ImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task Create_with_a_malformed_ImageId_returns_400()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGmBadImg", "itembadimg@teste.com");
        var request = MinimalItemGeral("Corda") with { ImageId = "not-a-guid" };
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(request) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_a_nonexistent_ImageId_returns_400_not_500()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGmMissingImg", "itemmissingimg@teste.com");
        var request = MinimalItemGeral("Corda") with { ImageId = Guid.NewGuid().ToString() };
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(request) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_another_gms_image_returns_400()
    {
        // R0010 scopes referencing to images "seus" (the GM's own) — attaching another GM's
        // image Guid must be rejected, not silently accepted.
        var ownerToken = await RegisterGmAndGetTokenAsync("ItemGmImgOwner", "itemimgowner@teste.com");
        var otherToken = await RegisterGmAndGetTokenAsync("ItemGmImgOther", "itemimgother@teste.com");
        var otherImageId = await UploadImageAsync(otherToken);

        var request = MinimalItemGeral("Corda") with { ImageId = otherImageId };
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(request) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_the_callers_own_uploaded_image_returns_201_with_the_matching_ImageUrl()
    {
        // End-to-end regression guard for the ImageId -> ImageUrl round trip: covers the
        // stored-XSS extension fix, the /images/ URL-prefix fix, the TryParse fix, and the
        // ownership-check fix all at once.
        var token = await RegisterGmAndGetTokenAsync("ItemGmOwnImg", "itemownimg@teste.com");
        var uploadMessage = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildImageUpload() };
        uploadMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var uploadResponse = await _client.SendAsync(uploadMessage);
        var uploadBody = await uploadResponse.Content.ReadFromJsonAsync<ImageUploadResponse>();

        var request = MinimalItemGeral("Corda") with { ImageId = uploadBody!.Id };
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(request) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ItemResponse>();
        body!.ImageUrl.Should().Be(uploadBody.Url);
        body.ImageUrl.Should().StartWith("/images/");
    }

    private async Task<HttpResponseMessage> PutItemAsync(string token, string id, UpdateItemRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Put, $"/api/items/{id}") { Content = JsonContent.Create(request) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(message);
    }

    private async Task<List<ItemResponse>> ListItemsAsync(string token)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, "/api/items");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(message);
        return (await response.Content.ReadFromJsonAsync<List<ItemResponse>>())!;
    }

    [Fact]
    public async Task Create_and_Update_persist_Subcategoria_for_Armadura()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemsSubcatGmArmadura", "itemssubcatarmadura@teste.com");

        var createResponse = await PostItemAsync(token,
            MinimalArmadura("Peitoral de Placas") with { Subcategoria = "Equipamento inicial - Armadura - Leve - Couro" });
        var armadura = await createResponse.Content.ReadFromJsonAsync<ItemResponse>();
        armadura!.Subcategoria.Should().Be("Equipamento inicial - Armadura - Leve - Couro");

        var update = new UpdateItemRequest("Peitoral de Placas", 8m, 100, null,
            "Equipamento inicial - Armadura - Pesada - Placas", "Placas forjadas em aço temperado.",
            null, null, null, null, null, null, null,
            "Pesada", 5, 2, 1,
            null, null, null, null, null);
        var updateResponse = await PutItemAsync(token, armadura.Id, update);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var items = await ListItemsAsync(token);
        items.Single(i => i.Id == armadura.Id).Subcategoria.Should().Be("Equipamento inicial - Armadura - Pesada - Placas");
    }

    [Fact]
    public async Task Create_and_Update_persist_Subcategoria_for_Escudo()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemsSubcatGmEscudo", "itemssubcatescudo@teste.com");

        var createResponse = await PostItemAsync(token,
            MinimalEscudo("Broquel") with { Subcategoria = "Equipamento inicial - Escudo - Leve - Madeira" });
        var escudo = await createResponse.Content.ReadFromJsonAsync<ItemResponse>();
        escudo!.Subcategoria.Should().Be("Equipamento inicial - Escudo - Leve - Madeira");

        var update = new UpdateItemRequest("Broquel", 4m, 60, null,
            "Equipamento inicial - Escudo - Pesada - Aço", "Um pequeno broquel de madeira.",
            null, null, null, null, null, null, null,
            "Leve", null, null, null,
            3, null, null, null, null);
        var updateResponse = await PutItemAsync(token, escudo.Id, update);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var items = await ListItemsAsync(token);
        items.Single(i => i.Id == escudo.Id).Subcategoria.Should().Be("Equipamento inicial - Escudo - Pesada - Aço");
    }

    [Fact]
    public async Task Create_and_Update_persist_Subcategoria_for_Artefato()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemsSubcatGmArtefato", "itemssubcatartefato@teste.com");

        var createResponse = await PostItemAsync(token,
            MinimalArtefato("Anel do Vigor") with { Subcategoria = "Equipamento inicial - Artefato - Anel - Atributo" });
        var artefato = await createResponse.Content.ReadFromJsonAsync<ItemResponse>();
        artefato!.Subcategoria.Should().Be("Equipamento inicial - Artefato - Anel - Atributo");

        var update = new UpdateItemRequest("Anel do Vigor", 0.2m, 200, null,
            "Equipamento inicial - Artefato - Amuleto - Atributo", "Um anel gravado com runas antigas.",
            null, null, null, null, null, null, null,
            null, null, null, null,
            null, "Atributo", "Força", 2, null);
        var updateResponse = await PutItemAsync(token, artefato.Id, update);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var items = await ListItemsAsync(token);
        items.Single(i => i.Id == artefato.Id).Subcategoria.Should().Be("Equipamento inicial - Artefato - Amuleto - Atributo");
    }

    // ---- Requisitos e Penalidade de equipamento ----

    private static CreateItemRequest Arma(string nome, RequisitosDePassivaDto? requisitos = null, PenalidadeDeEquipamentoDto? penalidade = null) =>
        MinimalArma(nome) with { Requisitos = requisitos, PenalidadeDeRequisitos = penalidade };

    private async Task<ItemResponse> CreateItemAsync(string token, CreateItemRequest request)
    {
        var response = await PostItemAsync(token, request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!;
    }

    private static UpdateItemRequest UpdateDe(ItemResponse i) => new(
        Nome: i.Nome, Peso: i.Peso, Preco: i.Preco, ImageId: null, Subcategoria: i.Subcategoria, Descricao: i.Descricao,
        Rank: i.Rank, Empunhadura: i.Empunhadura, Dados: i.Dados, Dano: i.Dano, Critico: i.Critico, Alcance: i.Alcance,
        TipoDeDano: i.TipoDeDano, Categoria: i.Categoria, Defesa: i.Defesa,
        RF: i.RF, RM: i.RM, BonusDefesa: i.BonusDefesa,
        TipoDeAlvo: i.TipoDeAlvo, Alvo: i.Alvo, Valor: i.Valor, CapacidadeExtra: i.CapacidadeExtra,
        Requisitos: i.Requisitos, PenalidadeDeRequisitos: i.PenalidadeDeRequisitos);

    [Fact]
    public async Task An_equipment_round_trips_its_requirements_and_penalty_and_spells_them_out()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemReqGm1", "itemreqgm1@teste.com");
        var requisitos = new RequisitosDePassivaDto(Vocacao: "Campeao", Atributos: [new("Vigor", 8)]);
        var penalidade = new PenalidadeDeEquipamentoDto(Atributos: [new("Forca", 2)], SubAtributos: [new("Movimentacao", 1)], Texto: "Desvantagem em furtividade");

        var created = await CreateItemAsync(token, Arma("Montante", requisitos, penalidade));

        created.Requisitos!.Vocacao.Should().Be("Campeao");
        created.Requisitos.Atributos.Should().Equal(new RequisitoMinimoDto("Vigor", 8));
        created.PenalidadeDeRequisitos!.Atributos.Should().Equal(new PenalidadeLinhaDto("Forca", 2));
        created.RequisitosPorExtenso.Should().Equal("Vocação: Campeão", "Vigor ≥ 8");
        created.PenalidadePorExtenso.Should().Equal("Força −2", "Movimentação −1", "Desvantagem em furtividade");
        (await ListItemsAsync(token)).Single(i => i.Id == created.Id).Requisitos!.Atributos.Should().HaveCount(1);
    }

    [Fact]
    public async Task Requirement_fields_an_equipment_does_not_accept_are_dropped()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemReqGm2", "itemreqgm2@teste.com");

        var created = await CreateItemAsync(token, Arma("Adaga", new RequisitosDePassivaDto(Nivel: 5, Graduacao: 3, CoracaoDeMana: true, Estrela: "Aeurer")));

        created.Requisitos!.Nivel.Should().BeNull();
        created.Requisitos.Graduacao.Should().BeNull();
        created.Requisitos.CoracaoDeMana.Should().BeNull();
        created.Requisitos.Estrela.Should().Be("Aeurer");
    }

    [Fact]
    public async Task Empty_requirements_and_penalty_are_stored_as_none()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemReqGm3", "itemreqgm3@teste.com");

        var created = await CreateItemAsync(token, Arma("Faca", new RequisitosDePassivaDto(), new PenalidadeDeEquipamentoDto(Texto: "   ")));

        created.Requisitos.Should().BeNull();
        created.PenalidadeDeRequisitos.Should().BeNull();
        created.RequisitosPorExtenso.Should().BeEmpty();
    }

    [Fact]
    public async Task An_item_geral_ignores_requirements_and_penalty()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemReqGm4", "itemreqgm4@teste.com");
        var request = Arma("Corda", new RequisitosDePassivaDto(Atributos: [new("Vigor", 8)]), new PenalidadeDeEquipamentoDto(Texto: "x")) with { Tipo = "ItemGeral" };

        var created = await CreateItemAsync(token, request);

        created.Requisitos.Should().BeNull();
        created.PenalidadeDeRequisitos.Should().BeNull();
    }

    [Theory]
    [InlineData("Sorte", 2, "Atributo desconhecido(a): Sorte.")]
    [InlineData("Forca", 0, "A penalidade de Atributo deve ser pelo menos 1.")]
    public async Task An_invalid_penalty_line_returns_400(string alvo, int valor, string mensagem)
    {
        var token = await RegisterGmAndGetTokenAsync($"ItemReqGm5{alvo}", $"itemreqgm5{alvo}@teste.com");

        var response = await PostItemAsync(token, Arma("Erro", penalidade: new PenalidadeDeEquipamentoDto(Atributos: [new(alvo, valor)])));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(mensagem);
    }

    [Fact]
    public async Task A_repeated_penalty_target_returns_400()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemReqGm6", "itemreqgm6@teste.com");

        var response = await PostItemAsync(token, Arma("Erro", penalidade: new PenalidadeDeEquipamentoDto(Atributos: [new("Forca", 1), new("Forca", 2)])));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Atributo repetido(a): Forca.");
    }

    [Fact]
    public async Task Update_replaces_and_clears_requirements_and_penalty()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemReqGm7", "itemreqgm7@teste.com");
        var created = await CreateItemAsync(token, Arma("Machado", new RequisitosDePassivaDto(Atributos: [new("Forca", 6)]), new PenalidadeDeEquipamentoDto(Texto: "Lento")));

        (await PutItemAsync(token, created.Id, UpdateDe(created) with { Requisitos = null, PenalidadeDeRequisitos = null }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var depois = (await ListItemsAsync(token)).Single(i => i.Id == created.Id);
        depois.Requisitos.Should().BeNull();
        depois.PenalidadeDeRequisitos.Should().BeNull();
    }

    [Fact]
    public async Task A_requirement_or_penalty_on_a_pericia_the_auditor_removed_disappears_from_the_response()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemReqGm8", "itemreqgm8@teste.com");
        string chave, nome;
        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var p = await db.Pericias.Where(x => !x.IsDeleted).OrderBy(x => x.Id).FirstAsync();
            (chave, nome, id) = (p.Chave, p.Nome, p.Id);
        }
        var created = await CreateItemAsync(token, Arma("Arco Pesado",
            new RequisitosDePassivaDto(Pericias: [new(chave, 3)]),
            new PenalidadeDeEquipamentoDto(Pericias: [new(chave, 2)], Texto: "Pesado")));
        created.Requisitos!.Pericias.Should().HaveCount(1);
        created.PenalidadeDeRequisitos!.Pericias.Should().HaveCount(1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            (await db.Pericias.SingleAsync(x => x.Id == id)).IsDeleted = true;
            await db.SaveChangesAsync();
        }

        var depois = (await ListItemsAsync(token)).Single(i => i.Id == created.Id);
        depois.Requisitos?.Pericias.Should().BeNullOrEmpty();
        depois.PenalidadeDeRequisitos!.Pericias.Should().BeEmpty();
        var textos = depois.RequisitosPorExtenso!.Concat(depois.PenalidadePorExtenso!).ToList();
        textos.Should().NotContain(t => t.Contains(nome));
        textos.Should().NotContain(t => t.Contains(id.ToString()));
        (await PutItemAsync(token, created.Id, UpdateDe(depois))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
