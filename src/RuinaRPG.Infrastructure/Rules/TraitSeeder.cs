using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Rules;

public static class TraitSeeder
{
    public static async Task<int> SeedAsync(RuinaRpgDbContext db, string caracteristicasMarkdown)
    {
        var parsed = TraitSeedParser.Parse(caracteristicasMarkdown);
        var existing = await db.Traits
            .Select(t => new { t.Nome, t.Custo, t.Polaridade })
            .ToListAsync();
        var existingKeys = existing
            .Select(t => (t.Nome, t.Custo, Polaridade: t.Polaridade.ToString()))
            .ToHashSet();

        var toInsert = parsed
            .Where(seed => !existingKeys.Contains((seed.Nome, seed.Custo, seed.Polaridade)))
            .Select(seed => new Trait
            {
                Id = Guid.NewGuid(),
                Nome = seed.Nome,
                Descricao = seed.Descricao,
                Custo = seed.Custo,
                Polaridade = Enum.Parse<Polaridade>(seed.Polaridade)
            })
            .ToList();

        if (toInsert.Count == 0)
            return 0;

        db.Traits.AddRange(toInsert);
        await db.SaveChangesAsync();
        return toInsert.Count;
    }
}
