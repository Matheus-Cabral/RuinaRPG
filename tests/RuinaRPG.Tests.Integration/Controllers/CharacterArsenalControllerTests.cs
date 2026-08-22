using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Items;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterArsenalControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CharacterArsenalControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> CreateArmaItemAsync(string gmToken, int durabilidadeMaxima)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Arma", "Espada", 1.5m, 50, null, "Espadas", null, "F", "UmaMao", "2D6", 3, "19", 2, "Cortante", null, durabilidadeMaxima, null, null, null, null, null, null, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    private async Task<string> CreateArmaduraItemAsync(string gmToken, int durabilidadeMaxima)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Armadura", "Elmo de Ferro", 3m, 30, null, null, null, null, null, null, null, null, null, null, null, durabilidadeMaxima, "Medio", 5, 1, 1, "-1 Furtividade", 2, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    private async Task<string> CreateEscudoItemAsync(string gmToken, int durabilidadeMaxima)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Escudo", "Broquel", 2m, 25, null, null, null, null, null, null, null, null, null, null, null, durabilidadeMaxima, "Leve", null, null, null, "-1 Agilidade", 1, 2, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    [Fact]
    public async Task AddWeapon_links_the_item_and_initializes_durability_from_its_maximo()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArsenalGm1", "arsenal1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "ArsenalPlayer1", "arsenalplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var weaponItemId = await CreateArmaItemAsync(gmToken, 20);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/weapons", playerToken,
            new AddCharacterWeaponRequest(weaponItemId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CharacterWeaponResponse>();
        body!.ItemId.Should().Be(weaponItemId);
        body.Nome.Should().Be("Espada");
        body.Dano.Should().Be(3);
        body.DurabilidadeAtual.Should().Be(20);
        body.DurabilidadeMaxima.Should().Be(20);
    }

    [Fact]
    public async Task Equipping_a_second_weapon_unequips_the_first_by_default()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArsenalGm2", "arsenal2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "ArsenalPlayer2", "arsenalplayer2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var weaponItemId1 = await CreateArmaItemAsync(gmToken, 20);
        var weaponItemId2 = await CreateArmaItemAsync(gmToken, 15);

        var add1 = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/weapons", playerToken, new AddCharacterWeaponRequest(weaponItemId1)));
        var weapon1Id = (await add1.Content.ReadFromJsonAsync<CharacterWeaponResponse>())!.Id;
        var add2 = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/weapons", playerToken, new AddCharacterWeaponRequest(weaponItemId2)));
        var weapon2Id = (await add2.Content.ReadFromJsonAsync<CharacterWeaponResponse>())!.Id;

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/weapons/{weapon1Id}", playerToken, true));
        var equipSecond = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/weapons/{weapon2Id}", playerToken, true));
        equipSecond.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/weapons", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterWeaponResponse>>();
        body!.Single(w => w.Id == weapon1Id).IsEquipped.Should().BeFalse();
        body!.Single(w => w.Id == weapon2Id).IsEquipped.Should().BeTrue();
    }

    [Fact]
    public async Task ArmorSlots_list_returns_all_3_slots_even_when_empty()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArsenalGm3", "arsenal3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "ArsenalPlayer3", "arsenalplayer3@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/armor-slots", playerToken));

        var body = await response.Content.ReadFromJsonAsync<List<CharacterArmorSlotResponse>>();
        body!.Should().HaveCount(3);
        body!.Select(s => s.Slot).Should().BeEquivalentTo(new[] { "Capacete", "Superior", "Inferior" });
        body!.Should().OnlyContain(s => s.ItemId == null);
    }

    [Fact]
    public async Task Update_an_armor_slot_links_the_item_and_initializes_durability()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArsenalGm4", "arsenal4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "ArsenalPlayer4", "arsenalplayer4@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var armorItemId = await CreateArmaduraItemAsync(gmToken, 12);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/armor-slots/Capacete", playerToken,
            new UpdateCharacterArmorSlotRequest(armorItemId)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/armor-slots", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterArmorSlotResponse>>();
        var slot = body!.Single(s => s.Slot == "Capacete");
        slot.ItemId.Should().Be(armorItemId);
        slot.Nome.Should().Be("Elmo de Ferro");
        slot.DurabilidadeAtual.Should().Be(12);
        slot.DurabilidadeMaxima.Should().Be(12);
    }

    [Fact]
    public async Task AddShield_and_list_returns_it_with_live_catalog_fields()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArsenalGm5", "arsenal5@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "ArsenalPlayer5", "arsenalplayer5@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var shieldItemId = await CreateEscudoItemAsync(gmToken, 10);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/shields", playerToken,
            new AddCharacterShieldRequest(shieldItemId)));
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/shields", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterShieldResponse>>();
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
        var gmToken = await RegisterGmAndGetTokenAsync("ArsenalGm7", "arsenal7@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "ArsenalPlayer7", "arsenalplayer7@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var weaponItemId = await CreateArmaItemAsync(gmToken, 20);
        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/weapons", playerToken, new AddCharacterWeaponRequest(weaponItemId)));
        var weaponId = (await addResponse.Content.ReadFromJsonAsync<CharacterWeaponResponse>())!.Id;

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/weapons/{weaponId}", playerToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/weapons", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterWeaponResponse>>();
        body!.Should().NotContain(w => w.Id == weaponId);
    }

    [Fact]
    public async Task Unlinking_an_armor_slot_clears_its_item_and_durability()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArsenalGm8", "arsenal8@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "ArsenalPlayer8", "arsenalplayer8@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var armorItemId = await CreateArmaduraItemAsync(gmToken, 12);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/armor-slots/Capacete", playerToken, new UpdateCharacterArmorSlotRequest(armorItemId)));

        var unlinkResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/armor-slots/Capacete", playerToken, new UpdateCharacterArmorSlotRequest(null)));
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/armor-slots", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterArmorSlotResponse>>();
        var slot = body!.Single(s => s.Slot == "Capacete");
        slot.ItemId.Should().BeNull();
        slot.DurabilidadeAtual.Should().BeNull();
    }

    [Fact]
    public async Task Delete_an_existing_shield_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArsenalGm9", "arsenal9@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "ArsenalPlayer9", "arsenalplayer9@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var shieldItemId = await CreateEscudoItemAsync(gmToken, 10);
        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/shields", playerToken, new AddCharacterShieldRequest(shieldItemId)));
        var shieldId = (await addResponse.Content.ReadFromJsonAsync<CharacterShieldResponse>())!.Id;

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/shields/{shieldId}", playerToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/shields", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterShieldResponse>>();
        body!.Should().NotContain(s => s.Id == shieldId);
    }

    [Fact]
    public async Task Weapon_actions_by_an_unrelated_jogador_return_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArsenalGm6", "arsenal6@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "ArsenalPlayer6", "arsenalplayer6@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "ArsenalPlayer6b", "arsenalplayer6b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var weaponItemId = await CreateArmaItemAsync(gmToken, 20);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/weapons", otherToken,
            new AddCharacterWeaponRequest(weaponItemId)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
