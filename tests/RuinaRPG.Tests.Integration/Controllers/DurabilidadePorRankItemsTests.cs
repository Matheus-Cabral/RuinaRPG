using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>
/// Durabilidade por Rank: an Arma/Armadura/Escudo's maximum durability is no longer typed — it's
/// resolved live from its Rank via the DurabilidadesPorRank table (seeded from the Tabela: F 20,
/// E 45, D 80, C 125, B 180, A 245, S/SS Inquebrável). Tests that edit the table restore it after,
/// since every Fact in this class shares one database.
/// </summary>
public class DurabilidadePorRankItemsTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public DurabilidadePorRankItemsTests(PostgresFixture postgres) => _postgres = postgres;

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
        var meResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", tokens!.AccessToken));
        return ((await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
    }

    private async Task<string> SetUpCharacterSheetAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }

    private static CreateItemRequest ItemRequest(string tipo, string nome, string? rank) => tipo switch
    {
        "Arma" => new CreateItemRequest("Arma", nome, 1.5m, 50, null, "Espadas", null, rank, "UmaMao", "2D6", 3, "19", 2, "Cortante",
            null, null, null, null, null, null, null, null, null),
        "Armadura" => new CreateItemRequest("Armadura", nome, 3m, 30, null, null, null, rank, null, null, null, null, null, null,
            "Medio", 5, 1, 1, null, null, null, null, null),
        "Escudo" => new CreateItemRequest("Escudo", nome, 2m, 25, null, null, null, rank, null, null, null, null, null, null,
            "Leve", null, null, null, 2, null, null, null, null),
        "ItemGeral" => new CreateItemRequest("ItemGeral", nome, 0.5m, 5, null, "Diversos", null, rank, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null),
        _ => throw new ArgumentException(tipo),
    };

    private async Task<HttpResponseMessage> PostItemAsync(string gmToken, CreateItemRequest request) =>
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken, request));

    private async Task<string> CreateItemAsync(string gmToken, string tipo, string? rank)
    {
        var response = await PostItemAsync(gmToken, ItemRequest(tipo, $"{tipo} {rank ?? "sem rank"}", rank));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    /// <summary>Edits one row of the table directly (the Auditoria API is a later task) and returns an undo.</summary>
    private async Task<Func<Task>> SetRankAsync(RankDeItem rank, int? durabilidade, bool inquebravel)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var row = await db.DurabilidadesPorRank.SingleAsync(d => d.Rank == rank);
        var (oldDurabilidade, oldInquebravel) = (row.Durabilidade, row.Inquebravel);
        row.Durabilidade = durabilidade;
        row.Inquebravel = inquebravel;
        await db.SaveChangesAsync();
        return async () =>
        {
            using var undoScope = _factory.Services.CreateScope();
            var undoDb = undoScope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var undoRow = await undoDb.DurabilidadesPorRank.SingleAsync(d => d.Rank == rank);
            undoRow.Durabilidade = oldDurabilidade;
            undoRow.Inquebravel = oldInquebravel;
            await undoDb.SaveChangesAsync();
        };
    }

    [Theory]
    [InlineData("Arma")]
    [InlineData("Armadura")]
    [InlineData("Escudo")]
    public async Task Create_with_Rank_D_resolves_DurabilidadeMaxima_80(string tipo)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"DurRankD{tipo}", $"durrankd{tipo.ToLowerInvariant()}@teste.com");

        var response = await PostItemAsync(gmToken, ItemRequest(tipo, $"{tipo} D", "D"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ItemResponse>();
        body!.Rank.Should().Be("D");
        body.DurabilidadeMaxima.Should().Be(80);
        body.Inquebravel.Should().BeFalse();
    }

    [Theory]
    [InlineData("Arma")]
    [InlineData("Armadura")]
    [InlineData("Escudo")]
    public async Task Create_with_Rank_SS_is_Inquebravel_with_no_DurabilidadeMaxima(string tipo)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"DurRankSS{tipo}", $"durrankss{tipo.ToLowerInvariant()}@teste.com");

        var response = await PostItemAsync(gmToken, ItemRequest(tipo, $"{tipo} SS", "SS"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ItemResponse>();
        body!.Rank.Should().Be("SS");
        body.DurabilidadeMaxima.Should().BeNull();
        body.Inquebravel.Should().BeTrue();
    }

    [Theory]
    [InlineData("Arma")]
    [InlineData("Armadura")]
    [InlineData("Escudo")]
    public async Task Create_without_Rank_has_no_durability(string tipo)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"DurRankNone{tipo}", $"durranknone{tipo.ToLowerInvariant()}@teste.com");

        var response = await PostItemAsync(gmToken, ItemRequest(tipo, $"{tipo} sem rank", null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ItemResponse>();
        body!.Rank.Should().BeNull();
        body.DurabilidadeMaxima.Should().BeNull();
        body.Inquebravel.Should().BeFalse();
    }

    [Theory]
    [InlineData("Arma")]
    [InlineData("Armadura")]
    [InlineData("Escudo")]
    public async Task Create_with_an_unknown_Rank_returns_400(string tipo)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"DurRankBad{tipo}", $"durrankbad{tipo.ToLowerInvariant()}@teste.com");

        var response = await PostItemAsync(gmToken, ItemRequest(tipo, $"{tipo} Z", "Z"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rank_on_an_ItemGeral_is_ignored()
    {
        // Matches how every Tipo-specific field on the shared request behaves for a Tipo it doesn't apply to.
        var gmToken = await RegisterGmAndGetTokenAsync("DurRankGeral", "durrankgeral@teste.com");

        var response = await PostItemAsync(gmToken, ItemRequest("ItemGeral", "Corda", "D"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ItemResponse>();
        body!.Rank.Should().BeNull();
        body.DurabilidadeMaxima.Should().BeNull();
    }

    [Fact]
    public async Task Update_changes_an_Armadura_Rank_and_the_list_shows_the_resolved_durability()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DurRankUpd", "durrankupd@teste.com");
        var itemId = await CreateItemAsync(gmToken, "Armadura", null);

        var update = new UpdateItemRequest("Armadura B", 3m, 30, null, null, null, "B", null, null, null, null, null, null,
            "Medio", 5, 1, 1, null, null, null, null, null);
        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/items/{itemId}", gmToken, update));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/items", gmToken))).Content.ReadFromJsonAsync<List<ItemResponse>>();
        var item = list!.Single(i => i.Id == itemId);
        item.Rank.Should().Be("B");
        item.DurabilidadeMaxima.Should().Be(180);
    }

    [Fact]
    public async Task Update_with_an_unknown_Rank_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DurRankUpdBad", "durrankupdbad@teste.com");
        var itemId = await CreateItemAsync(gmToken, "Escudo", "F");

        var update = new UpdateItemRequest("Escudo", 2m, 25, null, null, null, "Z", null, null, null, null, null, null,
            "Leve", null, null, null, 2, null, null, null, null);
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/items/{itemId}", gmToken, update));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Adding_a_Rank_C_weapon_to_a_character_sheet_starts_at_125()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DurRankChar1", "durrankchar1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "DurRankPlayer1", "durrankplayer1@teste.com");
        var sheetId = await SetUpCharacterSheetAsync(gmToken, playerId);
        var itemId = await CreateItemAsync(gmToken, "Arma", "C");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/weapons", playerToken, new AddCharacterWeaponRequest(itemId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CharacterWeaponResponse>();
        body!.Rank.Should().Be("C");
        body.DurabilidadeAtual.Should().Be(125);
        body.DurabilidadeMaxima.Should().Be(125);
        body.Inquebravel.Should().BeFalse();
    }

    [Fact]
    public async Task Lowering_a_Rank_caps_the_character_sheet_atual_on_read_and_on_save()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DurRankChar2", "durrankchar2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "DurRankPlayer2", "durrankplayer2@teste.com");
        var sheetId = await SetUpCharacterSheetAsync(gmToken, playerId);
        var itemId = await CreateItemAsync(gmToken, "Arma", "C");
        var add = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/weapons", playerToken, new AddCharacterWeaponRequest(itemId)));
        var weaponId = (await add.Content.ReadFromJsonAsync<CharacterWeaponResponse>())!.Id;

        var undo = await SetRankAsync(RankDeItem.C, 50, false);
        try
        {
            var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/weapons", playerToken)))
                .Content.ReadFromJsonAsync<List<CharacterWeaponResponse>>();
            var weapon = list!.Single(w => w.Id == weaponId);
            weapon.DurabilidadeAtual.Should().Be(50);
            weapon.DurabilidadeMaxima.Should().Be(50);

            var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/weapons/{weaponId}/durabilidade", playerToken, 120));
            put.StatusCode.Should().Be(HttpStatusCode.NoContent);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            (await db.CharacterWeapons.SingleAsync(w => w.Id == Guid.Parse(weaponId))).DurabilidadeAtual.Should().Be(50);
        }
        finally
        {
            await undo();
        }
    }

    [Fact]
    public async Task An_Inquebravel_Rank_shows_Inquebravel_on_the_character_sheet_and_stores_0()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DurRankChar3", "durrankchar3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "DurRankPlayer3", "durrankplayer3@teste.com");
        var sheetId = await SetUpCharacterSheetAsync(gmToken, playerId);
        var itemId = await CreateItemAsync(gmToken, "Arma", "C");
        var add = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/weapons", playerToken, new AddCharacterWeaponRequest(itemId)));
        var weaponId = (await add.Content.ReadFromJsonAsync<CharacterWeaponResponse>())!.Id;

        var undo = await SetRankAsync(RankDeItem.C, null, true);
        try
        {
            var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/weapons/{weaponId}/durabilidade", playerToken, 10));
            put.StatusCode.Should().Be(HttpStatusCode.NoContent);

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
                (await db.CharacterWeapons.SingleAsync(w => w.Id == Guid.Parse(weaponId))).DurabilidadeAtual.Should().Be(0);
            }

            var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/weapons", playerToken)))
                .Content.ReadFromJsonAsync<List<CharacterWeaponResponse>>();
            var weapon = list!.Single(w => w.Id == weaponId);
            weapon.Inquebravel.Should().BeTrue();
            weapon.DurabilidadeAtual.Should().Be(0);
        }
        finally
        {
            await undo();
        }
    }

    [Fact]
    public async Task Character_armor_slot_and_shield_resolve_their_max_from_the_Rank()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DurRankChar4", "durrankchar4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "DurRankPlayer4", "durrankplayer4@teste.com");
        var sheetId = await SetUpCharacterSheetAsync(gmToken, playerId);
        var armorId = await CreateItemAsync(gmToken, "Armadura", "E");
        var shieldId = await CreateItemAsync(gmToken, "Escudo", "S");

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/armor-slots/Capacete", playerToken, new UpdateCharacterArmorSlotRequest(armorId)));
        var addShield = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/shields", playerToken, new AddCharacterShieldRequest(shieldId)));

        var slots = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/armor-slots", playerToken)))
            .Content.ReadFromJsonAsync<List<CharacterArmorSlotResponse>>();
        var capacete = slots!.Single(s => s.Slot == "Capacete");
        capacete.DurabilidadeAtual.Should().Be(45);
        capacete.DurabilidadeMaxima.Should().Be(45);
        capacete.Inquebravel.Should().BeFalse();

        var shield = await addShield.Content.ReadFromJsonAsync<CharacterShieldResponse>();
        shield!.Inquebravel.Should().BeTrue();
        shield.DurabilidadeAtual.Should().Be(0);
        shield.DurabilidadeMaxima.Should().Be(0);
    }

    [Fact]
    public async Task Npc_arsenal_resolves_the_max_from_the_Rank_and_reports_Inquebravel()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DurRankNpc", "durranknpc@teste.com");
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        var sheetId = (await sheetResponse.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
        var weaponItemId = await CreateItemAsync(gmToken, "Arma", "A");
        var shieldItemId = await CreateItemAsync(gmToken, "Escudo", "SS");
        var armorItemId = await CreateItemAsync(gmToken, "Armadura", "SS");

        var weapon = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/weapons", gmToken, new AddNpcWeaponRequest(weaponItemId))))
            .Content.ReadFromJsonAsync<NpcWeaponResponse>();
        weapon!.Rank.Should().Be("A");
        weapon.DurabilidadeAtual.Should().Be(245);
        weapon.DurabilidadeMaxima.Should().Be(245);
        weapon.Inquebravel.Should().BeFalse();

        var shield = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/shields", gmToken, new AddNpcShieldRequest(shieldItemId))))
            .Content.ReadFromJsonAsync<NpcShieldResponse>();
        shield!.Inquebravel.Should().BeTrue();
        shield.DurabilidadeAtual.Should().Be(0);

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/armor-slots/Superior", gmToken, new UpdateNpcArmorSlotRequest(armorItemId)));
        var slots = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/armor-slots", gmToken)))
            .Content.ReadFromJsonAsync<List<NpcArmorSlotResponse>>();
        var superior = slots!.Single(s => s.Slot == "Superior");
        superior.Inquebravel.Should().BeTrue();
        superior.DurabilidadeMaxima.Should().BeNull();
        superior.DurabilidadeAtual.Should().Be(0);
    }

    [Fact]
    public async Task Creature_arsenal_resolves_the_max_from_the_Rank_and_reports_Inquebravel()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DurRankCreature", "durrankcreature@teste.com");
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        var sheetId = (await sheetResponse.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
        var weaponItemId = await CreateItemAsync(gmToken, "Arma", "B");
        var shieldItemId = await CreateItemAsync(gmToken, "Escudo", "S");

        var weapon = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/weapons", gmToken,
                new AddCreatureWeaponRequest(weaponItemId, null, null, null, null))))
            .Content.ReadFromJsonAsync<CreatureWeaponResponse>();
        weapon!.Rank.Should().Be("B");
        weapon.DurabilidadeAtual.Should().Be(180);
        weapon.DurabilidadeMaximo.Should().Be(180);
        weapon.Inquebravel.Should().BeFalse();

        var shield = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/shields", gmToken, new AddCreatureShieldRequest(shieldItemId))))
            .Content.ReadFromJsonAsync<CreatureShieldResponse>();
        shield!.Inquebravel.Should().BeTrue();
        shield.DurabilidadeAtual.Should().Be(0);
    }
}
