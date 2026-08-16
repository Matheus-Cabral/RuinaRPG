using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules.ReferenceData;

public static class XpPorNivelParser
{
    // The source file has 4 tables (XP, Atributos, Características, EAP); this parser only
    // reads the first — the XP thresholds — which is all Personagem's "Para o próximo" (1.b) needs.
    public static IReadOnlyList<XpPorNivel> Parse(string markdown)
    {
        var table = MarkdownTableParser.ParseTables(markdown).First();
        return table.Rows
            .Skip(1) // row 0 is the sub-header ("Nível", "Valores absolutos", "Valores relativos")
            .Select(row => new XpPorNivel(int.Parse(row[0]), row[1], row[2]))
            .ToList();
    }
}
