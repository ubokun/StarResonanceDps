// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Globalization;
using System.Windows.Data;

namespace StarResonanceDps.App.Converters;

/// <summary>
/// エンティティ情報の中身の幅から、1行に並べる枠(ヘイト・バフ・デバフ)の数を決め、HP ゲージの長さか枠の並びの幅を返す。
/// 枠の数は (中身の幅 − 枠の並びの左端) ÷ 枠の幅 の切り捨て(1以上)。
/// ゲージの長さは 枠の数 × 枠の幅 + 枠の並びの左端 − ゲージの左端(ゲージの右端を枠の並びの右端にそろえる)。
/// パラメーターが <c>Slots</c> なら枠の並びの幅(枠の数 × 枠の幅)を返す。大きさは XAML から渡す。
/// </summary>
public sealed class EntityInfoGaugeWidthConverter : IValueConverter
{
    /// <summary>枠の幅。</summary>
    public double SlotWidth { get; set; }

    /// <summary>中身の左端から枠の並びの左端まで。</summary>
    public double SlotsLeft { get; set; }

    /// <summary>中身の左端からゲージの左端まで。</summary>
    public double GaugeLeft { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var width = value is double number && double.IsFinite(number) ? number : 0d;
        var slotCount = SlotWidth > 0d
            ? Math.Max(Math.Floor((width - SlotsLeft) / SlotWidth), 1d)
            : 1d;
        var slotsWidth = slotCount * SlotWidth;
        return parameter as string == "Slots"
            ? slotsWidth
            : Math.Max(slotsWidth + SlotsLeft - GaugeLeft, 0d);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
