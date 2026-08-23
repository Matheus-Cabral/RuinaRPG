using FluentAssertions;
using RuinaRPG.Domain.Images;

namespace RuinaRPG.Tests.Unit.Images;

public class ImageFormatExtensionsTests
{
    [Theory]
    [InlineData(ImageFormat.Png, ".png")]
    [InlineData(ImageFormat.Jpeg, ".jpg")]
    [InlineData(ImageFormat.Gif, ".gif")]
    [InlineData(ImageFormat.Webp, ".webp")]
    public void ToFileExtension_maps_each_detected_format_to_its_extension(ImageFormat format, string expected)
    {
        format.ToFileExtension().Should().Be(expected);
    }

    [Theory]
    [InlineData(ImageFormat.Png, "image/png")]
    [InlineData(ImageFormat.Jpeg, "image/jpeg")]
    [InlineData(ImageFormat.Gif, "image/gif")]
    [InlineData(ImageFormat.Webp, "image/webp")]
    public void ToMimeType_maps_each_detected_format_to_its_mime_type(ImageFormat format, string expected)
    {
        format.ToMimeType().Should().Be(expected);
    }
}
