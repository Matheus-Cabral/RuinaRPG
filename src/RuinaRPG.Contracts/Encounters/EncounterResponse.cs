namespace RuinaRPG.Contracts.Encounters;

public record EncounterResponse(string Id, string? Nome, int CurrentRound, int CurrentParticipantIndex);
