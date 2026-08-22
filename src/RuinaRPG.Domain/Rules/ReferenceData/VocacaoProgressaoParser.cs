using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules.ReferenceData;

public static class VocacaoProgressaoParser
{
    // Column 0 is "Nível"; columns 1.. come in (Vida, Arcana) pairs, one pair per Vocação,
    // named by Headers[i] (the Vocação repeats across its pair) and disambiguated by row 0
    // (the sub-header: "Vida"/"Arcana"), same shape as ArquetipoProgressaoParser.
    public static IReadOnlyList<VocacaoProgressao> Parse(string markdown)
    {
        var table = MarkdownTableParser.ParseTables(markdown).Single();
        var subHeader = table.Rows[0];
        var results = new List<VocacaoProgressao>();

        for (var col = 1; col < table.Headers.Count; col += 2)
        {
            var vocacao = table.Headers[col];
            foreach (var row in table.Rows.Skip(1))
            {
                results.Add(new VocacaoProgressao(vocacao, int.Parse(row[0]), int.Parse(row[col]), int.Parse(row[col + 1])));
            }
        }

        return results;
    }
}
