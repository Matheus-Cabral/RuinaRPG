namespace RuinaRPG.Domain.Rules.Markdown;

public sealed record MarkdownSection(int Level, string Title, string Body, IReadOnlyList<MarkdownSection> Children);
