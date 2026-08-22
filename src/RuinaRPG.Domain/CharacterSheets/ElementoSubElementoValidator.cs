namespace RuinaRPG.Domain.CharacterSheets;

public static class ElementoSubElementoValidator
{
    private static readonly Dictionary<Elemento, SubElemento[]> ValidPairs = new()
    {
        [Elemento.Ar] = [SubElemento.Gelo, SubElemento.Raio, SubElemento.Prever, SubElemento.Ecomancia, SubElemento.Alma],
        [Elemento.Agua] = [SubElemento.Gelo, SubElemento.Flora, SubElemento.Purificar, SubElemento.Hemomancia, SubElemento.Alma],
        [Elemento.Fogo] = [SubElemento.Raio, SubElemento.Ferro, SubElemento.Curar, SubElemento.Necromancia, SubElemento.Vida],
        [Elemento.Terra] = [SubElemento.Ferro, SubElemento.Flora, SubElemento.Aprimorar, SubElemento.Invocacao, SubElemento.Vida]
    };

    public static bool IsValidCombination(Elemento elemento, SubElemento subElemento) =>
        ValidPairs[elemento].Contains(subElemento);
}
