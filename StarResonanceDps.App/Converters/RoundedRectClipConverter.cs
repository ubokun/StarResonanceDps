// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace StarResonanceDps.App.Converters;

/// <summary>
/// 長さの変わるゲージの塗りを切る角丸の四角。値は [幅, 高さ](切る要素の実際の大きさ)。角の丸みは XAML から渡す。
/// 長さが決まっているゲージは、切り抜きの四角を XAML に直に書く(エンティティリストの HP ゲージ)。
/// </summary>
public sealed class RoundedRectClipConverter : IMultiValueConverter
{
    public double RadiusX { get; set; }

    public double RadiusY { get; set; }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var geometry = new RectangleGeometry(
            new Rect(0d, 0d, GetFiniteDouble(values, 0), GetFiniteDouble(values, 1)),
            RadiusX,
            RadiusY);
        geometry.Freeze();
        return geometry;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static double GetFiniteDouble(IReadOnlyList<object> values, int index)
    {
        return index < values.Count && values[index] is double number && double.IsFinite(number)
            ? Math.Max(number, 0d)
            : 0d;
    }
}
