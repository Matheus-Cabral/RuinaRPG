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
