namespace RuinaRPG.Contracts.Encounters;

/// <param name="Condicoes">Condições antigas, de texto livre: só podem ser removidas.</param>
/// <param name="CondicoesAplicadas">Condições do catálogo aplicadas ao participante.</param>
/// <param name="Origem">"Personagem", "Npc" ou "Criatura" conforme a ficha de origem; null quando ela foi apagada.</param>
public record EncounterParticipantResponse(string Id, string Nome, int Iniciativa, int? PV, int? PF, int? PA, int AcoesRestantes, bool IsLiveSourced, List<string> Condicoes,
    List<CondicaoAplicadaResponse> CondicoesAplicadas, string? Origem = null);
