namespace RuinaRPG.Infrastructure.Invites;

public class InviteOptions
{
    public int CodeExpirationHours { get; set; } = 48;

    /// <summary>
    /// Parses the configured expiration-in-hours value, falling back to <paramref name="fallback"/>
    /// for null, empty/whitespace, or malformed input — mirrors
    /// ImageStorageOptions.ResolveMaxSizeMb's reasoning: Docker Compose substitutes an empty
    /// string (not "unset") for an undefined ${INVITE_CODE_EXPIRATION_HOURS} on a pre-existing
    /// .env file that predates this setting, and int.Parse(x ?? "48") does not catch that case.
    /// </summary>
    public static int ResolveCodeExpirationHours(string? raw, int fallback = 48) =>
        int.TryParse(raw, out var parsed) ? parsed : fallback;
}
