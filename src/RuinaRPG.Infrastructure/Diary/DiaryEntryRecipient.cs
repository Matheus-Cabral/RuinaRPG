namespace RuinaRPG.Infrastructure.Diary;

public class DiaryEntryRecipient
{
    public Guid DiaryEntryId { get; set; }
    public Guid UserId { get; set; }

    // Campanha R0015: null = the recipient hasn't opened the Notas Secretas tab since this note arrived.
    public DateTime? ReadAt { get; set; }
}
