using Microsoft.AspNetCore.SignalR;

namespace RuinaRPG.Api.Hubs;

public static class NotificationHubExtensions
{
    /// <summary>
    /// Empurra <paramref name="evento"/> ao usuário depois de um salvamento que deu certo. Uma falha do hub vai para o
    /// log e nunca derruba a ação.
    /// </summary>
    public static async Task AvisarUsuarioAsync(this IHubContext<NotificationHub> hub, ILogger logger, Guid usuarioId, string evento, params object?[] argumentos)
    {
        try
        {
            await hub.Clients.User(usuarioId.ToString()).SendCoreAsync(evento, argumentos);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push {Evento} to user {UsuarioId}", evento, usuarioId);
        }
    }
}
