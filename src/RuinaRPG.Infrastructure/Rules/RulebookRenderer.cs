using System.Net;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Persistence;

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
    Task<IReadOnlyList<RulebookDocument>> GetDocuments();

    /// <summary>A single document by Slug, or null when the Slug is unknown.</summary>
    Task<RulebookDocument?> GetDocument(string slug);
}

/// <summary>
/// Renders the Livro de Regras' 7 documents as displayable HTML. 4 of them (Sistema Básico, Graus
/// & Círculos, Tabela de Níveis, As Estrelas Alkerianas) render a RulebookDocumentOverride's
/// Markdown when the Rules Auditor has saved one for that Slug (see RulebookDocumentsController),
/// the embedded Docs/Sistema RPG resource otherwise — display-only, this never affects
/// IRulesDataProvider or any gameplay calculator. The 5th (Características) is rebuilt straight
/// from the live Traits table instead of any Markdown at all (see BuildCaracteristicasAsync) —
/// editing a Trait via TraitsController is what changes that one. The 6th (Históricos) gets the
/// same live-catalog treatment as Características, but from the Historicos table instead — no
/// Markdown-override support either (see BuildHistoricosAsync). The 7th (Equipagem) gets the same
/// hybrid treatment as Históricos — its IntroHtml still comes from Equipagem.md, but its per-kit
/// Sections come from the live EquipmentKits/EquipmentKitItems/EquipmentKitChoiceSlots tables
/// instead (see BuildEquipagemAsync).
///
/// Scoped (not Singleton — Program.cs registers it as such): it takes a RuinaRpgDbContext, and an
/// override can change between requests, so nothing here is cached across requests the way it used
/// to be with the old Lazy&lt;&gt; field.
/// </summary>
public class RulebookRenderer(RuinaRpgDbContext db) : IRulebookRenderer
{
    public async Task<IReadOnlyList<RulebookDocument>> GetDocuments() =>
    [
        await BuildSistemaBasicoAsync(),
        await BuildCaracteristicasAsync(),
        await BuildGrausECirculosAsync(),
        await BuildTabelaDeNiveisAsync(),
        await BuildEstrelasAlkerianasAsync(),
        await BuildHistoricosAsync(),
        await BuildEquipagemAsync(),
    ];

    public async Task<RulebookDocument?> GetDocument(string slug) => slug switch
    {
        "sistema-basico" => await BuildSistemaBasicoAsync(),
        "caracteristicas" => await BuildCaracteristicasAsync(),
        "graus-e-circulos" => await BuildGrausECirculosAsync(),
        "tabela-de-niveis" => await BuildTabelaDeNiveisAsync(),
        "estrelas-alkerianas" => await BuildEstrelasAlkerianasAsync(),
        "historicos" => await BuildHistoricosAsync(),
        "equipagem" => await BuildEquipagemAsync(),
        _ => null,
    };

    // UseAdvancedExtensions (not the bare default pipeline) is what turns GFM-style pipe tables
    // (e.g. Tabela de Níveis.md, and GRAUS & CÍRCULOS.md's own Grau-cost table) into real <table>
    // markup, and is also what assigns each heading a stable auto-generated id (AutoIdentifierExtension)
    // — reused below as each RulebookSection's Id, the anchor the client's table of contents jumps to.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    // "## 1. Atributos" / "## 2. Perícias e Progressão" / ... — 7 numbered top-level sections,
    // no content before the first one.
    private async Task<RulebookDocument> BuildSistemaBasicoAsync()
    {
        var (intro, sections) = SplitIntoSections(await ReadMarkdownAsync("sistema-basico"), splitLevel: 2);
        return new RulebookDocument("sistema-basico", "Sistema Básico", intro, sections);
    }

    /// <summary>
    /// Unlike the other 3 documents, this one has no Markdown override at all — it is rebuilt
    /// directly from the live Traits table (Requisitos - Auditoria de Regras' unification: editing
    /// a Característica via TraitsController is immediately visible here too, instead of this tab
    /// being a second, disconnected copy of the same prose Características.md used to be). Grouped
    /// by Polaridade (Positivas/Negativas) — same Grupo shape the client's existing filterable-list
    /// UI (LivroDeRegras.razor's "caracteristicas" branch) already expects, unchanged by this.
    /// </summary>
    private async Task<RulebookDocument> BuildCaracteristicasAsync()
    {
        var traits = await db.Traits
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.Polaridade)
            .ThenBy(t => t.Nome)
            .ToListAsync();

        var sections = traits.Select(t => new RulebookSection(
            Id: Slugify(t.Nome),
            Titulo: t.Nome,
            Html: WebUtility.HtmlEncode(t.Descricao).Replace("\n", "<br />") + $"<p><em>Custo: {t.Custo} ponto(s)</em></p>",
            Grupo: t.Polaridade == Polaridade.Positiva ? "Positivas" : "Negativas"
        )).ToList();

        return new RulebookDocument("caracteristicas", "Características", null, sections);
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
    private async Task<RulebookDocument> BuildGrausECirculosAsync()
    {
        var (intro, sections) = SplitIntoSections(await ReadMarkdownAsync("graus-e-circulos"), splitLevel: 1);
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

    /// <summary>
    /// "As estrelas alkerianas.md" splits on `#` (splitLevel: 1) — one card for Sina (its 5 usos,
    /// which are `##`, stay nested inside that card) plus one card per Estrela — same shape
    /// BuildGrausECirculosAsync uses for its own `#`/`##` document. Calendario alkeriano.jpeg has no
    /// matching section either (same reasoning as Escolas_de_Magia.png/Matriz_Elemental.png above),
    /// so it's prepended to IntroHtml the same way.
    /// </summary>
    private async Task<RulebookDocument> BuildEstrelasAlkerianasAsync()
    {
        var (intro, sections) = SplitIntoSections(await ReadMarkdownAsync("estrelas-alkerianas"), splitLevel: 1);
        return new RulebookDocument("estrelas-alkerianas", "As Estrelas", (intro ?? "") + CalendarImageHtml, sections);
    }

    private const string CalendarImageHtml = """
        <figure style="margin:0 0 16px">
            <img src="/rulebook/Calendario alkeriano.jpeg" alt="Calendário Alkeriano" style="max-width:100%" />
            <figcaption>Calendário Alkeriano</figcaption>
        </figure>
        """;

    /// <summary>
    /// Unlike every other document, this one has two different sources: IntroHtml comes from
    /// Historico.md's own lead-in paragraph (reused the same way BuildGrausECirculosAsync/
    /// BuildEstrelasAlkerianasAsync extract IntroHtml — via SplitIntoSections, discarding its
    /// Sections), but the per-entry Sections come from the live Historicos table instead of the
    /// Markdown — same "catalog is the source of truth" treatment BuildCaracteristicasAsync already
    /// gives Traits. Editing a Histórico via HistoricosController is what changes those.
    /// </summary>
    private async Task<RulebookDocument> BuildHistoricosAsync()
    {
        var (intro, _) = SplitIntoSections(RulesDataProvider.ReadResource("Historico.md"), splitLevel: 1);

        var historicos = await db.Historicos
            .Where(h => !h.IsDeleted)
            .OrderBy(h => h.Nome)
            .ToListAsync();

        var sections = historicos.Select(h => new RulebookSection(
            Id: Slugify(h.Nome),
            Titulo: h.Nome,
            Html: WebUtility.HtmlEncode(h.Descricao).Replace("\n", "<br />")
                + $"<p><em>+6 {WebUtility.HtmlEncode(PericiaLabels.Label(h.PericiaMaisSeis))} / +3 {WebUtility.HtmlEncode(PericiaLabels.Label(h.PericiaMaisTres))}</em></p>"
        )).ToList();

        return new RulebookDocument("historicos", "Históricos", intro, sections);
    }

    /// <summary>
    /// Same hybrid-source treatment as BuildHistoricosAsync: IntroHtml from Equipagem.md's own
    /// lead-in paragraph, but the per-kit Sections come from the live EquipmentKits table instead
    /// of the Markdown (editing a kit via EquipmentKitsController is what changes those).
    /// </summary>
    private async Task<RulebookDocument> BuildEquipagemAsync()
    {
        var (intro, _) = SplitIntoSections(RulesDataProvider.ReadResource("Equipagem.md"), splitLevel: 1);

        var kits = await db.EquipmentKits.Where(k => !k.IsDeleted).OrderBy(k => k.Nome).ToListAsync();

        var sections = new List<RulebookSection>();
        foreach (var kit in kits)
        {
            var items = await db.EquipmentKitItems.Where(i => i.KitId == kit.Id).ToListAsync();
            var slots = await db.EquipmentKitChoiceSlots.Where(s => s.KitId == kit.Id).ToListAsync();

            var html = WebUtility.HtmlEncode(kit.Descricao).Replace("\n", "<br />") + "<ul>"
                + string.Join("", items.Select(i => $"<li>{WebUtility.HtmlEncode(i.Nome)} x{i.Qtd}</li>"))
                + string.Join("", slots.Select(s => $"<li>{WebUtility.HtmlEncode(s.Label)} (escolha){(s.BonusNome is not null ? $" — +{s.BonusQtd} {WebUtility.HtmlEncode(s.BonusNome)} se escolher da subcategoria {WebUtility.HtmlEncode(s.BonusSubcategoria)}" : "")}</li>"))
                + "</ul>"
                + (kit.Ciclos > 0 ? $"<p><em>+{kit.Ciclos} Ciclos</em></p>" : "");

            sections.Add(new RulebookSection(Slugify(kit.Nome), kit.Nome, html));
        }

        return new RulebookDocument("equipagem", "Equipagem", intro, sections);
    }

    // No Markdown headings at all — one big GFM pipe table. Splitting finds nothing to split on, so
    // Sections stays empty and the whole rendered table lands in IntroHtml.
    private async Task<RulebookDocument> BuildTabelaDeNiveisAsync()
    {
        var (intro, sections) = SplitIntoSections(await ReadMarkdownAsync("tabela-de-niveis"), splitLevel: 2);
        return new RulebookDocument("tabela-de-niveis", "Tabela de Níveis", intro, sections);
    }

    /// <summary>
    /// Maps a document Slug to the exact embedded-resource filename RulesDataProvider.ReadResource
    /// expects, and returns its raw text. Exposed publicly (unlike the private Build* methods)
    /// specifically so RulebookDocumentsController can show the Rules Auditor the current default
    /// text for a document that has no override yet, without duplicating this mapping.
    /// </summary>
    public static string ReadEmbeddedMarkdown(string slug) => slug switch
    {
        "sistema-basico" => RulesDataProvider.ReadResource("Sistema Basico.md"),
        "graus-e-circulos" => RulesDataProvider.ReadResource("GRAUS e CIRCULOS.md"),
        "tabela-de-niveis" => RulesDataProvider.ReadResource("Tabela de Níveis.md"),
        "estrelas-alkerianas" => RulesDataProvider.ReadResource("Estrelas Alkerianas.md"),
        _ => throw new ArgumentOutOfRangeException(nameof(slug), slug, "Slug de documento desconhecido."),
    };

    /// <summary>The RulebookDocumentOverride for this Slug, if the Rules Auditor saved one — the embedded default otherwise.</summary>
    private async Task<string> ReadMarkdownAsync(string slug)
    {
        var over = await db.RulebookDocumentOverrides.FirstOrDefaultAsync(o => o.Slug == slug);
        return over?.MarkdownText ?? ReadEmbeddedMarkdown(slug);
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
