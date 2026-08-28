namespace RuinaRPG.Contracts.CreatureSheets;

public record CreatureShieldResponse(string Id, string ItemId, string Nome, string? Categoria, int? BonusDefesa, string? Penalidade, int? RequisitoVigor, decimal Peso, bool IsEquipped, int DurabilidadeAtual, int DurabilidadeMaxima);
