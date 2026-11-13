// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Media;

namespace StarResonanceDps.App.Services;

/// <summary>
/// One derived color set generated from a single widget or application window-surface color.
/// The generated brushes are immutable so item-level themes remain isolated from the global theme resources.
/// </summary>
public sealed class ThemeColorPalette
{
    private const double PanelContrastWeight = 0.030;
    private const double BorderSoftContrastWeight = 0.090;
    private const double BorderContrastWeight = 0.160;
    private const double BorderStrongContrastWeight = 0.230;
    private const double ControlContrastWeight = 0.100;
    private const double ControlActiveContrastWeight = 0.150;
    private const double InputContrastWeight = 0.020;
    private const double InputHoverContrastWeight = 0.060;
    private const double TextPrimaryThemeWeight = 0.075;
    private const double TextSecondaryThemeWeight = 0.380;
    private const double TextMutedThemeWeight = 0.620;

    private ThemeColorPalette(
        Color windowSurface,
        Color panelBackground,
        Color border,
        Color borderSoft,
        Color borderStrong,
        Color controlBackground,
        Color controlActiveBackground,
        Color inputBackground,
        Color inputHoverBackground,
        Color textPrimary,
        Color textSecondary,
        Color textMuted)
    {
        WindowSurface = windowSurface;
        PanelBackground = panelBackground;
        Border = border;
        BorderSoft = borderSoft;
        BorderStrong = borderStrong;
        ControlBackground = controlBackground;
        ControlActiveBackground = controlActiveBackground;
        InputBackground = inputBackground;
        InputHoverBackground = inputHoverBackground;
        TextPrimary = textPrimary;
        TextSecondary = textSecondary;
        TextMuted = textMuted;

        WindowSurfaceBrush = CreateBrush(WindowSurface);
        PanelBackgroundBrush = CreateBrush(PanelBackground);
        BorderBrush = CreateBrush(Border);
        BorderSoftBrush = CreateBrush(BorderSoft);
        BorderStrongBrush = CreateBrush(BorderStrong);
        ControlBackgroundBrush = CreateBrush(ControlBackground);
        ControlActiveBackgroundBrush = CreateBrush(ControlActiveBackground);
        InputBackgroundBrush = CreateBrush(InputBackground);
        InputHoverBackgroundBrush = CreateBrush(InputHoverBackground);
        TextPrimaryBrush = CreateBrush(TextPrimary);
        TextSecondaryBrush = CreateBrush(TextSecondary);
        TextMutedBrush = CreateBrush(TextMuted);
    }

    public Color WindowSurface { get; }
    public Color PanelBackground { get; }
    public Color Border { get; }
    public Color BorderSoft { get; }
    public Color BorderStrong { get; }
    public Color ControlBackground { get; }
    public Color ControlActiveBackground { get; }
    public Color InputBackground { get; }
    public Color InputHoverBackground { get; }
    public Color TextPrimary { get; }
    public Color TextSecondary { get; }
    public Color TextMuted { get; }

    public SolidColorBrush WindowSurfaceBrush { get; }
    public SolidColorBrush PanelBackgroundBrush { get; }
    public SolidColorBrush BorderBrush { get; }
    public SolidColorBrush BorderSoftBrush { get; }
    public SolidColorBrush BorderStrongBrush { get; }
    public SolidColorBrush ControlBackgroundBrush { get; }
    public SolidColorBrush ControlActiveBackgroundBrush { get; }
    public SolidColorBrush InputBackgroundBrush { get; }
    public SolidColorBrush InputHoverBackgroundBrush { get; }
    public SolidColorBrush TextPrimaryBrush { get; }
    public SolidColorBrush TextSecondaryBrush { get; }
    public SolidColorBrush TextMutedBrush { get; }

    public static ThemeColorPalette Create(Color windowSurface)
    {
        var contrastColor = ColorUtilities.GetReadableTextColor(windowSurface);

        return new ThemeColorPalette(
            windowSurface,
            ColorUtilities.Blend(windowSurface, contrastColor, PanelContrastWeight),
            ColorUtilities.Blend(windowSurface, contrastColor, BorderContrastWeight),
            ColorUtilities.Blend(windowSurface, contrastColor, BorderSoftContrastWeight),
            ColorUtilities.Blend(windowSurface, contrastColor, BorderStrongContrastWeight),
            ColorUtilities.Blend(windowSurface, contrastColor, ControlContrastWeight),
            ColorUtilities.Blend(windowSurface, contrastColor, ControlActiveContrastWeight),
            ColorUtilities.Blend(windowSurface, contrastColor, InputContrastWeight),
            ColorUtilities.Blend(windowSurface, contrastColor, InputHoverContrastWeight),
            ColorUtilities.Blend(contrastColor, windowSurface, TextPrimaryThemeWeight),
            ColorUtilities.Blend(contrastColor, windowSurface, TextSecondaryThemeWeight),
            ColorUtilities.Blend(contrastColor, windowSurface, TextMutedThemeWeight));
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
