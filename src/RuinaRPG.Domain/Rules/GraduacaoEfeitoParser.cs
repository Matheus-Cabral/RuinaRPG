using System.Text.RegularExpressions;
using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules;

public static partial class GraduacaoEfeitoParser
{
    public static IReadOnlyList<GraduacaoEfeito> Parse(string markdown)
    {
        var sections = MarkdownSectionParser.Parse(markdown);
        var results = new List<GraduacaoEfeito>();

        foreach (var gradeSection in sections)
        {
            var grau = ExtractGrauNumber(gradeSection.Title);
            if (grau is null)
                continue;

            foreach (var effect in gradeSection.Children.Where(c => c.Title != "Efeitos Básicos"))
            {
                var gasto = GastoRegex().Match(effect.Body) is { Success: true } gastoMatch
                    ? gastoMatch.Groups[1].Value.Trim()
                    : "";
                var preRequisitoMatch = PreRequisitoRegex().Match(effect.Body);

                results.Add(new GraduacaoEfeito(
                    effect.Title,
                    grau.Value,
                    effect.Body,
                    gasto,
                    preRequisitoMatch.Success,
                    preRequisitoMatch.Success ? preRequisitoMatch.Value.Trim() : null));
            }
        }

        return results;
    }

    private static int? ExtractGrauNumber(string sectionTitle)
    {
        var match = GrauNumberRegex().Match(sectionTitle);
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    [GeneratedRegex(@"^(\d+)º\s*GRAU")]
    private static partial Regex GrauNumberRegex();

    [GeneratedRegex(@"\*{0,2}Gasto:?\*{0,2}\s*(.+)$", RegexOptions.Multiline)]
    private static partial Regex GastoRegex();

    [GeneratedRegex(@"(?:[EÉ]\s+)?obrigatória a compra de[^.]*\.?", RegexOptions.IgnoreCase)]
    private static partial Regex PreRequisitoRegex();
}
