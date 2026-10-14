namespace RuinaRPG.Contracts.Encounters;

/// <param name="Condicoes">Condições antigas, de texto livre: só podem ser removidas.</param>
/// <param name="CondicoesAplicadas">Condições do catálogo aplicadas ao participante.</param>
public record EncounterParticipantResponse(string Id, string Nome, int Iniciativa, int? PV, int? PF, int? PA, int AcoesRestantes, bool IsLiveSourced, List<string> Condicoes,
    List<CondicaoAplicadaResponse> CondicoesAplicadas);
