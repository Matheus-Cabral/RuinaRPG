using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;

namespace RuinaRPG.Tests.Unit.Condicoes;

public class CondicaoValidatorTests
{
    private static bool Existe(string chave) => chave == "Furtividade";

    private static ModificadorDeCondicao Mod(AlvoDeCondicao alvo, string? especifico, int valor = -2, TipoDeValorDeCondicao tipo = TipoDeValorDeCondicao.Fixo, MomentoDeCondicao? momento = null) =>
        new(alvo, especifico, tipo, valor, momento);

    private static string? Validar(string nome = "Envenenado", ModificadorDeCondicao[]? mods = null, CategoriaDesabilitavel[]? desab = null, int? duracao = null) =>
        CondicaoValidator.Validar(nome, mods ?? [Mod(AlvoDeCondicao.Atributo, "Forca")], desab ?? [], duracao, Existe);

    [Fact]
    public void A_fully_valid_multi_line_condition_passes()
    {
        var mods = new[]
        {
            Mod(AlvoDeCondicao.Atributo, "Vigor"),
            Mod(AlvoDeCondicao.Pericia, null, -1),
            Mod(AlvoDeCondicao.Pericia, "Furtividade", -1),
            Mod(AlvoDeCondicao.SubAtributo, SubAtributoAlvo.Movimentacao, -50, TipoDeValorDeCondicao.Percentual),
            Mod(AlvoDeCondicao.RecursoTotal, "Vitalidade", -10, TipoDeValorDeCondicao.Percentual),
            Mod(AlvoDeCondicao.RecursoAtual, "Vitalidade", -3, momento: MomentoDeCondicao.ACadaVez),
            Mod(AlvoDeCondicao.AfinidadeElemental, null, 1),
            Mod(AlvoDeCondicao.DanoElemental, "Fogo", 2),
            Mod(AlvoDeCondicao.Dano, "Arcano", 1),
            Mod(AlvoDeCondicao.Maestria, null, 1),
        };

        Validar(mods: mods, desab: [CategoriaDesabilitavel.Magia, CategoriaDesabilitavel.Runa], duracao: 3).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_name_is_rejected(string nome) => Validar(nome).Should().NotBeNull();

    [Fact]
    public void A_condition_without_modifiers_or_disables_is_rejected() =>
        Validar(mods: [], desab: []).Should().NotBeNull();

    [Fact]
    public void A_condition_with_only_a_disable_is_valid() =>
        Validar(mods: [], desab: [CategoriaDesabilitavel.Arma]).Should().BeNull();

    [Fact]
    public void Momento_is_required_for_RecursoAtual() =>
        Validar(mods: [Mod(AlvoDeCondicao.RecursoAtual, "Foco")]).Should().NotBeNull();

    [Fact]
    public void Momento_is_rejected_for_other_targets() =>
        Validar(mods: [Mod(AlvoDeCondicao.RecursoTotal, "Foco", momento: MomentoDeCondicao.AoAplicar)]).Should().NotBeNull();

    [Fact]
    public void A_zero_value_is_rejected() =>
        Validar(mods: [Mod(AlvoDeCondicao.Atributo, "Forca", 0)]).Should().NotBeNull();

    [Theory]
    [InlineData(-101)]
    [InlineData(1001)]
    public void A_percentage_outside_the_range_is_rejected(int valor) =>
        Validar(mods: [Mod(AlvoDeCondicao.Atributo, "Forca", valor, TipoDeValorDeCondicao.Percentual)]).Should().NotBeNull();

    [Theory]
    [InlineData(-100)]
    [InlineData(1000)]
    public void A_percentage_at_the_limits_is_accepted(int valor) =>
        Validar(mods: [Mod(AlvoDeCondicao.Atributo, "Forca", valor, TipoDeValorDeCondicao.Percentual)]).Should().BeNull();

    [Fact]
    public void A_fixed_value_beyond_the_percentage_range_is_accepted() =>
        Validar(mods: [Mod(AlvoDeCondicao.Atributo, "Forca", 5000)]).Should().BeNull();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_default_duration_below_one_is_rejected(int duracao) => Validar(duracao: duracao).Should().NotBeNull();

    [Fact]
    public void A_duplicate_disable_category_is_rejected() =>
        Validar(desab: [CategoriaDesabilitavel.Arma, CategoriaDesabilitavel.Arma]).Should().NotBeNull();

    [Theory]
    [InlineData(AlvoDeCondicao.Atributo, null)]
    [InlineData(AlvoDeCondicao.Atributo, "Sorte")]
    [InlineData(AlvoDeCondicao.Atributo, "1")]
    [InlineData(AlvoDeCondicao.Pericia, "Inexistente")]
    [InlineData(AlvoDeCondicao.SubAtributo, null)]
    [InlineData(AlvoDeCondicao.SubAtributo, "Carisma")]
    [InlineData(AlvoDeCondicao.RecursoTotal, null)]
    [InlineData(AlvoDeCondicao.RecursoTotal, "Mana")]
    [InlineData(AlvoDeCondicao.AfinidadeElemental, "Luz")]
    [InlineData(AlvoDeCondicao.DanoElemental, "Luz")]
    [InlineData(AlvoDeCondicao.Dano, "Sagrado")]
    [InlineData(AlvoDeCondicao.Maestria, "Inexistente")]
    public void An_invalid_AlvoEspecifico_is_rejected(AlvoDeCondicao alvo, string? especifico) =>
        Validar(mods: [Mod(alvo, especifico)]).Should().NotBeNull();

    [Fact]
    public void RecursoAtual_requires_a_resource_name() =>
        Validar(mods: [Mod(AlvoDeCondicao.RecursoAtual, null, momento: MomentoDeCondicao.AoAplicar)]).Should().NotBeNull();

    [Theory]
    [InlineData(AlvoDeCondicao.Pericia)]
    [InlineData(AlvoDeCondicao.AfinidadeElemental)]
    [InlineData(AlvoDeCondicao.DanoElemental)]
    [InlineData(AlvoDeCondicao.Dano)]
    [InlineData(AlvoDeCondicao.Maestria)]
    public void A_null_AlvoEspecifico_means_all_where_allowed(AlvoDeCondicao alvo) =>
        Validar(mods: [Mod(alvo, null)]).Should().BeNull();
}
