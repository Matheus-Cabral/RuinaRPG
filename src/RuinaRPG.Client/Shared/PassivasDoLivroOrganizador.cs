using System.Globalization;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Client.Shared;

/// <summary>Filtros da aba Habilidades Passivas do Livro de Regras; null/vazio = não filtra.</summary>
public sealed record FiltroDePassivas(string Nome = "", string? Categoria = null, string? Vocacao = null, string? Classe = null);

public sealed record GrupoDePassivas(string Titulo, IReadOnlyList<PassivaDoLivroResponse> Passivas);

/// <summary>
/// Livro de Regras R0010: as Passivas sempre nos grupos Passivas Livres, Passivas Vocacionais e Passivas
/// de Classe, nessa ordem, em ordem alfabética dentro de cada um. Os filtros só reduzem o conteúdo dos
/// grupos; um grupo que fica vazio some.
/// </summary>
public static class PassivasDoLivroOrganizador
{
    // Nome do enum CategoriaDePassiva -> título do grupo, na ordem de exibição.
    private static readonly (string Categoria, string Titulo)[] Grupos =
        [("Livre", "Passivas Livres"), ("Vocacional", "Passivas Vocacionais"), ("DeClasse", "Passivas de Classe")];

    // O cliente não define InvariantGlobalization, então o Blazor WebAssembly carrega o ICU (subconjunto) e
    // IgnoreNonSpace funciona com a cultura invariante; ela evita depender de um pt-BR que pode não estar embarcado.
    private static readonly CompareInfo Comparador = CultureInfo.InvariantCulture.CompareInfo;
    private const CompareOptions SemCaixaNemAcento = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
    private static readonly IComparer<string> PorNome = Comparer<string>.Create((a, b) => Comparador.Compare(a, b, SemCaixaNemAcento));

    public static IReadOnlyList<GrupoDePassivas> Agrupar(IEnumerable<PassivaDoLivroResponse> passivas, FiltroDePassivas filtro)
    {
        var nome = filtro.Nome.Trim();
        var filtradas = passivas.Where(p =>
            (nome.Length == 0 || p.Nome.Contains(nome, StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrEmpty(filtro.Categoria) || p.Categoria == filtro.Categoria)
            && (string.IsNullOrEmpty(filtro.Vocacao) || p.Vocacao == filtro.Vocacao)
            && (string.IsNullOrEmpty(filtro.Classe) || p.Classe == filtro.Classe)).ToList();

        return Grupos
            .Select(g => new GrupoDePassivas(g.Titulo, filtradas.Where(p => p.Categoria == g.Categoria).OrderBy(p => p.Nome, PorNome).ToList()))
            .Where(g => g.Passivas.Count > 0)
            .ToList();
    }

    public static IReadOnlyList<string> Vocacoes(IEnumerable<PassivaDoLivroResponse> passivas) =>
        passivas.Select(p => p.Vocacao).OfType<string>().Distinct().OrderBy(v => v, PorNome).ToList();

    public static IReadOnlyList<string> Classes(IEnumerable<PassivaDoLivroResponse> passivas) =>
        passivas.Select(p => p.Classe).OfType<string>().Distinct().OrderBy(c => c, PorNome).ToList();
}
