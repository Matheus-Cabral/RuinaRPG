namespace RuinaRPG.Contracts.CharacterSheets;

/// <summary>
/// <c>ElementoValorComCondicoes</c> é o <c>ElementoValor</c> com as Condições ativas (igual a ele sem Condição). É só
/// para exibir: o que se edita e se salva é sempre <c>ElementoValor</c>, o valor guardado.
/// </summary>
public record CharacterAffinityResponse(string Id, string? Elemento, int? ElementoValor, string? SegundaEssencia, int? SegundaEssenciaValor, string? SubElemento, int? SubElementoValor, int? Experiencia,
    int? ElementoValorComCondicoes = null);
