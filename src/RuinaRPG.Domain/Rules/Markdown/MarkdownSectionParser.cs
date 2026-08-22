using System.Text;

namespace RuinaRPG.Domain.Rules.Markdown;

public static class MarkdownSectionParser
{
    public static IReadOnlyList<MarkdownSection> Parse(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var (sections, _) = ParseAtLevel(lines, 0, 1);
        return sections;
    }

    private static (List<MarkdownSection> Sections, int NextIndex) ParseAtLevel(string[] lines, int startIndex, int level)
    {
        var sections = new List<MarkdownSection>();
        var i = startIndex;

        while (i < lines.Length)
        {
            var headingLevel = HeadingLevelOf(lines[i]);
            if (headingLevel == 0)
            {
                i++;
                continue;
            }
            if (headingLevel < level)
                break; // belongs to an ancestor, let the caller handle it

            var title = lines[i].TrimStart('#').Trim();
            var bodyBuilder = new StringBuilder();
            var j = i + 1;
            while (j < lines.Length && (HeadingLevelOf(lines[j]) == 0 || HeadingLevelOf(lines[j]) < headingLevel && HeadingLevelOf(lines[j]) != 0 && false))
            {
                // Body text ends at the first heading of ANY level >= headingLevel + 0 (i.e. any next heading).
                if (HeadingLevelOf(lines[j]) != 0)
                    break;
                bodyBuilder.AppendLine(lines[j]);
                j++;
            }

            var (children, nextIndex) = j < lines.Length && HeadingLevelOf(lines[j]) > headingLevel
                ? ParseAtLevel(lines, j, headingLevel + 1)
                : (new List<MarkdownSection>(), j);

            sections.Add(new MarkdownSection(headingLevel, title, bodyBuilder.ToString().Trim(), children));
            i = nextIndex;
        }

        return (sections, i);
    }

    private static int HeadingLevelOf(string line)
    {
        var trimmed = line.TrimStart();
        var level = 0;
        while (level < trimmed.Length && trimmed[level] == '#')
            level++;
        return level > 0 && level < trimmed.Length && trimmed[level] == ' ' ? level : 0;
    }
}
