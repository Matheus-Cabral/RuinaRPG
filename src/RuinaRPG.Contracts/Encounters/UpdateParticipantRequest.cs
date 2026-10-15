namespace RuinaRPG.Contracts.Encounters;

/// <param name="PVCarregado">
/// Participante ligado a uma ficha (IsLiveSourced): os valores com que o formulário do GM foi carregado. O servidor só
/// grava na ficha o recurso que o GM mudou (enviado diferente do carregado), para não desfazer uma mudança simultânea
/// do jogador. Ignorados para a instância de NPC/Criatura do GM.
/// </param>
public record UpdateParticipantRequest(int Iniciativa, int? PV, int? PF, int? PA, int AcoesRestantes, List<string> Condicoes,
    int? PVCarregado = null, int? PFCarregado = null, int? PACarregado = null);
