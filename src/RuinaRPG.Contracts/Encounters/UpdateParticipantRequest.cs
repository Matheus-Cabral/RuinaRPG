namespace RuinaRPG.Contracts.Encounters;

public record UpdateParticipantRequest(int Iniciativa, int? PV, int? PF, int? PA, int AcoesRestantes, List<string> Condicoes);
