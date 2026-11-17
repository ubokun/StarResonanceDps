// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Globalization;
using System.Windows.Data;

namespace StarResonanceDps.App.Converters;

/// <summary>
/// マップの地図の画像に掛ける不透明度。値は [地図の濃さ(設定「マップの不透明度」、0〜1), 窓の枠の層の今の不透明度]。
/// 地図の画像は枠の層(窓の背景と同じ層)に置くので、そのまま掛けると濃さが枠の層の不透明度との積になる。
/// 枠の層の不透明度がどの値でも地図が設定の濃さで見えるよう、枠の層の画像と、中身の層(印の後ろ)の画像の2枚に分けて掛ける。
/// パラメーターなしは枠の層の画像に掛ける値で、地図の濃さを枠の層の不透明度で割る。枠の層のほうが薄ければ 1(背景を覆う)。
/// パラメーターが <c>Overlay</c> なら中身の層の画像に掛ける値で、枠の層の画像だけでは足りない分。足りていれば 0。
/// どちらかの値が届いていない間は 0(画像を出さない)。
/// </summary>
public sealed class MapImageOpacityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (!TryGetOpacity(values, 0, out var mapOpacity) || !TryGetOpacity(values, 1, out var frameOpacity))
        {
            return 0d;
        }

        if (parameter as string == "Overlay")
        {
            return mapOpacity > frameOpacity
                ? (mapOpacity - frameOpacity) / (1d - frameOpacity)
                : 0d;
        }

        return mapOpacity < frameOpacity
            ? mapOpacity / frameOpacity
            : 1d;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static bool TryGetOpacity(IReadOnlyList<object> values, int index, out double opacity)
    {
        if (index < values.Count && values[index] is double number && double.IsFinite(number))
        {
            opacity = Math.Clamp(number, 0d, 1d);
            return true;
        }

        opacity = 0d;
        return false;
    }
}
