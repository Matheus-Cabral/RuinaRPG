using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using Xunit;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class EscolaDeMagiaCatalogTests
{
    [Theory]
    [InlineData(Elemento.Ar)]
    [InlineData(Elemento.Agua)]
    [InlineData(Elemento.Fogo)]
    [InlineData(Elemento.Terra)]
    public void DoElemento_every_Elemento_belongs_to_Dobra(Elemento elemento)
    {
        EscolaDeMagiaCatalog.DoElemento(elemento).Should().Be(EscolaDeMagia.Dobra);
    }

    [Theory]
    [InlineData(SubElemento.Flora, EscolaDeMagia.Transmutacao)]
    [InlineData(SubElemento.Ferro, EscolaDeMagia.Transmutacao)]
    [InlineData(SubElemento.Raio, EscolaDeMagia.Transmutacao)]
    [InlineData(SubElemento.Gelo, EscolaDeMagia.Transmutacao)]
    [InlineData(SubElemento.Necromancia, EscolaDeMagia.Maculacao)]
    [InlineData(SubElemento.Invocacao, EscolaDeMagia.Maculacao)]
    [InlineData(SubElemento.Ecomancia, EscolaDeMagia.Maculacao)]
    [InlineData(SubElemento.Hemomancia, EscolaDeMagia.Maculacao)]
    [InlineData(SubElemento.Curar, EscolaDeMagia.Consagracao)]
    [InlineData(SubElemento.Aprimorar, EscolaDeMagia.Consagracao)]
    [InlineData(SubElemento.Prever, EscolaDeMagia.Consagracao)]
    [InlineData(SubElemento.Purificar, EscolaDeMagia.Consagracao)]
    // Alma e Vida não aparecem na imagem "Escolas de Magia" — confirmado com o usuário que os
    // dois pertencem a Consagração (ver docs/superpowers/specs/2026-09-15-automatizar-afinidades-design.md).
    [InlineData(SubElemento.Alma, EscolaDeMagia.Consagracao)]
    [InlineData(SubElemento.Vida, EscolaDeMagia.Consagracao)]
    public void DoSubElemento_maps_every_sub_elemento_to_its_escola(SubElemento subElemento, EscolaDeMagia esperado)
    {
        EscolaDeMagiaCatalog.DoSubElemento(subElemento).Should().Be(esperado);
    }
}
