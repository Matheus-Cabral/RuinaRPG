using System.Text.RegularExpressions;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Atributos 2.a: "A soma de Gasto de todos os 8 atributos ... não pode ultrapassar o total de
/// pontos que o personagem já recebeu (criação + níveis)." Tabela de Níveis' own level-1 row
/// already grants "+9 Pontos de Atributo" — the same 9 the requirement calls "pontos de
/// distribuição inicial da criação de personagem" — so creation isn't a separate flat add-on, it's
/// just level 1's row read like every other level's. "Pontos de Atributo" lives only as free text
/// inside the Bônus column (e.g. "+9 Pontos de Atributo  &lt;br&gt;+Status de Vida Aprimorado
/// ..."), so it's extracted with a regex rather than a dedicated table column.
/// </summary>
public static partial class AttributePointBudgetCalculator
{
    public static int Compute(int nivel, IReadOnlyList<LevelBonus> niveis) =>
        niveis
            .Where(n => n.Nivel <= nivel)
            .Sum(n => PontosDeAtributoRegex().Matches(n.BonusText).Sum(m => int.Parse(m.Groups[1].Value)));

    // IgnoreCase: the source table capitalizes "Atributo" inconsistently (e.g. Nível 10's own row
    // spells it "Pontos de atributo", lowercase) — a case-sensitive match silently dropped every
    // lowercase occurrence.
    [GeneratedRegex(@"\+(\d+)\s+Pontos?\s+de\s+Atributo\b", RegexOptions.IgnoreCase)]
    private static partial Regex PontosDeAtributoRegex();
}
