namespace RuinaRPG.Tests.Client.Shared;

/// <summary>
/// Collapses runs of whitespace (including the newlines/indentation Razor line breaks introduce
/// between inline elements) down to single spaces, so a popup's rendered TextContent can be
/// compared against an info-popup brief's verbatim paragraph without being sensitive to how the
/// .razor markup happens to be wrapped across lines.
/// </summary>
public static class TextNormalization
{
    public static string Collapse(string text) =>
        string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
