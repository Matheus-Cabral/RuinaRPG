using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class RacialTraitChoiceResolverTests
{
    private static readonly RacialTraitSlots SlotsWithObrigatoriaChoice = new(
        Gratuita: [new("Coragem"), new("Imunidade de Venenos (2 pontos)"), new("Sentidos Aguçados")],
        Obrigatoria: [new("Código de Honra"), new("Crédulo")]);

    private static readonly RacialTraitSlots SlotsWithNoObrigatoria = new(
        Gratuita: [new("Alfabetizado"), new("Sedutor"), new("Aparência Inofensiva (2 pontos)")],
        Obrigatoria: []);

    private static readonly RacialTraitSlots SlotsWithFixedObrigatoria = new(
        Gratuita: [new("Detectar Magia"), new("Amado por feras")],
        Obrigatoria: [new("Desvantagem Elemental", "Fogo")]);

    [Fact]
    public void ExpectedGrantCount_is_1_when_there_is_no_Obrigatoria_slot()
    {
        RacialTraitChoiceResolver.ExpectedGrantCount(SlotsWithNoObrigatoria).Should().Be(1);
    }

    [Fact]
    public void ExpectedGrantCount_is_2_when_an_Obrigatoria_slot_exists()
    {
        RacialTraitChoiceResolver.ExpectedGrantCount(SlotsWithFixedObrigatoria).Should().Be(2);
        RacialTraitChoiceResolver.ExpectedGrantCount(SlotsWithObrigatoriaChoice).Should().Be(2);
    }

    [Fact]
    public void IsResolved_compares_existing_count_against_ExpectedGrantCount()
    {
        RacialTraitChoiceResolver.IsResolved(SlotsWithObrigatoriaChoice, existingCount: 1).Should().BeFalse();
        RacialTraitChoiceResolver.IsResolved(SlotsWithObrigatoriaChoice, existingCount: 2).Should().BeTrue();
        RacialTraitChoiceResolver.IsResolved(SlotsWithNoObrigatoria, existingCount: 1).Should().BeTrue();
        RacialTraitChoiceResolver.IsResolved(SlotsWithNoObrigatoria, existingCount: 0).Should().BeFalse();
    }

    [Fact]
    public void Resolve_accepts_a_valid_Gratuita_pick_and_a_valid_Obrigatoria_pick()
    {
        var result = RacialTraitChoiceResolver.Resolve(SlotsWithObrigatoriaChoice, "Imunidade de Venenos (2 pontos)", "Crédulo");

        result.Error.Should().BeNull();
        result.Grants.Should().BeEquivalentTo(new[] { new RacialTraitOption("Imunidade de Venenos (2 pontos)"), new RacialTraitOption("Crédulo") });
    }

    [Fact]
    public void Resolve_rejects_a_Gratuita_pick_not_among_the_options()
    {
        var result = RacialTraitChoiceResolver.Resolve(SlotsWithObrigatoriaChoice, "Alfabetizado", "Crédulo");

        result.Error.Should().NotBeNull();
        result.Grants.Should().BeNull();
    }

    [Fact]
    public void Resolve_rejects_an_Obrigatoria_pick_not_among_the_options_when_there_is_a_real_choice()
    {
        var result = RacialTraitChoiceResolver.Resolve(SlotsWithObrigatoriaChoice, "Coragem", "Curioso");

        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_requires_an_Obrigatoria_pick_when_there_is_a_real_choice()
    {
        var result = RacialTraitChoiceResolver.Resolve(SlotsWithObrigatoriaChoice, "Coragem", obrigatoriaTraitNome: null);

        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_ignores_any_Obrigatoria_pick_when_there_is_no_such_slot()
    {
        var result = RacialTraitChoiceResolver.Resolve(SlotsWithNoObrigatoria, "Sedutor", obrigatoriaTraitNome: "Anything");

        result.Error.Should().BeNull();
        result.Grants.Should().BeEquivalentTo(new[] { new RacialTraitOption("Sedutor") });
    }

    [Fact]
    public void Resolve_auto_grants_the_single_fixed_Obrigatoria_option_regardless_of_input()
    {
        var result = RacialTraitChoiceResolver.Resolve(SlotsWithFixedObrigatoria, "Amado por feras", obrigatoriaTraitNome: null);

        result.Error.Should().BeNull();
        result.Grants.Should().BeEquivalentTo(new[] { new RacialTraitOption("Amado por feras"), new RacialTraitOption("Desvantagem Elemental", "Fogo") });
    }
}
