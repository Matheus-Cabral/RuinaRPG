using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

public class NewVersionWatcherTests
{
    private sealed class VersionHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => Task.FromResult(respond());
    }

    private static NewVersionWatcher Watcher(string clientSha, Func<HttpResponseMessage> respond) =>
        new(new HttpClient(new VersionHandler(respond)) { BaseAddress = new Uri("https://localhost/") }, clientSha);

    private static HttpResponseMessage Api(string sha) => new(HttpStatusCode.OK) { Content = JsonContent.Create(new VersionResponse("2.0.0", sha)) };

    [Fact]
    public async Task Raises_once_when_the_api_sha_differs()
    {
        var watcher = Watcher("aaaaaaa", () => Api("bbbbbbb"));
        var raised = 0;
        watcher.NewVersionAvailable += () => raised++;

        await watcher.CheckAsync();
        await watcher.CheckAsync();

        raised.Should().Be(1);
    }

    [Theory]
    [InlineData("aaaaaaa", "aaaaaaa")]
    [InlineData("dev", "bbbbbbb")]
    [InlineData("aaaaaaa", "dev")]
    public async Task Stays_quiet_when_same_or_unknown(string clientSha, string apiSha)
    {
        var watcher = Watcher(clientSha, () => Api(apiSha));
        var raised = false;
        watcher.NewVersionAvailable += () => raised = true;

        await watcher.CheckAsync();

        raised.Should().BeFalse();
    }

    [Fact]
    public async Task Failures_are_ignored()
    {
        var watcher = Watcher("aaaaaaa", () => throw new HttpRequestException());
        await watcher.Invoking(w => w.CheckAsync()).Should().NotThrowAsync();
    }

    [Fact]
    public async Task Malformed_body_is_ignored()
    {
        var watcher = Watcher("aaaaaaa", () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json") });
        await watcher.Invoking(w => w.CheckAsync()).Should().NotThrowAsync();
    }
}
