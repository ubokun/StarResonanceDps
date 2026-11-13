// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Media;

namespace StarResonanceDps.App.Services;

/// <summary>
/// Per-widget rendering colors for the floating widget window.
/// Surface-based elements are rendered with opaque colors as one layer, then the configured
/// window opacity is applied to that layer. The close icon stays opaque.
/// </summary>
public sealed class WidgetWindowThemePalette
{
    // Blend(from, to, 0.75) results in 25% text and 75% surface.
    private const double DividerSurfaceWeight = 0.75;
    private const double CloseSurfaceWeight = 0.50;
    private const double ScrollThumbSurfaceWeight = 0.625;
    private const double NonTextContrastSurfaceWeight = 0.075;

    /// <summary>
    /// 非テキストUIの不透明度の下限。
    ///
    /// <para>
    /// ウィジェットは <c>AllowsTransparency="True"</c> の階層化ウィンドウなので、
    /// <b>アルファ0のピクセルはOSレベルでクリックが素通りする</b>。枠の不透明度が0になると
    /// 窓全体が掴めなくなり、ヘッダーをドラッグできない。ここで0にならないよう床を敷く。
    /// <b>これ以上は下げられない。</b> 階層化ウィンドウのアルファは8bitなので、
    /// 非ゼロを保てる最小は 1/255 ≒ 0.39%。0.1% は丸めで 0 になり素通りに戻る。
    /// 0.5% なら丸めの向きに関係なくアルファ1が残り、目視ではまず分からない。
    /// </para>
    /// </summary>
    public const double MinimumNonTextOpacity = 0.005;

    /// <summary>
    /// アクティブ時に確保する不透明度。設定値がこれ未満でも、アクティブの間だけここまで上げる。
    /// 最小近くのままだとスクロールバー・ヘッダー/フッターの分割線・ボタンが見えず操作できない。
    /// </summary>
    public const double ActiveMinimumNonTextOpacity = 0.5;

    /// <summary>
    /// メニューとツールチップの背景の不透明度。<b>ウィジェットの設定には連動させない。</b>
    /// 本体を薄くしても、開いたメニューは読めなければ意味がないため固定にしている。
    /// </summary>
    public const double MenuOpacityValue = 0.5;

    private WidgetWindowThemePalette(
        Color surface,
        Color menuSurface,
        Color divider,
        Color close,
        Color scrollThumb,
        double nonTextOpacity)
    {
        WidgetWindowSurface = surface;
        WidgetWindowMenuSurface = menuSurface;
        WidgetWindowDivider = divider;
        WidgetWindowClose = close;
        WidgetWindowScrollThumb = scrollThumb;
        NonTextOpacity = nonTextOpacity;
        ActiveNonTextOpacity = Math.Max(nonTextOpacity, ActiveMinimumNonTextOpacity);

        WidgetWindowSurfaceBrush = CreateBrush(WidgetWindowSurface);
        WidgetWindowMenuSurfaceBrush = CreateBrush(WidgetWindowMenuSurface);
        WidgetWindowDividerBrush = CreateBrush(WidgetWindowDivider);
        WidgetWindowCloseBrush = CreateBrush(WidgetWindowClose);
        WidgetWindowScrollThumbBrush = CreateBrush(WidgetWindowScrollThumb);
    }

    public Color WidgetWindowSurface { get; }
    public Color WidgetWindowMenuSurface { get; }
    public Color WidgetWindowDivider { get; }
    public Color WidgetWindowClose { get; }
    public Color WidgetWindowScrollThumb { get; }

    /// <summary>
    /// Opacity shared by the widget window's background, menus, divider, scroll thumb, and future non-text body UI.
    /// The close icon intentionally does not use this value.
    /// </summary>
    public double NonTextOpacity { get; }

    /// <summary>
    /// アクティブ時に使う不透明度。設定値と <see cref="ActiveMinimumNonTextOpacity"/> の大きいほう。
    /// 切り替えはスタイルのトリガーで行うので、この値は不変のまま持てる
    /// (パレットを作り直すと一瞬消えて見えるため、作り直さない)。
    /// </summary>
    public double ActiveNonTextOpacity { get; }

    /// <summary>メニュー・ツールチップ用。常に <see cref="MenuOpacityValue"/>。</summary>
    public double MenuOpacity => MenuOpacityValue;

    public SolidColorBrush WidgetWindowSurfaceBrush { get; }
    public SolidColorBrush WidgetWindowMenuSurfaceBrush { get; }
    public SolidColorBrush WidgetWindowDividerBrush { get; }
    public SolidColorBrush WidgetWindowCloseBrush { get; }
    public SolidColorBrush WidgetWindowScrollThumbBrush { get; }

    public static WidgetWindowThemePalette Create(
        Color windowSurface,
        int opacityPercent,
        Color? backgroundImageAverageColor = null)
    {
        var surface = Color.FromRgb(windowSurface.R, windowSurface.G, windowSurface.B);
        var menuSurface = backgroundImageAverageColor is { } imageColor
            ? Color.FromRgb(imageColor.R, imageColor.G, imageColor.B)
            : surface;
        var opacity = Math.Max(
            Math.Clamp(opacityPercent, 0, 100) / 100d,
            MinimumNonTextOpacity);
        var readableBase = ColorUtilities.GetReadableTextColor(menuSurface);
        var nonTextContrast = ColorUtilities.Blend(
            readableBase,
            menuSurface,
            NonTextContrastSurfaceWeight);
        var divider = ColorUtilities.Blend(nonTextContrast, menuSurface, DividerSurfaceWeight);
        var close = ColorUtilities.Blend(nonTextContrast, menuSurface, CloseSurfaceWeight);
        var scrollThumb = ColorUtilities.Blend(nonTextContrast, menuSurface, ScrollThumbSurfaceWeight);

        return new WidgetWindowThemePalette(
            surface,
            menuSurface,
            divider,
            close,
            scrollThumb,
            opacity);
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
