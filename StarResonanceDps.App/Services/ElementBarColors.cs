// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Media;
using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 属性カラー(<see cref="ElementColorWidgetSettingsConfig"/>。与ダメ詳細などでは節の名前がアイコンカラーで、物理・魔法・無分類の行も持つ)からバーの色を作る。
/// スキル詳細・被ダメログ・与ダメ詳細などが使う。不透明度が掛かるのはウィジェットの側だけで、設定画面の色見本は素のまま。
/// </summary>
public static class ElementBarColors
{
    /// <summary>その属性の色見本で選んでいる色。</summary>
    public static Color Resolve(ElementColorWidgetSettingsConfig settings, Zproto.EDamageProperty element)
    {
        var key = element.ToString();
        return Resolve(settings, key, WidgetConfigDefaults.CreateDefaultElementColors(key), WidgetConfigDefaults.MinClassColorIndex + 1);
    }

    /// <summary>そのタイプ(物理・魔法・無分類)の色見本で選んでいる色。</summary>
    public static Color Resolve(ElementColorWidgetSettingsConfig settings, Zproto.EDamageMode mode)
    {
        var key = mode.ToString();
        return Resolve(settings, key, WidgetConfigDefaults.CreateDefaultDamageModeColors(key), WidgetConfigDefaults.MinClassColorIndex);
    }

    private static Color Resolve(ElementColorWidgetSettingsConfig settings, string key, List<string> defaults, int missingColorIndex)
    {
        var palette = settings.ColorPalettes.TryGetValue(key, out var colors) && colors.Count > 0
            ? colors
            : defaults;
        var selectedIndex = settings.ColorIndexes.TryGetValue(key, out var index)
            ? index
            : missingColorIndex;

        var hex = palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)];
        return ColorUtilities.TryParseHex(hex, out var color)
            ? color
            : Colors.Gray;
    }

    /// <summary>バーの塗り。色に不透明度を掛ける。</summary>
    public static SolidColorBrush CreateBarBrush(ElementColorWidgetSettingsConfig settings, Color color)
    {
        var opacity = Math.Clamp(
            settings.ColorOpacity,
            WidgetConfigDefaults.MinClassColorOpacity,
            WidgetConfigDefaults.MaxClassColorOpacity);

        var brush = new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(opacity / 100d * byte.MaxValue, MidpointRounding.AwayFromZero),
            color.R,
            color.G,
            color.B));
        brush.Freeze();
        return brush;
    }
}
