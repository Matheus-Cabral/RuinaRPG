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

    [Fact]
    public void EficienciaElemental_and_DanoElemental_pass_the_value_through_1_to_1()
    {
        // 1:1 por ora — cada um é sua própria função porque a proporção pode divergir no futuro
        // (ver docs/superpowers/specs/2026-09-15-automatizar-afinidades-design.md).
        SubAttributeFormulas.EficienciaElemental(valorDaAfinidadeCorrespondente: 5).Should().Be(5);
        SubAttributeFormulas.DanoElemental(valorDaAfinidadeCorrespondente: 5).Should().Be(5);
    }

    [Fact]
    public void ValorDaAfinidadeCorrespondente_is_0_when_Afinidade_is_null()
    {
        var linhas = new List<LinhaDeAfinidade> { new(Elemento.Fogo, 7, null, null) };

        SubAttributeFormulas.ValorDaAfinidadeCorrespondente(null, linhas).Should().Be(0);
    }

    [Fact]
    public void ValorDaAfinidadeCorrespondente_finds_the_matching_Elemento_row()
    {
        var linhas = new List<LinhaDeAfinidade> { new(Elemento.Fogo, 7, SubElemento.Vida, 2) };

        SubAttributeFormulas.ValorDaAfinidadeCorrespondente(AfinidadeElemental.Fogo, linhas).Should().Be(7);
    }

    [Fact]
    public void ValorDaAfinidadeCorrespondente_finds_the_matching_SubElemento_row()
    {
        var linhas = new List<LinhaDeAfinidade> { new(Elemento.Fogo, 7, SubElemento.Vida, 2) };

        SubAttributeFormulas.ValorDaAfinidadeCorrespondente(AfinidadeElemental.Vida, linhas).Should().Be(2);
    }

    [Fact]
    public void ValorDaAfinidadeCorrespondente_is_0_when_no_row_matches()
    {
        var linhas = new List<LinhaDeAfinidade> { new(Elemento.Fogo, 7, null, null) };

        SubAttributeFormulas.ValorDaAfinidadeCorrespondente(AfinidadeElemental.Terra, linhas).Should().Be(0);
    }

    [Fact]
    public void ValorDaAfinidadeCorrespondente_is_0_with_an_empty_list_of_linhas()
    {
        SubAttributeFormulas.ValorDaAfinidadeCorrespondente(AfinidadeElemental.Fogo, []).Should().Be(0);
    }

    [Fact]
    public void ValorDaAfinidadeCorrespondente_uses_the_highest_Valor_among_rows_with_that_Elemento_regardless_of_order()
    {
        var comValorPrimeiro = new List<LinhaDeAfinidade>
        {
            new(Elemento.Fogo, 5, SubElemento.Curar, null),
            new(Elemento.Fogo, null, SubElemento.Necromancia, null),
        };
        var comValorPorUltimo = new List<LinhaDeAfinidade> { comValorPrimeiro[1], comValorPrimeiro[0] };

        SubAttributeFormulas.ValorDaAfinidadeCorrespondente(AfinidadeElemental.Fogo, comValorPrimeiro).Should().Be(5);
        SubAttributeFormulas.ValorDaAfinidadeCorrespondente(AfinidadeElemental.Fogo, comValorPorUltimo).Should().Be(5);
    }

    [Fact]
    public void ValorDaAfinidadeCorrespondente_picks_the_max_when_two_rows_share_the_Elemento()
    {
        var linhas = new List<LinhaDeAfinidade>
        {
            new(Elemento.Fogo, 2, SubElemento.Curar, null),
            new(Elemento.Fogo, 7, SubElemento.Necromancia, null),
        };

        SubAttributeFormulas.ValorDaAfinidadeCorrespondente(AfinidadeElemental.Fogo, linhas).Should().Be(7);
    }
}
