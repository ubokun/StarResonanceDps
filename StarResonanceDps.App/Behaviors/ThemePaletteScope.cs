// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Behaviors;

/// <summary>
/// 部品の内側だけを、メイン画面の色ではなく渡した色の組(ウィジェットの色)で染める振る舞い。
///
/// <para>
/// 使い方: 部品に <c>ThemePaletteScope.Palette="{Binding ThemePalette}"</c> を付ける。部品の資源にメイン画面と同じ鍵(<c>Brush.*</c>)で
/// その色を書く(<see cref="ThemeManager.WriteBrushes"/>)ので、内側の共通のスタイル(選択リストの行・スクロールバーなど)はそのままその色で描かれる。
/// 開いたリスト(ポップアップ)の中も部品の資源を引くので同じ色になる。null を渡すと書いた鍵を消し、メイン画面の色に戻る。
/// </para>
/// </summary>
public static class ThemePaletteScope
{
    public static readonly DependencyProperty PaletteProperty = DependencyProperty.RegisterAttached(
        "Palette",
        typeof(ThemeColorPalette),
        typeof(ThemePaletteScope),
        new PropertyMetadata(null, OnPaletteChanged));

    public static ThemeColorPalette? GetPalette(DependencyObject obj)
    {
        return (ThemeColorPalette?)obj.GetValue(PaletteProperty);
    }

    public static void SetPalette(DependencyObject obj, ThemeColorPalette? value)
    {
        obj.SetValue(PaletteProperty, value);
    }

    private static void OnPaletteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        if (e.NewValue is ThemeColorPalette palette)
        {
            ThemeManager.WriteBrushes(element.Resources, palette);
        }
        else
        {
            ThemeManager.RemoveBrushes(element.Resources);
        }
    }
}
