namespace RuinaRPG.Domain.SpellsAndAbilities;

/// <summary>
/// Formats a Magia/Habilidade's Efeitos list into a short human-readable summary for display
/// (e.g. "Dano: 3d6, Alcance: 4, Área: 1"). Dano and Duração — the two Efeitos Básicos the top
/// table of "GRAUS & CÍRCULOS" scales by die *type* as well as count (shared "Dano / Duração"
/// column) — show as "NdX" using the die size for the entry's own Grau; every other effect with a
/// Quantidade shows the bare number, and an effect with no Quantidade (Fixo/DerivadoDeOutroEfeito/
/// already-resolved) shows only its name. Pure, no I/O.
/// </summary>
public static class SpellAbilityEffectSummaryFormatter
{
    private static readonly IReadOnlyDictionary<int, int> DadoPorGrau = new Dictionary<int, int>
    {
        [1] = 2, [2] = 4, [3] = 6, [4] = 8, [5] = 10, [6] = 12, [7] = 20, [8] = 30, [9] = 100,
    };

    public static string Formatar(int grau, IEnumerable<(string EfeitoNome, int? Quantidade)> efeitos) =>
        string.Join(", ", efeitos.Select(e => FormatarUm(grau, e.EfeitoNome, e.Quantidade)));

    private static string FormatarUm(int grau, string nome, int? quantidade)
    {
        if (quantidade is not { } q)
            return nome;

        if ((nome == "Dano" || nome == "Duração") && DadoPorGrau.TryGetValue(grau, out var dado))
            return $"{nome}: {q}d{dado}";

        return $"{nome}: {q}";
    }
}
