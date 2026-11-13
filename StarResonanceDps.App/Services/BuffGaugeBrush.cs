// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Media;
using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.Services;

/// <summary>
/// バフ・デバフの行のゲージの塗りと長さ。プレイヤーから開いた一覧・エンティティリストから開いた一覧・バフ・デバフグループ・蘇生リストが使う。
/// </summary>
public static class BuffGaugeBrush
{
    /// <summary>
    /// 設定の2色で左から右へのグラデーションにする(停止位置はエンティティリストの HP バーと同じ)。
    /// </summary>
    public static LinearGradientBrush Create(BuffGaugeColorConfig colors)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5)
        };

        var start = ResolveColor(colors, "GaugeStart");
        var end = ResolveColor(colors, "GaugeEnd");
        brush.GradientStops.Add(new GradientStop(start, 0d));
        brush.GradientStops.Add(new GradientStop(start, 0.05d));
        brush.GradientStops.Add(new GradientStop(end, 0.95d));
        brush.GradientStops.Add(new GradientStop(end, 1d));
        brush.Freeze();
        return brush;
    }

    /// <summary>ゲージが満タンになる残り時間(秒)。自動なら null(行ごとの長さを使う)。</summary>
    public static int? GetLengthSeconds(int gaugeLengthIndex)
    {
        return WidgetConfigDefaults.BuffGaugeLengthSeconds[gaugeLengthIndex];
    }

    /// <summary>
    /// 残り時間から、ゲージの長さ(0〜1)と 0 になるまでの秒(表示側はこのぶんかけて縮める)を出す。
    /// 満タンで頭打ちの間はまだ縮まない(縮み始めるのは残りがゲージの長さを切ってから)。長さが 0 以下ならずっと満タン。
    /// </summary>
    public static (double Ratio, double DecaySeconds) GetBarState(double remainingSeconds, double lengthSeconds)
    {
        if (lengthSeconds <= 0d)
        {
            return (1d, 0d);
        }

        var ratio = Math.Clamp(remainingSeconds / lengthSeconds, 0d, 1d);
        var decaySeconds = remainingSeconds <= lengthSeconds ? Math.Max(remainingSeconds, 0d) : 0d;
        return (ratio, decaySeconds);
    }

    private static Color ResolveColor(BuffGaugeColorConfig colors, string key)
    {
        var palette = colors.Palettes.TryGetValue(key, out var saved)
            ? saved
            : WidgetConfigDefaults.CreateDefaultGaugeColors(key);
        var selectedIndex = colors.Indexes.TryGetValue(key, out var index)
            ? index
            : WidgetConfigDefaults.MinClassColorIndex;
        var selectedColor = palette.Count == 0
            ? WidgetConfigDefaults.CreateDefaultGaugeColors(key)[0]
            : palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)];

        var color = ColorUtilities.TryParseHex(selectedColor, out var parsed)
            ? parsed
            : Colors.Gray;

        // 不透明度はメーターのクラスカラーと同じ作りで、表示に使う色のアルファへ掛ける。
        // 設定画面の色見本は素のままにしたいので、ここだけで掛ける。
        var opacity = Math.Clamp(
            colors.Opacity,
            WidgetConfigDefaults.MinClassColorOpacity,
            WidgetConfigDefaults.MaxClassColorOpacity);

        return Color.FromArgb(
            (byte)Math.Round(opacity / 100d * byte.MaxValue, MidpointRounding.AwayFromZero),
            color.R,
            color.G,
            color.B);
    }
}
