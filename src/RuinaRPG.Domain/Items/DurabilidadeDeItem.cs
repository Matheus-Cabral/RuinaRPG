namespace RuinaRPG.Domain.Items;

/// <summary>
/// Durabilidade máxima de Arma/Armadura/Escudo derivada do Rank (Tabela de Durabilidade por Rank).
/// Sem Rank = sem durabilidade; Rank inquebrável = sem máximo e Inquebravel.
/// </summary>
public static class DurabilidadeDeItem
{
    public static (int? Maxima, bool Inquebravel) Resolver(RankDeItem? rank, IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela)
    {
        if (rank is null || !tabela.TryGetValue(rank.Value, out var linha))
            return (null, false);
        return linha.Inquebravel ? (null, true) : (linha.Durabilidade, false);
    }

    /// <summary>A durabilidade atual nunca passa do máximo (nem fica negativa); sem máximo, é 0.</summary>
    public static int LimitarAtual(int atual, int? maxima) => Math.Clamp(atual, 0, maxima ?? 0);
}
