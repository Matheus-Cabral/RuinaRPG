namespace RuinaRPG.Domain.Images;

public static class ImageValidator
{
    public static ImageValidationResult Validate(byte[] fileBytes, int maxSizeBytes)
    {
        if (DetectFormat(fileBytes) is null)
            return ImageValidationResult.UnsupportedFormat;

        if (fileBytes.Length > maxSizeBytes)
            return ImageValidationResult.TooLarge;

        return ImageValidationResult.Valid;
    }

    /// <summary>
    /// Identifies the image format from the actual bytes (magic numbers), independent of any
    /// filename or claimed Content-Type. Returns null when the bytes don't match a supported
    /// format. Callers that already know <see cref="Validate"/> returned
    /// <see cref="ImageValidationResult.Valid"/> can rely on this never returning null for the
    /// same bytes.
    /// </summary>
    public static ImageFormat? DetectFormat(byte[] bytes)
    {
        if (StartsWith(bytes, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) // PNG
            return ImageFormat.Png;
        if (StartsWith(bytes, [0xFF, 0xD8, 0xFF])) // JPEG/JPG
            return ImageFormat.Jpeg;
        if (StartsWith(bytes, [0x47, 0x49, 0x46, 0x38])) // GIF (GIF87a/GIF89a)
            return ImageFormat.Gif;
        if (IsWebp(bytes))
            return ImageFormat.Webp;

        return null;
    }

    private static bool IsWebp(byte[] bytes) =>
        bytes.Length >= 12
        && StartsWith(bytes, [0x52, 0x49, 0x46, 0x46]) // "RIFF"
        && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50; // "WEBP"

    private static bool StartsWith(byte[] bytes, byte[] prefix) =>
        bytes.Length >= prefix.Length && prefix.AsSpan().SequenceEqual(bytes.AsSpan(0, prefix.Length));
}
