using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules.ReferenceData;

public static class ClasseProgressaoParser
{
    // Same shape as VocacaoProgressaoParser/ArquetipoProgressaoParser: column 0 is "Nível",
    // columns 1.. come in (Vida, Arcana) pairs, one pair per Classe, named by Headers[i] (the
    // Classe repeats across its pair) and disambiguated by row 0 (the sub-header: "Vida"/"Arcana").
    public static IReadOnlyList<ClasseProgressao> Parse(string markdown)
    {
        var table = MarkdownTableParser.ParseTables(markdown).Single();
        var results = new List<ClasseProgressao>();

        for (var col = 1; col < table.Headers.Count; col += 2)
        {
            var classe = table.Headers[col];
            foreach (var row in table.Rows.Skip(1))
            {
                results.Add(new ClasseProgressao(classe, int.Parse(row[0]), int.Parse(row[col]), int.Parse(row[col + 1])));
            }
        }

        return results;
    }
}
