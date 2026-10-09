using RuinaRPG.Contracts.Notifications;

namespace RuinaRPG.Client.Services;

/// <summary>
/// The realtime channel behind SecretNoteNotifier. An interface only so the notifier can be
/// tested without a SignalR server — SignalRNotificationConnection is the one real implementation.
/// </summary>
public interface INotificationConnection : IAsyncDisposable
{
    /// <summary>True while connected, connecting or auto-reconnecting.</summary>
    bool IsActive { get; }

    /// <summary><paramref name="onChanged"/> also runs after an automatic reconnect, since events may have been missed.</summary>
    Task StartAsync(Func<SecretNoteNotification, Task> onReceived, Func<Task> onChanged);

    Task StopAsync();
}
