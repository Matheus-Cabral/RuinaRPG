using RuinaRPG.Domain.Rules.Niveis;

namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Atributos 2.a: "A soma de Gasto de todos os 8 atributos ... não pode ultrapassar o total de
/// pontos que o personagem já recebeu (criação + níveis)." Tabela de Níveis' own level-1 row
/// already grants 9 Pontos de Atributo — the same 9 the requirement calls "pontos de
/// distribuição inicial da criação de personagem" — so creation isn't a separate flat add-on, it's
/// just level 1's row read like every other level's. The number comes from the structured
/// "Pontos de Atributo" column of the Tabela de Níveis (Auditoria da Tabela de Níveis).
/// </summary>
public static class AttributePointBudgetCalculator
{
    public static int Compute(int nivel, ProgressaoDeNivel tabela) =>
        tabela.Acumulado(ChavesDeNivel.PontosDeAtributo, nivel);
}
