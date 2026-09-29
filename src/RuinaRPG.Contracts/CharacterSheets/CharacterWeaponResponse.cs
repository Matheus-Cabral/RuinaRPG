namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterWeaponResponse(string Id, string ItemId, string Nome, string? TipoDeDano, int? Alcance, string? Dados, int? Dano, string? Critico, string? Rank, decimal Peso, bool IsEquipped, int DurabilidadeAtual, int DurabilidadeMaxima, string? ImageUrl, string? Descricao, bool Inquebravel = false);
