namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Enum.TryParse alone is lenient — it accepts numeric strings ("99"), comma-joined names
/// ("Arma, Escudo") and stray whitespace, silently mapping them to an underlying int value
/// that may not even be a defined enum member. Any user-facing enum field (choice-slot Tipo,
/// ArmorSlot, SubcategoriaOptions Tipo/Facet, …) must only ever accept an exact (ordinal,
/// case-sensitive) match to a defined member name — shared here so every controller that parses
/// a user-supplied enum string does it the same strict way.
/// </summary>
internal static class EnumParsing
{
    public static bool TryParseExact<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum =>
        Enum.TryParse(value, out result) && Enum.IsDefined(result) && result.ToString() == value;
}
