namespace RuinaRPG.Contracts.CreatureSheets;

public record CreatureWeaponResponse(string Id, string? ItemId, string Nome, string? TipoDeDano, string? Dados, int? Dano, int? Alcance, string? Critico, string? Rank, bool IsEquipped, int? DurabilidadeAtual, int? DurabilidadeMaximo, string? ImageUrl, string? Descricao, bool Inquebravel = false, List<string>? Requisitos = null, List<string>? RequisitosPendentes = null, List<string>? Penalidade = null);
