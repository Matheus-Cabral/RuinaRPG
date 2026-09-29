using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Domain.Rules;

/// <summary>
/// Generates the "## Nome" Markdown block for an Efeito, in the same shape as the hand-authored
/// blocks in Docs/Sistema RPG/GRAUS & CÍRCULOS.md — see
/// docs/superpowers/specs/2026-09-29-efeitos-no-livro-de-regras-design.md.
/// </summary>
public static class EfeitoMarkdownBlock
{
    public static string Gerar(EfeitoParaLivro e)
    {
        var paragrafos = new List<string>
        {
            $"## {EscaparCerquilha(e.Nome)}",
            $"**Gasto:** {Gasto(e)}.",
        };

        if (e.MaxUnidades is { } max)
            paragrafos.Add(!e.MaxEscalaPorGrau
                ? $"Max. {max} {e.UnidadeLabel}."
                : e.MaxContandoAPartirDoGrau is { } inicio
                    ? $"Max. {max} {e.UnidadeLabel} por Grau/Círculo, contando a partir do {inicio}º Grau/Círculo."
                    : $"Max. {max} {e.UnidadeLabel} por Grau/Círculo.");

        paragrafos.Add(string.Join("\n", e.Descricao.Split('\n').Select(EscaparCerquilha)));

        foreach (var grupo in e.PreRequisitos)
            paragrafos.Add($"Obrigatória a compra de {string.Join(" ou ", grupo)}.");

        return string.Join("\n\n", paragrafos) + "\n";
    }

    // Auditor-authored text must not open "#"/"##" headings, which would break the document's
    // Grau sections / Efeito blocks — a leading "\\#" renders as a literal "#".
    private static string EscaparCerquilha(string linha) => linha.StartsWith('#') ? "\\" + linha : linha;

    private static string Gasto(EfeitoParaLivro e)
    {
        var core = e.TipoDeCusto switch
        {
            TipoDeCusto.Fixo => $"{e.CustoFixo} PI",
            TipoDeCusto.PorUnidade => $"{e.CustoPorUnidade} PI por {e.UnidadeLabel}",
            TipoDeCusto.Manual => "X PI",
            TipoDeCusto.ManualPorUnidade => $"X PI por {e.UnidadeLabel}",
            TipoDeCusto.DerivadoDeOutroEfeito => $"igual à Quantidade de {e.QuantidadeDerivadaDeEfeito}",
            _ => throw new ArgumentOutOfRangeException(nameof(e), e.TipoDeCusto, "TipoDeCusto desconhecido."),
        };

        return e.CustoAlternativo is { } alt
            ? $"{core} ({alt} PI a partir do {e.CustoAlternativoAPartirDoGrau}º Grau)"
            : core;
    }
}
