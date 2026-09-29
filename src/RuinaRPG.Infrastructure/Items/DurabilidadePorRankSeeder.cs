using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Items;

/// <summary>
/// Insere as linhas da Tabela de Durabilidade por Rank que ainda faltam — nunca sobrescreve uma linha
/// existente, porque o Auditor de Regras pode tê-la editado (mesmo tratamento do HistoricoSeeder).
/// </summary>
public static class DurabilidadePorRankSeeder
{
    public static async Task<int> SeedAsync(RuinaRpgDbContext db, string markdown)
    {
        var existentes = await db.DurabilidadesPorRank.Select(d => d.Rank).ToListAsync();
        var novas = TabelaDeDurabilidadeParser.Parse(markdown).Where(l => !existentes.Contains(l.Rank)).ToList();
        foreach (var linha in novas)
            db.DurabilidadesPorRank.Add(new DurabilidadePorRank { Rank = linha.Rank, Durabilidade = linha.Durabilidade, Inquebravel = linha.Inquebravel });
        await db.SaveChangesAsync();
        return novas.Count;
    }
}
