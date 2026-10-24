namespace RuinaRPG.Contracts.Fichas;

public record InventoryItemResponse(string Id, string ItemId, string Nome, decimal Peso, int Qtd, decimal Total, string? ImageUrl, string? Descricao);
