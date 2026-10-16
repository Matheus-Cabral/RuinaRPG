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

public class EquipmentKitsControllerTests(ApiFixture fixture) : ApiTestBase(fixture)
{

    private async Task<string> CreateCharacterSheetAsync(string gmToken, string playerId, string campaignId)
    {
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await response.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }

    private static CreateEquipmentKitRequest ValidCreate(string fixedItemId) => new(
        "Kit de Teste", "Descrição de teste", 10,
        [new EquipmentKitItemInput(fixedItemId, 1)],
        [new EquipmentKitChoiceSlotInput("Arma", "Arma", null, "F", 1, null, null, null, null)]);

    private static CreateItemRequest FixoItemGeral(string nome) =>
        new("ItemGeral", nome, 0.5m, 5, null, "Equipamentos de Aventura", null,
            null, null, null, null, null, null, null,
            null, null, null, null,
            null, null, null, null, null);

    private static CreateItemRequest FixoArmadura(string nome) =>
        new("Armadura", nome, 8m, 100, null, null, null,
            "D", null, null, null, null, null, null,
            "Leve", 5, 2, 1,
            null, null, null, null, null);

    private static CreateItemRequest FixoArma(string nome) =>
        new("Arma", nome, 1m, 10, null, "Espadas", null,
            "F", "UmaMao", "2D6", 3, "19", 2, "Cortante",
            null, null, null, null,
            null, null, null, null, null);

    // Nomes únicos: os testes da classe compartilham um único banco.
    private static string Unico(string prefixo) => $"{prefixo} {Guid.NewGuid():N}";

    private async Task<EquipmentKitFixedItemResponse> CriarFixoAsync(string auditorToken, CreateItemRequest request)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kit-fixed-items", auditorToken, request));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EquipmentKitFixedItemResponse>())!;
    }

    private async Task<CreateEquipmentKitRequest> ValidCreateAsync(string auditorToken) =>
        ValidCreate((await CriarFixoAsync(auditorToken, FixoItemGeral(Unico("Mochila")))).Id);

    [Fact]
    public async Task List_is_open_to_any_authenticated_caller_and_returns_the_12_seeded_kits()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm1", "equipkits1@teste.com");
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/equipment-kits", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var kits = await response.Content.ReadFromJsonAsync<List<EquipmentKitResponse>>();
        kits!.Should().HaveCountGreaterOrEqualTo(12);
        kits.Should().Contain(k => k.Nome == "Viajante");
    }

    [Fact]
    public async Task Create_by_a_non_Auditor_GM_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm2", "equipkits2@teste.com");
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, ValidCreate(Guid.NewGuid().ToString())));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_rejects_a_choice_slot_of_Tipo_ItemGeral()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm3b", "equipkits3b@teste.com");
        await GrantRulesAuditorAsync("equipkits3b@teste.com");

        // EquipmentKitGrantService now dispatches choice slots to the matching Arma/Armadura/
        // Escudo/Artefato subtype, but ItemGeral has no such subtype to resolve options from, so
        // it's the one ItemTipo value that stays rejected here.
        var invalid = (await ValidCreateAsync(gmToken)) with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Item Geral", "ItemGeral", null, "F", 1, null, null, null, null)] };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_rejects_a_choice_slot_whose_Tipo_is_a_numeric_string()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm3c", "equipkits3c@teste.com");
        await GrantRulesAuditorAsync("equipkits3c@teste.com");

        // Enum.TryParse is lenient about numeric strings ("99") — the controller must reject them
        // via an exact-name (ordinal, case-sensitive) check rather than accepting any int cast to
        // a valid-looking ItemTipo.
        var invalid = (await ValidCreateAsync(gmToken)) with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Numérico", "99", null, "F", 1, null, null, null, null)] };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_rejects_a_choice_slot_whose_ArmorSlot_is_a_numeric_string()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm3d", "equipkits3d@teste.com");
        await GrantRulesAuditorAsync("equipkits3d@teste.com");

        var invalid = (await ValidCreateAsync(gmToken)) with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Armadura", "Armadura", null, null, 1, null, null, null, "99")] };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_accepts_a_choice_slot_of_Tipo_Armadura_with_an_ArmorSlot()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsArmorGm1", "equipkitsarmor1@teste.com");
        await GrantRulesAuditorAsync("equipkitsarmor1@teste.com");

        var request = (await ValidCreateAsync(gmToken)) with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Armadura", "Armadura", null, null, 1, null, null, null, "Superior")] };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, request));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var kit = await response.Content.ReadFromJsonAsync<EquipmentKitResponse>();
        kit!.ChoiceSlots.Should().ContainSingle(s => s.Tipo == "Armadura" && s.ArmorSlot == "Superior");
    }

    [Fact]
    public async Task Create_rejects_a_choice_slot_of_Tipo_Armadura_without_an_ArmorSlot()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsArmorGm2", "equipkitsarmor2@teste.com");
        await GrantRulesAuditorAsync("equipkitsarmor2@teste.com");

        var request = (await ValidCreateAsync(gmToken)) with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Armadura", "Armadura", null, null, 1, null, null, null, null)] };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, request));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_rejects_ArmorSlot_on_a_choice_slot_whose_Tipo_is_not_Armadura()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsArmorGm3", "equipkitsarmor3@teste.com");
        await GrantRulesAuditorAsync("equipkitsarmor3@teste.com");

        var request = (await ValidCreateAsync(gmToken)) with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Arma", "Arma", null, null, 1, null, null, null, "Superior")] };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, request));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // On Choose (EquipmentKitGrantService.BuildPlanAsync), a chosen Armadura overwrites whichever
    // ArmorSlot its choice slot targets — two Armadura choice slots aimed at the same ArmorSlot in
    // one kit would mean the second grant silently clobbers the first's write, so it's rejected at
    // authoring time instead.
    [Fact]
    public async Task Create_rejects_two_Armadura_choice_slots_targeting_the_same_ArmorSlot()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsArmorGm5", "equipkitsarmor5@teste.com");
        await GrantRulesAuditorAsync("equipkitsarmor5@teste.com");

        var request = (await ValidCreateAsync(gmToken)) with
        {
            ChoiceSlots =
            [
                new EquipmentKitChoiceSlotInput("Capacete 1", "Armadura", null, null, 1, null, null, null, "Capacete"),
                new EquipmentKitChoiceSlotInput("Capacete 2", "Armadura", null, null, 1, null, null, null, "Capacete"),
            ]
        };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, request));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_accepts_two_Armadura_choice_slots_targeting_different_ArmorSlots()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsArmorGm6", "equipkitsarmor6@teste.com");
        await GrantRulesAuditorAsync("equipkitsarmor6@teste.com");

        var request = (await ValidCreateAsync(gmToken)) with
        {
            ChoiceSlots =
            [
                new EquipmentKitChoiceSlotInput("Capacete", "Armadura", null, null, 1, null, null, null, "Capacete"),
                new EquipmentKitChoiceSlotInput("Superior", "Armadura", null, null, 1, null, null, null, "Superior"),
            ]
        };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, request));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // Tier only ever means anything for an Arma choice slot (EquipmentKitGrantService.
    // ResolveEligibleOptionsAsync only applies the Tier filter in the Arma branch) — a non-empty
    // Tier on an Armadura/Escudo/Artefato slot would be silently ignored at resolve time, so it's
    // rejected up front instead.
    [Fact]
    public async Task Create_rejects_a_non_empty_Tier_on_a_non_Arma_choice_slot()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsTierGm1", "equipkitstier1@teste.com");
        await GrantRulesAuditorAsync("equipkitstier1@teste.com");

        var request = (await ValidCreateAsync(gmToken)) with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Escudo", "Escudo", null, "F", 1, null, null, null, null)] };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, request));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("3")]
    [InlineData("99")]
    [InlineData("f")]
    public async Task Create_rejects_a_Rank_that_is_not_an_exact_RankDeItem_name(string rank)
    {
        var email = $"equipkitsrank{rank.ToLowerInvariant()}@teste.com";
        var gmToken = await RegisterGmAndGetTokenAsync($"EquipKitsRankGm{rank}", email);
        await GrantRulesAuditorAsync(email);

        var request = (await ValidCreateAsync(gmToken)) with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Arma", "Arma", null, rank, 1, null, null, null, null)] };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, request));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Rank inválido");
    }

    [Fact]
    public async Task Create_accepts_a_null_Tier_on_a_non_Arma_choice_slot()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsTierGm2", "equipkitstier2@teste.com");
        await GrantRulesAuditorAsync("equipkitstier2@teste.com");

        var request = (await ValidCreateAsync(gmToken)) with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Escudo", "Escudo", null, null, 1, null, null, null, null)] };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, request));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_accepts_choice_slots_of_Tipo_Escudo_and_Artefato()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsArmorGm4", "equipkitsarmor4@teste.com");
        await GrantRulesAuditorAsync("equipkitsarmor4@teste.com");

        var request = (await ValidCreateAsync(gmToken)) with
        {
            ChoiceSlots =
            [
                new EquipmentKitChoiceSlotInput("Escudo", "Escudo", null, null, 1, null, null, null, null),
                new EquipmentKitChoiceSlotInput("Artefato", "Artefato", null, null, 1, null, null, null, null),
            ]
        };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, request));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_by_an_Auditor_persists_items_and_choice_slots()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm4", "equipkits4@teste.com");
        await GrantRulesAuditorAsync("equipkits4@teste.com");

        var fixo = await CriarFixoAsync(gmToken, FixoItemGeral(Unico("Mochila")));
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, ValidCreate(fixo.Id)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var kit = await response.Content.ReadFromJsonAsync<EquipmentKitResponse>();
        kit!.Items.Should().ContainSingle(i => i.Nome == fixo.Nome && i.FixedItemId == fixo.Id);
        kit.ChoiceSlots.Should().ContainSingle(s => s.Label == "Arma" && s.Rank == "F");
    }

    [Fact]
    public async Task Update_replaces_the_kit_s_items_and_choice_slots_wholesale()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm5", "equipkits5@teste.com");
        await GrantRulesAuditorAsync("equipkits5@teste.com");
        var created = await (await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, await ValidCreateAsync(gmToken))))
            .Content.ReadFromJsonAsync<EquipmentKitResponse>();

        var corda = await CriarFixoAsync(gmToken, FixoItemGeral(Unico("Corda")));
        var updated = ValidCreate(corda.Id) with { Items = [new EquipmentKitItemInput(corda.Id, 2)], ChoiceSlots = [] };
        var putResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/equipment-kits/{created!.Id}", gmToken, updated));
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/equipment-kits", gmToken));
        var kit = (await listResponse.Content.ReadFromJsonAsync<List<EquipmentKitResponse>>())!.Single(k => k.Id == created.Id);
        kit.Items.Should().ContainSingle(i => i.Nome == corda.Nome && i.Qtd == 2);
        kit.ChoiceSlots.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_a_kit_already_chosen_by_a_CharacterSheet_returns_409()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm6", "equipkits6@teste.com");
        await GrantRulesAuditorAsync("equipkits6@teste.com");
        var created = await (await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, await ValidCreateAsync(gmToken))))
            .Content.ReadFromJsonAsync<EquipmentKitResponse>();

        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "EquipKitsPlayer6", "equipkitsplayer6@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha de Teste");
        var sheetId = await CreateCharacterSheetAsync(gmToken, playerId, campaignId);

        // Directly through the DB, to isolate this test from the full choose-flow this same plan builds in Task 9.
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            var s = await db.CharacterSheets.FindAsync(Guid.Parse(sheetId));
            s!.EquipmentKitId = Guid.Parse(created!.Id);
            await db.SaveChangesAsync();
        }

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/equipment-kits/{created.Id}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_kit_item_with_an_unknown_FixedItemId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsFixedGm1", "equipkitsfixed1@teste.com");
        await GrantRulesAuditorAsync("equipkitsfixed1@teste.com");

        foreach (var fixedItemId in new[] { Guid.NewGuid().ToString(), "nao-e-um-guid", "" })
        {
            var request = ValidCreate(fixedItemId);
            var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, request));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("Item fixo não encontrado na base de itens fixos.");
        }
    }

    [Fact]
    public async Task An_armadura_fixed_item_needs_an_ArmorSlot_and_other_types_must_not_have_one()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsFixedGm2", "equipkitsfixed2@teste.com");
        await GrantRulesAuditorAsync("equipkitsfixed2@teste.com");
        var armadura = await CriarFixoAsync(gmToken, FixoArmadura(Unico("Gibao")));
        var mochila = await CriarFixoAsync(gmToken, FixoItemGeral(Unico("Mochila")));
        var baseRequest = ValidCreate(mochila.Id) with { ChoiceSlots = [] };

        async Task<HttpResponseMessage> Post(CreateEquipmentKitRequest r) =>
            await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, r));

        var semSlot = await Post(baseRequest with { Items = [new EquipmentKitItemInput(armadura.Id, 1)] });
        semSlot.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await semSlot.Content.ReadAsStringAsync()).Should().Contain("precisa de um ArmorSlot válido");

        var slotInvalido = await Post(baseRequest with { Items = [new EquipmentKitItemInput(armadura.Id, 1, "99")] });
        slotInvalido.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var slotEmOutroTipo = await Post(baseRequest with { Items = [new EquipmentKitItemInput(mochila.Id, 1, "Superior")] });
        slotEmOutroTipo.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await slotEmOutroTipo.Content.ReadAsStringAsync()).Should().Contain("ArmorSlot só é aplicável");

        var ok = await Post(baseRequest with { Items = [new EquipmentKitItemInput(armadura.Id, 1, "Superior")] });
        ok.StatusCode.Should().Be(HttpStatusCode.Created);
        var kit = await ok.Content.ReadFromJsonAsync<EquipmentKitResponse>();
        kit!.Items.Should().ContainSingle(i => i.Tipo == "Armadura" && i.ArmorSlot == "Superior");
    }

    [Fact]
    public async Task Two_armadura_entries_of_a_kit_cannot_target_the_same_ArmorSlot()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsFixedGm3", "equipkitsfixed3@teste.com");
        await GrantRulesAuditorAsync("equipkitsfixed3@teste.com");
        var armadura1 = await CriarFixoAsync(gmToken, FixoArmadura(Unico("Gibao 1")));
        var armadura2 = await CriarFixoAsync(gmToken, FixoArmadura(Unico("Gibao 2")));
        var baseRequest = ValidCreate(armadura1.Id) with { ChoiceSlots = [] };

        async Task<HttpResponseMessage> Post(CreateEquipmentKitRequest r) =>
            await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, r));

        var doisFixos = await Post(baseRequest with { Items = [new EquipmentKitItemInput(armadura1.Id, 1, "Superior"), new EquipmentKitItemInput(armadura2.Id, 1, "Superior")] });
        doisFixos.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var fixoMaisSlot = await Post(baseRequest with
        {
            Items = [new EquipmentKitItemInput(armadura1.Id, 1, "Superior")],
            ChoiceSlots = [new EquipmentKitChoiceSlotInput("Armadura", "Armadura", null, null, 1, null, null, null, "Superior")],
        });
        fixoMaisSlot.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var slotsDiferentes = await Post(baseRequest with
        {
            Items = [new EquipmentKitItemInput(armadura1.Id, 1, "Superior")],
            ChoiceSlots = [new EquipmentKitChoiceSlotInput("Armadura", "Armadura", null, null, 1, null, null, null, "Capacete")],
        });
        slotsDiferentes.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_bonus_must_be_an_item_geral_fixed_item()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsFixedGm4", "equipkitsfixed4@teste.com");
        await GrantRulesAuditorAsync("equipkitsfixed4@teste.com");
        var flecha = await CriarFixoAsync(gmToken, FixoItemGeral(Unico("Flecha")));
        var espada = await CriarFixoAsync(gmToken, FixoArma(Unico("Espada")));
        var baseRequest = await ValidCreateAsync(gmToken);

        async Task<HttpResponseMessage> Post(string? subcategoria, string? bonusFixedItemId, int? bonusQtd) =>
            await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, baseRequest with
            {
                ChoiceSlots = [new EquipmentKitChoiceSlotInput("Arma à distância", "Arma", ["Arcos"], "F", 1, subcategoria, bonusFixedItemId, bonusQtd, null)],
            }));

        var bonusArma = await Post("Arcos", espada.Id, 1);
        bonusArma.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await bonusArma.Content.ReadAsStringAsync()).Should().Contain("O bônus de um slot precisa ser um item fixo do tipo Item Geral.");

        (await Post("Arcos", Guid.NewGuid().ToString(), 1)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Post("Arcos", null, null)).StatusCode.Should().Be(HttpStatusCode.BadRequest); // só a subcategoria
        (await Post(null, flecha.Id, 1)).StatusCode.Should().Be(HttpStatusCode.BadRequest); // só o item
        (await Post("Arcos", flecha.Id, 0)).StatusCode.Should().Be(HttpStatusCode.BadRequest); // BonusQtd < 1

        var ok = await Post("Arcos", flecha.Id, 10);
        ok.StatusCode.Should().Be(HttpStatusCode.Created);
        var slot = (await ok.Content.ReadFromJsonAsync<EquipmentKitResponse>())!.ChoiceSlots.Single();
        slot.BonusFixedItemId.Should().Be(flecha.Id);
        slot.BonusNome.Should().Be(flecha.Nome);
        slot.BonusQtd.Should().Be(10);
    }

    [Fact]
    public async Task Updating_a_kit_can_replace_a_rows_fixed_item_and_change_its_qtd()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsFixedGm5", "equipkitsfixed5@teste.com");
        await GrantRulesAuditorAsync("equipkitsfixed5@teste.com");
        var antes = await CriarFixoAsync(gmToken, FixoItemGeral(Unico("Tocha")));
        var depois = await CriarFixoAsync(gmToken, FixoItemGeral(Unico("Lampiao")));
        var created = await (await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken,
            ValidCreate(antes.Id) with { ChoiceSlots = [] }))).Content.ReadFromJsonAsync<EquipmentKitResponse>();

        var put = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/equipment-kits/{created!.Id}", gmToken,
            new UpdateEquipmentKitRequest("Kit de Teste", "Descrição de teste", 10, [new EquipmentKitItemInput(depois.Id, 4)], [])));
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var kit = (await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/equipment-kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitResponse>>())!.Single(k => k.Id == created.Id);
        var linha = kit.Items.Should().ContainSingle().Subject;
        linha.FixedItemId.Should().Be(depois.Id);
        linha.Nome.Should().Be(depois.Nome);
        linha.Qtd.Should().Be(4);

        var invalido = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/equipment-kits/{created.Id}", gmToken,
            new UpdateEquipmentKitRequest("Kit de Teste", "Descrição de teste", 10, [new EquipmentKitItemInput(depois.Id, 0)], [])));
        invalido.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_response_carries_the_fixed_items_name_type_and_incomplete_flag()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsFixedGm6", "equipkitsfixed6@teste.com");
        await GrantRulesAuditorAsync("equipkitsfixed6@teste.com");
        var completo = await CriarFixoAsync(gmToken, FixoArma(Unico("Espada")));
        var incompletoNome = Unico("Incompleto");
        Guid incompletoId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            var fixo = new RuinaRPG.Infrastructure.Rules.EquipmentKitFixedItem
            {
                Id = Guid.NewGuid(), Nome = incompletoNome, Tipo = RuinaRPG.Domain.Items.ItemTipo.ItemGeral, DetalhesIncompletos = true,
                Dados = RuinaRPG.Infrastructure.Items.ItemFactory.Serializar(FixoItemGeral(incompletoNome)),
            };
            db.EquipmentKitFixedItems.Add(fixo);
            await db.SaveChangesAsync();
            incompletoId = fixo.Id;
        }

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken,
            new CreateEquipmentKitRequest(Unico("Kit"), "D", 0,
                [new EquipmentKitItemInput(completo.Id, 1), new EquipmentKitItemInput(incompletoId.ToString(), 3)], [])));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var kit = (await response.Content.ReadFromJsonAsync<EquipmentKitResponse>())!;

        var linhaCompleta = kit.Items.Single(i => i.FixedItemId == completo.Id);
        linhaCompleta.Nome.Should().Be(completo.Nome);
        linhaCompleta.Tipo.Should().Be("Arma");
        linhaCompleta.DetalhesIncompletos.Should().BeFalse();
        var linhaIncompleta = kit.Items.Single(i => i.FixedItemId == incompletoId.ToString());
        linhaIncompleta.Nome.Should().Be(incompletoNome);
        linhaIncompleta.Tipo.Should().Be("ItemGeral");
        linhaIncompleta.Qtd.Should().Be(3);
        linhaIncompleta.DetalhesIncompletos.Should().BeTrue();
    }
}
