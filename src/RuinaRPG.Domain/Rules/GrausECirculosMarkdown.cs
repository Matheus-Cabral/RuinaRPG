using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RuinaRPG.Domain.Rules;

/// <summary>
/// Operations on the Graus & Círculos Markdown text (the Livro de Regras' "graus-e-circulos"
/// document): checking whether an Efeito already has a block, inserting/replacing/removing one —
/// see docs/superpowers/specs/2026-09-29-efeitos-no-livro-de-regras-design.md.
///
/// The document is `#` sections (one per Grau, in document order: 1st `#` = Grau 1, 2nd = Grau 2, …)
/// each containing `##` blocks. A block spans its heading line through the line before the next
/// `##`/`#` (whichever comes first). Three basic Efeitos (Dano, Alcance, Duração) share the
/// `## Efeitos Básicos` heading, and every `Nome (X)` Efeito (e.g. "Libra (Arcana)") shares the
/// `## Nome` heading with its siblings — those shared blocks are never rewritten or removed by sync.
/// </summary>
public static partial class GrausECirculosMarkdown
{
    private static readonly string[] RomanNumerals = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX"];
    private static readonly string[] NomesBasicos = ["dano", "alcance", "duracao"];

    public static bool Contem(string md, string nome)
    {
        md = NormalizarQuebrasDeLinha(md);
        var headings = ParseHeadings(md);
        var normBase = Normalizar(StripParenteses(nome));

        if (NomesBasicos.Contains(normBase))
            return headings.Any(h => h.Level == 2 && Normalizar(h.Title) == Normalizar("Efeitos Básicos"));

        var normFull = Normalizar(nome);
        return headings.Any(h => h.Level == 2 && (Normalizar(h.Title) == normFull || Normalizar(h.Title) == normBase));
    }

    public static bool EhBlocoCompartilhado(string nome)
    {
        var normBase = Normalizar(StripParenteses(nome));
        return NomesBasicos.Contains(normBase) || normBase == "libra";
    }

    public static string Inserir(string md, int grau, string bloco)
    {
        md = NormalizarQuebrasDeLinha(md);
        var nome = ExtrairNome(bloco);
        if (Contem(md, nome))
            return md;

        var secoes = ParseHeadings(md).Where(h => h.Level == 1).ToList();

        if (grau <= secoes.Count)
        {
            var start = secoes[grau - 1].Start;
            var end = grau < secoes.Count ? secoes[grau].Start : md.Length;
            return AppendToRange(md, start, end, bloco);
        }

        var novoTitulo = $"# {grau}º GRAU / CÍRCULO {RomanNumerals[grau - 1]}";
        return md.TrimEnd('\n') + "\n\n" + novoTitulo + "\n\n" + bloco;
    }

    public static string Substituir(string md, string nomeAntigo, int grau, string bloco)
    {
        md = NormalizarQuebrasDeLinha(md);
        if (EhBlocoCompartilhado(nomeAntigo))
            return md;

        var loc = LocalizarBloco(md, nomeAntigo);
        if (loc is null)
            return Inserir(md, grau, bloco);

        if (loc.Value.SectionIndex == grau - 1)
        {
            var resto = md[loc.Value.End..];
            var meio = resto.Length > 0 ? bloco + "\n" + resto : bloco;
            return md[..loc.Value.Start] + meio;
        }

        var semAntigo = md[..loc.Value.Start] + md[loc.Value.End..];
        return Inserir(semAntigo, grau, bloco);
    }

    public static string Remover(string md, string nome)
    {
        md = NormalizarQuebrasDeLinha(md);
        if (EhBlocoCompartilhado(nome))
            return md;

        var loc = LocalizarBloco(md, nome);
        if (loc is null)
            return md;

        return md[..loc.Value.Start] + md[loc.Value.End..];
    }

    private static string AppendToRange(string md, int start, int end, string bloco)
    {
        var conteudo = md[start..end].TrimEnd('\n');
        var resto = md[end..];
        var meio = conteudo + "\n\n" + bloco;
        return md[..start] + meio + (resto.Length > 0 ? "\n" + resto : "");
    }

    private readonly record struct BlocoLocalizado(int Start, int End, int SectionIndex);

    private static BlocoLocalizado? LocalizarBloco(string md, string nome)
    {
        var headings = ParseHeadings(md);
        var normBase = Normalizar(StripParenteses(nome));
        var normFull = Normalizar(nome);
        var sectionIndex = -1;

        for (var i = 0; i < headings.Count; i++)
        {
            var h = headings[i];
            if (h.Level == 1)
            {
                sectionIndex++;
                continue;
            }

            var titulo = Normalizar(h.Title);
            if (titulo == normFull || titulo == normBase)
            {
                var end = i + 1 < headings.Count ? headings[i + 1].Start : md.Length;
                return new BlocoLocalizado(h.Start, end, sectionIndex);
            }
        }

        return null;
    }

    private sealed record Heading(int Level, int Start, string Title);

    private static List<Heading> ParseHeadings(string md) =>
        HeadingRegex().Matches(md)
            .Select(m => new Heading(m.Groups[1].Length, m.Index, m.Groups[2].Value))
            .ToList();

    private static string ExtrairNome(string bloco)
    {
        var fimPrimeiraLinha = bloco.IndexOf('\n');
        var primeiraLinha = fimPrimeiraLinha >= 0 ? bloco[..fimPrimeiraLinha] : bloco;
        return primeiraLinha.TrimStart('#').Trim();
    }

    private static string StripParenteses(string nome)
    {
        var trimmed = nome.Trim();
        var match = ParenRegex().Match(trimmed);
        return match.Success ? match.Groups[1].Value : trimmed;
    }

    private static string NormalizarQuebrasDeLinha(string md) => md.Replace("\r\n", "\n");

    private static string Normalizar(string s)
    {
        var formaD = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formaD.Length);
        foreach (var c in formaD)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().ToLowerInvariant().Trim();
    }

    [GeneratedRegex(@"^(#{1,2})[ \t]+(.*?)[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(.+?)\s*\(.*\)$")]
    private static partial Regex ParenRegex();
}
