using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Ganss.Xss;

namespace RuinaRPG.Infrastructure.CharacterSheets;

/// <summary>
/// Ficha de Personagem/NPC, aba História: the backstory is HTML written by a player in a rich-text
/// editor and later opened by the GM, so it's run through an allowlist before it's ever persisted —
/// only the tags/attributes/CSS the Jodit toolbar can produce survive. Anything else (script, event
/// handlers, iframes, images, javascript:/data: links, class/id) is dropped, whatever the client sent.
/// </summary>
public static partial class HistoriaSanitizer
{
    public const int MaxLength = 200_000;
    public const string MaxLengthMessage = "A História pode ter no máximo 200.000 caracteres.";

    private static readonly HtmlSanitizer Sanitizer = Build();

    public static string? Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var clean = Sanitizer.Sanitize(html);
        return HasVisibleContent(clean) ? clean : null;
    }

    private static HtmlSanitizer Build()
    {
        var options = new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "p", "div", "br", "hr", "h1", "h2", "h3", "h4", "h5", "h6", "strong", "b", "em", "i", "u", "s", "strike",
                "sub", "sup", "span", "blockquote", "ul", "ol", "li", "a", "table", "thead", "tbody", "tr", "th", "td",
            },
            AllowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "style", "href", "colspan", "rowspan" },
            AllowedCssProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "color", "background-color", "font-family", "font-size", "text-align", "text-decoration", "padding-left", "margin-left",
            },
            AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto" },
            UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href" },
        };

        var sanitizer = new HtmlSanitizer(options);
        // target/rel aren't accepted from input (not in AllowedAttributes) — set here on every surviving link.
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is IElement { NodeName: "A" } link)
            {
                link.SetAttribute("target", "_blank");
                link.SetAttribute("rel", "noopener noreferrer");
            }
        };
        return sanitizer;
    }

    // "<p><br></p>" is what an emptied Jodit editor holds — store it as NULL, not as a blank story.
    private static bool HasVisibleContent(string html) =>
        html.Contains("<hr", StringComparison.OrdinalIgnoreCase)
        || html.Contains("<table", StringComparison.OrdinalIgnoreCase)
        || !string.IsNullOrWhiteSpace(WebUtility.HtmlDecode(TagRegex().Replace(html, "")).Replace(' ', ' '));

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();
}
