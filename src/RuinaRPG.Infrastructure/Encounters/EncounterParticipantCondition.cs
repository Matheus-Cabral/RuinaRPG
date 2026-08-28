namespace RuinaRPG.Infrastructure.Encounters;

public class EncounterParticipantCondition
{
    public Guid Id { get; set; }
    public Guid EncounterParticipantId { get; set; }
    public required string Texto { get; set; }
}
