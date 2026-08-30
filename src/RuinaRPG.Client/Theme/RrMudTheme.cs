using MudBlazor;

namespace RuinaRPG.Client.Theme;

/// <summary>
/// The app's single MudTheme instance. Deliberately sets no PaletteLight/PaletteDark — color
/// never lives here. Every --mud-palette-* variable MudBlazor would generate from those defaults
/// is overridden in wwwroot/css/theme.css (see Task 3), which stays the one source of color
/// truth for the Sol/Lua themes. This class only owns structure: typography and border radius.
/// </summary>
public static class RrMudTheme
{
    private static readonly string[] HeadingFontFamily = ["Cormorant Garamond", "Georgia", "Times New Roman", "serif"];
    private static readonly string[] BodyFontFamily = ["Inter", "-apple-system", "Segoe UI", "Roboto", "Helvetica", "Arial", "sans-serif"];

    public static readonly MudTheme Instance = new()
    {
        LayoutProperties = new LayoutProperties
        {
            // Mirrors --rr-radius-sm in theme.css. Kept as a literal here (not a color, so the
            // spec's "no palette duplicated in C#" rule doesn't apply) — if --rr-radius-sm ever
            // changes, update this value to match.
            DefaultBorderRadius = "4px",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = BodyFontFamily },
            H1 = new H1Typography { FontFamily = HeadingFontFamily },
            H2 = new H2Typography { FontFamily = HeadingFontFamily },
            H3 = new H3Typography { FontFamily = HeadingFontFamily },
            H4 = new H4Typography { FontFamily = HeadingFontFamily },
            H5 = new H5Typography { FontFamily = HeadingFontFamily },
            H6 = new H6Typography { FontFamily = HeadingFontFamily },
            Body1 = new Body1Typography { FontFamily = BodyFontFamily },
            Body2 = new Body2Typography { FontFamily = BodyFontFamily },
            Button = new ButtonTypography { FontFamily = BodyFontFamily },
            Caption = new CaptionTypography { FontFamily = BodyFontFamily },
            Subtitle1 = new Subtitle1Typography { FontFamily = BodyFontFamily },
            Subtitle2 = new Subtitle2Typography { FontFamily = BodyFontFamily },
        },
    };
}
