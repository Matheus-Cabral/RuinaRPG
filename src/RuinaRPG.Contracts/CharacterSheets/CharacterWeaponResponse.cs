namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterWeaponResponse(string Id, string ItemId, string Nome, string? TipoDeDano, int? Alcance, string? Dados, int? Dano, string? Critico, string? Tier, decimal Peso, bool IsEquipped, int DurabilidadeAtual, int DurabilidadeMaxima);
