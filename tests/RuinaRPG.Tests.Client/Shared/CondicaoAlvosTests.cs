using FluentAssertions;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Items;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class CondicaoAlvosTests
{
    private static readonly PericiaResponse[] Pericias =
    [
        new(1, "Atletismo", "Atletismo", null, null, true, false),
        new(2, "Furtividade", "Furtividade", null, null, true, false)
    ];

    private static List<string> Valores(AlvoDeCondicao alvo) => CondicaoAlvos.Especificos(alvo, Pericias).Select(o => o.Valor).ToList();

    [Fact]
    public void Every_Alvo_has_a_portuguese_label()
    {
        CondicaoAlvos.Alvos.Select(a => a.Valor).Should().BeEquivalentTo(Enum.GetNames<AlvoDeCondicao>());
        CondicaoAlvos.Alvos.Single(a => a.Valor == nameof(AlvoDeCondicao.RecursoAtual)).Rotulo.Should().Be("Valor atual de um recurso");
    }

    [Fact]
    public void Atributo_offers_exactly_the_eight_attributes_with_accents_and_no_general_choice()
    {
        var opcoes = CondicaoAlvos.Especificos(AlvoDeCondicao.Atributo, Pericias);
        opcoes.Select(o => o.Valor).Should().BeEquivalentTo(Enum.GetNames<Atributo>());
        opcoes.Single(o => o.Valor == "Forca").Rotulo.Should().Be("Força");
        CondicaoAlvos.ExigeEspecifico(AlvoDeCondicao.Atributo).Should().BeTrue();
    }

    [Theory]
    [InlineData(AlvoDeCondicao.Pericia)]
    [InlineData(AlvoDeCondicao.Maestria)]
    public void Pericia_and_Maestria_offer_Todas_then_every_active_Pericia(AlvoDeCondicao alvo)
    {
        var opcoes = CondicaoAlvos.Especificos(alvo, Pericias);
        opcoes.Select(o => o.Valor).Should().Equal("", "Atletismo", "Furtividade");
        opcoes[0].Rotulo.Should().Be("Todas");
        CondicaoAlvos.ExigeEspecifico(alvo).Should().BeFalse();
    }

    [Fact]
    public void SubAtributo_offers_the_SubAtributoAlvo_constants_only()
    {
        Valores(AlvoDeCondicao.SubAtributo).Should().Equal(
            SubAtributoAlvo.Iniciativa, SubAtributoAlvo.Movimentacao, SubAtributoAlvo.EsquivaNatural,
            SubAtributoAlvo.DefesaNatural, SubAtributoAlvo.ReducaoFisica, SubAtributoAlvo.ReducaoMagica,
            SubAtributoAlvo.Adrenalina);
    }

    [Theory]
    [InlineData(AlvoDeCondicao.RecursoTotal)]
    [InlineData(AlvoDeCondicao.RecursoAtual)]
    public void Resource_targets_require_one_of_the_four_resources(AlvoDeCondicao alvo)
    {
        Valores(alvo).Should().BeEquivalentTo(Enum.GetNames<RecursoDeCondicao>());
        CondicaoAlvos.ExigeEspecifico(alvo).Should().BeTrue();
    }

    [Theory]
    [InlineData(AlvoDeCondicao.AfinidadeElemental)]
    [InlineData(AlvoDeCondicao.DanoElemental)]
    public void Elemental_targets_offer_Todos_then_the_four_elements(AlvoDeCondicao alvo)
    {
        Valores(alvo).Should().Equal(new[] { "" }.Concat(Enum.GetNames<Elemento>()));
        CondicaoAlvos.Especificos(alvo, Pericias).Single(o => o.Valor == "Agua").Rotulo.Should().Be("Água");
        CondicaoAlvos.Especificos(alvo, Pericias)[0].Rotulo.Should().Be("Todos");
    }

    [Fact]
    public void Dano_offers_Todos_then_every_damage_type()
    {
        Valores(AlvoDeCondicao.Dano).Should().Equal(new[] { "" }.Concat(Enum.GetNames<TipoDeDano>()));
    }

    [Fact]
    public void Categories_and_moments_cover_every_enum_member()
    {
        CondicaoAlvos.Categorias.Select(c => c.Valor).Should().BeEquivalentTo(Enum.GetNames<CategoriaDesabilitavel>());
        CondicaoAlvos.Categorias.Should().HaveCount(8);
        CondicaoAlvos.Momentos.Select(m => m.Valor).Should().BeEquivalentTo(Enum.GetNames<MomentoDeCondicao>());
        CondicaoAlvos.Tipos.Select(m => m.Valor).Should().BeEquivalentTo(Enum.GetNames<TipoDeValorDeCondicao>());
    }
}
