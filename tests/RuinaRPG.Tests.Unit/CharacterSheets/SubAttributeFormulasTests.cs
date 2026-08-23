using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class SubAttributeFormulasTests
{
    [Fact]
    public void Iniciativa_sums_agilidade_bruto_prontidao_and_artefato_ou_item()
    {
        // "Iniciativa = Agilidade + Bruto Prontidão + Artefato ou item" — 2.b.
        SubAttributeFormulas.Iniciativa(agilidade: 5, brutoProntidao: 2, artefatoOuItem: 1).Should().Be(8);
    }

    [Fact]
    public void Movimentacao_applies_the_formula_with_no_sobrepeso()
    {
        // "Movimentação = (Agilidade × 2) + Artefato − Sobrepeso", Sobrepeso = max(0, Peso Total −
        // Limite de Carga), Limite de Carga = piso((Força + Vigor) / 2) — 2.b.
        // Limite de Carga = floor((10+10)/2) = 10; Peso Total 5 <= 10 → Sobrepeso 0.
        var result = SubAttributeFormulas.Movimentacao(agilidade: 4, artefato: 0, pesoTotalCarregado: 5, forca: 10, vigor: 10);

        result.Should().Be(8); // (4*2) + 0 - 0
    }

    [Fact]
    public void Movimentacao_subtracts_sobrepeso_when_carried_weight_exceeds_the_limit()
    {
        // Limite de Carga = floor((4+4)/2) = 4; Peso Total 10 → Sobrepeso = 10 - 4 = 6.
        var result = SubAttributeFormulas.Movimentacao(agilidade: 4, artefato: 0, pesoTotalCarregado: 10, forca: 4, vigor: 4);

        result.Should().Be(2); // (4*2) + 0 - 6 = 2, above the floor of 1
    }

    [Fact]
    public void Movimentacao_never_goes_below_the_absolute_minimum_of_1()
    {
        // Limite de Carga = floor((2+2)/2) = 2; Peso Total 100 → Sobrepeso huge → formula goes deeply
        // negative, but "mínimo absoluto de 1" clamps it.
        var result = SubAttributeFormulas.Movimentacao(agilidade: 1, artefato: 0, pesoTotalCarregado: 100, forca: 2, vigor: 2);

        result.Should().Be(1);
    }

    [Fact]
    public void EsquivaNatural_subtracts_armor_penalty()
    {
        // "Esquiva Natural = Agilidade + Bruto Reflexos + Artefatos − Penalidade de armadura" — 2.b.
        SubAttributeFormulas.EsquivaNatural(agilidade: 5, brutoReflexos: 2, artefatos: 1, penalidadeArmadura: 3).Should().Be(5);
    }

    [Fact]
    public void DefesaNatural_sums_every_term_including_cobertura()
    {
        // "Defesa Natural = Vigor + Bruto Fortitude + Escudo + Artefatos + Cobertura" — 2.b.
        SubAttributeFormulas.DefesaNatural(vigor: 5, brutoFortitude: 2, escudo: 3, artefatos: 1, cobertura: 5).Should().Be(16);
    }

    [Fact]
    public void ReducaoFisica_and_ReducaoMagica_sum_artefato_and_armor()
    {
        // "Redução Física = Artefato + Armadura" / "Redução Mágica = Artefato + Armadura mágica" — 2.b.
        SubAttributeFormulas.ReducaoFisica(artefato: 2, armadura: 3).Should().Be(5);
        SubAttributeFormulas.ReducaoMagica(artefato: 1, armaduraMagica: 4).Should().Be(5);
    }
}
