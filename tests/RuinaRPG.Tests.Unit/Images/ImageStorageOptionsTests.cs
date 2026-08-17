using FluentAssertions;
using RuinaRPG.Infrastructure.Images;

namespace RuinaRPG.Tests.Unit.Images;

public class ImageStorageOptionsTests
{
    // Docker Compose substitutes an empty string (not "unset") for ${IMG_MAX_SIZE_MB} when a
    // host's pre-existing .env file doesn't define that variable — a bare `?? "10"` fallback
    // does not catch that case, so this must fall back on empty/whitespace/malformed input too,
    // not only on a literal null.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    public void ResolveMaxSizeMb_falls_back_to_the_default_for_missing_or_malformed_input(string? raw)
    {
        var result = ImageStorageOptions.ResolveMaxSizeMb(raw);

        result.Should().Be(10);
    }

    [Fact]
    public void ResolveMaxSizeMb_parses_a_valid_value()
    {
        var result = ImageStorageOptions.ResolveMaxSizeMb("25");

        result.Should().Be(25);
    }

    [Fact]
    public void ResolveMaxSizeMb_accepts_a_custom_fallback()
    {
        var result = ImageStorageOptions.ResolveMaxSizeMb(null, fallback: 42);

        result.Should().Be(42);
    }
}
