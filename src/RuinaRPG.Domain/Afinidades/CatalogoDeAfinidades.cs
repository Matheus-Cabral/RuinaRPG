namespace RuinaRPG.Domain.Afinidades;

/// <summary>
/// Regras de Afinidades configuradas pelo Auditor. Em <paramref name="Visibilidade"/>, Vocacao é o nome do
/// enum <c>Vocacao</c> e Classe o nome da Classe como gravado na ficha; uma linha vale se casar com qualquer um.
/// </summary>
public sealed record CatalogoDeAfinidades(
    IReadOnlyList<ElementoDoCatalogo> Elementos,
    IReadOnlyList<Cruzamento> Cruzamentos,
    IReadOnlyDictionary<int, int> TotalPorCirculo,
    IReadOnlyList<(string? Vocacao, string? Classe, int ElementoId)> Visibilidade);
