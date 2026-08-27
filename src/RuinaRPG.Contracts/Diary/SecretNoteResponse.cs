namespace RuinaRPG.Contracts.Diary;

public record SecretNoteResponse(string Id, string Texto, DateTime CreatedAt, List<string> RecipientUserIds);
