using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules.ReferenceData;

public static class NivelBonusParser
{
    public static IReadOnlyList<LevelBonus> Parse(string markdown)
    {
        var table = MarkdownTableParser.ParseTables(markdown).Single();
        return table.Rows
            .Select(row => new LevelBonus(int.Parse(row[0]), row[1]))
            .ToList();
    }
}
