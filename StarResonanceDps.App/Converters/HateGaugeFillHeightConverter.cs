// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Globalization;
using System.Windows.Data;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Converters;

/// <summary>
/// エンティティリストのヘイトのゲージの充填の高さ(下から)。値は [割合, ゲージの実際の幅, ゲージの実際の高さ]
/// (<see cref="HateBadgeImages.GetGaugeFillHeight"/>)。ゲージの大きさは XAML で決める。
/// パラメーターが <c>Empty</c> なら、塗っていない部分の高さ(上から、ゲージの高さ − 充填の高さ)を返す(黒の地を塗る範囲)。
/// </summary>
public sealed class HateGaugeFillHeightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var height = GetFiniteDouble(values, 2);
        var fillHeight = HateBadgeImages.GetGaugeFillHeight(
            GetFiniteDouble(values, 0),
            GetFiniteDouble(values, 1),
            height);
        return parameter as string == "Empty"
            ? Math.Max(height - fillHeight, 0d)
            : fillHeight;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static double GetFiniteDouble(IReadOnlyList<object> values, int index)
    {
        return index < values.Count && values[index] is double number && double.IsFinite(number)
            ? number
            : 0d;
    }
}
