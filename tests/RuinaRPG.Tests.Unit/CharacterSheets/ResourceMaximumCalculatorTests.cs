using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class ResourceMaximumCalculatorTests
{
    [Fact]
    public void Vitalidade_is_vigor_times_2_plus_status_de_classe_vida()
    {
        // "Vitalidade = (Vigor × 2) + Status de classe Vida" — Formulas.md.
        ResourceMaximumCalculator.Vitalidade(vigor: 8, statusDeClasseVida: 12).Should().Be(28);
    }

    [Fact]
    public void Foco_is_astucia_times_2_plus_status_de_classe_foco()
    {
        // "Foco = Astúcia × 2 + Status de classe foco" — Formulas.md.
        ResourceMaximumCalculator.Foco(astucia: 6, statusDeClasseFoco: 8).Should().Be(20);
    }

    [Fact]
    public void Adrenalina_is_10_plus_artefato()
    {
        // "Adrenalina = 10 + Artefato" — Formulas.md.
        ResourceMaximumCalculator.Adrenalina(artefatoBonus: 3).Should().Be(13);
        ResourceMaximumCalculator.Adrenalina(artefatoBonus: 0).Should().Be(10);
    }

    [Fact]
    public void Estresse_is_a_flat_10()
    {
        ResourceMaximumCalculator.Estresse().Should().Be(10);
    }
}
