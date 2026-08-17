using FluentAssertions;
using RuinaRPG.Domain.Images;

namespace RuinaRPG.Tests.Unit.Images;

public class ImageValidatorTests
{
    private static readonly byte[] PngMagicBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
    private static readonly byte[] JpegMagicBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];
    private static readonly byte[] GifMagicBytes = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] WebpMagicBytes = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];
    private static readonly byte[] NotAnImage = "this is plain text, not an image"u8.ToArray();

    [Theory]
    [MemberData(nameof(SupportedFormats))]
    public void Validate_accepts_every_supported_format_within_the_size_limit(byte[] magicBytes)
    {
        var result = ImageValidator.Validate(magicBytes, maxSizeBytes: 1_000_000);

        result.Should().Be(ImageValidationResult.Valid);
    }

    public static IEnumerable<object[]> SupportedFormats()
    {
        yield return [PngMagicBytes];
        yield return [JpegMagicBytes];
        yield return [GifMagicBytes];
        yield return [WebpMagicBytes];
    }

    [Fact]
    public void Validate_rejects_bytes_that_dont_match_any_supported_magic_number()
    {
        var result = ImageValidator.Validate(NotAnImage, maxSizeBytes: 1_000_000);

        result.Should().Be(ImageValidationResult.UnsupportedFormat);
    }

    [Fact]
    public void Validate_ignores_a_spoofed_extension_and_checks_real_bytes()
    {
        // The caller never passes a filename/extension to this method at all — this test documents
        // that fact: even "malicious.png" bytes that are actually plain text still fail validation,
        // because Validate only ever looks at the bytes, never a claimed name or Content-Type.
        var result = ImageValidator.Validate(NotAnImage, maxSizeBytes: 1_000_000);

        result.Should().Be(ImageValidationResult.UnsupportedFormat);
    }

    [Fact]
    public void Validate_rejects_a_valid_format_that_exceeds_the_size_limit()
    {
        var result = ImageValidator.Validate(PngMagicBytes, maxSizeBytes: 5);

        result.Should().Be(ImageValidationResult.TooLarge);
    }

    [Fact]
    public void Validate_checks_size_before_declaring_format_unsupported_is_irrelevant_when_both_fail()
    {
        // Format is checked first; a too-small buffer that doesn't even contain a full magic number
        // is UnsupportedFormat, not a false Valid.
        var result = ImageValidator.Validate([0x00, 0x01], maxSizeBytes: 1_000_000);

        result.Should().Be(ImageValidationResult.UnsupportedFormat);
    }
}
