using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Rules;

public class EquipmentKitGrantServiceTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;

    public EquipmentKitGrantServiceTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    // Item.GmId and Campaign.GmId both carry a real DB-level FK to AspNetUsers, and
    // CampaignAttachment.CampaignId/ItemId carry real FKs to Campaigns/Items — a bare
    // Guid.NewGuid() used directly as GmId/CampaignId/ItemId (as a unit-test double might get
    // away with) violates those constraints here. These helpers create the actual parent rows.
    private static async Task<Guid> NewGmAsync(RuinaRpgDbContext db, string nickname)
    {
        var gm = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = $"{nickname}@equipagemgranttest.com",
            Email = $"{nickname}@equipagemgranttest.com", Nickname = nickname, Role = UserRole.GM,
        };
        db.Users.Add(gm);
        await db.SaveChangesAsync();
        return gm.Id;
    }

    private static async Task<Guid> NewCampaignAsync(RuinaRpgDbContext db, Guid gmId)
    {
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gmId, Nome = "Campanha", Descricao = "D" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();
        return campaign.Id;
    }

    [Fact]
    public async Task BuildPlanAsync_auto_creates_a_missing_fixed_ItemGeral_in_the_GM_s_own_catalog()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmAutoCria");
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var kitItem = new EquipmentKitItem { Id = Guid.NewGuid(), KitId = kit.Id, Nome = "Item Inexistente XYZ", Tipo = ItemTipo.ItemGeral, Qtd = 2, SubcategoriaHint = "Equipamentos de Aventura" };
        db.EquipmentKitItems.Add(kitItem);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var (plan, error) = await service.BuildPlanAsync(kit, [kitItem], [], gmId, []);
        // BuildPlanAsync only stages the auto-created ItemGeral on the change tracker (via
        // db.Add) — persisting it is the caller's job, same as Task 9's controllers will do
        // alongside their own per-Tipo inserts. Flush here so the assertion query below (which
        // always hits the DB, never the tracker) can see it.
        await db.SaveChangesAsync();

        error.Should().BeNull();
        plan!.Grants.Should().ContainSingle(g => g.Tipo == ItemTipo.ItemGeral && g.Qtd == 2);
        var created = await db.Set<ItemGeral>().SingleAsync(i => i.GmId == gmId && i.Nome == "Item Inexistente XYZ");
        created.Subcategoria.Should().Be("Equipamentos de Aventura");
        created.Peso.Should().Be(0);
        created.Preco.Should().Be(0);
    }

    [Fact]
    public async Task BuildPlanAsync_reuses_an_existing_item_instead_of_creating_a_duplicate()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmReusaItem");
        var existing = new ItemGeral { Id = Guid.NewGuid(), GmId = gmId, Nome = "Mochila", Subcategoria = "Equipamentos de Aventura", Peso = 5, Preco = 20 };
        db.Add(existing);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var kitItem = new EquipmentKitItem { Id = Guid.NewGuid(), KitId = kit.Id, Nome = "Mochila", Tipo = ItemTipo.ItemGeral, Qtd = 1 };
        db.EquipmentKitItems.Add(kitItem);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var (plan, _) = await service.BuildPlanAsync(kit, [kitItem], [], gmId, []);

        plan!.Grants.Single().ItemId.Should().Be(existing.Id);
        (await db.Set<ItemGeral>().CountAsync(i => i.GmId == gmId && i.Nome == "Mochila")).Should().Be(1);
    }

    [Fact]
    public async Task BuildPlanAsync_rejects_a_missing_choice_slot_selection()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = Guid.NewGuid();
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma", Tipo = ItemTipo.Arma, Tier = Tier.F, Qtd = 1 };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var (plan, error) = await service.BuildPlanAsync(kit, [], [slot], gmId, []);

        plan.Should().BeNull();
        error.Should().Contain("Arma");
    }

    [Fact]
    public async Task BuildPlanAsync_rejects_a_choice_selection_outside_the_slot_s_filter()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmForaDoFiltro");
        var outOfTierWeapon = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Espada Lendária", Subcategoria = "Espadas", Tier = Tier.S, Peso = 1, Preco = 0 };
        db.Add(outOfTierWeapon);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma", Tipo = ItemTipo.Arma, Tier = Tier.F, Qtd = 1 };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var (plan, error) = await service.BuildPlanAsync(kit, [], [slot], gmId, [new ChoiceSlotSelectionRequest(slot.Id.ToString(), outOfTierWeapon.Id.ToString())]);

        plan.Should().BeNull();
        error.Should().NotBeNull();
    }

    [Fact]
    public async Task BuildPlanAsync_grants_the_conditional_bonus_only_when_the_matching_alternative_is_chosen()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmBonusCondicional");
        var bow = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Arco de Teste", Subcategoria = "Arcos", Tier = Tier.F, Peso = 1, Preco = 0 };
        db.Add(bow);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot
        {
            Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma à distância", Tipo = ItemTipo.Arma,
            SubcategoriasCsv = "Arcos,Fundas e Baladeiras", Tier = Tier.F, Qtd = 1,
            BonusSubcategoria = "Arcos", BonusNome = "Flecha de Madeira", BonusQtd = 10,
        };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var (plan, error) = await service.BuildPlanAsync(kit, [], [slot], gmId, [new ChoiceSlotSelectionRequest(slot.Id.ToString(), bow.Id.ToString())]);

        error.Should().BeNull();
        plan!.Grants.Should().HaveCount(2);
        plan.Grants.Should().Contain(g => g.Tipo == ItemTipo.Arma && g.ItemId == bow.Id);
        plan.Grants.Should().Contain(g => g.Tipo == ItemTipo.ItemGeral && g.Qtd == 10);
    }

    // The test above only proves the bonus fires FOR the matching alternative — on its own it
    // can't rule out a bug where BonusNome always grants regardless of which eligible item was
    // picked. This is the other half: an equally eligible, non-matching alternative (a sling,
    // valid for the slot's own filter, but whose Subcategoria isn't the slot's BonusSubcategoria)
    // must NOT trigger the bonus.
    [Fact]
    public async Task BuildPlanAsync_does_not_grant_the_conditional_bonus_when_a_non_matching_alternative_is_chosen()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmBonusNaoCondicional");
        var sling = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Funda de Teste", Subcategoria = "Fundas e Baladeiras", Tier = Tier.F, Peso = 1, Preco = 0 };
        db.Add(sling);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot
        {
            Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma à distância", Tipo = ItemTipo.Arma,
            SubcategoriasCsv = "Arcos,Fundas e Baladeiras", Tier = Tier.F, Qtd = 1,
            BonusSubcategoria = "Arcos", BonusNome = "Flecha de Madeira", BonusQtd = 10,
        };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var (plan, error) = await service.BuildPlanAsync(kit, [], [slot], gmId, [new ChoiceSlotSelectionRequest(slot.Id.ToString(), sling.Id.ToString())]);

        error.Should().BeNull();
        plan!.Grants.Should().ContainSingle();
        plan.Grants.Should().Contain(g => g.Tipo == ItemTipo.Arma && g.ItemId == sling.Id);
        plan.Grants.Should().NotContain(g => g.Tipo == ItemTipo.ItemGeral);
    }

    [Fact]
    public async Task ResolveEligibleOptionsAsync_matches_by_parsed_Familia_for_items_built_by_the_constructor()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmFamiliaParse");
        var varinha = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Varinha Composta", Subcategoria = "Equipamento inicial - Arma - Mágica - Varinha", Tier = Tier.F, Peso = 1, Preco = 0 };
        var machado = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Machado Composto", Subcategoria = "Equipamento inicial - Arma - Corpo a Corpo - Machado", Tier = Tier.F, Peso = 1, Preco = 0 };
        db.AddRange(varinha, machado);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Condutor", Tipo = ItemTipo.Arma, SubcategoriasCsv = "Varinha", Qtd = 1 };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var options = await service.ResolveEligibleOptionsAsync(slot, gmId);

        options.Should().ContainSingle(o => o.ItemId == varinha.Id.ToString());
    }

    [Fact]
    public async Task ResolveEligibleOptionsAsync_still_matches_the_legacy_raw_Subcategoria_string()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmLegacyRaw");
        var arco = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Arco Legado", Subcategoria = "Arcos", Tier = Tier.F, Peso = 1, Preco = 0 };
        db.Add(arco);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma à distância", Tipo = ItemTipo.Arma, SubcategoriasCsv = "Arcos", Qtd = 1 };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var options = await service.ResolveEligibleOptionsAsync(slot, gmId);

        options.Should().ContainSingle(o => o.ItemId == arco.Id.ToString());
    }

    [Fact]
    public async Task ResolveEligibleOptionsAsync_resolves_Armadura_Escudo_and_Artefato_choice_slots()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmArmaduraEscudoArtefato");
        var armadura = new Armadura { Id = Guid.NewGuid(), GmId = gmId, Nome = "Armadura Teste", Subcategoria = "Equipamento inicial - Armadura - Leve - Couro", Peso = 1, Preco = 0 };
        var escudo = new Escudo { Id = Guid.NewGuid(), GmId = gmId, Nome = "Escudo Teste", Subcategoria = "Equipamento inicial - Escudo - Leve - Rodela", Peso = 1, Preco = 0 };
        var artefato = new Artefato { Id = Guid.NewGuid(), GmId = gmId, Nome = "Artefato Teste", Subcategoria = "Equipamento inicial - Artefato - Passivo - Anel", Peso = 0, Preco = 0 };
        db.AddRange(armadura, escudo, artefato);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var armaduraSlot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Armadura", Tipo = ItemTipo.Armadura, SubcategoriasCsv = "Couro", Qtd = 1, ArmorSlot = RuinaRPG.Domain.CharacterSheets.ArmorSlotType.Superior };
        var escudoSlot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Escudo", Tipo = ItemTipo.Escudo, SubcategoriasCsv = "Rodela", Qtd = 1 };
        var artefatoSlot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Artefato", Tipo = ItemTipo.Artefato, SubcategoriasCsv = "Anel", Qtd = 1 };
        db.EquipmentKitChoiceSlots.AddRange(armaduraSlot, escudoSlot, artefatoSlot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);

        (await service.ResolveEligibleOptionsAsync(armaduraSlot, gmId)).Should().ContainSingle(o => o.ItemId == armadura.Id.ToString());
        (await service.ResolveEligibleOptionsAsync(escudoSlot, gmId)).Should().ContainSingle(o => o.ItemId == escudo.Id.ToString());
        (await service.ResolveEligibleOptionsAsync(artefatoSlot, gmId)).Should().ContainSingle(o => o.ItemId == artefato.Id.ToString());
    }

    [Fact]
    public async Task BuildPlanAsync_carries_the_slot_s_ArmorSlot_through_to_the_grant_for_a_chosen_Armadura()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmArmorSlotCarry");
        var armadura = new Armadura { Id = Guid.NewGuid(), GmId = gmId, Nome = "Armadura Teste", Subcategoria = "Equipamento inicial - Armadura - Leve - Couro", DurabilidadeMaxima = 10, Peso = 1, Preco = 0 };
        db.Add(armadura);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Armadura", Tipo = ItemTipo.Armadura, SubcategoriasCsv = "Couro", Qtd = 1, ArmorSlot = RuinaRPG.Domain.CharacterSheets.ArmorSlotType.Capacete };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var (plan, error) = await service.BuildPlanAsync(kit, [], [slot], gmId, [new ChoiceSlotSelectionRequest(slot.Id.ToString(), armadura.Id.ToString())]);

        error.Should().BeNull();
        var grant = plan!.Grants.Single();
        grant.Tipo.Should().Be(ItemTipo.Armadura);
        grant.ArmorSlot.Should().Be(RuinaRPG.Domain.CharacterSheets.ArmorSlotType.Capacete);
        grant.DurabilidadeMaxima.Should().Be(10);
    }

    // A parsed-Família match must not cross Tipos: an Arma slot whose allowed Família list
    // contains "Couro" must not offer an Arma row whose Subcategoria is mis-composed to parse
    // as Tipo "Armadura" (Família segment "Couro") even though the raw Família string matches.
    // The row is an Arma (so it IS in the Arma-slot candidate set via the per-Tipo query — this
    // is not excluded for free by the switch dispatch), so only Matches()'s own
    // tipo == slot.Tipo gate can exclude it. Verified load-bearing: removing that gate makes
    // this test FAIL (see task-8-report.md, "Fix round 1").
    [Fact]
    public async Task ResolveEligibleOptionsAsync_parsed_Familia_match_does_not_cross_Tipos()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmFamiliaCrossTipo");
        var armaMisComposta = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Arma Mal-Composta", Subcategoria = "Equipamento inicial - Armadura - Leve - Couro", Tier = Tier.F, Peso = 1, Preco = 0 };
        db.Add(armaMisComposta);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var armaSlot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma", Tipo = ItemTipo.Arma, SubcategoriasCsv = "Couro", Qtd = 1 };
        db.EquipmentKitChoiceSlots.Add(armaSlot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var options = await service.ResolveEligibleOptionsAsync(armaSlot, gmId);

        options.Should().BeEmpty();
    }

    // BuildPlanAsync's conditional-bonus check must fire for a selection whose Subcategoria
    // only matches the slot's BonusSubcategoria via the parsed-Família path, not just via the
    // legacy raw-string equality already proven by the Caçador-style tests above.
    [Fact]
    public async Task BuildPlanAsync_grants_the_conditional_bonus_when_the_match_is_via_parsed_Familia()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmBonusFamilia");
        var varinha = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Varinha Composta", Subcategoria = "Equipamento inicial - Arma - Mágica - Varinha", Tier = Tier.F, Peso = 1, Preco = 0 };
        db.Add(varinha);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot
        {
            Id = Guid.NewGuid(), KitId = kit.Id, Label = "Condutor", Tipo = ItemTipo.Arma,
            SubcategoriasCsv = "Varinha", Tier = Tier.F, Qtd = 1,
            BonusSubcategoria = "Varinha", BonusNome = "Cristal de Foco", BonusQtd = 1,
        };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var (plan, error) = await service.BuildPlanAsync(kit, [], [slot], gmId, [new ChoiceSlotSelectionRequest(slot.Id.ToString(), varinha.Id.ToString())]);

        error.Should().BeNull();
        plan!.Grants.Should().HaveCount(2);
        plan.Grants.Should().Contain(g => g.Tipo == ItemTipo.Arma && g.ItemId == varinha.Id);
        plan.Grants.Should().Contain(g => g.Tipo == ItemTipo.ItemGeral && g.Qtd == 1);
    }

    // Mirrors ResolveEligibleOptionsAsync_parsed_Familia_match_does_not_cross_Tipos above, but for
    // BuildPlanAsync's conditional-bonus check: the selected item's Subcategoria parses to a
    // DIFFERENT Tipo than the slot's own (Arma slot, but the item is mis-composed as an "Armadura"
    // string) whose parsed Família happens to equal BonusSubcategoria. Matches() itself already
    // guards ResolveEligibleOptionsAsync against this cross-Tipo case, but with SubcategoriasCsv
    // left null (matches any Subcategoria unconditionally) the item is still an eligible choice —
    // so only the bonus-match expression's own Tipo gate can stop the bonus from firing here.
    [Fact]
    public async Task BuildPlanAsync_does_not_grant_the_conditional_bonus_when_the_parsed_Familia_match_crosses_Tipos()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmBonusFamiliaCrossTipo");
        var armaMisComposta = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Arma Mal-Composta", Subcategoria = "Equipamento inicial - Armadura - Leve - Couro", Tier = Tier.F, Peso = 1, Preco = 0 };
        db.Add(armaMisComposta);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot
        {
            Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma", Tipo = ItemTipo.Arma, Qtd = 1,
            BonusSubcategoria = "Couro", BonusNome = "Não Deveria Ser Concedido", BonusQtd = 1,
        };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var (plan, error) = await service.BuildPlanAsync(kit, [], [slot], gmId, [new ChoiceSlotSelectionRequest(slot.Id.ToString(), armaMisComposta.Id.ToString())]);

        error.Should().BeNull();
        plan!.Grants.Should().ContainSingle();
        plan.Grants.Should().Contain(g => g.Tipo == ItemTipo.Arma && g.ItemId == armaMisComposta.Id);
        plan.Grants.Should().NotContain(g => g.Tipo == ItemTipo.ItemGeral);
    }

    [Fact]
    public async Task UpsertCampaignAttachmentAsync_creates_a_public_attachment_when_none_exists()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmUpsertNovo");
        var campaignId = await NewCampaignAsync(db, gmId);
        var item = new ItemGeral { Id = Guid.NewGuid(), GmId = gmId, Nome = "Mochila", Subcategoria = "Equipamentos de Aventura", Peso = 5, Preco = 20 };
        db.Add(item);
        await db.SaveChangesAsync();
        var itemId = item.Id;
        var service = new EquipmentKitGrantService(db);

        await service.UpsertCampaignAttachmentAsync(campaignId, itemId);
        await db.SaveChangesAsync();

        var attachment = await db.CampaignAttachments.SingleAsync(a => a.CampaignId == campaignId && a.ItemId == itemId);
        attachment.IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertCampaignAttachmentAsync_flips_an_existing_private_attachment_to_public()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmUpsertFlip");
        var campaignId = await NewCampaignAsync(db, gmId);
        var item = new ItemGeral { Id = Guid.NewGuid(), GmId = gmId, Nome = "Mochila", Subcategoria = "Equipamentos de Aventura", Peso = 5, Preco = 20 };
        db.Add(item);
        await db.SaveChangesAsync();
        var itemId = item.Id;
        db.CampaignAttachments.Add(new CampaignAttachment { Id = Guid.NewGuid(), CampaignId = campaignId, ItemId = itemId, IsPublic = false });
        await db.SaveChangesAsync();
        var service = new EquipmentKitGrantService(db);

        await service.UpsertCampaignAttachmentAsync(campaignId, itemId);
        await db.SaveChangesAsync();

        (await db.CampaignAttachments.CountAsync(a => a.CampaignId == campaignId && a.ItemId == itemId)).Should().Be(1);
        (await db.CampaignAttachments.SingleAsync(a => a.CampaignId == campaignId && a.ItemId == itemId)).IsPublic.Should().BeTrue();
    }
}
