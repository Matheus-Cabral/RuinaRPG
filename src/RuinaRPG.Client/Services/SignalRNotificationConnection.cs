using Microsoft.AspNetCore.SignalR.Client;
using RuinaRPG.Contracts.Notifications;

namespace RuinaRPG.Client.Services;

public sealed class SignalRNotificationConnection(HttpClient http, AuthStateService authState, NewVersionWatcher watcher) : INotificationConnection
{
    private HubConnection? _connection;

    public bool IsActive => _connection is { State: not HubConnectionState.Disconnected };

    public async Task StartAsync(Func<SecretNoteNotification, Task> onReceived, Func<Task> onChanged,
        Func<CondicoesAlteradasNotification, Task> onCondicoes, Func<string, Task> onFichaNoCemiterio,
        Func<Task> onCampanhasDoJogador)
    {
        await StopAsync();

        // Same URL/token wiring as GerenciadorDeEncontros.razor's encounter hub connection.
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(http.BaseAddress!, "/"), "hubs/notifications"), options =>
            {
                options.AccessTokenProvider = async () => await authState.GetAccessTokenAsync();
            })
            .WithAutomaticReconnect(new LongReconnectPolicy())
            .Build();
        connection.On(NotificationEvents.SecretNoteReceived, onReceived);
        connection.On(NotificationEvents.SecretNotesChanged, onChanged);
        connection.On(NotificationEvents.CondicoesAlteradas, onCondicoes);
        connection.On(NotificationEvents.FichaNoCemiterioAlterada, onFichaNoCemiterio);
        connection.On(NotificationEvents.CampanhasDoJogadorAlteradas, onCampanhasDoJogador);
        connection.Reconnected += _ => onChanged();
        connection.Reconnected += _ => watcher.CheckAsync();

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
