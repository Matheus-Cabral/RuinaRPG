using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Domain.Rules;

/// <summary>
/// The Efeito fields needed to generate its "## Nome" block in the Graus & Círculos Markdown —
/// see docs/superpowers/specs/2026-09-29-efeitos-no-livro-de-regras-design.md.
/// </summary>
public sealed record EfeitoParaLivro(
    string Nome,
    int Grau,
    string Descricao,
    TipoDeCusto TipoDeCusto,
    int? CustoFixo,
    int? CustoPorUnidade,
    string? UnidadeLabel,
    string? QuantidadeDerivadaDeEfeito,
    int? MaxUnidades,
    bool MaxEscalaPorGrau,
    int? CustoAlternativo,
    int? CustoAlternativoAPartirDoGrau,
    IReadOnlyList<IReadOnlyList<string>> PreRequisitos);
