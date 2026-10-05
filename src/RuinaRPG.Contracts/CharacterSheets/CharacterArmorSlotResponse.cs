namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterArmorSlotResponse(string Slot, string? ItemId, string? Nome, string? Categoria, int? Defesa, int? RF, int? RM, decimal? Peso, int? DurabilidadeAtual, int? DurabilidadeMaxima, string? ImageUrl, string? Descricao, bool Inquebravel = false);
