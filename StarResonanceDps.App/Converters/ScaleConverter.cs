// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StarResonanceDps.App.Converters;

/// <summary>
/// 倍率(値)に <c>ConverterParameter</c> の大きさを掛ける。渡す先が <see cref="Thickness"/> なら、パラメーターを余白として読み、四辺それぞれに掛ける。
/// 拡大の変形(<c>RenderTransform</c>)の代わりに、大きさそのものに倍率を掛けて組むのに使う。
/// 変形で拡大すると、影の効果を持つ文字が画素の途中に置かれる倍率でにじむ。
/// </summary>
public sealed class ScaleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double scale)
        {
            throw new InvalidOperationException($"ScaleConverter expects a double scale, but got {value?.GetType().Name ?? "null"}.");
        }

        if (parameter is not string text)
        {
            throw new InvalidOperationException("ScaleConverter needs the size as ConverterParameter.");
        }

        if (targetType == typeof(Thickness))
        {
            var thickness = (Thickness)new ThicknessConverter().ConvertFromInvariantString(text)!;
            return new Thickness(
                thickness.Left * scale,
                thickness.Top * scale,
                thickness.Right * scale,
                thickness.Bottom * scale);
        }

        return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture) * scale;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
