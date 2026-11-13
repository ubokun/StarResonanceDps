// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StarResonanceDps.App.Converters;

/// <summary>
/// <c>false</c> を <see cref="Visibility.Hidden"/> にする。
///
/// <para>
/// 標準の <c>BooleanToVisibilityConverter</c> は <see cref="Visibility.Collapsed"/> にするので、
/// 消した分だけ親の大きさが変わる。<b>位置を動かしたくない所ではこちらを使う。</b>
/// </para>
/// </summary>
public sealed class BooleanToVisibilityHiddenConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Visible : Visibility.Hidden;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}
