using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class SubAttributeFormulasTests
{
    private static readonly LinhaDaTabelaDeAfinidades[] TabelaComLacunas =
    {
        new(10, 5, 5), new(3, 2, 1), new(5, 3, 2), // fora de ordem de propósito
    };

    [Theory]
    [InlineData(3, 2, 1)]   // valor exato
    [InlineData(4, 2, 1)]   // entre linhas: vale a maior que não ultrapassa
    [InlineData(7, 3, 2)]   // lacuna entre 5 e 10
    [InlineData(10, 5, 5)]  // última linha
    [InlineData(99, 5, 5)]  // acima da tabela: valores máximos
    [InlineData(2, 0, 0)]   // abaixo da menor linha
    [InlineData(-1, 0, 0)]  // negativo
    public void EficienciaElemental_and_DanoElemental_come_from_the_highest_row_not_above_the_value(int valor, int eficiencia, int dano)
    {
        SubAttributeFormulas.EficienciaElemental(valor, TabelaComLacunas).Should().Be(eficiencia);
        SubAttributeFormulas.DanoElemental(valor, TabelaComLacunas).Should().Be(dano);
    }

    [Fact]
    public void EficienciaElemental_and_DanoElemental_are_0_with_an_empty_table()
    {
        SubAttributeFormulas.EficienciaElemental(7, Array.Empty<LinhaDaTabelaDeAfinidades>()).Should().Be(0);
        SubAttributeFormulas.DanoElemental(7, Array.Empty<LinhaDaTabelaDeAfinidades>()).Should().Be(0);
    }

    [Fact]
    public void Iniciativa_sums_agilidade_bruto_prontidao_and_artefato_ou_item()
    {
        // "Iniciativa = Agilidade + Bruto Prontidão + Artefato ou item" — 2.b.
        SubAttributeFormulas.Iniciativa(agilidade: 5, brutoProntidao: 2, artefatoOuItem: 1).Should().Be(8);
    }

    [Fact]
    public void Movimentacao_applies_the_formula_with_no_sobrepeso()
    {
        // "Movimentação = (Agilidade × 2) + Artefato − Sobrepeso" — 2.b. PesoAtual 5 <= PesoMaximo
        // 10 → Sobrepeso 0.
        var result = SubAttributeFormulas.Movimentacao(agilidade: 4, artefato: 0, pesoAtual: 5m, pesoMaximo: 10m);

        result.Should().Be(8); // (4*2) + 0 - 0
    }

    [Fact]
    public void Movimentacao_subtracts_sobrepeso_when_carried_weight_exceeds_the_limit()
    {
        // PesoAtual 10, PesoMaximo 4 → Sobrepeso = 10 - 4 = 6.
        var result = SubAttributeFormulas.Movimentacao(agilidade: 4, artefato: 0, pesoAtual: 10m, pesoMaximo: 4m);

        result.Should().Be(2); // (4*2) + 0 - 6 = 2, above the floor of 1
    }

    [Fact]
    public void Movimentacao_never_goes_below_the_absolute_minimum_of_1()
    {
        var result = SubAttributeFormulas.Movimentacao(agilidade: 1, artefato: 0, pesoAtual: 100m, pesoMaximo: 2m);

        result.Should().Be(1);
    }

    [Fact]
    public void Movimentacao_rounds_a_fractional_sobrepeso_up_rather_than_truncating()
    {
        // PesoAtual 5.5, PesoMaximo 5 → Sobrepeso 0.5, rounded UP to 1 (not truncated to 0) — half a
        // kilo over the limit still costs a point. This is the precision fix over the old code's
        // `(int)` cast, which would have silently discarded the 0.5 and reported no penalty at all.
        var result = SubAttributeFormulas.Movimentacao(agilidade: 4, artefato: 0, pesoAtual: 5.5m, pesoMaximo: 5m);

        result.Should().Be(7); // (4*2) + 0 - 1
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

    // Ids do catálogo de elementos: Fogo = 2, Terra = 0, Vida = 14.
    private static readonly Dictionary<int, int> Pontos = new() { [2] = 7, [14] = 2 };

    [Fact]
    public void ValorDaAfinidadeCorrespondente_is_0_when_Afinidade_is_null() =>
        SubAttributeFormulas.ValorDaAfinidadeCorrespondente(null, Pontos).Should().Be(0);

    [Fact]
    public void ValorDaAfinidadeCorrespondente_is_the_points_of_the_chosen_element() =>
        SubAttributeFormulas.ValorDaAfinidadeCorrespondente(2, Pontos).Should().Be(7);

    [Fact]
    public void ValorDaAfinidadeCorrespondente_works_for_any_catalog_element() =>
        SubAttributeFormulas.ValorDaAfinidadeCorrespondente(14, Pontos).Should().Be(2);

    [Fact]
    public void ValorDaAfinidadeCorrespondente_is_0_when_the_element_has_no_points() =>
        SubAttributeFormulas.ValorDaAfinidadeCorrespondente(0, Pontos).Should().Be(0);
}
