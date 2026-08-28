namespace RuinaRPG.Infrastructure.Encounters;

public class Encounter
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string? Nome { get; set; }
    public int CurrentRound { get; set; } = 1;
    public int CurrentParticipantIndex { get; set; }
}
