namespace RuinaRPG.Contracts.Encounters;

public record EncounterLogEntryResponse(long Sequencia, DateTime OccurredAt, int Turno, string? VezNome, string Tipo,
    string? ParticipanteNome, string? Campo, string? ValorAntes, string? ValorDepois, string? Texto, string? VezParticipantId = null);
