namespace RuinaRPG.Contracts.NpcSheets;

// Mesmo formato de AddCharacterRuneRequest.
public record AddNpcRuneRequest(string? Nome, string? Descricao, int? Grau, string? SourceBankEntryId = null, string? ImageId = null);
