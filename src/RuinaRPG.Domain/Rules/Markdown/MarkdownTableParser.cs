namespace RuinaRPG.Domain.Rules.Markdown;

public static class MarkdownTableParser
{
    public static IReadOnlyList<MarkdownTable> ParseTables(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var tables = new List<MarkdownTable>();

        for (var i = 0; i < lines.Length; i++)
        {
            if (!IsTableRow(lines[i]) || i + 1 >= lines.Length || !IsSeparatorRow(lines[i + 1]))
                continue;

            var headers = SplitRow(lines[i]);
            var rows = new List<IReadOnlyList<string>>();
            var j = i + 2;
            while (j < lines.Length && IsTableRow(lines[j]))
            {
                rows.Add(SplitRow(lines[j]));
                j++;
            }

            tables.Add(new MarkdownTable(headers, rows));
            i = j - 1;
        }

        return tables;
    }

    private static bool IsTableRow(string line) => line.TrimStart().StartsWith('|');

    private static bool IsSeparatorRow(string line) =>
        IsTableRow(line) && line.Replace("|", "").Trim().All(c => c is '-' or ':' or ' ');

    private static IReadOnlyList<string> SplitRow(string line)
    {
        var trimmed = line.Trim().Trim('|');
        return trimmed.Split('|').Select(cell => cell.Trim()).ToList();
    }
}
