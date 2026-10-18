using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Items;
// Pedidos e respostas de Inventário e Artefato têm o mesmo JSON nos três tipos de ficha.
using AdicionarArtefato = RuinaRPG.Contracts.CharacterSheets.AddCharacterArtifactRequest;
using AdicionarItem = RuinaRPG.Contracts.CharacterSheets.AddCharacterInventoryItemRequest;
using ArtefatoDaPosse = RuinaRPG.Contracts.CharacterSheets.CharacterArtifactResponse;
using ItemDoInventario = RuinaRPG.Contracts.CharacterSheets.CharacterInventoryItemResponse;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

/// <summary>Artefatos da ficha (<c>/artifacts</c>), nos três tipos.</summary>
public abstract class PossesDaFichaTestes<TTipo>(ApiFixture fixture) : TestesDeFicha<TTipo>(fixture) where TTipo : ITipoDeTeste, new()
{
    [Fact]
    public async Task AddArtifact_links_the_item_and_list_returns_it_with_live_catalog_fields()
    {
        var f = await NovaFichaAsync();
        var artifactItemId = await CreateArtefatoItemAsync(f.GmToken, "Anel do Poder", "Atributo", "Vigor", 2);

        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), f.Token,
            new AdicionarArtefato(artifactItemId)));
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "artifacts"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<ArtefatoDaPosse>>();
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
        var f = await NovaFichaAsync();
        var artifactItemId = await CreateArtefatoItemAsync(f.GmToken, "Anel da Sorte", "Atributo", "Astucia", 1);
        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), f.Token, new AdicionarArtefato(artifactItemId)));
        var artifactId = (await addResponse.Content.ReadFromJsonAsync<ArtefatoDaPosse>())!.Id;

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"artifacts/{artifactId}"), f.Token));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "artifacts"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<ArtefatoDaPosse>>();
        body!.Should().NotContain(a => a.Id == artifactId);
    }

    [Fact]
    public async Task AddArtifact_a_4th_of_the_same_TipoDeAlvo_returns_400()
    {
        var f = await NovaFichaAsync();
        var artifact1 = await CreateArtefatoItemAsync(f.GmToken, "Anel 1", "Atributo", "Vigor", 1);
        var artifact2 = await CreateArtefatoItemAsync(f.GmToken, "Anel 2", "Atributo", "Forca", 1);
        var artifact3 = await CreateArtefatoItemAsync(f.GmToken, "Anel 3", "Atributo", "Agilidade", 1);
        var artifact4 = await CreateArtefatoItemAsync(f.GmToken, "Anel 4", "Atributo", "Astucia", 1);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), f.Token, new AdicionarArtefato(artifact1)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), f.Token, new AdicionarArtefato(artifact2)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), f.Token, new AdicionarArtefato(artifact3)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), f.Token, new AdicionarArtefato(artifact4)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A parte de Artefatos do antigo <c>Possession_actions_by_…</c>; a de Inventário/Espólios fica abaixo.</summary>
    [Fact]
    public async Task Artifact_actions_by_someone_without_access_return_the_denied_status()
    {
        var f = await NovaFichaAsync();
        var otherToken = await Tipo.CriarIntrusoAsync(this, f);
        var artifactItemId = await CreateArtefatoItemAsync(f.GmToken, "Anel do Poder", "Atributo", "Vigor", 2);
        var addArtifact = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), f.Token, new AdicionarArtefato(artifactItemId)));
        var artifactId = (await addArtifact.Content.ReadFromJsonAsync<ArtefatoDaPosse>())!.Id;

        var addArtifactResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), otherToken, new AdicionarArtefato(artifactItemId)));
        addArtifactResponse.StatusCode.Should().Be(Tipo.StatusNegado);

        var listArtifactsResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "artifacts"), otherToken));
        listArtifactsResponse.StatusCode.Should().Be(Tipo.StatusNegado);

        var deleteArtifactResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"artifacts/{artifactId}"), otherToken));
        deleteArtifactResponse.StatusCode.Should().Be(Tipo.StatusNegado);
    }

    [Fact]
    public async Task AddArtifact_with_a_malformed_ArtifactItemId_returns_400()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), f.Token,
            new AdicionarArtefato("not-a-guid")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

/// <summary>Inventário (<c>/inventory</c>), que só Personagem e NPC têm; a Criatura tem Espólios.</summary>
public abstract class PossesDePersonagemOuNpcTestes<TTipo>(ApiFixture fixture) : PossesDaFichaTestes<TTipo>(fixture) where TTipo : ITipoDeTestePersonagemOuNpc, new()
{
    [Fact]
    public async Task AddInventoryItem_links_the_item_and_computes_total_as_peso_times_qtd()
    {
        var f = await NovaFichaAsync();
        var itemId = await CreateItemGeralAsync(f.GmToken, "Corda", 1.5m, 5);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "inventory"), f.Token,
            new AdicionarItem(itemId, 3)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ItemDoInventario>();
        body!.ItemId.Should().Be(itemId);
        body.Nome.Should().Be("Corda");
        body.Qtd.Should().Be(3);
        body.Total.Should().Be(4.5m);
    }

    [Fact]
    public async Task ListInventory_returns_the_added_item()
    {
        var f = await NovaFichaAsync();
        var itemId = await CreateItemGeralAsync(f.GmToken, "Tocha", 0.5m, 2);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "inventory"), f.Token, new AdicionarItem(itemId, 2)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "inventory"), f.Token));

        var body = await response.Content.ReadFromJsonAsync<List<ItemDoInventario>>();
        body!.Should().ContainSingle(i => i.ItemId == itemId);
    }

    [Fact]
    public async Task Delete_an_existing_inventory_item_returns_204_and_it_no_longer_appears_on_list()
    {
        var f = await NovaFichaAsync();
        var itemId = await CreateItemGeralAsync(f.GmToken, "Lanterna", 1m, 8);
        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "inventory"), f.Token, new AdicionarItem(itemId, 1)));
        var inventoryItemId = (await addResponse.Content.ReadFromJsonAsync<ItemDoInventario>())!.Id;

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"inventory/{inventoryItemId}"), f.Token));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "inventory"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<ItemDoInventario>>();
        body!.Should().NotContain(i => i.Id == inventoryItemId);
    }

    [Fact]
    public async Task UpdateInventoryItemQtd_sets_Qtd_and_recomputes_Total()
    {
        var f = await NovaFichaAsync();
        var itemId = await CreateItemGeralAsync(f.GmToken, "Corda", 1.5m, 5);
        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "inventory"), f.Token, new AdicionarItem(itemId, 1)));
        var inventoryItemId = (await addResponse.Content.ReadFromJsonAsync<ItemDoInventario>())!.Id;

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"inventory/{inventoryItemId}/qtd"), f.Token, 4));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "inventory"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<ItemDoInventario>>();
        var item = body!.Single(i => i.Id == inventoryItemId);
        item.Qtd.Should().Be(4);
        item.Total.Should().Be(6m); // Peso (1.5) * Qtd (4)
    }

    [Fact]
    public async Task Inventory_and_artifact_responses_include_the_catalog_items_ImageUrl_and_Descricao()
    {
        var f = await NovaFichaAsync();
        var imageId = await UploadImageAsync(f.GmToken);
        var itemGeralId = await CreateItemGeralAsync(f.GmToken, "Corda Encantada", 1.5m, 5, imageId, "Brilha levemente no escuro.");
        var artifactItemId = await CreateArtefatoItemAsync(f.GmToken, "Anel do Poder", "Atributo", "Vigor", 2, imageId, "Um anel de ouro maciço.");

        var inventoryResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "inventory"), f.Token, new AdicionarItem(itemGeralId, 1)));
        var inventoryBody = await inventoryResponse.Content.ReadFromJsonAsync<ItemDoInventario>();
        inventoryBody!.ImageUrl.Should().StartWith("/images/");
        inventoryBody.Descricao.Should().Be("Brilha levemente no escuro.");

        var artifactResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), f.Token, new AdicionarArtefato(artifactItemId)));
        var artifactBody = await artifactResponse.Content.ReadFromJsonAsync<ArtefatoDaPosse>();
        artifactBody!.ImageUrl.Should().StartWith("/images/");
        artifactBody.Descricao.Should().Be("Um anel de ouro maciço.");
    }

    /// <summary>A parte de Inventário do antigo <c>Possession_actions_by_…</c>.</summary>
    [Fact]
    public async Task Inventory_actions_by_someone_without_access_return_the_denied_status()
    {
        var f = await NovaFichaAsync();
        var otherToken = await Tipo.CriarIntrusoAsync(this, f);
        var itemId = await CreateItemGeralAsync(f.GmToken, "Corda", 1m, 5);
        var addInventory = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "inventory"), f.Token, new AdicionarItem(itemId, 1)));
        var inventoryItemId = (await addInventory.Content.ReadFromJsonAsync<ItemDoInventario>())!.Id;

        var addInventoryResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "inventory"), otherToken, new AdicionarItem(itemId, 1)));
        addInventoryResponse.StatusCode.Should().Be(Tipo.StatusNegado);

        var listInventoryResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "inventory"), otherToken));
        listInventoryResponse.StatusCode.Should().Be(Tipo.StatusNegado);

        var updateInventoryQtdResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"inventory/{inventoryItemId}/qtd"), otherToken, 3));
        updateInventoryQtdResponse.StatusCode.Should().Be(Tipo.StatusNegado);

        var deleteInventoryResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"inventory/{inventoryItemId}"), otherToken));
        deleteInventoryResponse.StatusCode.Should().Be(Tipo.StatusNegado);
    }

    [Fact]
    public async Task AddInventoryItem_with_a_malformed_ItemId_returns_400()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "inventory"), f.Token,
            new AdicionarItem("not-a-guid", 1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

public sealed class PossesDaFichaTestesPersonagem(ApiFixture fixture) : PossesDePersonagemOuNpcTestes<TipoDeTestePersonagem>(fixture)
{
    [Fact]
    public async Task AddInventoryItem_rejects_a_catalog_id_that_is_not_an_ItemGeral()
    {
        // Ficha de Personagem 5.a: "Armas, Armaduras e Escudos não aparecem aqui" — a Weapon
        // catalog id must not be addable as a plain inventory row (would double-count its Peso).
        var f = await NovaFichaAsync();
        var armaResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", f.GmToken,
            new CreateItemRequest("Arma", "Espada Longa", 1.5m, 50, null, "Espadas", null, "F", "UmaMao", "2D6", 3, "19", 2, "Cortante", null, null, null, null, null, null, null, null, null)));
        var armaItemId = (await armaResponse.Content.ReadFromJsonAsync<ItemResponse>())!.Id;

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "inventory"), f.Token,
            new AdicionarItem(armaItemId, 1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

public sealed class PossesDaFichaTestesNpc(ApiFixture fixture) : PossesDePersonagemOuNpcTestes<TipoDeTesteNpc>(fixture);

/// <summary>Espólios (<c>/spoils</c>), que só a Criatura tem.</summary>
public sealed class PossesDaFichaTestesCriatura(ApiFixture fixture) : PossesDaFichaTestes<TipoDeTesteCriatura>(fixture)
{
    [Fact]
    public async Task AddSpoil_computes_CustoTotal_from_the_items_Preco_and_Qtd()
    {
        var f = await NovaFichaAsync();
        var itemId = await CreateItemGeralAsync(f.GmToken, "Corda", 1.5m, 5);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spoils"), f.Token,
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
        var f = await NovaFichaAsync();
        var itemId = await CreateItemGeralAsync(f.GmToken, "Tocha", 0.5m, 2);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spoils"), f.Token, new AddCreatureSpoilRequest(itemId, 2, 10)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "spoils"), f.Token));

        var body = await response.Content.ReadFromJsonAsync<List<CreatureSpoilResponse>>();
        body!.Should().ContainSingle(i => i.ItemId == itemId);
    }

    [Fact]
    public async Task Delete_an_existing_spoil_returns_204_and_it_no_longer_appears_on_list()
    {
        var f = await NovaFichaAsync();
        var itemId = await CreateItemGeralAsync(f.GmToken, "Lanterna", 1m, 8);
        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spoils"), f.Token, new AddCreatureSpoilRequest(itemId, 1, 12)));
        var spoilId = (await addResponse.Content.ReadFromJsonAsync<CreatureSpoilResponse>())!.Id;

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"spoils/{spoilId}"), f.Token));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "spoils"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureSpoilResponse>>();
        body!.Should().NotContain(i => i.Id == spoilId);
    }

    [Fact]
    public async Task UpdateSpoilQtd_sets_Qtd_and_recomputes_CustoTotal()
    {
        var f = await NovaFichaAsync();
        var itemId = await CreateItemGeralAsync(f.GmToken, "Corda", 1m, 5);
        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spoils"), f.Token, new AddCreatureSpoilRequest(itemId, 1, 10)));
        var spoilId = (await addResponse.Content.ReadFromJsonAsync<CreatureSpoilResponse>())!.Id;

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"spoils/{spoilId}/qtd"), f.Token, 4));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "spoils"), f.Token));
        var updatedBody = await listResponse.Content.ReadFromJsonAsync<List<CreatureSpoilResponse>>();
        var spoil = updatedBody!.Single(i => i.Id == spoilId);
        spoil.Qtd.Should().Be(4);
        spoil.CustoTotal.Should().Be(20); // Preco (5) * Qtd (4)
    }

    [Fact]
    public async Task Spoil_and_artifact_responses_include_the_catalog_items_ImageUrl_and_Descricao()
    {
        var f = await NovaFichaAsync();
        var imageId = await UploadImageAsync(f.GmToken);
        var itemGeralId = await CreateItemGeralAsync(f.GmToken, "Corda Encantada", 1.5m, 5, imageId, "Brilha levemente no escuro.");
        var artifactItemId = await CreateArtefatoItemAsync(f.GmToken, "Anel do Poder", "Atributo", "Vigor", 2, imageId, "Um anel de ouro maciço.");

        var spoilResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spoils"), f.Token, new AddCreatureSpoilRequest(itemGeralId, 1, 5)));
        var spoilBody = await spoilResponse.Content.ReadFromJsonAsync<CreatureSpoilResponse>();
        spoilBody!.ImageUrl.Should().StartWith("/images/");
        spoilBody.Descricao.Should().Be("Brilha levemente no escuro.");

        var artifactResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), f.Token, new AddCreatureArtifactRequest(artifactItemId)));
        var artifactBody = await artifactResponse.Content.ReadFromJsonAsync<CreatureArtifactResponse>();
        artifactBody!.ImageUrl.Should().StartWith("/images/");
        artifactBody.Descricao.Should().Be("Um anel de ouro maciço.");
    }

    /// <summary>A parte de Espólios do antigo <c>Possession_actions_by_a_different_gm_return_404</c>.</summary>
    [Fact]
    public async Task Spoil_actions_by_a_different_gm_return_404()
    {
        var f = await NovaFichaAsync();
        var gmTokenOther = await Tipo.CriarIntrusoAsync(this, f);
        var itemId = await CreateItemGeralAsync(f.GmToken, "Corda", 1m, 5);
        var addSpoil = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spoils"), f.Token, new AddCreatureSpoilRequest(itemId, 1, 10)));
        var spoilId = (await addSpoil.Content.ReadFromJsonAsync<CreatureSpoilResponse>())!.Id;

        var addSpoilResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spoils"), gmTokenOther, new AddCreatureSpoilRequest(itemId, 1, 10)));
        addSpoilResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listSpoilsResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "spoils"), gmTokenOther));
        listSpoilsResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var updateSpoilQtdResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"spoils/{spoilId}/qtd"), gmTokenOther, 3));
        updateSpoilQtdResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteSpoilResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"spoils/{spoilId}"), gmTokenOther));
        deleteSpoilResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddSpoil_with_a_malformed_ItemId_returns_400()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spoils"), f.Token,
            new AddCreatureSpoilRequest("not-a-guid", 1, 10)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
