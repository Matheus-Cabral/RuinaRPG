namespace RuinaRPG.Domain.Items;

/// <summary>Uma linha da Tabela de Durabilidade por Rank: Durabilidade nula quando Inquebrável.</summary>
public sealed record DurabilidadeDeRank(RankDeItem Rank, int? Durabilidade, bool Inquebravel);
