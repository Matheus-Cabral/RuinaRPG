using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class NpcArsenalControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public NpcArsenalControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> CreateArmaItemAsync(string gmToken, int durabilidadeMaxima)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Arma", "Espada", 1.5m, 50, null, "Espadas", null, "F", "UmaMao", "2D6", 3, "19", 2, "Cortante", null, durabilidadeMaxima, null, null, null, null, null, null, null, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    private async Task<string> CreateArmaduraItemAsync(string gmToken, int durabilidadeMaxima)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Armadura", "Elmo de Ferro", 3m, 30, null, null, null, null, null, null, null, null, null, null, null, durabilidadeMaxima, "Medio", 5, 1, 1, "-1 Furtividade", 2, null, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    private async Task<string> CreateEscudoItemAsync(string gmToken, int durabilidadeMaxima)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Escudo", "Broquel", 2m, 25, null, null, null, null, null, null, null, null, null, null, null, durabilidadeMaxima, "Leve", null, null, null, "-1 Agilidade", 1, 2, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    [Fact]
    public async Task AddWeapon_links_the_item_and_initializes_durability_from_its_maximo()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcArsenalGm1", "npcarsenal1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var weaponItemId = await CreateArmaItemAsync(gmToken, 20);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/weapons", gmToken,
            new AddNpcWeaponRequest(weaponItemId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<NpcWeaponResponse>();
        body!.ItemId.Should().Be(weaponItemId);
        body.Nome.Should().Be("Espada");
        body.Dano.Should().Be(3);
        body.DurabilidadeAtual.Should().Be(20);
        body.DurabilidadeMaxima.Should().Be(20);
    }

    [Fact]
    public async Task Equipping_a_second_weapon_unequips_the_first_by_default()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcArsenalGm2", "npcarsenal2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var weaponItemId1 = await CreateArmaItemAsync(gmToken, 20);
        var weaponItemId2 = await CreateArmaItemAsync(gmToken, 15);

        var add1 = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/weapons", gmToken, new AddNpcWeaponRequest(weaponItemId1)));
        var weapon1Id = (await add1.Content.ReadFromJsonAsync<NpcWeaponResponse>())!.Id;
        var add2 = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/weapons", gmToken, new AddNpcWeaponRequest(weaponItemId2)));
        var weapon2Id = (await add2.Content.ReadFromJsonAsync<NpcWeaponResponse>())!.Id;

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/weapons/{weapon1Id}", gmToken, true));
        var equipSecond = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/weapons/{weapon2Id}", gmToken, true));
        equipSecond.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/weapons", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcWeaponResponse>>();
        body!.Single(w => w.Id == weapon1Id).IsEquipped.Should().BeFalse();
        body!.Single(w => w.Id == weapon2Id).IsEquipped.Should().BeTrue();
    }

    [Fact]
    public async Task ArmorSlots_list_returns_all_3_slots_even_when_empty()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcArsenalGm3", "npcarsenal3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/armor-slots", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<NpcArmorSlotResponse>>();
        body!.Should().HaveCount(3);
        body!.Select(s => s.Slot).Should().BeEquivalentTo(new[] { "Capacete", "Superior", "Inferior" });
        body!.Should().OnlyContain(s => s.ItemId == null);
    }

    [Fact]
    public async Task Update_an_armor_slot_links_the_item_and_initializes_durability()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcArsenalGm4", "npcarsenal4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var armorItemId = await CreateArmaduraItemAsync(gmToken, 12);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/armor-slots/Capacete", gmToken,
            new UpdateNpcArmorSlotRequest(armorItemId)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/armor-slots", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcArmorSlotResponse>>();
        var slot = body!.Single(s => s.Slot == "Capacete");
        slot.ItemId.Should().Be(armorItemId);
        slot.Nome.Should().Be("Elmo de Ferro");
        slot.DurabilidadeAtual.Should().Be(12);
        slot.DurabilidadeMaxima.Should().Be(12);
    }

    [Fact]
    public async Task AddShield_and_list_returns_it_with_live_catalog_fields()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcArsenalGm5", "npcarsenal5@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var shieldItemId = await CreateEscudoItemAsync(gmToken, 10);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/shields", gmToken,
            new AddNpcShieldRequest(shieldItemId)));
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/shields", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcShieldResponse>>();
        body!.Should().HaveCount(1);
        var shield = body!.Single();
        shield.ItemId.Should().Be(shieldItemId);
        shield.Nome.Should().Be("Broquel");
        shield.BonusDefesa.Should().Be(2);
        shield.DurabilidadeAtual.Should().Be(10);
        shield.DurabilidadeMaxima.Should().Be(10);
    }

    [Fact]
    public async Task Delete_an_existing_weapon_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcArsenalGm7", "npcarsenal7@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var weaponItemId = await CreateArmaItemAsync(gmToken, 20);
        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/weapons", gmToken, new AddNpcWeaponRequest(weaponItemId)));
        var weaponId = (await addResponse.Content.ReadFromJsonAsync<NpcWeaponResponse>())!.Id;

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/npc-sheets/{sheetId}/weapons/{weaponId}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/weapons", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcWeaponResponse>>();
        body!.Should().NotContain(w => w.Id == weaponId);
    }

    [Fact]
    public async Task Unlinking_an_armor_slot_clears_its_item_and_durability()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcArsenalGm8", "npcarsenal8@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var armorItemId = await CreateArmaduraItemAsync(gmToken, 12);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/armor-slots/Capacete", gmToken, new UpdateNpcArmorSlotRequest(armorItemId)));

        var unlinkResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/armor-slots/Capacete", gmToken, new UpdateNpcArmorSlotRequest(null)));
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/armor-slots", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcArmorSlotResponse>>();
        var slot = body!.Single(s => s.Slot == "Capacete");
        slot.ItemId.Should().BeNull();
        slot.DurabilidadeAtual.Should().BeNull();
    }

    [Fact]
    public async Task Delete_an_existing_shield_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcArsenalGm9", "npcarsenal9@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var shieldItemId = await CreateEscudoItemAsync(gmToken, 10);
        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/shields", gmToken, new AddNpcShieldRequest(shieldItemId)));
        var shieldId = (await addResponse.Content.ReadFromJsonAsync<NpcShieldResponse>())!.Id;

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/npc-sheets/{sheetId}/shields/{shieldId}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/shields", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcShieldResponse>>();
        body!.Should().NotContain(s => s.Id == shieldId);
    }

    [Fact]
    public async Task Weapon_actions_by_a_different_gm_return_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcArsenalGmOwner6", "npcarsenalowner6@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcArsenalGmOther6", "npcarsenalother6@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);
        var weaponItemId = await CreateArmaItemAsync(gmTokenOwner, 20);
        var shieldItemId = await CreateEscudoItemAsync(gmTokenOwner, 10);
        var addWeapon = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/weapons", gmTokenOwner, new AddNpcWeaponRequest(weaponItemId)));
        var weaponId = (await addWeapon.Content.ReadFromJsonAsync<NpcWeaponResponse>())!.Id;
        var addShield = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/shields", gmTokenOwner, new AddNpcShieldRequest(shieldItemId)));
        var shieldId = (await addShield.Content.ReadFromJsonAsync<NpcShieldResponse>())!.Id;

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/weapons", gmTokenOther,
            new AddNpcWeaponRequest(weaponItemId)));
        addResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/weapons/{weaponId}", gmTokenOther, true));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/npc-sheets/{sheetId}/weapons/{weaponId}", gmTokenOther));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var addShieldResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/shields", gmTokenOther,
            new AddNpcShieldRequest(shieldItemId)));
        addShieldResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteShieldResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/npc-sheets/{sheetId}/shields/{shieldId}", gmTokenOther));
        deleteShieldResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListWeapons_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcArsenalGmOwner10", "npcarsenalowner10@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcArsenalGmOther10", "npcarsenalother10@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/weapons", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListArmorSlots_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcArsenalGmOwner11", "npcarsenalowner11@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcArsenalGmOther11", "npcarsenalother11@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/armor-slots", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListShields_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcArsenalGmOwner12", "npcarsenalowner12@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcArsenalGmOther12", "npcarsenalother12@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/shields", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddWeapon_with_a_malformed_ItemId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcArsenalGm13", "npcarsenal13@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/weapons", gmToken,
            new AddNpcWeaponRequest("not-a-guid")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateArmorSlot_with_a_malformed_ItemId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcArsenalGm14", "npcarsenal14@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/armor-slots/Capacete", gmToken,
            new UpdateNpcArmorSlotRequest("not-a-guid")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddShield_with_a_malformed_ItemId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcArsenalGm15", "npcarsenal15@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/shields", gmToken,
            new AddNpcShieldRequest("not-a-guid")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Equipping_a_second_shield_unequips_the_first()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcArsenalGm16", "npcarsenal16@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var shieldItemId1 = await CreateEscudoItemAsync(gmToken, 10);
        var shieldItemId2 = await CreateEscudoItemAsync(gmToken, 8);
        var add1 = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/shields", gmToken, new AddNpcShieldRequest(shieldItemId1)));
        var shield1Id = (await add1.Content.ReadFromJsonAsync<NpcShieldResponse>())!.Id;
        var add2 = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/shields", gmToken, new AddNpcShieldRequest(shieldItemId2)));
        var shield2Id = (await add2.Content.ReadFromJsonAsync<NpcShieldResponse>())!.Id;

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/shields/{shield1Id}", gmToken, true));
        var equipSecond = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/shields/{shield2Id}", gmToken, true));
        equipSecond.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/shields", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcShieldResponse>>();
        body!.Single(s => s.Id == shield1Id).IsEquipped.Should().BeFalse();
        body!.Single(s => s.Id == shield2Id).IsEquipped.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateWeaponDurability_clamps_to_the_item_DurabilidadeMaxima()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcArsenalGm17", "npcarsenal17@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var weaponItemId = await CreateArmaItemAsync(gmToken, 20);
        var add = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/weapons", gmToken, new AddNpcWeaponRequest(weaponItemId)));
        var weaponId = (await add.Content.ReadFromJsonAsync<NpcWeaponResponse>())!.Id;

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/weapons/{weaponId}/durabilidade", gmToken, 999));

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/weapons", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcWeaponResponse>>();
        body!.Single(w => w.Id == weaponId).DurabilidadeAtual.Should().Be(20);
    }

    [Fact]
    public async Task UpdateShieldDurability_clamps_to_the_item_DurabilidadeMaxima()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcArsenalGm18", "npcarsenal18@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var shieldItemId = await CreateEscudoItemAsync(gmToken, 10);
        var add = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/shields", gmToken, new AddNpcShieldRequest(shieldItemId)));
        var shieldId = (await add.Content.ReadFromJsonAsync<NpcShieldResponse>())!.Id;

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/shields/{shieldId}/durabilidade", gmToken, 999));

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/shields", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcShieldResponse>>();
        body!.Single(s => s.Id == shieldId).DurabilidadeAtual.Should().Be(10);
    }

    [Fact]
    public async Task UpdateArmorSlotDurability_clamps_to_the_item_DurabilidadeMaxima()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcArsenalGm19", "npcarsenal19@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var armorItemId = await CreateArmaduraItemAsync(gmToken, 15);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/armor-slots/Capacete", gmToken, new UpdateNpcArmorSlotRequest(armorItemId)));

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/armor-slots/Capacete/durabilidade", gmToken, 999));

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/armor-slots", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcArmorSlotResponse>>();
        body!.Single(a => a.Slot == "Capacete").DurabilidadeAtual.Should().Be(15);
    }

    [Fact]
    public async Task UpdateArmorSlotDurability_on_an_empty_slot_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcArsenalGm20", "npcarsenal20@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/armor-slots/Capacete/durabilidade", gmToken, 5));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
