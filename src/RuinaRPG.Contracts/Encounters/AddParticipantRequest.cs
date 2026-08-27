namespace RuinaRPG.Contracts.Encounters;

public record AddParticipantRequest(string? SourceCharacterSheetId, string? SourceNpcSheetId, string? SourceCreatureSheetId, int Iniciativa);
