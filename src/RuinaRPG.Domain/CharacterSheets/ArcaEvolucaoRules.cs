namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Regras das evoluções de Arca (Requisitos - Habilidades Raciais R0006): uma evolução vale para a
/// ficha a partir do seu Nível. Genérico sobre o tipo da evolução para servir tanto a entidade EF
/// quanto DTOs sem o Domain depender de nenhum dos dois.
/// </summary>
public static class ArcaEvolucaoRules
{
    /// <summary>Uma evolução só pode ser marcada num nível que existe na Tabela de Níveis.</summary>
    public static bool NivelValido(int nivel, int ultimoNivel) => nivel >= 1 && nivel <= ultimoNivel;

    public static IReadOnlyList<T> Desbloqueadas<T>(IEnumerable<T> evolucoes, Func<T, int> nivel, Func<T, DateTimeOffset> criadaEm, int nivelDaFicha) =>
        evolucoes.Where(e => nivel(e) <= nivelDaFicha)
            .OrderBy(nivel)
            .ThenBy(criadaEm)
            .ToList();
}
