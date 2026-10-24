using System.Net;
using FluentAssertions;
using RuinaRPG.Client.Services;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

public class RetryHandlerTests
{
    private sealed class ScriptedHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] steps) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var step = steps[Math.Min(Calls, steps.Length - 1)];
            Calls++;
            return Task.FromResult(step(request));
        }
    }

    private static HttpResponseMessage Maintenance()
    {
        var r = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        r.Headers.Add(RetryHandler.MaintenanceHeader, "1");
        return r;
    }

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code);
    private static HttpResponseMessage Throw() => throw new HttpRequestException("network down");

    private static (HttpClient Client, ScriptedHandler Inner, List<TimeSpan> Waits) Build(params Func<HttpRequestMessage, HttpResponseMessage>[] steps)
    {
        var waits = new List<TimeSpan>();
        var inner = new ScriptedHandler(steps);
        var handler = new RetryHandler((d, _) => { waits.Add(d); return Task.CompletedTask; }) { InnerHandler = inner };
        return (new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") }, inner, waits);
    }

    [Fact]
    public async Task Post_is_retried_on_maintenance_503_until_success()
    {
        var (client, inner, waits) = Build(_ => Maintenance(), _ => Maintenance(), _ => Status(HttpStatusCode.Created));

        var response = await client.PostAsync("api/x", new StringContent("{}"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        inner.Calls.Should().Be(3);
        waits.Should().Equal(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Post_is_not_retried_on_a_plain_503()
    {
        var (client, inner, _) = Build(_ => Status(HttpStatusCode.ServiceUnavailable), _ => Status(HttpStatusCode.OK));

        var response = await client.PostAsync("api/x", new StringContent("{}"));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        inner.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Post_is_not_retried_on_a_network_failure()
    {
        var (client, inner, _) = Build(_ => Throw(), _ => Status(HttpStatusCode.OK));

        var act = () => client.PostAsync("api/x", new StringContent("{}"));

        await act.Should().ThrowAsync<HttpRequestException>();
        inner.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Get_is_retried_on_502_and_503(HttpStatusCode code)
    {
        var (client, inner, _) = Build(_ => Status(code), _ => Status(HttpStatusCode.OK));

        (await client.GetAsync("api/x")).StatusCode.Should().Be(HttpStatusCode.OK);
        inner.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Get_is_retried_on_a_network_failure()
    {
        var (client, inner, _) = Build(_ => Throw(), _ => Status(HttpStatusCode.OK));

        (await client.GetAsync("api/x")).StatusCode.Should().Be(HttpStatusCode.OK);
        inner.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Other_statuses_are_returned_untouched()
    {
        var (client, inner, _) = Build(_ => Status(HttpStatusCode.InternalServerError));

        (await client.GetAsync("api/x")).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        inner.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Gives_up_when_the_total_wait_would_exceed_the_budget()
    {
        var (client, _, waits) = Build(_ => Maintenance());

        var response = await client.GetAsync("api/x");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        waits.Sum(w => w.TotalSeconds).Should().BeLessThanOrEqualTo(RetryHandler.TotalBudget.TotalSeconds);
        waits.Should().StartWith(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) });
    }
}
