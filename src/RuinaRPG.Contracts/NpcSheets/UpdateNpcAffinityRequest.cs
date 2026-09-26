namespace RuinaRPG.Contracts.NpcSheets;

public record UpdateNpcAffinityRequest(string? Elemento, int? ElementoValor, string? SubElemento, int? SubElementoValor, string? CaminhoNome, int? Experiencia,
    string? SegundaEssencia = null, int? SegundaEssenciaValor = null);
