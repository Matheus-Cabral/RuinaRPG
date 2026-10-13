namespace RuinaRPG.Contracts.Encounters;

public record EncounterResponse(string Id, string? Nome, int CurrentRound, int CurrentParticipantIndex, string Status = "Preparacao", DateTime? FinalizadoEm = null, string? CurrentParticipantId = null);
