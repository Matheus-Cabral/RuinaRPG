namespace RuinaRPG.Domain.Auth;

// Regras da conta de um Jogador: só o GM que o convidou a gere, e o estado só muda por
// transições válidas (Ativa <-> Desativada; Ativa/Desativada -> Excluida, sem volta).
public static class ContaDeJogador
{
    public static bool PodeGerir(Guid gmId, Guid? invitedByGmId) => invitedByGmId == gmId;

    /// <summary>O novo estado, ou <c>null</c> se a ação não vale para o estado atual.</summary>
    public static EstadoDaConta? Aplicar(EstadoDaConta atual, AcaoDeConta acao) => (atual, acao) switch
    {
        (EstadoDaConta.Ativa, AcaoDeConta.Desativar) => EstadoDaConta.Desativada,
        (EstadoDaConta.Desativada, AcaoDeConta.Reativar) => EstadoDaConta.Ativa,
        (EstadoDaConta.Ativa or EstadoDaConta.Desativada, AcaoDeConta.Excluir) => EstadoDaConta.Excluida,
        _ => null,
    };
}
