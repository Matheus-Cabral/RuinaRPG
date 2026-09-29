using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Items;

/// <summary>Tabela de Durabilidade por Rank carregada uma vez por escopo; resolve o máximo de um item.</summary>
public class DurabilidadePorRankProvider(RuinaRpgDbContext db)
{
    private IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank>? _tabela;

    public async Task<IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank>> TabelaAsync() =>
        _tabela ??= await db.DurabilidadesPorRank.AsNoTracking()
            .ToDictionaryAsync(d => d.Rank, d => new DurabilidadeDeRank(d.Rank, d.Durabilidade, d.Inquebravel));

    public async Task<(int? Maxima, bool Inquebravel)> ResolverAsync(RankDeItem? rank) =>
        DurabilidadeDeItem.Resolver(rank, await TabelaAsync());
}
