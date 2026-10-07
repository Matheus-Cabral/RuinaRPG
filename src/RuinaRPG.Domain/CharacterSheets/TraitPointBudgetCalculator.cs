using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Rules.Niveis;

namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Características 5.d: each list has its own budget, applied independently — Positivas total may
/// not exceed the positive budget, and Negativas total (magnitude; Custo is stored negative —
/// TraitSeedParser flips its sign) may not exceed the negative one, which is twice the positive.
/// Taking Negativas never frees extra Positiva points.
/// Every character gets a flat 5-point creation grant that never appears in the Tabela de Níveis
/// (unlike Atributo/Perícia, whose level-1 row IS their creation grant — Característica's column
/// only starts granting at level 4), plus whatever the "Espaços de Característica" column grants
/// at every level up to the character's own.
/// </summary>
public static class TraitPointBudgetCalculator
{
    private const int CriacaoBase = 5;
    private const int FatorDeNegativas = 2;

    /// <summary>O orçamento de Positivas.</summary>
    public static int Compute(int nivel, ProgressaoDeNivel tabela) =>
        CriacaoBase + tabela.Acumulado(ChavesDeNivel.EspacosDeCaracteristica, nivel);

    /// <summary>O orçamento de Negativas: o dobro do de Positivas, em qualquer nível.</summary>
    public static int ComputeNegativas(int nivel, ProgressaoDeNivel tabela) => FatorDeNegativas * Compute(nivel, tabela);

    public static int Para(Polaridade polaridade, int nivel, ProgressaoDeNivel tabela) =>
        polaridade == Polaridade.Negativa ? ComputeNegativas(nivel, tabela) : Compute(nivel, tabela);
}
