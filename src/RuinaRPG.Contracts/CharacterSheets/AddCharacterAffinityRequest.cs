namespace RuinaRPG.Contracts.CharacterSheets;

public record AddCharacterAffinityRequest(string? Elemento, int? ElementoValor, string? SubElemento, int? SubElementoValor, string? CaminhoNome, int? Experiencia);
