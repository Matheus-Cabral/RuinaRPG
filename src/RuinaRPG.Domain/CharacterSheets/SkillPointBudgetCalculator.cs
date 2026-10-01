using RuinaRPG.Domain.Rules.Niveis;

namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Perícias 2.d: "ver Tabela de Níveis para os Pontos de Perícia concedidos por nível" — the
/// level-derived half of the budget only. A Perícia can also gain points from a Critical Hit
/// mid-session (§2 of Ruína RPG - Sistema Básico), a source this table knows nothing about;
/// CharacterSheet.PontosDePericiaBonusCritico tracks that separately and is subtracted from Gasto
/// Total by the caller (CharacterSkillsController.Budget) so it isn't flagged as over budget.
/// Still displayed for reference only, not enforced server-side like AttributePointBudgetCalculator's.
/// </summary>
public static class SkillPointBudgetCalculator
{
    public static int Compute(int nivel, ProgressaoDeNivel tabela) =>
        tabela.Acumulado(ChavesDeNivel.PontosDePericia, nivel);
}
