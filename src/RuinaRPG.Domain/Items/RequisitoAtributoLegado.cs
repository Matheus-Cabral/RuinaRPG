using System.Globalization;
using System.Text.RegularExpressions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Domain.Items;

/// <summary>
/// Lê o antigo "Requisito de Atributo" de uma Arma — texto livre como "10 Dex" — para a migration que o
/// converte em requisito estruturado. Só entende um número não negativo e um atributo, em qualquer ordem.
/// </summary>
public static partial class RequisitoAtributoLegado
{
    public static IReadOnlyDictionary<string, Atributo> Abreviacoes { get; } = new Dictionary<string, Atributo>(StringComparer.OrdinalIgnoreCase)
    {
        ["Instinto"] = Atributo.Instinto, ["Ins"] = Atributo.Instinto,
        ["Vontade"] = Atributo.Vontade, ["Von"] = Atributo.Vontade,
        ["Vigor"] = Atributo.Vigor, ["Vig"] = Atributo.Vigor,
        ["Influência"] = Atributo.Influencia, ["Influencia"] = Atributo.Influencia, ["Inf"] = Atributo.Influencia,
        ["Agilidade"] = Atributo.Agilidade, ["Agi"] = Atributo.Agilidade,
        ["Destreza"] = Atributo.Destreza, ["Dex"] = Atributo.Destreza, ["Des"] = Atributo.Destreza,
        ["Astúcia"] = Atributo.Astucia, ["Astucia"] = Atributo.Astucia, ["Ast"] = Atributo.Astucia,
        ["Força"] = Atributo.Forca, ["Forca"] = Atributo.Forca, ["For"] = Atributo.Forca,
    };

    public static bool TryParse(string? texto, out Atributo atributo, out int minimo)
    {
        atributo = default;
        minimo = 0;
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        var match = NumeroDepoisNome().Match(texto);
        if (!match.Success)
            match = NomeDepoisNumero().Match(texto);
        if (!match.Success)
            return false;

        return Abreviacoes.TryGetValue(match.Groups["nome"].Value, out atributo)
            && int.TryParse(match.Groups["numero"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out minimo);
    }

    [GeneratedRegex(@"^\s*(?<numero>\d+)\s+(?<nome>\p{L}+)\s*$")]
    private static partial Regex NumeroDepoisNome();

    [GeneratedRegex(@"^\s*(?<nome>\p{L}+)\s+(?<numero>\d+)\s*$")]
    private static partial Regex NomeDepoisNumero();
}
