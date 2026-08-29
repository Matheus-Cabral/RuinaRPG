using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules.ReferenceData;

public static class EapPorNivelParser
{
    // The source file has 4 tables (XP, Atributos, Características, EAP), in that order; this
    // parser reads the 4th — "EAP atual: ... segue a tabela ... e é somado pelo resultado de
    // Âmbares Absorvidos" (Ficha de Personagem 1.b) — unlike the XP table, every row here is a
    // real number (no "Lvl. Max" placeholder at level 50), so no string column is needed.
    public static IReadOnlyList<EapPorNivel> Parse(string markdown)
    {
        var table = MarkdownTableParser.ParseTables(markdown).ElementAt(3);
        return table.Rows
            .Skip(1) // row 0 is the sub-header ("Nível", "Valores absolutos", "Ganho por nível")
            .Select(row => new EapPorNivel(int.Parse(row[0]), int.Parse(row[1])))
            .ToList();
    }
}
