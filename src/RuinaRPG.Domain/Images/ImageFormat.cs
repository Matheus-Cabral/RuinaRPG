namespace RuinaRPG.Domain.Images;

public enum ImageFormat
{
    Png,
    Jpeg,
    Gif,
    Webp
}

public static class ImageFormatExtensions
{
    /// <summary>
    /// The extension that must be used on disk for a file whose bytes were detected as this
    /// format. Always derive the on-disk extension from this — never from a client-supplied
    /// filename — or an attacker can smuggle valid image bytes under a dangerous extension
    /// (e.g. ".html") and have nginx serve them same-origin as that content type.
    /// </summary>
    public static string ToFileExtension(this ImageFormat format) => format switch
    {
        ImageFormat.Png => ".png",
        ImageFormat.Jpeg => ".jpg",
        ImageFormat.Gif => ".gif",
        ImageFormat.Webp => ".webp",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
    };

    /// <summary>
    /// The MIME type that must be persisted for a file whose bytes were detected as this format.
    /// Always derive the stored Content-Type from this — never store the client's claimed
    /// Content-Type header verbatim.
    /// </summary>
    public static string ToMimeType(this ImageFormat format) => format switch
    {
        ImageFormat.Png => "image/png",
        ImageFormat.Jpeg => "image/jpeg",
        ImageFormat.Gif => "image/gif",
        ImageFormat.Webp => "image/webp",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
    };
}
