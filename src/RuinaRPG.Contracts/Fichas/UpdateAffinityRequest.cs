namespace RuinaRPG.Contracts.Fichas;

public record UpdateAffinityRequest(string? Elemento, int? ElementoValor, string? SegundaEssencia, int? SegundaEssenciaValor, int? SubElementoValor, int? Experiencia);
