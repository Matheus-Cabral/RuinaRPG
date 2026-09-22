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
    public void Modificador_adds_the_Historico_bonus_on_top_of_gasto_divided_by_3()
    {
        SkillFormulas.Modificador(gasto: 9, historicoBonus: 6).Should().Be(9); // 3 + 6
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
