using FluentAssertions;
using RuinaRPG.Domain.SpellsAndAbilities;
using Xunit;

namespace RuinaRPG.Tests.Unit.SpellsAndAbilities;

public class SpellAbilityEffectSummaryFormatterTests
{
    [Fact]
    public void Dano_shows_the_die_size_for_the_entrys_Grau()
    {
        var resumo = SpellAbilityEffectSummaryFormatter.Formatar(3, [("Dano", 3)]);

        resumo.Should().Be("Dano: 3d6");
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 6)]
    [InlineData(4, 8)]
    [InlineData(5, 10)]
    [InlineData(6, 12)]
    [InlineData(7, 20)]
    [InlineData(8, 30)]
    [InlineData(9, 100)]
    public void Dado_por_Grau_matches_the_GRAUS_e_CIRCULOS_table(int grau, int dado)
    {
        SpellAbilityEffectSummaryFormatter.Formatar(grau, [("Dano", 1)]).Should().Be($"Dano: 1d{dado}");
    }

    [Fact]
    public void Duracao_also_shows_the_die_size_for_the_entrys_Grau()
    {
        var resumo = SpellAbilityEffectSummaryFormatter.Formatar(3, [("Duração", 2)]);

        resumo.Should().Be("Duração: 2d6");
    }

    [Fact]
    public void Every_other_effect_with_a_Quantidade_shows_just_the_number()
    {
        var resumo = SpellAbilityEffectSummaryFormatter.Formatar(3, [("Alcance", 4), ("Área", 1)]);

        resumo.Should().Be("Alcance: 4, Área: 1");
    }

    [Fact]
    public void An_effect_with_no_Quantidade_shows_only_its_name()
    {
        var resumo = SpellAbilityEffectSummaryFormatter.Formatar(1, [("Contrato Mágico", null)]);

        resumo.Should().Be("Contrato Mágico");
    }

    [Fact]
    public void Multiple_effects_join_with_a_comma()
    {
        var resumo = SpellAbilityEffectSummaryFormatter.Formatar(3, [("Duração", 2), ("Dano", 3), ("Cura", null)]);

        resumo.Should().Be("Duração: 2d6, Dano: 3d6, Cura");
    }

    [Fact]
    public void No_effects_produces_an_empty_string()
    {
        SpellAbilityEffectSummaryFormatter.Formatar(1, []).Should().Be("");
    }
}
