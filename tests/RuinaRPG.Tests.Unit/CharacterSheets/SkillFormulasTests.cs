using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class SkillFormulasTests
{
    [Fact]
    public void Modificador_divides_gasto_by_3_rounded_down()
    {
        // "Modificador = Gasto ÷ 3 (arredondado para baixo)" — Sistema Básico §2.
        SkillFormulas.Modificador(gasto: 8, historicoBonus: 0).Should().Be(2);
        SkillFormulas.Modificador(gasto: 9, historicoBonus: 0).Should().Be(3);
        SkillFormulas.Modificador(gasto: 0, historicoBonus: 0).Should().Be(0);
    }

    [Fact]
    public void Modificador_counts_the_Historico_bonus_as_Pericia_points_spent_not_as_raw_Modificador_points()
    {
        // Histórico's raw bonus (6 or 3) is a multiple of 3, so it is folded into (Gasto + bonus) / 3
        // BEFORE the division — it counts as skill points, not as points added on top of the division.
        // Modificador = (Gasto + historicoBonus) / 3, arredondado para baixo.
        SkillFormulas.Modificador(gasto: 9, historicoBonus: 6).Should().Be(5); // (9+6)/3 = 5, not 3+6=9
        SkillFormulas.Modificador(gasto: 0, historicoBonus: 6).Should().Be(2); // (0+6)/3 = 2
        SkillFormulas.Modificador(gasto: 0, historicoBonus: 3).Should().Be(1); // (0+3)/3 = 1
        SkillFormulas.Modificador(gasto: 2, historicoBonus: 3).Should().Be(1); // (2+3)/3 = 1 (floor)
        SkillFormulas.Modificador(gasto: 4, historicoBonus: 6).Should().Be(3); // (4+6)/3 = 3 (floor)
        SkillFormulas.Modificador(gasto: 9, historicoBonus: 0).Should().Be(3); // no Histórico -> gasto/3
    }

    [Fact]
    public void Total_adds_modificador_and_the_chosen_atributo_total()
    {
        // "Total = Modificador + Total do Atributo escolhido + Artefato(s)" — Ficha de Personagem
        // 2.d, corrected to include the Artefato term R0009/5.b's Tipo de Alvo = Perícia feeds.
        SkillFormulas.Total(modificador: 3, atributoTotal: 6, artefatos: 0).Should().Be(9);
    }

    [Fact]
    public void Total_adds_the_artefato_bonus_targeting_this_pericia()
    {
        SkillFormulas.Total(modificador: 3, atributoTotal: 6, artefatos: 2).Should().Be(11);
    }
}
