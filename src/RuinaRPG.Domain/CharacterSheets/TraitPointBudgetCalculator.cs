using RuinaRPG.Domain.Rules.Niveis;

namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Características 5.d: the budget applies independently to each list — Positivas total may not
/// exceed it, and Negativas total (magnitude; Custo is stored negative — TraitSeedParser flips its
/// sign) may not exceed it either. A character that spends the full budget on both sides nets to 0.
/// Every character gets a flat 5-point creation grant that never appears in the Tabela de Níveis
/// (unlike Atributo/Perícia, whose level-1 row IS their creation grant — Característica's column
/// only starts granting at level 4), plus whatever the "Espaços de Característica" column grants
/// at every level up to the character's own.
/// </summary>
public static class TraitPointBudgetCalculator
{
    private const int CriacaoBase = 5;

    public static int Compute(int nivel, ProgressaoDeNivel tabela) =>
        CriacaoBase + tabela.Acumulado(ChavesDeNivel.EspacosDeCaracteristica, nivel);
}
