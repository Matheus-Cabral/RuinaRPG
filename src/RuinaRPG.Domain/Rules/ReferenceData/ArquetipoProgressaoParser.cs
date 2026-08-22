using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules.ReferenceData;

public static class ArquetipoProgressaoParser
{
    // Same (Vida, Arcana)-pair shape as VocacaoProgressaoParser, but here row 0 ("Nível") is the
    // real column-0 label and the Vocação/Arquétipo names live in Headers, so Nível comes from
    // table.Rows[1..][0] while the header pairs still start at column 1.
    public static IReadOnlyList<ArquetipoProgressao> Parse(string markdown)
    {
        var table = MarkdownTableParser.ParseTables(markdown).Single();
        var results = new List<ArquetipoProgressao>();

        for (var col = 1; col < table.Headers.Count; col += 2)
        {
            var arquetipo = table.Headers[col];
            foreach (var row in table.Rows.Skip(1))
            {
                results.Add(new ArquetipoProgressao(arquetipo, int.Parse(row[0]), int.Parse(row[col]), int.Parse(row[col + 1])));
            }
        }

        return results;
    }
}
