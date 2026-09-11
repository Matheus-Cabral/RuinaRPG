namespace RuinaRPG.Infrastructure.Rules;

/// <summary>
/// GM-editable override of one Livro de Regras document's raw Markdown, keyed by the same Slug
/// RulebookRenderer already uses ("sistema-basico", "graus-e-circulos", "tabela-de-niveis" — never
/// "caracteristicas", which is DB-driven from Traits instead, see RulebookRenderer). One row per
/// Slug; absence of a row means "use the embedded .md resource", exactly as before this feature
/// existed. Display-only: nothing here feeds IRulesDataProvider or any gameplay calculator.
/// </summary>
public class RulebookDocumentOverride
{
    public Guid Id { get; set; }
    public required string Slug { get; set; }
    public required string MarkdownText { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
}
