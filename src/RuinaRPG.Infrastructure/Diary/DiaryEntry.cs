namespace RuinaRPG.Infrastructure.Diary;

public class DiaryEntry
{
    public Guid Id { get; set; }
    public Guid AuthorUserId { get; set; }
    public Guid? CharacterSheetId { get; set; }
    public Guid? CampaignId { get; set; }
    public bool IsSecretNote { get; set; }
    public required string Texto { get; set; }
    public DateTime CreatedAt { get; set; }
}
