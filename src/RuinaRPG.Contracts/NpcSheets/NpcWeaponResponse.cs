namespace RuinaRPG.Contracts.NpcSheets;

public record NpcWeaponResponse(string Id, string ItemId, string Nome, string? TipoDeDano, int? Alcance, string? Dados, int? Dano, string? Critico, string? Rank, decimal Peso, bool IsEquipped, int DurabilidadeAtual, int DurabilidadeMaxima, string? ImageUrl, string? Descricao, bool Inquebravel = false, List<string>? Requisitos = null, List<string>? RequisitosPendentes = null, List<string>? Penalidade = null);
