using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using RuinaRPG.Client.Services;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

public class LongReconnectPolicyTests
{
    private static TimeSpan? Next(long previousRetries, TimeSpan elapsed) =>
        new LongReconnectPolicy().NextRetryDelay(new RetryContext { PreviousRetryCount = previousRetries, ElapsedTime = elapsed });

    [Fact]
    public void Retries_quickly_then_every_five_seconds() =>
        new[] { Next(0, TimeSpan.Zero), Next(1, TimeSpan.FromSeconds(1)), Next(2, TimeSpan.FromSeconds(3)), Next(3, TimeSpan.FromSeconds(6)), Next(10, TimeSpan.FromSeconds(60)) }
            .Should().Equal(TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

    [Fact]
    public void Gives_up_after_five_minutes() =>
        Next(60, TimeSpan.FromMinutes(5)).Should().BeNull();
}
