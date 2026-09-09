using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Items;

public static class DefaultCatalogSeeder
{
    /// <summary>
    /// Seeds a single, brand-new GM's catalog with DefaultCatalogItems. Called right after GM
    /// registration (AuthController.RegisterGm) so a new GM's catalog is populated immediately,
    /// without waiting for the next --migrate run.
    /// </summary>
    public static async Task SeedForNewGmAsync(RuinaRpgDbContext db, Guid gmId)
    {
        db.Items.AddRange(DefaultCatalogItems.Build(gmId));
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Backfills every existing GM who currently has zero catalog items — for GMs that registered
    /// before this feature existed. Idempotent by construction: a GM who already has any items
    /// (their own, or a catalog seeded by an earlier run) is left untouched; only ever inserts,
    /// never updates or deletes. Wired into the same "--migrate"/Development startup path
    /// TraitSeeder already uses (see Program.cs), so this runs once per explicit migrate rather
    /// than on every ordinary container restart — a GM who deliberately empties their catalog
    /// back to zero would only see it repopulated by a subsequent deploy's migrate step, not on
    /// every restart.
    /// </summary>
    public static async Task<int> SeedMissingAsync(RuinaRpgDbContext db)
    {
        var gmIds = await db.Users.Where(u => u.Role == UserRole.GM).Select(u => u.Id).ToListAsync();
        var gmIdsWithItems = await db.Items.Select(i => i.GmId).Distinct().ToListAsync();
        var missingGmIds = gmIds.Except(gmIdsWithItems).ToList();

        foreach (var gmId in missingGmIds)
            db.Items.AddRange(DefaultCatalogItems.Build(gmId));

        if (missingGmIds.Count > 0)
            await db.SaveChangesAsync();

        return missingGmIds.Count;
    }
}
