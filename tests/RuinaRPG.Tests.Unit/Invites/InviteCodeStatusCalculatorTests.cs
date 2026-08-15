using FluentAssertions;
using RuinaRPG.Domain.Invites;

namespace RuinaRPG.Tests.Unit.Invites;

public class InviteCodeStatusCalculatorTests
{
    private static readonly DateTime Now = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Compute_returns_Ativo_when_not_revoked_not_redeemed_and_not_expired()
    {
        var status = InviteCodeStatusCalculator.Compute(
            revokedAt: null, redeemedByUserId: null, expiresAt: Now.AddHours(1), now: Now);

        status.Should().Be(InviteCodeStatus.Ativo);
    }

    [Fact]
    public void Compute_returns_Expirado_once_expiresAt_has_passed()
    {
        var status = InviteCodeStatusCalculator.Compute(
            revokedAt: null, redeemedByUserId: null, expiresAt: Now.AddHours(-1), now: Now);

        status.Should().Be(InviteCodeStatus.Expirado);
    }

    [Fact]
    public void Compute_returns_Usado_when_redeemed_even_if_also_expired()
    {
        var status = InviteCodeStatusCalculator.Compute(
            revokedAt: null, redeemedByUserId: Guid.NewGuid(), expiresAt: Now.AddHours(-1), now: Now);

        status.Should().Be(InviteCodeStatus.Usado);
    }

    [Fact]
    public void Compute_returns_Revogado_even_if_also_redeemed_or_expired()
    {
        var status = InviteCodeStatusCalculator.Compute(
            revokedAt: Now.AddMinutes(-5), redeemedByUserId: Guid.NewGuid(), expiresAt: Now.AddHours(-1), now: Now);

        status.Should().Be(InviteCodeStatus.Revogado);
    }
}
