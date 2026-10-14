using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.Notifications;

namespace RuinaRPG.Tests.Client.Shared;

/// <summary>
/// Stand-in for the SignalR connection (which bUnit can't host): records starts/stops and lets a
/// test play the server's part by raising the hub events.
/// </summary>
public sealed class FakeNotificationConnection : INotificationConnection
{
    private Func<SecretNoteNotification, Task>? _onReceived;
    private Func<Task>? _onChanged;
    private Func<CondicoesAlteradasNotification, Task>? _onCondicoes;

    public bool IsActive { get; private set; }
    public int StartCount { get; private set; }
    public int StopCount { get; private set; }
    public bool StartShouldThrow { get; set; }

    public Task StartAsync(Func<SecretNoteNotification, Task> onReceived, Func<Task> onChanged,
        Func<CondicoesAlteradasNotification, Task> onCondicoes)
    {
        StartCount++;
        if (StartShouldThrow)
            throw new HttpRequestException("hub unreachable");

        _onReceived = onReceived;
        _onChanged = onChanged;
        _onCondicoes = onCondicoes;
        IsActive = true;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        StopCount++;
        IsActive = false;
        return Task.CompletedTask;
    }

    public Task RaiseReceivedAsync(SecretNoteNotification notification) => _onReceived!(notification);

    public Task RaiseCondicoesAlteradasAsync(CondicoesAlteradasNotification notification) => _onCondicoes!(notification);

    public Task RaiseChangedAsync() => _onChanged!();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
