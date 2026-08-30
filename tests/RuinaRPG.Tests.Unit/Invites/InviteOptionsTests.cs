using FluentAssertions;
using RuinaRPG.Infrastructure.Invites;

namespace RuinaRPG.Tests.Unit.Invites;

public class InviteOptionsTests
{
    // Same reasoning as ImageStorageOptions.ResolveMaxSizeMb: Docker Compose substitutes an
    // empty string (not "unset") for ${INVITE_CODE_EXPIRATION_HOURS} when a host's pre-existing
    // .env file doesn't define that variable — a bare `?? "48"` fallback does not catch that.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    public void ResolveCodeExpirationHours_falls_back_to_the_default_for_missing_or_malformed_input(string? raw)
    {
        var result = InviteOptions.ResolveCodeExpirationHours(raw);

        result.Should().Be(48);
    }

    [Fact]
    public void ResolveCodeExpirationHours_parses_a_valid_value()
    {
        var result = InviteOptions.ResolveCodeExpirationHours("72");

        result.Should().Be(72);
    }

    [Fact]
    public void ResolveCodeExpirationHours_accepts_a_custom_fallback()
    {
        var result = InviteOptions.ResolveCodeExpirationHours(null, fallback: 24);

        result.Should().Be(24);
    }
}
