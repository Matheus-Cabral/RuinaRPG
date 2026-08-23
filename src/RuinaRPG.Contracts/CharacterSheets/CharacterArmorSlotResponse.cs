namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterArmorSlotResponse(string Slot, string? ItemId, string? Nome, string? Categoria, int? Defesa, int? RF, int? RM, string? Penalidade, int? RequisitoVigor, decimal? Peso, int? DurabilidadeAtual, int? DurabilidadeMaxima);
