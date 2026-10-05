namespace RuinaRPG.Contracts.CreatureSheets;

public record CreatureArmorSlotResponse(string Slot, string? ItemId, string? Nome, string? Categoria, int? Defesa, int? RF, int? RM, decimal? Peso, int? DurabilidadeAtual, int? DurabilidadeMaximo, string? ImageUrl, string? Descricao, bool Inquebravel = false);
