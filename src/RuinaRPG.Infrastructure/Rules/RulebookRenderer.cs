using Markdig;

namespace RuinaRPG.Infrastructure.Rules;

public record RulebookDocument(string Slug, string Titulo, string Html);

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
/// </summary>
public class RulebookRenderer : IRulebookRenderer
{
    // Lazy — same reasoning as RulesDataProvider: read+render once, reuse for every request.
    private readonly Lazy<IReadOnlyList<RulebookDocument>> _documents = new(BuildDocuments);

    public IReadOnlyList<RulebookDocument> GetDocuments() => _documents.Value;

    private static IReadOnlyList<RulebookDocument> BuildDocuments() =>
    [
        new("caracteristicas", "Características", RenderPlain(RulesDataProvider.ReadResource("Caracteristicas.md"))),
        new("sistema-basico", "Ruína RPG - Sistema Básico", RenderPlain(RulesDataProvider.ReadResource("Sistema Basico.md"))),
        new("graus-e-circulos", "Graus & Círculos", RenderGrausECirculos(RulesDataProvider.ReadResource("GRAUS e CIRCULOS.md"))),
        new("tabela-de-niveis", "Tabela de Níveis", RenderPlain(RulesDataProvider.ReadResource("Tabela de Níveis.md"))),
    ];

    // UseAdvancedExtensions (not the bare default pipeline) is what turns GFM-style pipe tables
    // (e.g. Tabela de Níveis.md, and GRAUS & CÍRCULOS.md's own Grau-cost table) into real <table>
    // markup — without it Markdig leaves "| a | b |" rows as literal pipe-delimited text.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    private static string RenderPlain(string markdown) => Markdown.ToHtml(markdown, Pipeline);

    /// <summary>
    /// Escolas_de_Magia.png / Matriz_Elemental.png have no matching section in the source
    /// document — no "Escolas de Magia" heading exists at all, and the closest thing to
    /// "Matriz Elemental" (the "Encantamento Elemental" effect, 2º Grau) is just one of many
    /// effects, not a natural home for a document-wide reference image. They're inserted as
    /// reference material right before the doc's first heading ("# 1º GRAU / CÍRCULO I")
    /// instead — after the source's own intro (the Grau cost table) and before the graded
    /// sections, serving the whole document rather than any one Grau/effect. The images live as
    /// static wwwroot assets (src/RuinaRPG.Client/wwwroot/rulebook/) rather than embedded in the
    /// source .md, so Docs/ stays untouched as the single source of truth.
    /// </summary>
    private static string RenderGrausECirculos(string markdown)
    {
        var html = Markdown.ToHtml(markdown, Pipeline);
        const string referenceImages = """
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

        var insertAt = html.IndexOf("<h1", StringComparison.Ordinal);
        return insertAt >= 0 ? html.Insert(insertAt, referenceImages) : referenceImages + html;
    }
}
