using Microsoft.AspNetCore.SignalR;

namespace RuinaRPG.Api.Hubs;

public static class EncounterHubExtensions
{
    /// <summary>
    /// Avisa (ParticipantsChanged) o grupo do encontro depois de um salvamento que deu certo. Uma falha do hub (ex.: o
    /// backplane do SignalR fora do ar por um instante) vai para o log e nunca derruba a ação: a sincronização ao vivo
    /// só fica para trás até a próxima mudança; nada se perde.
    /// </summary>
    public static async Task AvisarParticipantesAlteradosAsync(this IHubContext<EncounterHub> hub, ILogger logger, Guid encounterId)
    {
        try
        {
            await hub.Clients.Group($"encounter-{encounterId}").SendAsync("ParticipantsChanged");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify encounter {EncounterId} of a ParticipantsChanged update.", encounterId);
        }
    }
}
