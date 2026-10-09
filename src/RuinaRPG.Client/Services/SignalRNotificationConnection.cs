using Microsoft.AspNetCore.SignalR.Client;
using RuinaRPG.Contracts.Notifications;

namespace RuinaRPG.Client.Services;

public sealed class SignalRNotificationConnection(HttpClient http, AuthStateService authState) : INotificationConnection
{
    private HubConnection? _connection;

    public bool IsActive => _connection is { State: not HubConnectionState.Disconnected };

    public async Task StartAsync(Func<SecretNoteNotification, Task> onReceived, Func<Task> onChanged)
    {
        await StopAsync();

        // Same URL/token wiring as GerenciadorDeEncontros.razor's encounter hub connection.
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(http.BaseAddress!, "/"), "hubs/notifications"), options =>
            {
                options.AccessTokenProvider = async () => await authState.GetAccessTokenAsync();
            })
            .WithAutomaticReconnect()
            .Build();
        connection.On(NotificationEvents.SecretNoteReceived, onReceived);
        connection.On(NotificationEvents.SecretNotesChanged, onChanged);
        connection.Reconnected += _ => onChanged();

        _connection = connection;
        await connection.StartAsync();
    }

    public async Task StopAsync()
    {
        if (_connection is null)
            return;

        var connection = _connection;
        _connection = null;
        await connection.DisposeAsync();
    }

    public ValueTask DisposeAsync() => new(StopAsync());
}
