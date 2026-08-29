namespace RuinaRPG.Domain.Rules;

/// <summary>
/// Épico 6 item 5 of the gap audit: the Compêndio dumped each result's full raw content, even for
/// large Regra entries — this trims it to a short window around the match (or the start of the
/// content, if there's no query or no match) and separates out the substring to highlight.
/// </summary>
public static class CompendioSnippetBuilder
{
    public const int DefaultMaxLength = 160;

    public static CompendioSnippet Build(string content, string? query, int maxLength = DefaultMaxLength)
    {
        if (string.IsNullOrEmpty(content))
            return new CompendioSnippet("", null, "");

        var matchIndex = string.IsNullOrWhiteSpace(query)
            ? -1
            : content.IndexOf(query, StringComparison.OrdinalIgnoreCase);

        if (matchIndex < 0)
            return new CompendioSnippet(Truncate(content, maxLength), null, "");

        var matchLength = query!.Length;
        // Center the window on the match, then clamp to the content's bounds.
        var windowStart = Math.Max(0, matchIndex - (maxLength - matchLength) / 2);
        var windowEnd = Math.Min(content.Length, windowStart + maxLength);
        // Re-clamp windowStart in case windowEnd hit the content's end first, so the window still
        // spans up to maxLength characters where the content allows it.
        windowStart = Math.Max(0, windowEnd - maxLength);

        var before = content[windowStart..matchIndex];
        var match = content.Substring(matchIndex, matchLength); // original casing, not the query's
        var after = content[(matchIndex + matchLength)..windowEnd];

        if (windowStart > 0)
            before = "…" + before;
        if (windowEnd < content.Length)
            after += "…";

        return new CompendioSnippet(before, match, after);
    }

    private static string Truncate(string content, int maxLength) =>
        content.Length <= maxLength ? content : content[..maxLength] + "…";
}
