using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class RacialTraitLookupTests
{
    // Real text from Ruína RPG - Sistema Básico.md §7.
    [Fact]
    public void Sinir_and_Laonir_have_a_3_way_Gratuita_choice_and_no_Obrigatoria()
    {
        foreach (var variante in new[] { Variante.Sinir, Variante.Laonir })
        {
            var result = RacialTraitLookup.For(variante);

            result.Gratuita.Select(o => o.TraitNome).Should().BeEquivalentTo("Alfabetizado", "Sedutor", "Aparência Inofensiva");
            result.Obrigatoria.Should().BeEmpty();
        }
    }

    [Fact]
    public void PhylacTai_and_EsPhylauc_have_a_Gratuita_and_an_Obrigatoria_choice()
    {
        foreach (var variante in new[] { Variante.PhylacTai, Variante.EsPhylauc })
        {
            var result = RacialTraitLookup.For(variante);

            result.Gratuita.Select(o => o.TraitNome).Should().BeEquivalentTo("Coragem", "Imunidade de Venenos", "Sentidos Aguçados");
            result.Obrigatoria.Select(o => o.TraitNome).Should().BeEquivalentTo("Código de Honra", "Crédulo");
        }
    }

    [Fact]
    public void Yavos_has_Curioso_or_Covarde_as_Obrigatoria()
    {
        var result = RacialTraitLookup.For(Variante.Yavos);

        result.Gratuita.Select(o => o.TraitNome).Should().BeEquivalentTo("Saque Rápido", "Visão Noturna");
        result.Obrigatoria.Select(o => o.TraitNome).Should().BeEquivalentTo("Curioso", "Covarde");
    }

    [Fact]
    public void Koroanos_has_Distracao_or_Covarde_as_Obrigatoria()
    {
        var result = RacialTraitLookup.For(Variante.Koroanos);

        result.Gratuita.Select(o => o.TraitNome).Should().BeEquivalentTo("Saque Rápido", "Visão Noturna");
        result.Obrigatoria.Select(o => o.TraitNome).Should().BeEquivalentTo("Distração", "Covarde");
    }

    [Fact]
    public void Alora_has_a_Gratuita_choice_and_a_single_fixed_Obrigatoria_with_Especificacao_Fogo()
    {
        var result = RacialTraitLookup.For(Variante.Alora);

        result.Gratuita.Select(o => o.TraitNome).Should().BeEquivalentTo("Detectar Magia", "Amado por feras");
        result.Obrigatoria.Should().ContainSingle();
        result.Obrigatoria[0].TraitNome.Should().Be("Desvantagem Elemental");
        result.Obrigatoria[0].Especificacao.Should().Be("Fogo");
    }
}
