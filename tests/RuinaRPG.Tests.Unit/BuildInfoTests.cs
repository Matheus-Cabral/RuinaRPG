using FluentAssertions;
using RuinaRPG.Domain;

namespace RuinaRPG.Tests.Unit;

public class BuildInfoTests
{
    [Theory]
    [InlineData("1.0.0+0123456789abcdef0123456789abcdef01234567", "0123456789abcdef0123456789abcdef01234567")]
    [InlineData("1.0.0+abc1234", "abc1234")]
    [InlineData("1.0.0", "dev")]
    [InlineData(null, "dev")]
    [InlineData("1.0.0+not-a-sha!", "dev")]
    public void Sha_is_the_hex_suffix_after_plus_or_dev(string? informational, string expected) =>
        BuildInfo.ShaFromInformationalVersion(informational).Should().Be(expected);
}
