namespace RuinaRPG.Contracts.NpcSheets;

public record AddNpcAffinityRequest(string? Elemento, int? ElementoValor, string? SubElemento, int? SubElementoValor, string? CaminhoNome, int? Experiencia,
    string? SegundaEssencia = null, int? SegundaEssenciaValor = null);
