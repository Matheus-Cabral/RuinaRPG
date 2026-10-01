using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>Identificador estável de uma perícia nova: o Nome sem acentos, em PascalCase, só letras e dígitos.</summary>
public static class PericiaChave
{
    public static string Gerar(string nome, IEnumerable<string> chavesExistentes)
    {
        var semAcento = new string(nome.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        var palavras = Regex.Split(semAcento, "[^A-Za-z0-9]+");
        var baseChave = string.Concat(palavras.Where(p => p.Length > 0).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
        if (baseChave.Length == 0)
            baseChave = "Pericia";

        var existentes = new HashSet<string>(chavesExistentes, StringComparer.OrdinalIgnoreCase);
        if (!existentes.Contains(baseChave))
            return baseChave;
        for (var n = 2; ; n++)
            if (!existentes.Contains(baseChave + n))
                return baseChave + n;
    }
}
