namespace RuinaRPG.Infrastructure.Images;

public class ImageStorageOptions
{
    public required string ImagesPath { get; set; }
    public int MaxSizeMb { get; set; }

    /// <summary>
    /// Parses the configured max-size-in-MB value, falling back to <paramref name="fallback"/>
    /// for null, empty/whitespace, or malformed input. Docker Compose substitutes an empty
    /// string (not "unset") for an undefined ${IMG_MAX_SIZE_MB}, e.g. on a pre-existing .env
    /// file that predates this setting — int.Parse(x ?? "10") does not catch that case and
    /// crashes the API at startup, so this must use TryParse rather than the null-coalescing
    /// fallback pattern used elsewhere.
    /// </summary>
    public static int ResolveMaxSizeMb(string? raw, int fallback = 10) =>
        int.TryParse(raw, out var parsed) ? parsed : fallback;
}
