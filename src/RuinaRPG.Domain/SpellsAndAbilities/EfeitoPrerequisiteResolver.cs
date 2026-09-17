namespace RuinaRPG.Domain.SpellsAndAbilities;

/// <summary>
/// The result of trying to resolve one target Efeito's unmet prerequisites — see
/// docs/superpowers/specs/2026-09-17-pre-requisitos-automaticos-de-efeitos-design.md.
/// </summary>
public sealed record EfeitoResolutionResult(
    IReadOnlyList<EfeitoRegra> ParaAutoAdicionar,
    IReadOnlyList<string>? GrupoAmbiguo);

/// <summary>
/// Resolves which prerequisite Efeitos are still missing for a target Efeito — recursively, in
/// dependency order — instead of the old behavior of simply hiding the target until its
/// prerequisites were bought manually. Pure, no I/O.
/// </summary>
public static class EfeitoPrerequisiteResolver
{
    public static EfeitoResolutionResult Resolver(
        EfeitoRegra alvo,
        IReadOnlyList<EfeitoRegra> catalogo,
        IReadOnlySet<string> nomesPresentes,
        int grauDaMagia,
        IReadOnlyDictionary<string, string> escolhasForcadas)
    {
        var paraAdicionar = new List<EfeitoRegra>();
        var jaConsiderados = new HashSet<string>(nomesPresentes);

        var grupoAmbiguo = ResolverRecursivo(alvo, catalogo, jaConsiderados, grauDaMagia, escolhasForcadas, paraAdicionar);
        return new EfeitoResolutionResult(paraAdicionar, grupoAmbiguo);
    }

    /// <summary>
    /// Chave usada em <paramref name="escolhasForcadas"/> para uma resposta já dada a um grupo
    /// ambíguo — os nomes elegíveis (já filtrados por Grau), na ordem em que aparecem no catálogo,
    /// unidos por "|". Exposta porque o chamador (AddEfeitoForm) monta a mesma chave ao guardar a
    /// resposta do jogador.
    /// </summary>
    public static string ChaveDoGrupo(IReadOnlyList<string> elegiveis) => string.Join("|", elegiveis);

    private static IReadOnlyList<string>? ResolverRecursivo(
        EfeitoRegra efeito, IReadOnlyList<EfeitoRegra> catalogo, HashSet<string> jaConsiderados,
        int grauDaMagia, IReadOnlyDictionary<string, string> escolhasForcadas, List<EfeitoRegra> paraAdicionar)
    {
        foreach (var grupo in efeito.PreRequisitos)
        {
            if (grupo.Any(jaConsiderados.Contains))
                continue;

            var elegiveis = grupo
                .Select(nome => catalogo.First(c => c.Nome == nome))
                .Where(c => c.Grau <= grauDaMagia)
                .ToList();

            if (elegiveis.Count == 0)
                return Array.Empty<string>();

            EfeitoRegra escolhido;
            if (elegiveis.Count == 1)
            {
                escolhido = elegiveis[0];
            }
            else
            {
                var nomesElegiveis = elegiveis.Select(c => c.Nome).ToList();
                if (!escolhasForcadas.TryGetValue(ChaveDoGrupo(nomesElegiveis), out var nomeEscolhido))
                    return nomesElegiveis;

                escolhido = elegiveis.First(c => c.Nome == nomeEscolhido);
            }

            var grupoAmbiguoAninhado = ResolverRecursivo(escolhido, catalogo, jaConsiderados, grauDaMagia, escolhasForcadas, paraAdicionar);
            if (grupoAmbiguoAninhado is not null)
                return grupoAmbiguoAninhado;

            jaConsiderados.Add(escolhido.Nome);
            paraAdicionar.Add(escolhido);
        }

        return null;
    }
}
