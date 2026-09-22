using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Rules;

public static class HistoricoSeeder
{
    /// <summary>
    /// Historico.md is the source of truth, re-read on every startup/migrate: a Histórico missing
    /// from the table (matched by Nome alone) is inserted. Unlike TraitSeeder, an existing
    /// non-customized row is never resynced field-by-field — there's no cheap metadata field on
    /// Historico worth resyncing the way TraitSeeder resyncs RequerEspecificacao, so presence is
    /// the only thing checked.
    ///
    /// Two exceptions, both mirroring TraitSeeder (Rules Audit System, see HistoricosController):
    /// - A row with IsCustomized = true is skipped entirely.
    /// - The match-key lookup includes soft-deleted rows, so a deliberately deleted Histórico is
    ///   recognized as "already present" and never reinserted.
    ///
    /// Known limitation of the Nome-only match key: renaming a Histórico via the Auditoria page
    /// (HistoricosController.Update) does not rename its seed-matching key. The old Nome from
    /// Historico.md is then no longer present in the table, so the next restart/migrate re-seeds it
    /// as a brand-new row — leaving both the renamed entry and a resurrected duplicate under the
    /// original name. This is a deliberate scope cut, not a bug to silently work around; recovering
    /// from it means the Auditor manually deletes the duplicate.
    /// </summary>
    public static async Task<(int Inserted, int Updated)> SeedAsync(RuinaRpgDbContext db, string historicoMarkdown)
    {
        var parsed = HistoricoSeedParser.Parse(historicoMarkdown);
        var existingNomes = await db.Historicos.Select(h => h.Nome).ToListAsync();
        var existingSet = existingNomes.ToHashSet();

        var toInsert = new List<Historico>();
        foreach (var seed in parsed)
        {
            if (existingSet.Contains(seed.Nome))
                continue;

            toInsert.Add(new Historico
            {
                Id = Guid.NewGuid(),
                Nome = seed.Nome,
                Descricao = seed.Descricao,
                PericiaMaisSeis = seed.PericiaMaisSeis,
                PericiaMaisTres = seed.PericiaMaisTres,
            });
        }

        if (toInsert.Count == 0)
            return (0, 0);

        db.Historicos.AddRange(toInsert);
        await db.SaveChangesAsync();
        return (toInsert.Count, 0);
    }
}
