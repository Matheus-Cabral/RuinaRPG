using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules.ReferenceData;

public static class CirculoGrauPorEapParser
{
    public static IReadOnlyList<CirculoGrauPorEap> Parse(string markdown)
    {
        var table = MarkdownTableParser.ParseTables(markdown).Single();
        return table.Rows
            .Skip(1) // row 0 is the sub-header ("Círculo/Grau", "Valores absolutos", ...)
            .Select(row => new CirculoGrauPorEap(
                int.Parse(row[0]),
                row[1],
                row[2],
                int.Parse(row[3]),
                int.Parse(row[4])))
            .ToList();
    }
}
