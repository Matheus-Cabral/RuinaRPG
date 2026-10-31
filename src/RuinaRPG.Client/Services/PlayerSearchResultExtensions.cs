using RuinaRPG.Contracts.Players;
using RuinaRPG.Domain.Auth;

namespace RuinaRPG.Client.Services;

public static class PlayerSearchResultExtensions
{
    /// <summary>
    /// O estado da conta como o enum do Domain. O contrato leva o nome do estado em texto (o formato do JSON não
    /// muda); o cliente compara o enum, nunca o texto.
    /// </summary>
    public static EstadoDaConta ComoEstadoDaConta(this PlayerSearchResultResponse jogador) =>
        Enum.Parse<EstadoDaConta>(jogador.Estado);
}
