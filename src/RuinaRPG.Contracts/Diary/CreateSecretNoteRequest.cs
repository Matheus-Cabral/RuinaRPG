namespace RuinaRPG.Contracts.Diary;

public record CreateSecretNoteRequest(string Texto, List<string> RecipientUserIds);
