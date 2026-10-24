namespace RuinaRPG.Contracts.Fichas;

public record RuneResponse(string Id, string Nome, string Descricao, int Grau, string? ImageUrl = null, string? Tipo = null, string? Disciplina = null, string? DesabilitadoPor = null);
