using FluentAssertions;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Afinidades;
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

    // Um recorte do catálogo de elementos que não são Caminho (os ids do catálogo), mais um criado pelo Auditor.
    private static readonly ElementoDoCatalogo[] Elementos =
    [
        new(1, "Água", TipoDeElemento.Base, null), new(2, "Fogo", TipoDeElemento.Base, null), new(19, "Luz", TipoDeElemento.SubElemento, null)
    ];

    private static IReadOnlyList<OpcaoDeCondicao> Especificos(AlvoDeCondicao alvo) => CondicaoAlvos.Especificos(alvo, Pericias, Elementos);

    private static List<string> Valores(AlvoDeCondicao alvo) => Especificos(alvo).Select(o => o.Valor).ToList();

    [Fact]
    public void Every_Alvo_has_a_portuguese_label()
    {
        CondicaoAlvos.Alvos.Select(a => a.Valor).Should().BeEquivalentTo(Enum.GetNames<AlvoDeCondicao>());
        // Nomes explícitos: "Dano" é o modificador de dano dos ataques, não tira Vitalidade; isso é o Valor atual.
        string Rotulo(AlvoDeCondicao alvo) => CondicaoAlvos.Alvos.Single(a => a.Valor == alvo.ToString()).Rotulo;
        Rotulo(AlvoDeCondicao.RecursoAtual).Should().Be("Valor atual de um recurso (dano ou cura por vez)");
        Rotulo(AlvoDeCondicao.RecursoTotal).Should().Be("Total (máximo) de um recurso");
        Rotulo(AlvoDeCondicao.Dano).Should().Be("Modificador de dano dos ataques");
        Rotulo(AlvoDeCondicao.DanoElemental).Should().Be("Dano elemental dos ataques");
    }

    [Fact]
    public void Atributo_offers_exactly_the_eight_attributes_with_accents_and_no_general_choice()
    {
        var opcoes = Especificos(AlvoDeCondicao.Atributo);
        opcoes.Select(o => o.Valor).Should().BeEquivalentTo(Enum.GetNames<Atributo>());
        opcoes.Single(o => o.Valor == "Forca").Rotulo.Should().Be("Força");
        CondicaoAlvos.ExigeEspecifico(AlvoDeCondicao.Atributo).Should().BeTrue();
    }

    [Theory]
    [InlineData(AlvoDeCondicao.Pericia)]
    [InlineData(AlvoDeCondicao.Maestria)]
    public void Pericia_and_Maestria_offer_Todas_then_every_active_Pericia(AlvoDeCondicao alvo)
    {
        var opcoes = Especificos(alvo);
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
    public void Elemento_criado_aparece_nos_pickers(AlvoDeCondicao alvo)
    {
        // Todos, depois os elementos do catálogo dados (sem Caminho), pelo id em texto e com o nome do catálogo.
        Valores(alvo).Should().Equal("", "1", "2", "19");
        Especificos(alvo).Single(o => o.Valor == "1").Rotulo.Should().Be("Água");
        Especificos(alvo).Single(o => o.Valor == "19").Rotulo.Should().Be("Luz");
        Especificos(alvo)[0].Rotulo.Should().Be("Todos");
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
