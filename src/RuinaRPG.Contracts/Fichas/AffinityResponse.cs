namespace RuinaRPG.Contracts.Fichas;

/// <summary>
/// <c>ElementoValorComCondicoes</c> é o <c>ElementoValor</c> com as Condições ativas (igual a ele sem Condição). É só
/// para exibir: o que se edita e se salva é sempre <c>ElementoValor</c>, o valor guardado.
/// </summary>
/// <remarks>Linhas antigas de Afinidade: a Api não tem mais estas rotas (2.2.0, pontos por elemento); só a seção de linhas do cliente ainda usa, até ser trocada.</remarks>
public record AffinityResponse(string Id, string? Elemento, int? ElementoValor, string? SegundaEssencia, int? SegundaEssenciaValor, string? SubElemento, int? SubElementoValor, int? Experiencia,
    int? ElementoValorComCondicoes = null);
