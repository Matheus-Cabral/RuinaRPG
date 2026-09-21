using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class CaminhoSubElementoRulesTests
{
    // Matriz Elemental: Alma liga Prever/Purificar, Vida liga Curar/Aprimorar, Mundano liga
    // Ecomancia/Hemomancia/Necromancia/Invocação.
    [Theory]
    [InlineData(SubElemento.Prever, Caminho.Alma)]
    [InlineData(SubElemento.Purificar, Caminho.Alma)]
    [InlineData(SubElemento.Curar, Caminho.Vida)]
    [InlineData(SubElemento.Aprimorar, Caminho.Vida)]
    [InlineData(SubElemento.Ecomancia, Caminho.Mundano)]
    [InlineData(SubElemento.Hemomancia, Caminho.Mundano)]
    [InlineData(SubElemento.Necromancia, Caminho.Mundano)]
    [InlineData(SubElemento.Invocacao, Caminho.Mundano)]
    public void CaminhoExigido_is_the_path_the_Matriz_Elemental_links_the_SubElemento_to(SubElemento subElemento, Caminho esperado)
    {
        CaminhoSubElementoRules.CaminhoExigido(subElemento).Should().Be(esperado);
    }

    [Theory]
    [InlineData(SubElemento.Gelo)]
    [InlineData(SubElemento.Raio)]
    [InlineData(SubElemento.Flora)]
    [InlineData(SubElemento.Ferro)]
    [InlineData(SubElemento.Alma)]
    [InlineData(SubElemento.Vida)]
    public void CaminhoExigido_is_null_for_SubElementos_that_are_not_gated(SubElemento subElemento)
    {
        CaminhoSubElementoRules.CaminhoExigido(subElemento).Should().BeNull();
    }

    [Theory]
    [InlineData(Elemento.Fogo, Caminho.Vida, SubElemento.Curar, true)]
    [InlineData(Elemento.Terra, Caminho.Vida, SubElemento.Aprimorar, true)]
    [InlineData(Elemento.Ar, Caminho.Alma, SubElemento.Prever, true)]
    [InlineData(Elemento.Agua, Caminho.Alma, SubElemento.Purificar, true)]
    [InlineData(Elemento.Ar, Caminho.Mundano, SubElemento.Ecomancia, true)]
    [InlineData(Elemento.Terra, Caminho.Mundano, SubElemento.Invocacao, true)]
    [InlineData(Elemento.Fogo, Caminho.Mundano, SubElemento.Curar, false)]   // caminho errado
    [InlineData(Elemento.Fogo, Caminho.Alma, SubElemento.Curar, false)]      // caminho errado
    [InlineData(Elemento.Ar, Caminho.Vida, SubElemento.Curar, false)]        // elemento errado
    [InlineData(Elemento.Agua, Caminho.Mundano, SubElemento.Necromancia, false)] // elemento errado
    public void Permite_requires_both_the_matching_Elemento_and_the_matching_Caminho(Elemento elemento, Caminho caminho, SubElemento subElemento, bool esperado)
    {
        CaminhoSubElementoRules.Permite(elemento, caminho, subElemento).Should().Be(esperado);
    }

    [Theory]
    [InlineData(null, Caminho.Vida)]
    [InlineData(Elemento.Fogo, null)]
    [InlineData(null, null)]
    public void Permite_a_gated_SubElemento_is_false_when_the_Elemento_or_the_Caminho_is_missing(Elemento? elemento, Caminho? caminho)
    {
        CaminhoSubElementoRules.Permite(elemento, caminho, SubElemento.Curar).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(Elemento.Fogo, Caminho.Mundano)]
    public void Permite_an_ungated_SubElemento_is_always_true(Elemento? elemento, Caminho? caminho)
    {
        CaminhoSubElementoRules.Permite(elemento, caminho, SubElemento.Gelo).Should().BeTrue();
    }

    [Theory]
    [InlineData("Alma", Caminho.Alma)]
    [InlineData("Vida", Caminho.Vida)]
    [InlineData("Mundano", Caminho.Mundano)]
    public void TryParse_accepts_only_the_three_paths(string raw, Caminho esperado)
    {
        CaminhoSubElementoRules.TryParseCaminho(raw, out var caminho).Should().BeTrue();
        caminho.Should().Be(esperado);
    }

    [Theory]
    [InlineData("Caminho da Fênix")]
    [InlineData("vida")]
    [InlineData("7")]
    [InlineData("")]
    public void TryParse_rejects_anything_else(string raw)
    {
        CaminhoSubElementoRules.TryParseCaminho(raw, out _).Should().BeFalse();
    }

    [Fact]
    public void Validar_accepts_a_matching_new_row()
    {
        CaminhoSubElementoRules.Validar(Elemento.Fogo, SubElemento.Curar, "Vida", null, null, null).Should().BeNull();
    }

    [Fact]
    public void Validar_rejects_a_free_text_Caminho_when_it_is_new()
    {
        CaminhoSubElementoRules.Validar(null, null, "Caminho da Fênix", null, null, null).Should().NotBeNull();
    }

    [Fact]
    public void Validar_accepts_an_empty_Caminho_on_an_ungated_row()
    {
        CaminhoSubElementoRules.Validar(Elemento.Fogo, SubElemento.Vida, null, null, null, null).Should().BeNull();
    }

    [Fact]
    public void Validar_rejects_a_gated_SubElemento_with_the_wrong_Caminho()
    {
        CaminhoSubElementoRules.Validar(Elemento.Fogo, SubElemento.Curar, "Mundano", null, null, null).Should().NotBeNull();
    }

    [Fact]
    public void Validar_rejects_a_gated_SubElemento_with_no_Elemento()
    {
        CaminhoSubElementoRules.Validar(null, SubElemento.Curar, "Vida", null, null, null).Should().NotBeNull();
    }

    [Fact]
    public void Validar_grandfathers_a_saved_row_that_is_resubmitted_unchanged()
    {
        // Linha antiga: Caminho em texto livre + Curar sem Caminho válido, reenviada igual.
        CaminhoSubElementoRules.Validar(Elemento.Fogo, SubElemento.Curar, "Caminho da Fênix",
            Elemento.Fogo, SubElemento.Curar, "Caminho da Fênix").Should().BeNull();
    }

    [Fact]
    public void Validar_still_checks_a_saved_gated_row_once_the_Elemento_changes()
    {
        CaminhoSubElementoRules.Validar(Elemento.Ar, SubElemento.Curar, "Vida",
            Elemento.Fogo, SubElemento.Curar, "Vida").Should().NotBeNull();
    }
}
