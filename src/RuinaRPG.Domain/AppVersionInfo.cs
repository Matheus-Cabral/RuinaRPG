namespace RuinaRPG.Domain;

/// <summary>
/// Single source of truth for the app's current version — compared against
/// ApplicationUser.LastSeenAppVersion by AuthController.Me() to decide whether to show the
/// changelog popup. Bump this (and ChangelogDialog.razor's hardcoded text) on every release that
/// should re-surface the popup to GMs who already dismissed an older version.
/// </summary>
public static class AppVersionInfo
{
    public const string Current = "1.3.1";
}
