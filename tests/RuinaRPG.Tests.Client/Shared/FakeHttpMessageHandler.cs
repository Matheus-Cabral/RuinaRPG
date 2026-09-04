namespace RuinaRPG.Tests.Client.Shared;

/// <summary>
/// Minimal stand-in for the real API backend, for components (like ImageAttachmentField) that
/// inject HttpClient directly rather than taking a delegate. Delegates every request to a
/// caller-supplied function instead of hitting the network.
/// </summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_respond(request));

    public static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond) => new(new FakeHttpMessageHandler(respond))
    {
        BaseAddress = new Uri("http://localhost/api/"),
    };
}
