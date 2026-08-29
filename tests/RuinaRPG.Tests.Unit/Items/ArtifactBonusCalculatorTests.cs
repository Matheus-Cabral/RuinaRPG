using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Tests.Unit.Items;

public class ArtifactBonusCalculatorTests
{
    [Fact]
    public void Sum_adds_only_artefatos_matching_both_TipoDeAlvo_and_Alvo()
    {
        var artefatos = new[]
        {
            new ArtifactBonusInput(TipoDeAlvo.Atributo, "Forca", 3),
            new ArtifactBonusInput(TipoDeAlvo.Atributo, "Forca", 2),
            new ArtifactBonusInput(TipoDeAlvo.Atributo, "Vigor", 5), // different Alvo — excluded
            new ArtifactBonusInput(TipoDeAlvo.SubAtributo, "Forca", 100), // different TipoDeAlvo — excluded
        };

        ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, "Forca").Should().Be(5);
    }

    [Fact]
    public void Sum_matches_Alvo_case_insensitively()
    {
        var artefatos = new[] { new ArtifactBonusInput(TipoDeAlvo.SubAtributo, "iniciativa", 4) };

        ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Iniciativa).Should().Be(4);
    }

    [Fact]
    public void Sum_of_no_matches_is_zero()
    {
        ArtifactBonusCalculator.Sum(Array.Empty<ArtifactBonusInput>(), TipoDeAlvo.Pericia, "Atletismo").Should().Be(0);
    }

    [Fact]
    public void Sum_treats_a_null_Alvo_as_never_matching()
    {
        var artefatos = new[] { new ArtifactBonusInput(TipoDeAlvo.Atributo, null, 10) };

        ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, "Forca").Should().Be(0);
    }
}
