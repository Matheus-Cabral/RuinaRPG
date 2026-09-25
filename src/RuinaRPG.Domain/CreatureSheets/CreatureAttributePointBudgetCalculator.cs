using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Domain.CreatureSheets;

/// <summary>
/// Ficha de Criaturas R0005 2.a: a Criatura começa com os Pontos de Atributo do seu Rank no lugar
/// dos "+9 Pontos de Atributo" do Nível 1 da Tabela de Níveis, e dali em diante ganha pontos pela
/// mesma tabela do Personagem/NPC. Sem Rank, vale a tabela pura (mesma conta do NPC).
/// </summary>
public static class CreatureAttributePointBudgetCalculator
{
    public static int Compute(Rank? rank, int nivel, IReadOnlyList<LevelBonus> niveis)
    {
        var pelaTabela = AttributePointBudgetCalculator.Compute(nivel, niveis);
        if (rank is null)
            return pelaTabela;

        return PontosIniciais(rank.Value) + pelaTabela - AttributePointBudgetCalculator.Compute(nivel: 1, niveis);
    }

    private static int PontosIniciais(Rank rank) => rank switch
    {
        Rank.F => 6,
        Rank.E => 7,
        Rank.D => 8,
        Rank.C => 9,
        Rank.B => 10,
        Rank.A => 12,
        Rank.S => 14,
        _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, null),
    };
}
