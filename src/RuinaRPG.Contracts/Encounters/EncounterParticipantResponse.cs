namespace RuinaRPG.Contracts.Encounters;

public record EncounterParticipantResponse(string Id, string Nome, int Iniciativa, int? PV, int? PF, int? PA, int AcoesRestantes, bool IsLiveSourced, List<string> Condicoes);
