namespace RuinaRPG.Contracts.NpcSheets;

public record NpcShieldResponse(string Id, string ItemId, string Nome, string? Categoria, int? BonusDefesa, decimal Peso, bool IsEquipped, int DurabilidadeAtual, int DurabilidadeMaxima, string? ImageUrl, string? Descricao, bool Inquebravel = false);
