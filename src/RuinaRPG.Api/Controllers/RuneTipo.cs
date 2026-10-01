using RuinaRPG.Domain.Runes;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Conversão do Tipo da Runa entre o fio (texto: "Arcana", "Negra", null ou "" = sem tipo) e o enum.
/// O valor é o nome exato do enum — qualquer outro texto é desconhecido.
/// </summary>
internal static class RuneTipo
{
    public const string UnknownMessage = "Tipo de Runa desconhecido.";

    public static bool TryParse(string? value, out TipoDeRuna? tipo)
    {
        tipo = null;
        if (string.IsNullOrEmpty(value))
            return true;

        // Exige o nome exato: Enum.TryParse aceitaria "arcana" (ignoreCase) e números como "1".
        if (value == nameof(TipoDeRuna.Arcana)) { tipo = TipoDeRuna.Arcana; return true; }
        if (value == nameof(TipoDeRuna.Negra)) { tipo = TipoDeRuna.Negra; return true; }
        return false;
    }

    public static string? Format(TipoDeRuna? tipo) => tipo?.ToString();
}
