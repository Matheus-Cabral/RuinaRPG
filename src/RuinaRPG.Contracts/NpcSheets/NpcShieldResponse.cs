namespace RuinaRPG.Contracts.NpcSheets;

public record NpcShieldResponse(string Id, string ItemId, string Nome, string? Categoria, int? BonusDefesa, string? Penalidade, int? RequisitoVigor, decimal Peso, bool IsEquipped, int DurabilidadeAtual, int DurabilidadeMaxima, string? ImageUrl, string? Descricao);
