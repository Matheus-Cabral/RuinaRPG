using System.Net;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace RuinaRPG.Infrastructure.Rules;

/// <summary>
/// One navigable chunk of a rulebook document — the unit the client's table of contents and
/// per-section cards are built from. Grupo is set only for documents that split at a heading level
/// nested under a shallower "group" heading (currently only Características: Positivas/Negativas).
/// </summary>
public record RulebookSection(string Id, string Titulo, string Html, string? Grupo = null);

public record RulebookDocument(string Slug, string Titulo, string? IntroHtml, IReadOnlyList<RulebookSection> Sections);

public interface IRulebookRenderer
{
    IReadOnlyList<RulebookDocument> GetDocuments();
}

/// <summary>
/// Renders a fixed set of the same Docs/Sistema RPG embedded resources RulesDataProvider already
/// reads (see its own ReadResource) as displayable HTML, for the client's "Livro de Regras" page.
/// RulesDataProvider only ever exposes these as parsed structured records (LevelBonus,
/// GraduacaoEfeito, ...) for gameplay calculations — this is the one place the raw prose gets
/// rendered for a human to read end to end.
///
/// Each document is split into RulebookSections at a chosen heading level (see SplitIntoSections)
/// so the client can render a table of contents and one card per section instead of one giant
/// undifferentiated block — the original flat-Html rendering made a long document (up to 60
/// headings) unreadable and unsearchable.
/// </summary>
public class RulebookRenderer : IRulebookRenderer
{
    // Lazy — same reasoning as RulesDataProvider: read+render once, reuse for every request.
    private readonly Lazy<IReadOnlyList<RulebookDocument>> _documents = new(BuildDocuments);

    public IReadOnlyList<RulebookDocument> GetDocuments() => _documents.Value;

    private static IReadOnlyList<RulebookDocument> BuildDocuments() =>
    [
        BuildCaracteristicas(),
        BuildSistemaBasico(),
        BuildGrausECirculos(),
        BuildTabelaDeNiveis(),
    ];

    // UseAdvancedExtensions (not the bare default pipeline) is what turns GFM-style pipe tables
    // (e.g. Tabela de Níveis.md, and GRAUS & CÍRCULOS.md's own Grau-cost table) into real <table>
    // markup, and is also what assigns each heading a stable auto-generated id (AutoIdentifierExtension)
    // — reused below as each RulebookSection's Id, the anchor the client's table of contents jumps to.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    // "## 1. Atributos" / "## 2. Perícias e Progressão" / ... — 7 numbered top-level sections,
    // no content before the first one.
    private static RulebookDocument BuildSistemaBasico()
    {
        var (intro, sections) = SplitIntoSections(RulesDataProvider.ReadResource("Sistema Basico.md"), splitLevel: 2);
        return new RulebookDocument("sistema-basico", "Sistema Básico", intro, sections);
    }

    // "# Positivas" / "# Negativas" group two flat lists of "### <Trait Name>" entries — grouping
    // by the level-1 heading and splitting at level 3 turns each trait into its own section, tagged
    // with which group it belongs to, so the client can render this as a filterable list instead of
    // scrolling prose (58 traits is too many for a plain table of contents to be useful).
    private static RulebookDocument BuildCaracteristicas()
    {
        var (intro, sections) = SplitIntoSections(RulesDataProvider.ReadResource("Caracteristicas.md"), splitLevel: 3, groupLevel: 1);
        return new RulebookDocument("caracteristicas", "Características", intro, sections);
    }

    /// <summary>
    /// Escolas_de_Magia.png / Matriz_Elemental.png have no matching section in the source
    /// document — no "Escolas de Magia" heading exists at all, and the closest thing to
    /// "Matriz Elemental" (the "Encantamento Elemental" effect, 2º Grau) is just one of many
    /// effects, not a natural home for a document-wide reference image. Appended to IntroHtml
    /// (after the document's own lead-in cost table, before the per-Grau sections) instead —
    /// reference material for the whole document, not any one Grau/effect. The images live as
    /// static wwwroot assets (src/RuinaRPG.Client/wwwroot/rulebook/) rather than embedded in the
    /// source .md, so Docs/ stays untouched as the single source of truth.
    /// </summary>
    private static RulebookDocument BuildGrausECirculos()
    {
        var (intro, sections) = SplitIntoSections(RulesDataProvider.ReadResource("GRAUS e CIRCULOS.md"), splitLevel: 1);
        return new RulebookDocument("graus-e-circulos", "Graus & Círculos", (intro ?? "") + ReferenceImagesHtml, sections);
    }

    private const string ReferenceImagesHtml = """
        <div class="rulebook-reference-images" style="display:flex;gap:16px;flex-wrap:wrap;margin-bottom:16px">
            <figure style="margin:0">
                <img src="/rulebook/Escolas_de_Magia.png" alt="Escolas de Magia" style="max-width:100%" />
                <figcaption>Escolas de Magia</figcaption>
            </figure>
            <figure style="margin:0">
                <img src="/rulebook/Matriz_Elemental.png" alt="Matriz Elemental" style="max-width:100%" />
                <figcaption>Matriz Elemental</figcaption>
            </figure>
        </div>
        """;

    // No Markdown headings at all — one big GFM pipe table. Splitting finds nothing to split on, so
    // Sections stays empty and the whole rendered table lands in IntroHtml.
    private static RulebookDocument BuildTabelaDeNiveis()
    {
        var (intro, sections) = SplitIntoSections(RulesDataProvider.ReadResource("Tabela de Níveis.md"), splitLevel: 2);
        return new RulebookDocument("tabela-de-niveis", "Tabela de Níveis", intro, sections);
    }

    /// <summary>
    /// Walks the document's top-level blocks, starting a new section every time a heading at
    /// <paramref name="splitLevel"/> is reached (that heading's text becomes the section's Titulo,
    /// its Markdig auto-generated id becomes the section's Id — deeper headings stay nested inside
    /// the section's own Html as real sub-headings). When <paramref name="groupLevel"/> is given
    /// (shallower than splitLevel), a heading at that level doesn't start a section itself — it just
    /// tags every following section with that heading's text as Grupo, until the next one. Any
    /// content before the first split-level heading is returned separately as IntroHtml (an image,
    /// a lead-in paragraph, a reference table — nothing to attach a Titulo to).
    /// </summary>
    private static (string? IntroHtml, List<RulebookSection> Sections) SplitIntoSections(string markdown, int splitLevel, int? groupLevel = null)
    {
        var document = Markdown.Parse(markdown, Pipeline);
        var sections = new List<RulebookSection>();
        var introBlocks = new List<Block>();
        List<Block>? currentBlocks = null;
        string? currentGroup = null;
        string? currentTitle = null;
        string? currentId = null;

        void Flush()
        {
            if (currentBlocks is null)
                return;
            sections.Add(new RulebookSection(currentId!, currentTitle!, RenderBlocks(currentBlocks), currentGroup));
        }

        foreach (var block in document)
        {
            if (block is HeadingBlock heading)
            {
                if (groupLevel is not null && heading.Level == groupLevel)
                {
                    currentGroup = ExtractText(heading);
                    continue;
                }

                if (heading.Level == splitLevel)
                {
                    Flush();
                    currentTitle = ExtractText(heading);
                    currentId = heading.TryGetAttributes()?.Id;
                    if (string.IsNullOrEmpty(currentId))
                        currentId = Slugify(currentTitle);
                    currentBlocks = new List<Block>();
                    continue;
                }
            }

            (currentBlocks ?? introBlocks).Add(block);
        }

        Flush();

        return (introBlocks.Count > 0 ? RenderBlocks(introBlocks) : null, sections);
    }

    private static string RenderBlocks(List<Block> blocks)
    {
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        foreach (var block in blocks)
            renderer.Render(block);
        writer.Flush();
        return writer.ToString();
    }

    // Renders the heading (its own tag included) then strips every tag — the simplest way to get
    // plain text out of arbitrary inline content (bold, emoji, etc.) without hand-walking the inline
    // tree. HTML-decode afterward so entities (e.g. "&amp;" in "GRAUS &amp; CÍRCULOS") come back as
    // real characters.
    private static string ExtractText(HeadingBlock heading)
    {
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(heading);
        writer.Flush();
        var withoutTags = Regex.Replace(writer.ToString(), "<[^>]*>", string.Empty);
        return WebUtility.HtmlDecode(withoutTags).Trim();
    }

    // Fallback only — every heading Markdig's AutoIdentifierExtension sees gets a real id, this is
    // just defensive in case a future document has a heading shape that somehow doesn't.
    private static string Slugify(string text) =>
        Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
}
