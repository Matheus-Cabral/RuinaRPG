using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Ganss.Xss;

namespace RuinaRPG.Infrastructure.CharacterSheets;

/// <summary>
/// Ficha de Personagem/NPC, aba História: the backstory is HTML written by a player in a rich-text
/// editor and later opened by the GM, so it's run through an allowlist before it's ever persisted —
/// only the tags/attributes/CSS the Jodit toolbar can produce survive. Anything else (script, event
/// handlers, iframes, javascript:/data: links, class/id) is dropped, whatever the client sent.
/// <para>
/// Images: an &lt;img&gt; survives only when its src is EXACTLY the app's own image URL
/// ("/images/{guid}.{png|jpg|gif|webp}" — what ImagesController.Upload hands out and nginx serves) AND
/// that file is in the set the caller passed (the images this user may use on this sheet). Everything
/// else — external http(s)/protocol-relative URLs (tracking pixels), data:, blob:, javascript:, other
/// same-origin paths (an &lt;img&gt; pointed at an API route is a GET the viewer never asked for), path
/// traversal, query strings — removes the whole element. Nothing is fetched or converted.
/// </para>
/// A disallowed tag is unwrapped so its text survives (pasted &lt;pre&gt;, &lt;section&gt;, wrappers…),
/// except for the non-text elements in <see cref="DroppedWithContent"/>, which go away whole.
/// </summary>
public static partial class HistoriaSanitizer
{
    public const int MaxLength = 200_000;
    // The raw request may be larger than MaxLength (sanitizing can also shrink it); this only caps the work.
    public const int MaxRawLength = MaxLength * 2;
    public const string MaxLengthMessage = "A História pode ter no máximo 200.000 caracteres.";

    // Unwrapping these would leak code, CSS or hidden/fallback markup into the story as text.
    private static readonly HashSet<string> DroppedWithContent = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "template", "noscript", "iframe", "object", "embed", "svg", "math", "textarea", "select",
    };

    private static readonly HashSet<string> ImageOnlyAttributes = new(StringComparer.OrdinalIgnoreCase) { "src", "alt", "width", "height" };
    private static readonly IReadOnlySet<string> NoImages = new HashSet<string>();

    /// <param name="html">What the client sent.</param>
    /// <param name="imagensPermitidas">File names ("{guid}.{ext}", as in Image.Path) the caller may embed;
    /// null/empty means no image survives. Use <see cref="ImageFiles"/> to find out which ones to check.</param>
    public static string? Sanitize(string? html, IReadOnlySet<string>? imagensPermitidas = null)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var clean = Build(imagensPermitidas ?? NoImages).Sanitize(html);
        return HasVisibleContent(clean) ? clean : null;
    }

    /// <summary>
    /// The app-image file names referenced by &lt;img src&gt; in <paramref name="html"/> (canonical URLs only;
    /// the same text elsewhere — prose, a link — doesn't count). The controller uses it to ask the database
    /// which of them the caller may use before sanitizing, and to find what the stored História already embeds.
    /// </summary>
    public static IReadOnlySet<string> ImageFiles(string? html)
    {
        var files = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(html) || !html.Contains("<img", StringComparison.OrdinalIgnoreCase))
            return files;

        foreach (var img in new HtmlParser().ParseDocument(html).QuerySelectorAll("img"))
        {
            if (AppImageFile(img.GetAttribute("src")) is { } file)
                files.Add(file);
        }
        return files;
    }

    /// <summary>The file name when <paramref name="src"/> is exactly "/images/{guid}.{ext}"; null otherwise.</summary>
    private static string? AppImageFile(string? src)
    {
        if (src is null)
            return null;

        var match = AppImageUrlRegex().Match(src);
        return match.Success ? match.Groups[1].Value : null;
    }

    // A fresh sanitizer per call: the allowed-image set is per request, and HtmlSanitizer's events are per instance.
    private static HtmlSanitizer Build(IReadOnlySet<string> imagensPermitidas)
    {
        var options = new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "p", "div", "br", "hr", "h1", "h2", "h3", "h4", "h5", "h6", "strong", "b", "em", "i", "u", "s", "strike",
                "sub", "sup", "span", "blockquote", "ul", "ol", "li", "a", "table", "thead", "tbody", "tr", "th", "td", "img",
            },
            // src/alt/width/height are for <img> only — PostProcessNode below takes them off everything else.
            AllowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "style", "href", "colspan", "rowspan", "src", "alt", "width", "height" },
            AllowedCssProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "color", "background-color", "font-family", "font-size", "text-align", "text-decoration", "padding-left", "margin-left",
                "list-style-type", "vertical-align", "width", "height", "border-collapse",
            },
            AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto" },
            // src is deliberately NOT a UriAttribute: it isn't checked against schemes at all, it has to match
            // the app's image URL character by character (AppImageUrlRegex) or the <img> is removed.
            UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href" },
        };

        var sanitizer = new HtmlSanitizer(options) { KeepChildNodes = true };
        // With KeepChildNodes a removed tag is replaced by its children; emptying it first makes it vanish whole.
        sanitizer.RemovingTag += (_, e) =>
        {
            if (DroppedWithContent.Contains(e.Tag.LocalName))
                e.Tag.TextContent = string.Empty;
        };
        // target/rel aren't accepted from input (not in AllowedAttributes) — set here on every surviving link.
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is not IElement element)
                return;

            if (element.NodeName == "A")
            {
                // Só http, https e mailto ABSOLUTOS: o filtro de esquemas da biblioteca deixa passar href relativo
                // e protocol-relative ("//evil/x", "/api/auth/logout"), que seriam links do app/de terceiros no
                // contexto do leitor. Sem href válido o <a> fica só com o texto (e as imagens dentro dele).
                if (element.GetAttribute("href") is not { } href || !AbsoluteLinkRegex().IsMatch(href))
                {
                    element.RemoveAttribute("href");
                }
                else
                {
                    element.SetAttribute("target", "_blank");
                    element.SetAttribute("rel", "noopener noreferrer");
                }
            }

            if (element.NodeName != "IMG")
            {
                foreach (var name in ImageOnlyAttributes)
                    element.RemoveAttribute(name);
                return;
            }

            // The node list being walked was materialised before post-processing, so removing is safe here.
            if (AppImageFile(element.GetAttribute("src")) is not { } file || !imagensPermitidas.Contains(file))
            {
                element.Remove();
                return;
            }

            foreach (var name in new[] { "width", "height" })
            {
                if (element.GetAttribute(name) is { } size && !PixelSizeRegex().IsMatch(size))
                    element.RemoveAttribute(name);
            }
        };
        return sanitizer;
    }

    // "<p><br></p>" is what an emptied Jodit editor holds — store it as NULL, not as a blank story.
    private static bool HasVisibleContent(string html) =>
        html.Contains("<hr", StringComparison.OrdinalIgnoreCase)
        || html.Contains("<table", StringComparison.OrdinalIgnoreCase)
        || html.Contains("<img", StringComparison.OrdinalIgnoreCase)
        || !string.IsNullOrWhiteSpace(WebUtility.HtmlDecode(TagRegex().Replace(html, "")));

    // DiskImageFileStore names files "{Guid.NewGuid()}{ext}" (lowercase "D" format) with the extensions of
    // ImageFormat.ToFileExtension. \z, not $: $ would also accept a trailing newline.
    [GeneratedRegex(@"\A/images/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\.(?:png|jpg|gif|webp))\z", RegexOptions.CultureInvariant)]
    private static partial Regex AppImageUrlRegex();

    [GeneratedRegex(@"\A(?:https?://|mailto:)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex AbsoluteLinkRegex();

    [GeneratedRegex(@"\A[1-9][0-9]{0,3}\z", RegexOptions.CultureInvariant)]
    private static partial Regex PixelSizeRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();
}
