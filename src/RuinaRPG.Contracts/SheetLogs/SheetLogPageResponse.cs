namespace RuinaRPG.Contracts.SheetLogs;

public record SheetLogPageResponse(List<SheetLogEntryResponse> Itens, string? ProximoCursor);
