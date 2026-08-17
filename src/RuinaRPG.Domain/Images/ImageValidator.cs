namespace RuinaRPG.Domain.Images;

public static class ImageValidator
{
    public static ImageValidationResult Validate(byte[] fileBytes, int maxSizeBytes)
    {
        if (!IsSupportedFormat(fileBytes))
            return ImageValidationResult.UnsupportedFormat;

        if (fileBytes.Length > maxSizeBytes)
            return ImageValidationResult.TooLarge;

        return ImageValidationResult.Valid;
    }

    private static bool IsSupportedFormat(byte[] bytes) =>
        StartsWith(bytes, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) // PNG
        || StartsWith(bytes, [0xFF, 0xD8, 0xFF]) // JPEG/JPG
        || StartsWith(bytes, [0x47, 0x49, 0x46, 0x38]) // GIF (GIF87a/GIF89a)
        || IsWebp(bytes);

    private static bool IsWebp(byte[] bytes) =>
        bytes.Length >= 12
        && StartsWith(bytes, [0x52, 0x49, 0x46, 0x46]) // "RIFF"
        && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50; // "WEBP"

    private static bool StartsWith(byte[] bytes, byte[] prefix) =>
        bytes.Length >= prefix.Length && prefix.AsSpan().SequenceEqual(bytes.AsSpan(0, prefix.Length));
}
