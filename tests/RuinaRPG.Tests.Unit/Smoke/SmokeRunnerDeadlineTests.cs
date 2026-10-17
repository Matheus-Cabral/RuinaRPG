using System.Net;
using FluentAssertions;
using Moq;
using RuinaRPG.Infrastructure.Auth;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Smoke;
using Xunit;

namespace RuinaRPG.Tests.Unit.Smoke;

public class SmokeRunnerDeadlineTests
{
    // Each request moves the clock forward by one minute.
    private sealed class SteppingClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 17, 0, 0, 0, TimeSpan.Zero);
        public void Advance() => _now = _now.AddMinutes(1);
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class OkHandler(SteppingClock clock) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            clock.Advance();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        }
    }

    [Fact]
    public async Task Stops_at_the_deadline_and_reports_the_remaining_paths()
    {
        var clock = new SteppingClock();
        var handler = new OkHandler(clock);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        var jwt = new Mock<IJwtTokenService>();
        jwt.Setup(j => j.CreateAccessToken(It.IsAny<ApplicationUser>())).Returns("t");
        var user = new ApplicationUser { Id = Guid.NewGuid(), Nickname = "gm" };
        var users = new Dictionary<Guid, ApplicationUser> { [user.Id] = user };
        var plan = Enumerable.Range(1, 5).Select(i => new SmokeRequest(user.Id, $"/api/p{i}")).ToList();

        var failures = await SmokeRunner.RunAsync(http, jwt.Object, users, plan, TimeSpan.FromMinutes(2), clock);

        handler.Paths.Should().Equal("/api/p1", "/api/p2");
        failures.Should().Equal("0 /api/p3 (prazo)", "0 /api/p4 (prazo)", "0 /api/p5 (prazo)");
    }

    [Fact]
    public async Task Runs_everything_within_the_deadline()
    {
        var clock = new SteppingClock();
        var handler = new OkHandler(clock);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        var jwt = new Mock<IJwtTokenService>();
        jwt.Setup(j => j.CreateAccessToken(It.IsAny<ApplicationUser>())).Returns("t");
        var user = new ApplicationUser { Id = Guid.NewGuid(), Nickname = "gm" };
        var users = new Dictionary<Guid, ApplicationUser> { [user.Id] = user };
        var plan = Enumerable.Range(1, 3).Select(i => new SmokeRequest(user.Id, $"/api/p{i}")).ToList();

        var failures = await SmokeRunner.RunAsync(http, jwt.Object, users, plan, TimeSpan.FromMinutes(10), clock);

        handler.Paths.Should().HaveCount(3);
        failures.Should().BeEmpty();
    }
}
