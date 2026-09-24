namespace RuinaRPG.Domain.Items;

/// <summary>
/// Composes/parses the "Item Inicial" Subcategoria convention: exactly 4 segments joined by
/// " - ", no grammar/pluralization logic — Categoria and Família are raw Auditor-managed values
/// (see SubcategoriaOption). The literal Prefix segment is the signal Equipagem's choice-slot
/// matching (EquipmentKitGrantService) uses to recognize an item as built by this constructor,
/// in parallel with the legacy raw-Subcategoria-string match already shipped kits rely on.
/// </summary>
public static class SubcategoriaBuilder
{
    public const string Prefix = "Equipamento inicial";

    public static string Compose(ItemTipo tipo, string categoria, string familia) =>
        string.Join(" - ", [Prefix, tipo.ToString(), categoria, familia]);

    public static bool TryParse(string? subcategoria, out ItemTipo tipo, out string categoria, out string familia)
    {
        tipo = default;
        categoria = "";
        familia = "";

        if (string.IsNullOrEmpty(subcategoria))
            return false;

        var parts = subcategoria.Split(" - ");
        if (parts.Length != 4 || parts[0] != Prefix)
            return false;

        if (!Enum.TryParse<ItemTipo>(parts[1], out tipo))
            return false;

        categoria = parts[2];
        familia = parts[3];
        return true;
    }
}
