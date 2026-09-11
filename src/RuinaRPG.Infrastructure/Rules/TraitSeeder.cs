using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Rules;

public static class TraitSeeder
{
    /// <summary>
    /// Características.md is the source of truth, re-read on every startup/migrate: a trait
    /// missing from the table (matched by Nome+Custo+Polaridade) is inserted, and an existing row
    /// whose RequerEspecificacao no longer matches the freshly parsed value is corrected in place
    /// — this is what lets a row seeded before that flag existed pick up its correct value without
    /// a manual data fix on already-deployed databases. Descricao is intentionally NOT synced on
    /// update: only the match key and RequerEspecificacao are ever compared/written for an existing
    /// row.
    ///
    /// Two exceptions, both added for the Rules Audit System (a GM-editable Características
    /// catalog, see TraitsController):
    /// - A row with IsCustomized = true (manually created or edited through that admin page) is
    ///   skipped entirely — not even RequerEspecificacao is synced. A manual edit wins forever.
    /// - The match-key lookup includes soft-deleted rows (IsDeleted = true), so a deliberately
    ///   deleted trait is recognized as "already present" and never reinserted.
    /// </summary>
    public static async Task<(int Inserted, int Updated)> SeedAsync(RuinaRpgDbContext db, string caracteristicasMarkdown)
    {
        var parsed = TraitSeedParser.Parse(caracteristicasMarkdown);
        var existing = await db.Traits.ToListAsync();
        var existingByKey = existing.ToDictionary(t => (t.Nome, t.Custo, Polaridade: t.Polaridade.ToString()));

        var toInsert = new List<Trait>();
        var updated = 0;

        foreach (var seed in parsed)
        {
            if (existingByKey.TryGetValue((seed.Nome, seed.Custo, seed.Polaridade), out var existingTrait))
            {
                if (existingTrait.IsCustomized)
                    continue;

                if (existingTrait.RequerEspecificacao != seed.RequerEspecificacao)
                {
                    existingTrait.RequerEspecificacao = seed.RequerEspecificacao;
                    updated++;
                }
                continue;
            }

            toInsert.Add(new Trait
            {
                Id = Guid.NewGuid(),
                Nome = seed.Nome,
                Descricao = seed.Descricao,
                Custo = seed.Custo,
                Polaridade = Enum.Parse<Polaridade>(seed.Polaridade),
                RequerEspecificacao = seed.RequerEspecificacao
            });
        }

        if (toInsert.Count == 0 && updated == 0)
            return (0, 0);

        if (toInsert.Count > 0)
            db.Traits.AddRange(toInsert);

        await db.SaveChangesAsync();
        return (toInsert.Count, updated);
    }
}
