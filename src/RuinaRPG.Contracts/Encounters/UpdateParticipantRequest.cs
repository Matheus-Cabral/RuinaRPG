namespace RuinaRPG.Contracts.Encounters;

/// <param name="PVCarregado">
/// Os valores com que o formulário do GM foi carregado. O servidor só grava o recurso que o GM mudou (enviado diferente
/// do carregado), para não desfazer uma mudança simultânea: do jogador ou de um tique de Condição, na ficha ligada
/// (IsLiveSourced), ou de um tique na instância de NPC/Criatura do GM. Sem eles, a instância grava o que veio.
/// </param>
public record UpdateParticipantRequest(int Iniciativa, int? PV, int? PF, int? PA, int AcoesRestantes, List<string> Condicoes,
    int? PVCarregado = null, int? PFCarregado = null, int? PACarregado = null);
