using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterEquipagemControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CharacterEquipagemControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    // Copied verbatim from CharacterSheetsControllerTests.cs's established helpers —
    // CharacterSheetsController.Create requires OwnerId to be a real CampaignMember.
    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meBody = await (await _client.SendAsync(me)).Content.ReadFromJsonAsync<MeResponse>();
        return (meBody!.Id, tokens.AccessToken);
    }

    private async Task<(string CampaignId, string SheetId)> CreateCampaignAndSheetAsync(string gmToken)
    {
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, $"EquipagemPlayer{Guid.NewGuid():N}", $"{Guid.NewGuid():N}@teste.com");
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaign = await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>();
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaign!.Id}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaign.Id}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        var sheet = await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        return (campaign.Id, sheet!.Id);
    }

    [Fact]
    public async Task ListKits_returns_every_seeded_kit_with_choice_slot_options_resolved_against_the_GM_s_catalog()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemGm1", "equipagem1@teste.com");
        var (_, sheetId) = await CreateCampaignAndSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/equipagem/kits", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var kits = await response.Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        kits!.Should().HaveCountGreaterOrEqualTo(12);
        var patrulheiro = kits!.Single(k => k.Nome == "Patrulheiro");
        patrulheiro.ChoiceSlots.Should().ContainSingle();
        patrulheiro.ChoiceSlots.Single().Options.Should().NotBeEmpty(); // Varinha de Carvalho and every default Tier-F weapon are seeded per-GM already
    }

    [Fact]
    public async Task Choose_a_kit_with_no_choice_slots_places_every_fixed_item_and_adds_Ciclos()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemGm2", "equipagem2@teste.com");
        var (_, sheetId) = await CreateCampaignAndSheetAsync(gmToken);
        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var viajante = kits!.Single(k => k.Nome == "Viajante");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/equipagem/choose", gmToken,
            new ChooseEquipmentKitRequest(viajante.Id, [])));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", gmToken));
        var sheet = await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        sheet!.EquipmentKitId.Should().Be(viajante.Id);
        sheet.Ciclos.Should().Be(25);

        var inventoryResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/inventory", gmToken));
        var inventory = await inventoryResponse.Content.ReadFromJsonAsync<List<CharacterInventoryItemResponse>>();
        inventory!.Should().Contain(i => i.Nome == "Mochila");
        inventory.Should().Contain(i => i.Nome == "Ração de Viagem" && i.Qtd == 2);
    }

    [Fact]
    public async Task Choose_a_kit_with_a_choice_slot_places_the_selected_weapon()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemGm3", "equipagem3@teste.com");
        var (_, sheetId) = await CreateCampaignAndSheetAsync(gmToken);
        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var patrulheiro = kits!.Single(k => k.Nome == "Patrulheiro");
        var chosenWeapon = patrulheiro.ChoiceSlots.Single().Options.First();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/equipagem/choose", gmToken,
            new ChooseEquipmentKitRequest(patrulheiro.Id, [new ChoiceSlotSelectionRequest(patrulheiro.ChoiceSlots.Single().SlotId, chosenWeapon.ItemId)])));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var weaponsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/weapons", gmToken));
        var weapons = await weaponsResponse.Content.ReadFromJsonAsync<List<CharacterWeaponResponse>>();
        weapons!.Should().ContainSingle(w => w.Nome == chosenWeapon.Nome);

        var shieldsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/shields", gmToken));
        var shields = await shieldsResponse.Content.ReadFromJsonAsync<List<CharacterShieldResponse>>();
        shields!.Should().ContainSingle(s => s.Nome == "Tampa de Madeira");
    }

    [Fact]
    public async Task Choose_Cacador_with_the_Arco_alternative_also_grants_10_Flechas_de_Madeira()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemGm4", "equipagem4@teste.com");
        var (_, sheetId) = await CreateCampaignAndSheetAsync(gmToken);

        // DefaultCatalogItems.cs's Tier-F rows for "Arcos"/"Fundas e Baladeiras" are empty (its lowest
        // bow is Arco Curto at Tier E) — this test can't rely on incidental default-catalog data for
        // Caçador's choice slot, so it seeds its own Tier-F "Arcos" weapon directly, the same way a GM
        // would need to before this kit's choice slot has any real option.
        var meResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken));
        var gmId = (await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id;
        Guid testBowId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            var bow = new RuinaRPG.Infrastructure.Items.Arma
            {
                Id = Guid.NewGuid(), GmId = Guid.Parse(gmId), Nome = "Arco de Teste F", Subcategoria = "Arcos",
                Tier = RuinaRPG.Domain.Items.Tier.F, Peso = 1, Preco = 0,
            };
            db.Add(bow);
            await db.SaveChangesAsync();
            testBowId = bow.Id;
        }

        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var cacador = kits!.Single(k => k.Nome == "Caçador");
        var slot = cacador.ChoiceSlots.Single();
        var chosen = slot.Options.Single(o => o.ItemId == testBowId.ToString());

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/equipagem/choose", gmToken,
            new ChooseEquipmentKitRequest(cacador.Id, [new ChoiceSlotSelectionRequest(slot.SlotId, chosen.ItemId)])));

        var inventoryResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/inventory", gmToken));
        var inventory = await inventoryResponse.Content.ReadFromJsonAsync<List<CharacterInventoryItemResponse>>();
        inventory!.Should().Contain(i => i.Nome == "Flecha de Madeira" && i.Qtd == 10);
    }

    [Fact]
    public async Task Choose_a_second_time_on_an_already_chosen_sheet_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemGm5", "equipagem5@teste.com");
        var (_, sheetId) = await CreateCampaignAndSheetAsync(gmToken);
        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var negociante = kits!.Single(k => k.Nome == "Negociante");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/equipagem/choose", gmToken, new ChooseEquipmentKitRequest(negociante.Id, [])));

        var second = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/equipagem/choose", gmToken, new ChooseEquipmentKitRequest(negociante.Id, [])));

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Choose_auto_attaches_every_granted_item_to_the_sheet_s_campaign_as_public()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemGm6", "equipagem6@teste.com");
        var (campaignId, sheetId) = await CreateCampaignAndSheetAsync(gmToken);
        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var viajante = kits!.Single(k => k.Nome == "Viajante");

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/equipagem/choose", gmToken, new ChooseEquipmentKitRequest(viajante.Id, [])));

        var meResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken));
        var gmId = Guid.Parse((await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        // Scoped by GmId — ItemGeral "Mochila" is per-GM (Equipagem's fixed-item resolution creates
        // one independently for every GM that applies a kit containing it), so an unscoped Nome-only
        // lookup would collide with another test in this class that also grants a "Mochila" (e.g.
        // Choose_a_kit_with_no_choice_slots_places_every_fixed_item_and_adds_Ciclos above), since all
        // Facts in this class share one Postgres database via PostgresFixture.
        var mochila = await db.Set<RuinaRPG.Infrastructure.Items.ItemGeral>().SingleAsync(i => i.Nome == "Mochila" && i.GmId == gmId);
        var attachment = await db.CampaignAttachments.SingleAsync(a => a.CampaignId == Guid.Parse(campaignId) && a.ItemId == mochila.Id);
        attachment.IsPublic.Should().BeTrue();
    }
}
