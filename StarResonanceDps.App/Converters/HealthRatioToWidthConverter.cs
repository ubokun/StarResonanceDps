// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Globalization;
using System.Windows.Data;

namespace StarResonanceDps.App.Converters;

public sealed class HealthRatioToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var ratio = GetFiniteDouble(values, 0);
        var availableWidth = GetFiniteDouble(values, 1);
        return Math.Clamp(ratio, 0d, 1d) * Math.Max(availableWidth, 0d);
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
