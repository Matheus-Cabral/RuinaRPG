namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterShieldResponse(string Id, string ItemId, string Nome, string? Categoria, int? BonusDefesa, string? Penalidade, int? RequisitoVigor, decimal Peso, bool IsEquipped, int DurabilidadeAtual, int DurabilidadeMaxima);
