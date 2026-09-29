using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Items;

/// <summary>
/// Uma linha da Tabela de Durabilidade por Rank — dado estático seedado a partir de
/// "Tabela de Durabilidade por Rank.md", editável pelo Auditor de Regras. Rank é a própria PK (um
/// valor fixo de <see cref="RankDeItem"/> por linha, nunca duplicado).
/// </summary>
public class DurabilidadePorRank
{
    public RankDeItem Rank { get; set; }
    public int? Durabilidade { get; set; }
    public bool Inquebravel { get; set; }
}
