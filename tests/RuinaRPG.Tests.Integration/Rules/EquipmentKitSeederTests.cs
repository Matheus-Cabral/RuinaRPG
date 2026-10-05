using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Rules;

public class EquipmentKitSeederTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public EquipmentKitSeederTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<RuinaRpgDbContext> NewDbAsync()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        // PostgresFixture is an IClassFixture: its single container/database is shared by every
        // fact in this class, and their execution order is not guaranteed to be declaration order.
        // Reset the table so each test starts from the empty state its body assumes, instead of
        // silently depending on a sibling test having (not) run first. EquipmentKitItems and
        // EquipmentKitChoiceSlots both have a real DB-level ON DELETE CASCADE FK to EquipmentKits
        // (see the AddEquipmentKitsAndSheetEquipmentKitId migration), so deleting EquipmentKits
        // alone clears all three tables.
        await db.EquipmentKits.ExecuteDeleteAsync();
        await db.EquipmentKitFixedItems.ExecuteDeleteAsync();

        return db;
    }

    [Fact]
    public async Task SeedAsync_inserts_all_12_kits_with_their_items_and_choice_slots_on_an_empty_table()
    {
        await using var db = await NewDbAsync();

        var inserted = await EquipmentKitSeeder.SeedAsync(db);

        inserted.Should().Be(12);
        (await db.EquipmentKits.CountAsync()).Should().Be(12);
        var patrulheiro = await db.EquipmentKits.SingleAsync(k => k.Nome == "Patrulheiro");
        (await db.EquipmentKitItems.Where(i => i.KitId == patrulheiro.Id).CountAsync()).Should().Be(3); // Tampa de Madeira, Mochila, Tônico de Vida simples
        (await db.EquipmentKitChoiceSlots.Where(s => s.KitId == patrulheiro.Id).CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SeedAsync_is_idempotent_and_never_touches_a_kit_the_Auditor_already_edited()
    {
        await using var db = await NewDbAsync();
        await EquipmentKitSeeder.SeedAsync(db);

        var viajante = await db.EquipmentKits.SingleAsync(k => k.Nome == "Viajante");
        viajante.Descricao = "Editado pelo Auditor";
        await db.SaveChangesAsync();

        var secondRun = await EquipmentKitSeeder.SeedAsync(db);

        secondRun.Should().Be(0);
        (await db.EquipmentKits.CountAsync()).Should().Be(12);
        (await db.EquipmentKits.SingleAsync(k => k.Nome == "Viajante")).Descricao.Should().Be("Editado pelo Auditor");
    }

    [Fact]
    public async Task SeedAsync_links_every_item_and_slot_bonus_to_a_fixed_item_without_duplicates()
    {
        await using var db = await NewDbAsync();

        await EquipmentKitSeeder.SeedAsync(db);

        (await db.EquipmentKitItems.CountAsync(i => i.FixedItemId == null)).Should().Be(0);
        (await db.EquipmentKitChoiceSlots.CountAsync(s => s.BonusNome != null && s.BonusFixedItemId == null)).Should().Be(0);
        (await db.EquipmentKitChoiceSlots.CountAsync(s => s.BonusNome != null)).Should().BeGreaterThan(0);
        var fixos = await db.EquipmentKitFixedItems.Select(f => new { f.Nome, f.Tipo }).ToListAsync();
        fixos.Should().NotBeEmpty();
        fixos.Should().OnlyHaveUniqueItems();
    }
}
