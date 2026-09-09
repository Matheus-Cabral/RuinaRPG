namespace RuinaRPG.Contracts.CreatureSheets;

public record CreatureWeaponResponse(string Id, string? ItemId, string Nome, string? TipoDeDano, string? Dados, int? Dano, int? Alcance, string? Critico, string? Tier, bool IsEquipped, int? DurabilidadeAtual, int? DurabilidadeMaximo, string? ImageUrl, string? Descricao);
