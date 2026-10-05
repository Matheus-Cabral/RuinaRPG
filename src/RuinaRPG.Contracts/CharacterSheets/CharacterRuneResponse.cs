namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterRuneResponse(string Id, string Nome, string Descricao, int Grau, string? ImageUrl = null, string? Tipo = null, string? Disciplina = null);
