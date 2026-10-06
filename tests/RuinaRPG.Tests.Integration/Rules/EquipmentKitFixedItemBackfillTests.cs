using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Rules;

public class EquipmentKitFixedItemBackfillTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public EquipmentKitFixedItemBackfillTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<RuinaRpgDbContext> NovoDbAsync()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();
        // Os itens fixos referenciados por kits têm FK Restrict: apagar os kits (cascade) primeiro.
        await db.EquipmentKits.ExecuteDeleteAsync();
        await db.EquipmentKitFixedItems.ExecuteDeleteAsync();
        return db;
    }

    private static async Task<EquipmentKit> KitLegadoAsync(RuinaRpgDbContext db, string nome,
        (string Nome, ItemTipo Tipo)[] itens, string? bonus = null, string? hint = null)
    {
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = nome, Descricao = "d", Ciclos = 1 };
        db.EquipmentKits.Add(kit);
        foreach (var (n, t) in itens)
            db.EquipmentKitItems.Add(new EquipmentKitItem { Id = Guid.NewGuid(), KitId = kit.Id, Nome = n, Tipo = t, Qtd = 1, SubcategoriaHint = hint });
        if (bonus is not null)
            db.EquipmentKitChoiceSlots.Add(new EquipmentKitChoiceSlot
            {
                Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma", Tipo = ItemTipo.Arma, Qtd = 1,
                BonusSubcategoria = "Munição", BonusNome = bonus, BonusQtd = 10,
            });
        await db.SaveChangesAsync();
        return kit;
    }

    [Fact]
    public async Task Links_every_legacy_kit_item_and_slot_bonus_to_a_fixed_item_created_once_per_name_and_type()
    {
        await using var db = await NovoDbAsync();
        await KitLegadoAsync(db, "Kit A", [("Mochila", ItemTipo.ItemGeral), ("Tampa de Madeira", ItemTipo.Escudo)], bonus: "Flecha");
        await KitLegadoAsync(db, "Kit B", [("Mochila", ItemTipo.ItemGeral), ("Item Que Não Existe", ItemTipo.Arma)]);

        var criados = await EquipmentKitFixedItemBackfill.RunAsync(db);

        criados.Should().Be(4);
        var fixos = await db.EquipmentKitFixedItems.AsNoTracking().ToListAsync();
        fixos.Select(f => f.Nome).Should().BeEquivalentTo("Mochila", "Tampa de Madeira", "Flecha", "Item Que Não Existe");
        (await db.EquipmentKitItems.AsNoTracking().Where(i => i.Nome == "Mochila").Select(i => i.FixedItemId).Distinct().ToListAsync())
            .Should().ContainSingle().Which.Should().Be(fixos.Single(f => f.Nome == "Mochila").Id);
        (await db.EquipmentKitItems.AsNoTracking().CountAsync(i => i.FixedItemId == null)).Should().Be(0);
        (await db.EquipmentKitChoiceSlots.AsNoTracking().SingleAsync(s => s.BonusNome == "Flecha")).BonusFixedItemId
            .Should().Be(fixos.Single(f => f.Nome == "Flecha").Id);
    }

    [Fact]
    public async Task A_name_found_in_the_default_catalog_gets_its_full_data_and_one_that_is_not_is_flagged_incomplete()
    {
        await using var db = await NovoDbAsync();
        await KitLegadoAsync(db, "Kit", [("Tampa de Madeira", ItemTipo.Escudo), ("Item Que Não Existe", ItemTipo.Arma)]);

        await EquipmentKitFixedItemBackfill.RunAsync(db);

        var escudo = await db.EquipmentKitFixedItems.AsNoTracking().SingleAsync(f => f.Nome == "Tampa de Madeira");
        var padrao = (Escudo)DefaultCatalogItems.Build(Guid.Empty).Single(i => i is Escudo && i.Nome == "Tampa de Madeira");
        escudo.DetalhesIncompletos.Should().BeFalse();
        ItemFactory.Desserializar(escudo.Dados).BonusDefesa.Should().Be(padrao.BonusDefesa);
        escudo.Requisitos.Should().BeEquivalentTo(padrao.Requisitos);

        var inventado = await db.EquipmentKitFixedItems.AsNoTracking().SingleAsync(f => f.Nome == "Item Que Não Existe");
        inventado.DetalhesIncompletos.Should().BeTrue();
        ItemFactory.Desserializar(inventado.Dados).Nome.Should().Be("Item Que Não Existe");
    }

    [Fact]
    public async Task The_subcategoria_hint_of_a_name_only_item_geral_is_kept()
    {
        await using var db = await NovoDbAsync();
        await KitLegadoAsync(db, "Kit", [("Tônico Inventado", ItemTipo.ItemGeral)], hint: "Poções e Tônicos");

        await EquipmentKitFixedItemBackfill.RunAsync(db);

        var fixo = await db.EquipmentKitFixedItems.AsNoTracking().SingleAsync(f => f.Nome == "Tônico Inventado");
        ItemFactory.Desserializar(fixo.Dados).Subcategoria.Should().Be("Poções e Tônicos");
    }

    [Fact]
    public async Task Running_it_twice_changes_nothing_the_second_time()
    {
        await using var db = await NovoDbAsync();
        await KitLegadoAsync(db, "Kit", [("Mochila", ItemTipo.ItemGeral)]);
        await EquipmentKitFixedItemBackfill.RunAsync(db);

        (await EquipmentKitFixedItemBackfill.RunAsync(db)).Should().Be(0);
        (await db.EquipmentKitFixedItems.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Rows_already_linked_and_fixed_items_the_auditor_edited_are_left_alone()
    {
        await using var db = await NovoDbAsync();
        var editado = new EquipmentKitFixedItem
        {
            Id = Guid.NewGuid(), Nome = "Mochila", Tipo = ItemTipo.ItemGeral, DetalhesIncompletos = false,
            Dados = ItemFactory.Serializar(new RuinaRPG.Contracts.Items.CreateItemRequest(
                "ItemGeral", "Mochila", 9.5m, 77, null, "Equipamentos de Aventura", "Editada pelo Auditor",
                null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)),
        };
        db.EquipmentKitFixedItems.Add(editado);
        await db.SaveChangesAsync();
        var kit = await KitLegadoAsync(db, "Kit", [("Mochila", ItemTipo.ItemGeral)]);

        var criados = await EquipmentKitFixedItemBackfill.RunAsync(db);

        criados.Should().Be(0);
        (await db.EquipmentKitItems.AsNoTracking().SingleAsync(i => i.KitId == kit.Id)).FixedItemId.Should().Be(editado.Id);
        // jsonb normaliza o texto: compara o conteúdo, não a string.
        var depois = ItemFactory.Desserializar((await db.EquipmentKitFixedItems.AsNoTracking().SingleAsync(f => f.Id == editado.Id)).Dados);
        depois.Descricao.Should().Be("Editada pelo Auditor");
        depois.Preco.Should().Be(77);
    }

    [Fact]
    public async Task Rows_of_a_soft_deleted_kit_are_skipped_and_stay_unlinked()
    {
        await using var db = await NovoDbAsync();
        var kit = await KitLegadoAsync(db, "Kit Excluído", [("Mochila", ItemTipo.ItemGeral)], bonus: "Flecha");
        kit.IsDeleted = true;
        await db.SaveChangesAsync();

        var criados = await EquipmentKitFixedItemBackfill.RunAsync(db);

        criados.Should().Be(0);
        (await db.EquipmentKitFixedItems.CountAsync()).Should().Be(0);
        (await db.EquipmentKitItems.AsNoTracking().SingleAsync(i => i.KitId == kit.Id)).FixedItemId.Should().BeNull();
        (await db.EquipmentKitChoiceSlots.AsNoTracking().SingleAsync(s => s.KitId == kit.Id)).BonusFixedItemId.Should().BeNull();
    }
}
