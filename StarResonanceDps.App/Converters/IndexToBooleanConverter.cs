// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StarResonanceDps.App.Converters;

public sealed class IndexToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return TryReadIndex(value, out var selectedIndex)
            && TryReadIndex(parameter, out var targetIndex)
            && selectedIndex == targetIndex;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true && TryReadIndex(parameter, out var targetIndex)
            ? targetIndex
            : DependencyProperty.UnsetValue;
    }

    private static bool TryReadIndex(object? value, out int index)
    {
        switch (value)
        {
            case int intValue:
                index = intValue;
                return true;

            case string stringValue when int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                index = parsed;
                return true;

            default:
                index = 0;
                return false;
        }
    }
}
