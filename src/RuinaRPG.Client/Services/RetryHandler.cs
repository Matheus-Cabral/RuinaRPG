using System.Net;

namespace RuinaRPG.Client.Services;

/// Holds requests through a deploy (Técnico R0014): during a maintenance window nginx answers 503 with
/// X-Ruina-Manutencao — the request never reached the API, so any method is retried. Other 502/503s and
/// network failures are retried only for GET/HEAD, since a POST may already have been processed.
/// No message is shown; the page just loads a bit slower.
public sealed class RetryHandler(Func<TimeSpan, CancellationToken, Task>? delay = null) : DelegatingHandler
{
    public const string MaintenanceHeader = "X-Ruina-Manutencao";
    public static readonly TimeSpan TotalBudget = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan MaxStep = TimeSpan.FromSeconds(32);

    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var idempotent = request.Method == HttpMethod.Get || request.Method == HttpMethod.Head;
        var waited = TimeSpan.Zero;
        var step = TimeSpan.FromSeconds(1);

        while (true)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await base.SendAsync(request, cancellationToken);
                var maintenance = response.StatusCode == HttpStatusCode.ServiceUnavailable && response.Headers.Contains(MaintenanceHeader);
                var transient = response.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable;
                if (!(maintenance || (idempotent && transient)) || waited + step > TotalBudget)
                    return response;
            }
            catch (HttpRequestException) when (idempotent && waited + step <= TotalBudget)
            {
            }

            response?.Dispose();
            await _delay(step, cancellationToken);
            waited += step;
            step = step * 2 > MaxStep ? MaxStep : step * 2;
        }
    }
}
