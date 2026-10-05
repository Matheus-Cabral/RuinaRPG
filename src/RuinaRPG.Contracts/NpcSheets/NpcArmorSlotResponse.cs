namespace RuinaRPG.Contracts.NpcSheets;

public record NpcArmorSlotResponse(string Slot, string? ItemId, string? Nome, string? Categoria, int? Defesa, int? RF, int? RM, decimal? Peso, int? DurabilidadeAtual, int? DurabilidadeMaxima, string? ImageUrl, string? Descricao, bool Inquebravel = false, List<string>? Requisitos = null, List<string>? RequisitosPendentes = null, List<string>? Penalidade = null);
