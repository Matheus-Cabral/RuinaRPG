namespace RuinaRPG.Contracts.Diary;

public record UpdateSecretNoteRequest(string Texto, List<string> RecipientUserIds);
