namespace RuinaRPG.Contracts.NpcSheets;

public record NpcSheetSummaryResponse(string Id, string Nome, string? Linhagem, string? Vocacao, string? SubVocacao, int Nivel, string? OwnerNickname, string? ImageUrl);
