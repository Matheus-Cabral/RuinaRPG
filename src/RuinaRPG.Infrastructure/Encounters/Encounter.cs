namespace RuinaRPG.Infrastructure.Encounters;

public class Encounter
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string? Nome { get; set; }
    public int CurrentRound { get; set; } = 1;

    // Tracks whose turn it is by identity, not by a raw position — a raw index silently
    // mis-targets whoever now happens to sit there after a mid-round Add or Iniciativa edit
    // reorders the participant list (Épico 5 item 2 of the gap audit). Null before the encounter
    // has ever advanced, or if that participant was since removed (ON DELETE SET NULL).
    public Guid? CurrentParticipantId { get; set; }
}
