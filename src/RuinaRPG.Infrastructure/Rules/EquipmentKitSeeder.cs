using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Rules;

/// <summary>
/// Insert-if-missing-by-Nome, same treatment as HistoricoSeeder — an Auditor's edit via
/// EquipmentKitsController is never overwritten by a later restart/migrate. Same known limitation
/// as HistoricoSeeder: renaming a kit via Auditoria doesn't update this match key, so the old Nome
/// re-seeds as a duplicate on the next restart; recovering means the Auditor deletes the duplicate.
/// </summary>
public static class EquipmentKitSeeder
{
    public static async Task<int> SeedAsync(RuinaRpgDbContext db)
    {
        var existingNomes = (await db.EquipmentKits.Select(k => k.Nome).ToListAsync()).ToHashSet();

        var toInsert = EquipmentKitSeedData.All.Where(seed => !existingNomes.Contains(seed.Nome)).ToList();
        if (toInsert.Count == 0)
            return 0;

        foreach (var seed in toInsert)
        {
            var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = seed.Nome, Descricao = seed.Descricao, Ciclos = seed.Ciclos };
            db.EquipmentKits.Add(kit);

            foreach (var item in seed.Items)
                db.EquipmentKitItems.Add(new EquipmentKitItem { Id = Guid.NewGuid(), KitId = kit.Id, Nome = item.Nome, Tipo = item.Tipo, Qtd = item.Qtd, SubcategoriaHint = item.SubcategoriaHint });

            foreach (var slot in seed.ChoiceSlots)
                db.EquipmentKitChoiceSlots.Add(new EquipmentKitChoiceSlot
                {
                    Id = Guid.NewGuid(),
                    KitId = kit.Id,
                    Label = slot.Label,
                    Tipo = slot.Tipo,
                    SubcategoriasCsv = slot.Subcategorias is null ? null : string.Join(",", slot.Subcategorias),
                    Tier = slot.Tier,
                    Qtd = slot.Qtd,
                    BonusSubcategoria = slot.BonusSubcategoria,
                    BonusNome = slot.BonusNome,
                    BonusQtd = slot.BonusQtd,
                });
        }

        await db.SaveChangesAsync();
        return toInsert.Count;
    }
}
