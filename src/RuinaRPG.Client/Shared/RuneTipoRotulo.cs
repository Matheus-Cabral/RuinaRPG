namespace RuinaRPG.Client.Shared;

/// <summary>Rótulo exibido do Tipo da Runa ("Arcana"/"Negra"/null no contrato).</summary>
public static class RuneTipoRotulo
{
    public static string De(string? tipo) => tipo switch
    {
        "Arcana" => "Runa Arcana",
        "Negra" => "Runa Negra",
        _ => "—",
    };
}
