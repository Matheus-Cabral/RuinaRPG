namespace RuinaRPG.Contracts.Runes;

public record RuneBankEntryResponse(string Id, string Nome, string Descricao, int Grau, string? ImageId = null, string? ImageUrl = null);
