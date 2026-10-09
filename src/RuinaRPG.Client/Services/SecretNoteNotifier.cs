using System.Net.Http.Json;
using RuinaRPG.Contracts.Diary;
using RuinaRPG.Contracts.Notifications;

namespace RuinaRPG.Client.Services;

/// <summary>
/// Campanha R0015: the player's unread Notas Secretas, per campaign, kept live by the
/// notifications hub. Scoped — one per app instance — so the layout, the nav menu and the
/// campaign pages all read the same counters.
/// </summary>
public class SecretNoteNotifier(HttpClient http, INotificationConnection connection)
{
    private Dictionary<string, int> _unread = new();
    private Task? _starting;

    public IReadOnlyDictionary<string, int> UnreadByCampaign => _unread;
    public int TotalUnread => _unread.Values.Sum();
    public int UnreadFor(string campaignId) => _unread.GetValueOrDefault(campaignId);

    /// <summary>The counters changed. May be raised off the renderer's context — use InvokeAsync.</summary>
    public event Action? Changed;

    /// <summary>A note arrived in real time (never raised for the initial load or a silent refresh).</summary>
    public event Action<SecretNoteNotification>? Received;

    /// <summary>
    /// Idempotent, and safe to call on every navigation: MainLayout does exactly that, which is
    /// also what brings a connection that gave up reconnecting back to life.
    /// </summary>
    public Task StartAsync()
    {
        if (connection.IsActive)
            return Task.CompletedTask;
        if (_starting is { IsCompleted: false })
            return _starting;
        return _starting = StartCoreAsync();
    }

    private async Task StartCoreAsync()
    {
        await RefreshAsync();
        try
        {
            await connection.StartAsync(OnReceivedAsync, RefreshAsync);
        }
        catch (Exception)
        {
            // No realtime channel right now (offline, API restarting). The counters above are
            // still valid, and the next StartAsync retries.
        }
    }

    public async Task StopAsync()
    {
        if (!connection.IsActive && _unread.Count == 0)
            return;

        await connection.StopAsync();
        _unread = new();
        Changed?.Invoke();
    }

    public async Task MarkCampaignReadAsync(string campaignId)
    {
        var response = await http.PostAsync($"campaigns/{campaignId}/secret-notes/mark-read", null);
        if (!response.IsSuccessStatusCode)
            return;

        if (_unread.Remove(campaignId))
            Changed?.Invoke();
    }

    private async Task RefreshAsync()
    {
        var response = await http.GetAsync("secret-notes/unread");
        if (!response.IsSuccessStatusCode)
            return;

        var unread = await response.Content.ReadFromJsonAsync<List<UnreadSecretNotesResponse>>() ?? new();
        _unread = unread.ToDictionary(u => u.CampaignId, u => u.Count);
        Changed?.Invoke();
    }

    private Task OnReceivedAsync(SecretNoteNotification notification)
    {
        _unread[notification.CampaignId] = UnreadFor(notification.CampaignId) + 1;
        Changed?.Invoke();
        Received?.Invoke(notification);
        return Task.CompletedTask;
    }
}
