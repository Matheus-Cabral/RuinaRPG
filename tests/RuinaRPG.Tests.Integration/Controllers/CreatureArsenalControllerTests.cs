using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Items;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CreatureArsenalControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CreatureArsenalControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> CreateArmaItemAsync(string gmToken, int durabilidadeMaxima, string? imageId = null, string? descricao = null)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Arma", "Espada", 1.5m, 50, imageId, "Espadas", descricao, "F", "UmaMao", "2D6", 3, "19", 2, "Cortante", null, durabilidadeMaxima, null, null, null, null, null, null, null, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    private async Task<string> CreateArmaduraItemAsync(string gmToken, int durabilidadeMaxima, string? imageId = null, string? descricao = null)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Armadura", "Elmo de Ferro", 3m, 30, imageId, null, descricao, null, null, null, null, null, null, null, null, durabilidadeMaxima, "Medio", 5, 1, 1, "-1 Furtividade", 2, null, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    private async Task<string> CreateEscudoItemAsync(string gmToken, int durabilidadeMaxima, string? imageId = null, string? descricao = null)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Escudo", "Broquel", 2m, 25, imageId, null, descricao, null, null, null, null, null, null, null, null, durabilidadeMaxima, "Leve", null, null, null, "-1 Agilidade", 1, 2, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    private static MultipartFormDataContent BuildImageUpload()
    {
        var content = new MultipartFormDataContent();
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");
        return content;
    }

    private async Task<string> UploadImageAsync(string token)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildImageUpload() };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(message);
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Images.ImageUploadResponse>())!.Id;
    }

    [Fact]
    public async Task AddWeapon_from_the_catalogo_links_the_item_same_as_a_character()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm1", "creaturearsenal1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var weaponItemId = await CreateArmaItemAsync(gmToken, 20);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/weapons", gmToken,
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
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm2", "creaturearsenal2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/weapons", gmToken,
            new AddCreatureWeaponRequest(null, "Garras", "Cortante", "1D6", 2)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreatureWeaponResponse>();
        body!.ItemId.Should().BeNull();
        body.Nome.Should().Be("Garras");
        body.TipoDeDano.Should().Be("Cortante");
        body.Dados.Should().Be("1D6");
        body.Dano.Should().Be(2);
        body.Alcance.Should().BeNull();
        body.Critico.Should().BeNull();
        body.Tier.Should().BeNull();
        body.DurabilidadeAtual.Should().BeNull();
        body.DurabilidadeMaximo.Should().BeNull();
    }

    [Fact]
    public async Task AddWeapon_with_both_ItemId_and_manual_fields_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm3", "creaturearsenal3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var weaponItemId = await CreateArmaItemAsync(gmToken, 20);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/weapons", gmToken,
            new AddCreatureWeaponRequest(weaponItemId, "Garras", "Cortante", "1D6", 2)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddWeapon_with_neither_ItemId_nor_manual_fields_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm4", "creaturearsenal4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/weapons", gmToken,
            new AddCreatureWeaponRequest(null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Equipping_a_second_weapon_unequips_the_first_by_default()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm5", "creaturearsenal5@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var weaponItemId1 = await CreateArmaItemAsync(gmToken, 20);

        var add1 = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/weapons", gmToken, new AddCreatureWeaponRequest(weaponItemId1, null, null, null, null)));
        var weapon1Id = (await add1.Content.ReadFromJsonAsync<CreatureWeaponResponse>())!.Id;
        var add2 = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/weapons", gmToken, new AddCreatureWeaponRequest(null, "Garras", "Cortante", "1D6", 2)));
        var weapon2Id = (await add2.Content.ReadFromJsonAsync<CreatureWeaponResponse>())!.Id;

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/weapons/{weapon1Id}", gmToken, true));
        var equipSecond = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/weapons/{weapon2Id}", gmToken, true));
        equipSecond.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/weapons", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureWeaponResponse>>();
        body!.Single(w => w.Id == weapon1Id).IsEquipped.Should().BeFalse();
        body!.Single(w => w.Id == weapon2Id).IsEquipped.Should().BeTrue();
    }

    [Fact]
    public async Task ArmorSlots_list_returns_all_3_slots_even_when_empty()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm6", "creaturearsenal6@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/armor-slots", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<CreatureArmorSlotResponse>>();
        body!.Should().HaveCount(3);
        body!.Select(s => s.Slot).Should().BeEquivalentTo(new[] { "Capacete", "Superior", "Inferior" });
        body!.Should().OnlyContain(s => s.ItemId == null);
    }

    [Fact]
    public async Task Update_an_armor_slot_links_the_item_and_initializes_durability()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm7", "creaturearsenal7@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var armorItemId = await CreateArmaduraItemAsync(gmToken, 12);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/armor-slots/Capacete", gmToken,
            new UpdateCreatureArmorSlotRequest(armorItemId)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/armor-slots", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureArmorSlotResponse>>();
        var slot = body!.Single(s => s.Slot == "Capacete");
        slot.ItemId.Should().Be(armorItemId);
        slot.Nome.Should().Be("Elmo de Ferro");
        slot.DurabilidadeAtual.Should().Be(12);
        slot.DurabilidadeMaximo.Should().Be(12);
    }

    [Fact]
    public async Task AddShield_and_list_returns_it_with_live_catalog_fields()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm8", "creaturearsenal8@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var shieldItemId = await CreateEscudoItemAsync(gmToken, 10);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/shields", gmToken,
            new AddCreatureShieldRequest(shieldItemId)));
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/shields", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureShieldResponse>>();
        body!.Should().HaveCount(1);
        var shield = body!.Single();
        shield.ItemId.Should().Be(shieldItemId);
        shield.Nome.Should().Be("Broquel");
        shield.BonusDefesa.Should().Be(2);
        shield.DurabilidadeAtual.Should().Be(10);
        shield.DurabilidadeMaxima.Should().Be(10);
    }

    [Fact]
    public async Task Weapon_armor_slot_and_shield_responses_include_the_catalog_items_ImageUrl_and_Descricao()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGmImg1", "creaturearsenalimg1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var imageId = await UploadImageAsync(gmToken);
        var weaponItemId = await CreateArmaItemAsync(gmToken, 20, imageId, "Uma lâmina antiga.");
        var armorItemId = await CreateArmaduraItemAsync(gmToken, 12, imageId, "Placas enferrujadas.");
        var shieldItemId = await CreateEscudoItemAsync(gmToken, 10, imageId, "Um broquel rachado.");

        var weaponResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/weapons", gmToken, new AddCreatureWeaponRequest(weaponItemId, null, null, null, null)));
        var weaponBody = await weaponResponse.Content.ReadFromJsonAsync<CreatureWeaponResponse>();
        weaponBody!.ImageUrl.Should().StartWith("/images/");
        weaponBody.Descricao.Should().Be("Uma lâmina antiga.");

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/armor-slots/Capacete", gmToken, new UpdateCreatureArmorSlotRequest(armorItemId)));
        var armorSlotsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/armor-slots", gmToken));
        var armorSlot = (await armorSlotsResponse.Content.ReadFromJsonAsync<List<CreatureArmorSlotResponse>>())!.Single(s => s.Slot == "Capacete");
        armorSlot.ImageUrl.Should().StartWith("/images/");
        armorSlot.Descricao.Should().Be("Placas enferrujadas.");

        var shieldResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/shields", gmToken, new AddCreatureShieldRequest(shieldItemId)));
        var shieldBody = await shieldResponse.Content.ReadFromJsonAsync<CreatureShieldResponse>();
        shieldBody!.ImageUrl.Should().StartWith("/images/");
        shieldBody.Descricao.Should().Be("Um broquel rachado.");
    }

    [Fact]
    public async Task A_manual_natural_attack_weapon_has_no_ImageUrl_or_Descricao()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGmImg2", "creaturearsenalimg2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/weapons", gmToken,
            new AddCreatureWeaponRequest(null, "Garras", "Cortante", "1D6", 2)));

        var body = await response.Content.ReadFromJsonAsync<CreatureWeaponResponse>();
        body!.ImageUrl.Should().BeNull();
        body.Descricao.Should().BeNull();
    }

    [Fact]
    public async Task Delete_an_existing_weapon_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm9", "creaturearsenal9@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var weaponItemId = await CreateArmaItemAsync(gmToken, 20);
        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/weapons", gmToken, new AddCreatureWeaponRequest(weaponItemId, null, null, null, null)));
        var weaponId = (await addResponse.Content.ReadFromJsonAsync<CreatureWeaponResponse>())!.Id;

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}/weapons/{weaponId}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/weapons", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureWeaponResponse>>();
        body!.Should().NotContain(w => w.Id == weaponId);
    }

    [Fact]
    public async Task Unlinking_an_armor_slot_clears_its_item_and_durability()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm10", "creaturearsenal10@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var armorItemId = await CreateArmaduraItemAsync(gmToken, 12);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/armor-slots/Capacete", gmToken, new UpdateCreatureArmorSlotRequest(armorItemId)));

        var unlinkResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/armor-slots/Capacete", gmToken, new UpdateCreatureArmorSlotRequest(null)));
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/armor-slots", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureArmorSlotResponse>>();
        var slot = body!.Single(s => s.Slot == "Capacete");
        slot.ItemId.Should().BeNull();
        slot.DurabilidadeAtual.Should().BeNull();
    }

    [Fact]
    public async Task Delete_an_existing_shield_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm11", "creaturearsenal11@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var shieldItemId = await CreateEscudoItemAsync(gmToken, 10);
        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/shields", gmToken, new AddCreatureShieldRequest(shieldItemId)));
        var shieldId = (await addResponse.Content.ReadFromJsonAsync<CreatureShieldResponse>())!.Id;

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}/shields/{shieldId}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/shields", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureShieldResponse>>();
        body!.Should().NotContain(s => s.Id == shieldId);
    }

    [Fact]
    public async Task Weapon_actions_by_a_different_gm_return_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureArsenalGmOwner12", "creaturearsenalowner12@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureArsenalGmOther12", "creaturearsenalother12@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);
        var weaponItemId = await CreateArmaItemAsync(gmTokenOwner, 20);
        var shieldItemId = await CreateEscudoItemAsync(gmTokenOwner, 10);
        var addWeapon = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/weapons", gmTokenOwner, new AddCreatureWeaponRequest(weaponItemId, null, null, null, null)));
        var weaponId = (await addWeapon.Content.ReadFromJsonAsync<CreatureWeaponResponse>())!.Id;
        var addShield = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/shields", gmTokenOwner, new AddCreatureShieldRequest(shieldItemId)));
        var shieldId = (await addShield.Content.ReadFromJsonAsync<CreatureShieldResponse>())!.Id;

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/weapons", gmTokenOther,
            new AddCreatureWeaponRequest(weaponItemId, null, null, null, null)));
        addResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/weapons/{weaponId}", gmTokenOther, true));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}/weapons/{weaponId}", gmTokenOther));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var addShieldResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/shields", gmTokenOther,
            new AddCreatureShieldRequest(shieldItemId)));
        addShieldResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteShieldResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}/shields/{shieldId}", gmTokenOther));
        deleteShieldResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListWeapons_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureArsenalGmOwner13", "creaturearsenalowner13@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureArsenalGmOther13", "creaturearsenalother13@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/weapons", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListArmorSlots_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureArsenalGmOwner14", "creaturearsenalowner14@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureArsenalGmOther14", "creaturearsenalother14@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/armor-slots", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListShields_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureArsenalGmOwner15", "creaturearsenalowner15@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureArsenalGmOther15", "creaturearsenalother15@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/shields", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddWeapon_from_catalogo_with_a_malformed_ItemId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm16", "creaturearsenal16@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/weapons", gmToken,
            new AddCreatureWeaponRequest("not-a-guid", null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateArmorSlot_with_a_malformed_ItemId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm17", "creaturearsenal17@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/armor-slots/Capacete", gmToken,
            new UpdateCreatureArmorSlotRequest("not-a-guid")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddShield_with_a_malformed_ItemId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm18", "creaturearsenal18@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/shields", gmToken,
            new AddCreatureShieldRequest("not-a-guid")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Equipping_a_second_shield_unequips_the_first()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm19", "creaturearsenal19@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var shieldItemId1 = await CreateEscudoItemAsync(gmToken, 10);
        var shieldItemId2 = await CreateEscudoItemAsync(gmToken, 8);
        var add1 = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/shields", gmToken, new AddCreatureShieldRequest(shieldItemId1)));
        var shield1Id = (await add1.Content.ReadFromJsonAsync<CreatureShieldResponse>())!.Id;
        var add2 = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/shields", gmToken, new AddCreatureShieldRequest(shieldItemId2)));
        var shield2Id = (await add2.Content.ReadFromJsonAsync<CreatureShieldResponse>())!.Id;

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/shields/{shield1Id}", gmToken, true));
        var equipSecond = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/shields/{shield2Id}", gmToken, true));
        equipSecond.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/shields", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureShieldResponse>>();
        body!.Single(s => s.Id == shield1Id).IsEquipped.Should().BeFalse();
        body!.Single(s => s.Id == shield2Id).IsEquipped.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateWeaponDurability_clamps_to_the_item_DurabilidadeMaxima_for_a_catalog_weapon()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm20", "creaturearsenal20@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var weaponItemId = await CreateArmaItemAsync(gmToken, 20);
        var add = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/weapons", gmToken, new AddCreatureWeaponRequest(weaponItemId, null, null, null, null)));
        var weaponId = (await add.Content.ReadFromJsonAsync<CreatureWeaponResponse>())!.Id;

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/weapons/{weaponId}/durabilidade", gmToken, 999));

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/weapons", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureWeaponResponse>>();
        body!.Single(w => w.Id == weaponId).DurabilidadeAtual.Should().Be(20);
    }

    [Fact]
    public async Task UpdateWeaponDurability_on_a_manual_natural_attack_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm21", "creaturearsenal21@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var add = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/weapons", gmToken, new AddCreatureWeaponRequest(null, "Garras", "Cortante", "1D6", 2)));
        var weaponId = (await add.Content.ReadFromJsonAsync<CreatureWeaponResponse>())!.Id;

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/weapons/{weaponId}/durabilidade", gmToken, 5));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateShieldDurability_clamps_to_the_item_DurabilidadeMaxima()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm22", "creaturearsenal22@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var shieldItemId = await CreateEscudoItemAsync(gmToken, 10);
        var add = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/shields", gmToken, new AddCreatureShieldRequest(shieldItemId)));
        var shieldId = (await add.Content.ReadFromJsonAsync<CreatureShieldResponse>())!.Id;

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/shields/{shieldId}/durabilidade", gmToken, 999));

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/shields", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureShieldResponse>>();
        body!.Single(s => s.Id == shieldId).DurabilidadeAtual.Should().Be(10);
    }

    [Fact]
    public async Task UpdateArmorSlotDurability_clamps_to_the_item_DurabilidadeMaxima()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm23", "creaturearsenal23@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var armorItemId = await CreateArmaduraItemAsync(gmToken, 15);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/armor-slots/Capacete", gmToken, new UpdateCreatureArmorSlotRequest(armorItemId)));

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/armor-slots/Capacete/durabilidade", gmToken, 999));

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/armor-slots", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureArmorSlotResponse>>();
        body!.Single(a => a.Slot == "Capacete").DurabilidadeAtual.Should().Be(15);
    }

    [Fact]
    public async Task UpdateArmorSlotDurability_on_an_empty_slot_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureArsenalGm24", "creaturearsenal24@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/armor-slots/Capacete/durabilidade", gmToken, 5));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
