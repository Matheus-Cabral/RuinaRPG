namespace RuinaRPG.Contracts.NpcSheets;

public record NpcArmorSlotResponse(string Slot, string? ItemId, string? Nome, string? Categoria, int? Defesa, int? RF, int? RM, string? Penalidade, int? RequisitoVigor, decimal? Peso, int? DurabilidadeAtual, int? DurabilidadeMaxima);
