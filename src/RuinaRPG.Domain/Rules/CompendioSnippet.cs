namespace RuinaRPG.Domain.Rules;

/// <summary>
/// A window of a search result's Conteudo, ready to render: Before/After are plain text, Match
/// (null when the query wasn't found in this particular piece of text) is the substring to
/// highlight, in its ORIGINAL casing from the source content — not the query's casing.
/// </summary>
public sealed record CompendioSnippet(string Before, string? Match, string After);
