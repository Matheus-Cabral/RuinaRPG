using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using RuinaRPG.Contracts;

namespace RuinaRPG.Client.Services;

/// Notices that a new version was deployed while this tab was open (Técnico R0014) by comparing this
/// client's build sha with the API's. Checked every 5 minutes, on SignalR reconnect and on window focus.
public sealed class NewVersionWatcher(HttpClient http, string clientSha) : IAsyncDisposable
{
    private bool _raised;
    private PeriodicTimer? _timer;

    public event Action? NewVersionAvailable;

    public async Task CheckAsync()
    {
        if (_raised || clientSha == "dev") return;
        try
        {
            var api = await http.GetFromJsonAsync<VersionResponse>("api/version");
            if (api is null || api.Sha == "dev" || api.Sha == clientSha) return;
            _raised = true;
            NewVersionAvailable?.Invoke();
        }
        catch (HttpRequestException) { }
        catch (NotSupportedException) { }
        catch (JsonException) { }
    }

    public void Start()
    {
        if (_timer is not null) return;
        var timer = _timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        _ = Task.Run(async () =>
        {
            try { while (await timer.WaitForNextTickAsync()) await CheckAsync(); }
            catch (ObjectDisposedException) { }
        });
    }

    [JSInvokable]
    public Task OnFocusAsync() => CheckAsync();

    public ValueTask DisposeAsync()
    {
        _timer?.Dispose();
        return ValueTask.CompletedTask;
    }
}
