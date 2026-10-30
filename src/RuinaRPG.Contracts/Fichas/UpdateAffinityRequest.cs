namespace RuinaRPG.Contracts.Fichas;

/// <remarks>Linhas antigas de Afinidade: a Api não tem mais estas rotas (2.2.0, pontos por elemento); só a seção de linhas do cliente ainda usa, até ser trocada.</remarks>
public record UpdateAffinityRequest(string? Elemento, int? ElementoValor, string? SegundaEssencia, int? SegundaEssenciaValor, int? SubElementoValor, int? Experiencia);
