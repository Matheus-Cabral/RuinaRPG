using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Contracts.Items;
// Os pedidos de arma (com ItemId do catálogo), armadura e escudo têm o mesmo JSON nos três tipos de ficha;
// o pedido de arma da Criatura só acrescenta os campos do ataque natural, que ficam nulos.
using AdicionarArma = RuinaRPG.Contracts.Fichas.AddWeaponRequest;
using AdicionarEscudo = RuinaRPG.Contracts.Fichas.AddShieldRequest;
using AtualizarSlot = RuinaRPG.Contracts.Fichas.UpdateArmorSlotRequest;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

/// <summary>Armas, armaduras e escudos da ficha (<c>/weapons</c>, <c>/armor-slots</c>, <c>/shields</c>), nos três tipos.</summary>
public abstract class ArsenalDaFichaTestes<TTipo>(ApiFixture fixture) : TestesDeFicha<TTipo>(fixture) where TTipo : ITipoDeTeste, new()
{
    protected async Task<string> CreateArmaItemAsync(string gmToken, string? rank, string? imageId = null, string? descricao = null)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Arma", "Espada", 1.5m, 50, imageId, "Espadas", descricao, rank, "UmaMao", "2D6", 3, "19", 2, "Cortante", null, null, null, null, null, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    protected async Task<string> CreateArmaduraItemAsync(string gmToken, string? rank, string? imageId = null, string? descricao = null)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Armadura", "Elmo de Ferro", 3m, 30, imageId, null, descricao, rank, null, null, null, null, null, null, "Medio", 5, 1, 1, null, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    protected async Task<string> CreateEscudoItemAsync(string gmToken, string? rank, string? imageId = null, string? descricao = null)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Escudo", "Broquel", 2m, 25, imageId, null, descricao, rank, null, null, null, null, null, null, "Leve", null, null, null, 2, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    [Fact]
    public async Task Equipping_a_second_weapon_unequips_the_first_by_default()
    {
        var f = await NovaFichaAsync();
        var weaponItemId1 = await CreateArmaItemAsync(f.GmToken, "F");
        var weaponItemId2 = await CreateArmaItemAsync(f.GmToken, "F");

        var add1 = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), f.Token, new AdicionarArma(weaponItemId1)));
        var weapon1Id = (await add1.Content.ReadFromJsonAsync<ArmaLida>())!.Id;
        var add2 = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), f.Token, new AdicionarArma(weaponItemId2)));
        var weapon2Id = (await add2.Content.ReadFromJsonAsync<ArmaLida>())!.Id;

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"weapons/{weapon1Id}"), f.Token, true));
        var equipSecond = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"weapons/{weapon2Id}"), f.Token, true));
        equipSecond.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "weapons"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<ArmaLida>>();
        body!.Single(w => w.Id == weapon1Id).IsEquipped.Should().BeFalse();
        body!.Single(w => w.Id == weapon2Id).IsEquipped.Should().BeTrue();
    }

    [Fact]
    public async Task ArmorSlots_list_returns_all_3_slots_even_when_empty()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "armor-slots"), f.Token));

        var body = await response.Content.ReadFromJsonAsync<List<SlotLido>>();
        body!.Should().HaveCount(3);
        body!.Select(s => s.Slot).Should().BeEquivalentTo(new[] { "Capacete", "Superior", "Inferior" });
        body!.Should().OnlyContain(s => s.ItemId == null);
    }

    [Fact]
    public async Task AddShield_and_list_returns_it_with_live_catalog_fields()
    {
        var f = await NovaFichaAsync();
        var shieldItemId = await CreateEscudoItemAsync(f.GmToken, "F");

        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "shields"), f.Token, new AdicionarEscudo(shieldItemId)));
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "shields"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<EscudoLido>>();
        body!.Should().HaveCount(1);
        var shield = body!.Single();
        shield.ItemId.Should().Be(shieldItemId);
        shield.Nome.Should().Be("Broquel");
        shield.BonusDefesa.Should().Be(2);
        shield.DurabilidadeAtual.Should().Be(20);
        shield.DurabilidadeMaxima.Should().Be(20);
    }

    [Fact]
    public async Task Weapon_armor_slot_and_shield_responses_include_the_catalog_items_ImageUrl_and_Descricao()
    {
        var f = await NovaFichaAsync();
        var imageId = await UploadImageAsync(f.GmToken);
        var weaponItemId = await CreateArmaItemAsync(f.GmToken, "F", imageId, "Uma lâmina antiga.");
        var armorItemId = await CreateArmaduraItemAsync(f.GmToken, "F", imageId, "Placas enferrujadas.");
        var shieldItemId = await CreateEscudoItemAsync(f.GmToken, "F", imageId, "Um broquel rachado.");

        var weaponResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), f.Token, new AdicionarArma(weaponItemId)));
        var weaponBody = await weaponResponse.Content.ReadFromJsonAsync<ArmaLida>();
        weaponBody!.ImageUrl.Should().StartWith("/images/");
        weaponBody.Descricao.Should().Be("Uma lâmina antiga.");

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "armor-slots/Capacete"), f.Token, new AtualizarSlot(armorItemId)));
        var armorSlotsResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "armor-slots"), f.Token));
        var armorSlot = (await armorSlotsResponse.Content.ReadFromJsonAsync<List<SlotLido>>())!.Single(s => s.Slot == "Capacete");
        armorSlot.ImageUrl.Should().StartWith("/images/");
        armorSlot.Descricao.Should().Be("Placas enferrujadas.");

        var shieldResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "shields"), f.Token, new AdicionarEscudo(shieldItemId)));
        var shieldBody = await shieldResponse.Content.ReadFromJsonAsync<EscudoLido>();
        shieldBody!.ImageUrl.Should().StartWith("/images/");
        shieldBody.Descricao.Should().Be("Um broquel rachado.");
    }

    [Fact]
    public async Task Delete_an_existing_weapon_returns_204_and_it_no_longer_appears_on_list()
    {
        var f = await NovaFichaAsync();
        var weaponItemId = await CreateArmaItemAsync(f.GmToken, "F");
        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), f.Token, new AdicionarArma(weaponItemId)));
        var weaponId = (await addResponse.Content.ReadFromJsonAsync<ArmaLida>())!.Id;

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"weapons/{weaponId}"), f.Token));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "weapons"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<ArmaLida>>();
        body!.Should().NotContain(w => w.Id == weaponId);
    }

    [Fact]
    public async Task Unlinking_an_armor_slot_clears_its_item_and_durability()
    {
        var f = await NovaFichaAsync();
        var armorItemId = await CreateArmaduraItemAsync(f.GmToken, "F");
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "armor-slots/Capacete"), f.Token, new AtualizarSlot(armorItemId)));

        var unlinkResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "armor-slots/Capacete"), f.Token, new AtualizarSlot(null)));
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "armor-slots"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<SlotLido>>();
        var slot = body!.Single(s => s.Slot == "Capacete");
        slot.ItemId.Should().BeNull();
        slot.DurabilidadeAtual.Should().BeNull();
    }

    [Fact]
    public async Task Delete_an_existing_shield_returns_204_and_it_no_longer_appears_on_list()
    {
        var f = await NovaFichaAsync();
        var shieldItemId = await CreateEscudoItemAsync(f.GmToken, "F");
        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "shields"), f.Token, new AdicionarEscudo(shieldItemId)));
        var shieldId = (await addResponse.Content.ReadFromJsonAsync<EscudoLido>())!.Id;

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"shields/{shieldId}"), f.Token));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "shields"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<EscudoLido>>();
        body!.Should().NotContain(s => s.Id == shieldId);
    }

    [Fact]
    public async Task Weapon_actions_by_someone_without_access_return_the_denied_status()
    {
        var f = await NovaFichaAsync();
        var otherToken = await Tipo.CriarIntrusoAsync(this, f);
        var weaponItemId = await CreateArmaItemAsync(f.GmToken, "F");
        var shieldItemId = await CreateEscudoItemAsync(f.GmToken, "F");
        var addWeapon = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), f.Token, new AdicionarArma(weaponItemId)));
        var weaponId = (await addWeapon.Content.ReadFromJsonAsync<ArmaLida>())!.Id;
        var addShield = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "shields"), f.Token, new AdicionarEscudo(shieldItemId)));
        var shieldId = (await addShield.Content.ReadFromJsonAsync<EscudoLido>())!.Id;

        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), otherToken, new AdicionarArma(weaponItemId)));
        addResponse.StatusCode.Should().Be(Tipo.StatusNegado);

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"weapons/{weaponId}"), otherToken, true));
        updateResponse.StatusCode.Should().Be(Tipo.StatusNegado);

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"weapons/{weaponId}"), otherToken));
        deleteResponse.StatusCode.Should().Be(Tipo.StatusNegado);

        var addShieldResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "shields"), otherToken, new AdicionarEscudo(shieldItemId)));
        addShieldResponse.StatusCode.Should().Be(Tipo.StatusNegado);

        var deleteShieldResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"shields/{shieldId}"), otherToken));
        deleteShieldResponse.StatusCode.Should().Be(Tipo.StatusNegado);
    }

    [Theory]
    [InlineData("weapons")]
    [InlineData("armor-slots")]
    [InlineData("shields")]
    public async Task List_by_someone_without_access_returns_the_denied_status(string lista)
    {
        var f = await NovaFichaAsync();
        var intruso = await Tipo.CriarIntrusoAsync(this, f);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, lista), intruso));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddWeapon_with_a_malformed_ItemId_returns_400()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), f.Token, new AdicionarArma("not-a-guid")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateArmorSlot_with_a_malformed_ItemId_returns_400()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "armor-slots/Capacete"), f.Token, new AtualizarSlot("not-a-guid")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddShield_with_a_malformed_ItemId_returns_400()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "shields"), f.Token, new AdicionarEscudo("not-a-guid")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Equipping_a_second_shield_unequips_the_first()
    {
        var f = await NovaFichaAsync();
        var shieldItemId1 = await CreateEscudoItemAsync(f.GmToken, "F");
        var shieldItemId2 = await CreateEscudoItemAsync(f.GmToken, "F");

        var add1 = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "shields"), f.Token, new AdicionarEscudo(shieldItemId1)));
        var shield1Id = (await add1.Content.ReadFromJsonAsync<EscudoLido>())!.Id;
        var add2 = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "shields"), f.Token, new AdicionarEscudo(shieldItemId2)));
        var shield2Id = (await add2.Content.ReadFromJsonAsync<EscudoLido>())!.Id;

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"shields/{shield1Id}"), f.Token, true));
        var equipSecond = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"shields/{shield2Id}"), f.Token, true));
        equipSecond.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "shields"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<EscudoLido>>();
        body!.Single(s => s.Id == shield1Id).IsEquipped.Should().BeFalse();
        body!.Single(s => s.Id == shield2Id).IsEquipped.Should().BeTrue();
    }

    private async Task<(FichaDeTeste Ficha, string WeaponId)> FichaComArmaAsync()
    {
        var f = await NovaFichaAsync();
        var weaponItemId = await CreateArmaItemAsync(f.GmToken, "F");
        var add = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), f.Token, new AdicionarArma(weaponItemId)));
        return (f, (await add.Content.ReadFromJsonAsync<ArmaLida>())!.Id);
    }

    private async Task<(FichaDeTeste Ficha, string ShieldId)> FichaComEscudoAsync()
    {
        var f = await NovaFichaAsync();
        var shieldItemId = await CreateEscudoItemAsync(f.GmToken, "F");
        var add = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "shields"), f.Token, new AdicionarEscudo(shieldItemId)));
        return (f, (await add.Content.ReadFromJsonAsync<EscudoLido>())!.Id);
    }

    private async Task<FichaDeTeste> FichaComCapaceteAsync()
    {
        var f = await NovaFichaAsync();
        var armorItemId = await CreateArmaduraItemAsync(f.GmToken, "F");
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "armor-slots/Capacete"), f.Token, new AtualizarSlot(armorItemId)));
        return f;
    }

    [Fact]
    public async Task UpdateWeaponDurability_sets_DurabilidadeAtual()
    {
        var (f, weaponId) = await FichaComArmaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"weapons/{weaponId}/durabilidade"), f.Token, 12));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "weapons"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<ArmaLida>>();
        body!.Single(w => w.Id == weaponId).DurabilidadeAtual.Should().Be(12);
    }

    [Fact]
    public async Task UpdateShieldDurability_sets_DurabilidadeAtual()
    {
        var (f, shieldId) = await FichaComEscudoAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"shields/{shieldId}/durabilidade"), f.Token, 4));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "shields"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<EscudoLido>>();
        body!.Single(s => s.Id == shieldId).DurabilidadeAtual.Should().Be(4);
    }

    [Fact]
    public async Task UpdateArmorSlotDurability_sets_DurabilidadeAtual_when_a_slot_has_an_item()
    {
        var f = await FichaComCapaceteAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "armor-slots/Capacete/durabilidade"), f.Token, 7));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "armor-slots"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<SlotLido>>();
        body!.Single(a => a.Slot == "Capacete").DurabilidadeAtual.Should().Be(7);
    }

    [Fact]
    public async Task UpdateWeaponDurability_clamps_to_the_item_DurabilidadeMaxima_instead_of_rejecting()
    {
        // Ficha de Personagem 3.a: "não pode exceder o Máximo".
        var (f, weaponId) = await FichaComArmaAsync();

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"weapons/{weaponId}/durabilidade"), f.Token, 999));

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "weapons"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<ArmaLida>>();
        body!.Single(w => w.Id == weaponId).DurabilidadeAtual.Should().Be(20);
    }

    [Fact]
    public async Task UpdateShieldDurability_clamps_to_the_item_DurabilidadeMaxima_instead_of_rejecting()
    {
        var (f, shieldId) = await FichaComEscudoAsync();

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"shields/{shieldId}/durabilidade"), f.Token, 999));

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "shields"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<EscudoLido>>();
        body!.Single(s => s.Id == shieldId).DurabilidadeAtual.Should().Be(20);
    }

    [Fact]
    public async Task UpdateArmorSlotDurability_clamps_to_the_item_DurabilidadeMaxima_instead_of_rejecting()
    {
        var f = await FichaComCapaceteAsync();

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "armor-slots/Capacete/durabilidade"), f.Token, 999));

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "armor-slots"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<SlotLido>>();
        body!.Single(a => a.Slot == "Capacete").DurabilidadeAtual.Should().Be(20);
    }

    [Fact]
    public async Task UpdateArmorSlotDurability_on_an_empty_slot_returns_400()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "armor-slots/Capacete/durabilidade"), f.Token, 7));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

/// <summary>Personagem e NPC: a durabilidade máxima vem em <c>DurabilidadeMaxima</c> (na Criatura, <c>DurabilidadeMaximo</c>).</summary>
public abstract class ArsenalDePersonagemOuNpcTestes<TTipo>(ApiFixture fixture) : ArsenalDaFichaTestes<TTipo>(fixture) where TTipo : ITipoDeTestePersonagemOuNpc, new()
{
    [Fact]
    public async Task AddWeapon_links_the_item_and_initializes_durability_from_its_maximo()
    {
        var f = await NovaFichaAsync();
        var weaponItemId = await CreateArmaItemAsync(f.GmToken, "F");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), f.Token, new AdicionarArma(weaponItemId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ArmaLida>();
        body!.ItemId.Should().Be(weaponItemId);
        body.Nome.Should().Be("Espada");
        body.Dano.Should().Be(3);
        body.DurabilidadeAtual.Should().Be(20);
        body.DurabilidadeMaxima.Should().Be(20);
    }

    [Fact]
    public async Task Update_an_armor_slot_links_the_item_and_initializes_durability()
    {
        var f = await NovaFichaAsync();
        var armorItemId = await CreateArmaduraItemAsync(f.GmToken, "F");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "armor-slots/Capacete"), f.Token, new AtualizarSlot(armorItemId)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "armor-slots"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<SlotLido>>();
        var slot = body!.Single(s => s.Slot == "Capacete");
        slot.ItemId.Should().Be(armorItemId);
        slot.Nome.Should().Be("Elmo de Ferro");
        slot.DurabilidadeAtual.Should().Be(20);
        slot.DurabilidadeMaxima.Should().Be(20);
    }
}

public sealed class ArsenalDaFichaTestesPersonagem(ApiFixture fixture) : ArsenalDePersonagemOuNpcTestes<TipoDeTestePersonagem>(fixture);

public sealed class ArsenalDaFichaTestesNpc(ApiFixture fixture) : ArsenalDePersonagemOuNpcTestes<TipoDeTesteNpc>(fixture);

/// <summary>Criatura: ataque natural manual (sem item do catálogo) e <c>DurabilidadeMaximo</c>.</summary>
public sealed class ArsenalDaFichaTestesCriatura(ApiFixture fixture) : ArsenalDaFichaTestes<TipoDeTesteCriatura>(fixture)
{
    private static AddCreatureWeaponRequest Garras() => new(null, "Garras", "Cortante", "1D6", 2);

    [Fact]
    public async Task AddWeapon_from_the_catalogo_links_the_item_same_as_a_character()
    {
        var f = await NovaFichaAsync();
        var weaponItemId = await CreateArmaItemAsync(f.GmToken, "F");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), f.Token,
            new AddCreatureWeaponRequest(weaponItemId, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreatureWeaponResponse>();
        body!.ItemId.Should().Be(weaponItemId);
        body.Nome.Should().Be("Espada");
        body.Dano.Should().Be(3);
        body.Alcance.Should().Be(2);
        body.DurabilidadeAtual.Should().Be(20);
        body.DurabilidadeMaximo.Should().Be(20);
    }

    [Fact]
    public async Task AddWeapon_manual_creates_a_natural_attack_with_no_durability_or_alcance()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), f.Token, Garras()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreatureWeaponResponse>();
        body!.ItemId.Should().BeNull();
        body.Nome.Should().Be("Garras");
        body.TipoDeDano.Should().Be("Cortante");
        body.Dados.Should().Be("1D6");
        body.Dano.Should().Be(2);
        body.Alcance.Should().BeNull();
        body.Critico.Should().BeNull();
        body.Rank.Should().BeNull();
        body.DurabilidadeAtual.Should().BeNull();
        body.DurabilidadeMaximo.Should().BeNull();
    }

    [Fact]
    public async Task AddWeapon_with_both_ItemId_and_manual_fields_returns_400()
    {
        var f = await NovaFichaAsync();
        var weaponItemId = await CreateArmaItemAsync(f.GmToken, "F");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), f.Token,
            new AddCreatureWeaponRequest(weaponItemId, "Garras", "Cortante", "1D6", 2)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddWeapon_with_neither_ItemId_nor_manual_fields_returns_400()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), f.Token,
            new AddCreatureWeaponRequest(null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Equipping_a_natural_attack_unequips_the_catalog_weapon()
    {
        var f = await NovaFichaAsync();
        var weaponItemId1 = await CreateArmaItemAsync(f.GmToken, "F");

        var add1 = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), f.Token, new AddCreatureWeaponRequest(weaponItemId1, null, null, null, null)));
        var weapon1Id = (await add1.Content.ReadFromJsonAsync<CreatureWeaponResponse>())!.Id;
        var add2 = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), f.Token, Garras()));
        var weapon2Id = (await add2.Content.ReadFromJsonAsync<CreatureWeaponResponse>())!.Id;

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"weapons/{weapon1Id}"), f.Token, true));
        var equipSecond = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"weapons/{weapon2Id}"), f.Token, true));
        equipSecond.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "weapons"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureWeaponResponse>>();
        body!.Single(w => w.Id == weapon1Id).IsEquipped.Should().BeFalse();
        body!.Single(w => w.Id == weapon2Id).IsEquipped.Should().BeTrue();
    }

    [Fact]
    public async Task Update_an_armor_slot_links_the_item_and_initializes_durability()
    {
        var f = await NovaFichaAsync();
        var armorItemId = await CreateArmaduraItemAsync(f.GmToken, "F");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "armor-slots/Capacete"), f.Token,
            new UpdateArmorSlotRequest(armorItemId)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "armor-slots"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureArmorSlotResponse>>();
        var slot = body!.Single(s => s.Slot == "Capacete");
        slot.ItemId.Should().Be(armorItemId);
        slot.Nome.Should().Be("Elmo de Ferro");
        slot.DurabilidadeAtual.Should().Be(20);
        slot.DurabilidadeMaximo.Should().Be(20);
    }

    [Fact]
    public async Task A_manual_natural_attack_weapon_has_no_ImageUrl_or_Descricao()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), f.Token, Garras()));

        var body = await response.Content.ReadFromJsonAsync<CreatureWeaponResponse>();
        body!.ImageUrl.Should().BeNull();
        body.Descricao.Should().BeNull();
    }

    [Fact]
    public async Task UpdateWeaponDurability_on_a_manual_natural_attack_returns_400()
    {
        var f = await NovaFichaAsync();
        var add = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), f.Token, Garras()));
        var weaponId = (await add.Content.ReadFromJsonAsync<CreatureWeaponResponse>())!.Id;

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"weapons/{weaponId}/durabilidade"), f.Token, 5));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
