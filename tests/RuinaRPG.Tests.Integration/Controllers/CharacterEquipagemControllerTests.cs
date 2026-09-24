using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Items;
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

    // Copied verbatim from EquipmentKitsControllerTests.cs / HistoricosControllerTests.cs — the
    // established direct-DB way to grant the Rules Auditor role in a test (no grant endpoint exists).
    private async Task GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
    }

    // Copied verbatim from CharacterPossessionsControllerTests.cs's helper.
    private async Task<string> CreateArtefatoItemAsync(string gmToken, string nome, string tipoDeAlvo, string alvo, int valor)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Artefato", nome, 0.1m, 500, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, tipoDeAlvo, alvo, valor, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
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
    public async Task Choose_with_ChoiceSelections_omitted_from_the_request_body_returns_400_not_500()
    {
        // ChooseEquipmentKitRequest.ChoiceSelections is a non-nullable List<...> in the C# record,
        // but a hand-crafted JSON body omitting the field deserializes it to null. Without a
        // null-coalescing guard, BuildPlanAsync's foreach over choiceSlots would NRE (500) the first
        // time it tries selections.FirstOrDefault(...) for a kit that actually has a choice slot
        // (Patrulheiro) — it should fail cleanly with 400 instead.
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemGm7", "equipagem7@teste.com");
        var (_, sheetId) = await CreateCampaignAndSheetAsync(gmToken);
        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var patrulheiro = kits!.Single(k => k.Nome == "Patrulheiro");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/character-sheets/{sheetId}/equipagem/choose");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        request.Content = JsonContent.Create(new { KitId = patrulheiro.Id });
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Choose_a_kit_that_would_push_an_Artefato_TipoDeAlvo_past_3_returns_400_and_grants_nothing()
    {
        // Mirrors CharacterPossessionsController.AddArtifact's own "limite de 3 por TipoDeAlvo
        // validado na aplicação" cap (Requisitos - Modelo de Dados) — no path in the Equipagem
        // feature enforced this before this fix, even though nothing stops an Auditor from
        // authoring a kit with an Artefato fixed item via the Auditoria page's Tipo dropdown.
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemGm8", "equipagem8@teste.com");
        await GrantRulesAuditorAsync("equipagem8@teste.com");
        var (_, sheetId) = await CreateCampaignAndSheetAsync(gmToken);

        var artifact1 = await CreateArtefatoItemAsync(gmToken, "Anel Equipagem 1", "Atributo", "Vigor", 1);
        var artifact2 = await CreateArtefatoItemAsync(gmToken, "Anel Equipagem 2", "Atributo", "Forca", 1);
        var artifact3 = await CreateArtefatoItemAsync(gmToken, "Anel Equipagem 3", "Atributo", "Agilidade", 1);
        var artifact4Nome = "Anel Equipagem 4";
        await CreateArtefatoItemAsync(gmToken, artifact4Nome, "Atributo", "Astucia", 1);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", gmToken, new AddCharacterArtifactRequest(artifact1)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", gmToken, new AddCharacterArtifactRequest(artifact2)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", gmToken, new AddCharacterArtifactRequest(artifact3)));

        var kitRequest = new CreateEquipmentKitRequest("Kit com Artefato", "Kit de teste com Artefato", 0,
            [new EquipmentKitItemInput(artifact4Nome, "Artefato", 1, null)], []);
        var kitResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, kitRequest));
        var kit = await kitResponse.Content.ReadFromJsonAsync<EquipmentKitResponse>();

        var chooseResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/equipagem/choose", gmToken,
            new ChooseEquipmentKitRequest(kit!.Id, [])));

        chooseResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", gmToken));
        var sheet = await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        sheet!.EquipmentKitId.Should().BeNull(); // rejected before SaveChangesAsync — nothing partially applied

        var artifactsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/artifacts", gmToken));
        var artifacts = await artifactsResponse.Content.ReadFromJsonAsync<List<CharacterArtifactResponse>>();
        artifacts!.Should().HaveCount(3);
    }

    [Fact]
    public async Task ListKits_and_Choose_by_an_unrelated_jogador_return_403()
    {
        // Same CanEdit gate as every other sheet sub-controller (see CharacterMasteriesControllerTests's
        // identical Add_by_an_unrelated_jogador_returns_403) — a caller who is neither the sheet's
        // owner nor its campaign's GM cannot list kits or choose one.
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemGm9", "equipagem9@teste.com");
        var (_, sheetId) = await CreateCampaignAndSheetAsync(gmToken);
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "EquipagemStranger9", "equipagemstranger9@teste.com");

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/equipagem/kits", otherToken));
        listResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var chooseResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/equipagem/choose", otherToken,
            new ChooseEquipmentKitRequest(Guid.NewGuid().ToString(), [])));
        chooseResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

    [Fact]
    public async Task Choose_a_kit_with_an_Armadura_choice_slot_updates_the_target_armor_slot()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemArmorGm1", "equipagemarmor1@teste.com");
        await GrantRulesAuditorAsync("equipagemarmor1@teste.com"); // this file's own copy of the Auditor-grant helper, per Task 6/9's established pattern
        var (_, sheetId) = await CreateCampaignAndSheetAsync(gmToken);

        var meResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken));
        var gmId = (await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id;
        Guid armaduraId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            var armadura = new RuinaRPG.Infrastructure.Items.Armadura
            {
                Id = Guid.NewGuid(), GmId = Guid.Parse(gmId), Nome = "Armadura de Teste F", Subcategoria = "Equipamento inicial - Armadura - Leve - Couro",
                DurabilidadeMaxima = 8, Peso = 1, Preco = 0,
            };
            db.Add(armadura);
            await db.SaveChangesAsync();
            armaduraId = armadura.Id;
        }

        var kitResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken,
            new CreateEquipmentKitRequest("Kit com Armadura", "D", 0, [],
                [new EquipmentKitChoiceSlotInput("Armadura", "Armadura", ["Couro"], null, 1, null, null, null, "Superior")])));
        var kit = await kitResponse.Content.ReadFromJsonAsync<EquipmentKitResponse>();

        var chooseResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/equipagem/choose", gmToken,
            new ChooseEquipmentKitRequest(kit!.Id, [new ChoiceSlotSelectionRequest(kit.ChoiceSlots.Single().Id, armaduraId.ToString())])));
        chooseResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var slotsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/armor-slots", gmToken));
        var slots = await slotsResponse.Content.ReadFromJsonAsync<List<CharacterArmorSlotResponse>>();
        var superior = slots!.Single(s => s.Slot == "Superior");
        superior.Nome.Should().Be("Armadura de Teste F");
        superior.DurabilidadeAtual.Should().Be(8);
    }
}
