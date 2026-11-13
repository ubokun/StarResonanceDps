// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Globalization;
using System.Windows.Data;

namespace StarResonanceDps.App.Converters;

/// <summary>
/// 要素を枠の中心にそろえるときの左端の位置。値は (枠の幅, 要素の幅)。
/// 要素が枠より広ければ負になり、左右に同じだけはみ出す。
/// </summary>
public sealed class CenteredOffsetConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var containerWidth = GetFiniteDouble(values, 0);
        var elementWidth = GetFiniteDouble(values, 1);
        return (containerWidth - elementWidth) / 2d;
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
