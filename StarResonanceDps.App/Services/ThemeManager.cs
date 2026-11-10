using System.Windows;
using System.Windows.Media;
using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.Services;

public sealed class ThemeManager
{
    private const byte WidgetCardImageButtonAlpha = 0x80;

    /// <summary>
    /// 色の組から書くブラシの鍵(<c>Brush.*</c>)と色。アプリ全体の資源(メイン画面の色)と、
    /// ウィジェットの色で染める範囲(<see cref="Behaviors.ThemePaletteScope"/>)が同じ一覧を使う。
    /// </summary>
    private static readonly (string Key, Func<ThemeColorPalette, Color> GetColor)[] BrushColors =
    [
        ("Brush.WindowSurface", colors => colors.WindowSurface),
        ("Brush.PanelBackground", colors => colors.PanelBackground),
        ("Brush.Border", colors => colors.Border),
        ("Brush.BorderSoft", colors => colors.BorderSoft),
        ("Brush.BorderStrong", colors => colors.BorderStrong),
        ("Brush.ControlBackground", colors => colors.ControlBackground),
        ("Brush.ControlActiveBackground", colors => colors.ControlActiveBackground),
        ("Brush.WidgetCardImageButtonBackground", colors => WithAlpha(colors.ControlBackground)),
        ("Brush.WidgetCardImageButtonActiveBackground", colors => WithAlpha(colors.ControlActiveBackground)),
        ("Brush.WidgetCardImageButtonBorder", colors => WithAlpha(colors.Border)),
        ("Brush.InputBackground", colors => colors.InputBackground),
        ("Brush.InputHoverBackground", colors => colors.InputHoverBackground),
        ("Brush.TextPrimary", colors => colors.TextPrimary),
        ("Brush.TextSecondary", colors => colors.TextSecondary),
        ("Brush.TextMuted", colors => colors.TextMuted)
    ];

    private static readonly Lazy<ThemeManager> LazyInstance = new(() => new ThemeManager());

    private ThemeManager()
    {
    }

    public static ThemeManager Instance => LazyInstance.Value;

    public void ApplyGlobalTheme(SettingsConfig settings)
    {
        var normalized = AppConfigDefaults.CloneNormalizedSettings(settings);
        var selectedHex = normalized.WindowColors[normalized.WindowColorIndex];

        if (!ColorUtilities.TryParseHex(selectedHex, out var windowSurface))
        {
            return;
        }

        ApplyWindowSurface(windowSurface);
    }

    public void ApplyWindowSurface(Color windowSurface)
    {
        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        WriteBrushes(resources, ThemeColorPalette.Create(windowSurface));
    }

    /// <summary>
    /// 色の組のブラシを、メイン画面と同じ鍵(<c>Brush.*</c>)で資源に書く。書いた資源の内側で <c>DynamicResource</c> の
    /// <c>Brush.*</c> を引く部品は、その色で描かれる。
    /// </summary>
    public static void WriteBrushes(ResourceDictionary resources, ThemeColorPalette colors)
    {
        foreach (var (key, getColor) in BrushColors)
        {
            SetThemeBrush(resources, key, getColor(colors));
        }
    }

    /// <summary><see cref="WriteBrushes"/> で書いた鍵を資源から消す。内側の部品は外側の資源(メイン画面の色)で描かれるようになる。</summary>
    public static void RemoveBrushes(ResourceDictionary resources)
    {
        foreach (var (key, _) in BrushColors)
        {
            resources.Remove(key);
        }
    }

    private static void SetThemeBrush(ResourceDictionary resources, string brushKey, Color color)
    {
        if (resources[brushKey] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
            return;
        }

        resources[brushKey] = new SolidColorBrush(color);
    }

    private static Color WithAlpha(Color color)
    {
        return Color.FromArgb(WidgetCardImageButtonAlpha, color.R, color.G, color.B);
    }
}
