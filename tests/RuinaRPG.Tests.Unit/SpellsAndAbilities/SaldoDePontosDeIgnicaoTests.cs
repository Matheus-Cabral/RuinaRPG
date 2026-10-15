using FluentAssertions;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Tests.Unit.SpellsAndAbilities;

public class SaldoDePontosDeIgnicaoTests
{
    [Fact]
    public void A_cost_within_the_balance_has_no_error() =>
        SaldoDePontosDeIgnicao.ErroAoDebitar(saldo: 20, gasto: 14).Should().BeNull();

    [Fact]
    public void A_cost_equal_to_the_balance_has_no_error() =>
        SaldoDePontosDeIgnicao.ErroAoDebitar(saldo: 14, gasto: 14).Should().BeNull();

    [Fact]
    public void A_cost_above_the_balance_reports_both_numbers() =>
        SaldoDePontosDeIgnicao.ErroAoDebitar(saldo: 13, gasto: 14)
            .Should().Be("Pontos de Ignição insuficientes: custa 14 e a ficha tem 13.");

    [Fact]
    public void A_zero_or_negative_cost_never_errors_even_with_an_empty_balance()
    {
        SaldoDePontosDeIgnicao.ErroAoDebitar(saldo: 0, gasto: 0).Should().BeNull();
        SaldoDePontosDeIgnicao.ErroAoDebitar(saldo: 0, gasto: -5).Should().BeNull();
    }

    [Fact]
    public void Debitar_subtracts_and_a_negative_cost_refunds()
    {
        SaldoDePontosDeIgnicao.Debitar(saldo: 20, gasto: 14).Should().Be(6);
        SaldoDePontosDeIgnicao.Debitar(saldo: 6, gasto: -14).Should().Be(20);
    }

    [Theory]
    [InlineData(SpellAbilityTipo.Magia, true)]
    [InlineData(SpellAbilityTipo.Habilidade, true)]
    [InlineData(SpellAbilityTipo.Passiva, false)]
    [InlineData(SpellAbilityTipo.Racial, false)]
    public void Only_Magia_and_Habilidade_consume_the_balance(SpellAbilityTipo tipo, bool consome) =>
        SaldoDePontosDeIgnicao.Consome(tipo).Should().Be(consome);
}
