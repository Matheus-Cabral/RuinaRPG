using Microsoft.AspNetCore.SignalR.Client;

namespace RuinaRPG.Client.Services;

public sealed class SignalREncounterConnection(HttpClient http, AuthStateService authState) : IEncounterConnection
{
    private HubConnection? _connection;

    public async Task StartAsync(string encounterId, Func<Task> onChanged)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(http.BaseAddress!, "/"), "hubs/encounters"), options =>
            {
                options.AccessTokenProvider = async () => await authState.GetAccessTokenAsync();
            })
            .WithAutomaticReconnect()
            .Build();
        connection.On("ParticipantsChanged", onChanged);
        // A reconnect gets a brand-new connection id server-side, so group membership (JoinEncounter)
        // doesn't survive it — without re-joining here, live sync would silently stop after any
        // network hiccup until the whole page was manually reloaded (item 5 of the gap audit).
        // Also re-fetch state in case anything changed while disconnected.
        connection.Reconnected += async _ =>
        {
            await connection.SendAsync("JoinEncounter", encounterId);
            await onChanged();
        };

        _connection = connection;
        await connection.StartAsync();
        await connection.SendAsync("JoinEncounter", encounterId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is null)
            return;

        var connection = _connection;
        _connection = null;
        await connection.DisposeAsync();
    }
}
