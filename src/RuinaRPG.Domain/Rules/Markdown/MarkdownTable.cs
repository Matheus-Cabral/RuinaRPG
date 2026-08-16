namespace RuinaRPG.Domain.Rules.Markdown;

public sealed record MarkdownTable(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);
