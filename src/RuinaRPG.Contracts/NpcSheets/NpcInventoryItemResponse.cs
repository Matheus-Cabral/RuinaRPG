namespace RuinaRPG.Contracts.NpcSheets;

public record NpcInventoryItemResponse(string Id, string ItemId, string Nome, decimal Peso, int Qtd, decimal Total, string? ImageUrl, string? Descricao);
