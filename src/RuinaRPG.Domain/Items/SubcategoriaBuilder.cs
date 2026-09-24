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
    public const string Separator = " - ";

    public static string Compose(ItemTipo tipo, string categoria, string familia)
    {
        if (string.IsNullOrWhiteSpace(categoria))
            throw new ArgumentException("Categoria cannot be null or whitespace.", nameof(categoria));

        if (string.IsNullOrWhiteSpace(familia))
            throw new ArgumentException("Família cannot be null or whitespace.", nameof(familia));

        if (categoria.Contains(Separator))
            throw new ArgumentException($"Categoria cannot contain the separator '{Separator}'.", nameof(categoria));

        if (familia.Contains(Separator))
            throw new ArgumentException($"Família cannot contain the separator '{Separator}'.", nameof(familia));

        // Mirrors SubcategoriaOptionsController.Create's own Valor validation (trim + reject a
        // leading/trailing '-') — Categoria/Família come from that same Auditor-managed
        // vocabulary, so a value that slipped past the controller must still be rejected here
        // rather than composing a malformed 4-segment string.
        if (categoria != categoria.Trim())
            throw new ArgumentException("Categoria cannot have leading or trailing whitespace.", nameof(categoria));

        if (familia != familia.Trim())
            throw new ArgumentException("Família cannot have leading or trailing whitespace.", nameof(familia));

        if (categoria.StartsWith('-') || categoria.EndsWith('-'))
            throw new ArgumentException("Categoria cannot start or end with '-'.", nameof(categoria));

        if (familia.StartsWith('-') || familia.EndsWith('-'))
            throw new ArgumentException("Família cannot start or end with '-'.", nameof(familia));

        return string.Join(Separator, [Prefix, tipo.ToString(), categoria, familia]);
    }

    public static bool TryParse(string? subcategoria, out ItemTipo tipo, out string categoria, out string familia)
    {
        tipo = default;
        categoria = "";
        familia = "";

        if (string.IsNullOrEmpty(subcategoria))
            return false;

        var parts = subcategoria.Split(Separator);
        if (parts.Length != 4 || parts[0] != Prefix)
            return false;

        // Tipo segment must be an exact (ordinal, case-sensitive) match to a defined ItemTipo name
        if (!Enum.TryParse<ItemTipo>(parts[1], out tipo) || !Enum.IsDefined(tipo) || tipo.ToString() != parts[1])
            return false;

        categoria = parts[2];
        familia = parts[3];
        return true;
    }
}
