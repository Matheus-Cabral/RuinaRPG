using Microsoft.AspNetCore.SignalR.Client;

namespace RuinaRPG.Client.Services;

/// The default automatic reconnect gives up after ~42 s, which is shorter than a deploy's
/// maintenance window. This keeps retrying for up to five minutes.
public sealed class LongReconnectPolicy : IRetryPolicy
{
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan[] FirstDelays = [TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)];

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        if (retryContext.ElapsedTime >= GiveUpAfter) return null;
        return retryContext.PreviousRetryCount < FirstDelays.Length
            ? FirstDelays[retryContext.PreviousRetryCount]
            : TimeSpan.FromSeconds(5);
    }
}
