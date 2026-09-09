namespace RuinaRPG.Contracts.CreatureSheets;

public record CreatureSpoilResponse(string Id, string ItemId, string Nome, int Custo, int Qtd, int CustoTotal, int DT, string? ImageUrl, string? Descricao);
