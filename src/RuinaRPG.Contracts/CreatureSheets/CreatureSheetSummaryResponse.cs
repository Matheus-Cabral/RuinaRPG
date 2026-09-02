namespace RuinaRPG.Contracts.CreatureSheets;

public record CreatureSheetSummaryResponse(string Id, string Nome, string? Raca, string? Arquetipo, string? Rank, int Nivel, string? OwnerNickname, string? ImageUrl);
