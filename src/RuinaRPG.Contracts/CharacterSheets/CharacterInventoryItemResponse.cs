namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterInventoryItemResponse(string Id, string ItemId, string Nome, decimal Peso, int Qtd, decimal Total, string? ImageUrl, string? Descricao);
