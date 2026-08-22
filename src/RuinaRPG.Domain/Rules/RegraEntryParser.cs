using System.Text;
using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules;

public static class RegraEntryParser
{
    public static IReadOnlyList<RegraEntry> Parse(string markdown)
    {
        // The doc's real top level is H2 ("## 1. Atributos"); MarkdownSectionParser.Parse starts
        // at level 1 (H1) and finds none, so re-parse starting one level down by prefixing a
        // synthetic H1 wrapper is unnecessary — instead walk every H2 directly via the section
        // parser's own recursive descent by treating H2 as this document's root level.
        var topLevelSections = MarkdownSectionParser.Parse(PromoteH2ToH1(markdown));

        return topLevelSections
            .Select(section => new RegraEntry(section.Title, FlattenContent(section)))
            .ToList();
    }

    private static string PromoteH2ToH1(string markdown) =>
        string.Join('\n', markdown.Replace("\r\n", "\n").Split('\n').Select(line =>
            line.TrimStart().StartsWith("## ") && !line.TrimStart().StartsWith("### ") ? line.TrimStart()[1..] : line));

    private static string FlattenContent(MarkdownSection section)
    {
        var builder = new StringBuilder(section.Body);
        foreach (var child in section.Children)
        {
            builder.AppendLine().AppendLine(child.Title).AppendLine(FlattenContent(child));
        }
        return builder.ToString().Trim();
    }
}
