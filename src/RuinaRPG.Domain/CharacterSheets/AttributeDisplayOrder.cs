namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// The order the 8 Personagem/NPC attributes are displayed in, independent of the
/// <see cref="Atributo"/> enum's underlying int values (those are persisted in the
/// database — reordering the enum's declaration would silently remap every existing
/// CharacterAttributes/NpcAttributes/*Skills.AtributoEscolhido/*Masteries.Atributo row).
/// </summary>
public static class AttributeDisplayOrder
{
    private static readonly Atributo[] Order =
    {
        Atributo.Forca, Atributo.Vigor, Atributo.Agilidade, Atributo.Destreza,
        Atributo.Astucia, Atributo.Instinto, Atributo.Influencia, Atributo.Vontade,
    };

    public static int Rank(Atributo atributo) => Array.IndexOf(Order, atributo);
}
