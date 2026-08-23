namespace RuinaRPG.Contracts.Diary;

public record DiaryEntryResponse(string Id, string Texto, DateTime CreatedAt, List<string> ImageUrls);
