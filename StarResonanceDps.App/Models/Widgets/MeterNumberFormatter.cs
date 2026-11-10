using System.Globalization;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// 大きな数値を、全体設定「数値」の表記で単位つきに縮める。番号0は英語表記(K・M・B・T)、1は日本語表記(万・億・兆)で、単位の字はどちらも表示言語の文言。
/// 単位の決め方は、メーターの数値とグラフの軸の数字で同じ。
/// </summary>
public static class MeterNumberFormatter
{
    /// <summary>メーター・詳細の数値。単位を付けた値は小数点以下2位で固定する(1.20K のように末尾の0も出す)。単位を付けない値は整数に小数を足さない。</summary>
    public static string Format(double value, int formatIndex)
    {
        return FormatWithUnit(value, formatIndex, scaledFormat: "F2", unscaledFormat: "0.##");
    }

    /// <summary>グラフの軸の数字。小数は要る桁だけ出す(1.5M・750K・0)。</summary>
    public static string FormatCompact(double value, int formatIndex)
    {
        return FormatWithUnit(value, formatIndex, scaledFormat: "0.##", unscaledFormat: "0");
    }

    private static string FormatWithUnit(double value, int formatIndex, string scaledFormat, string unscaledFormat)
    {
        var normalized = double.IsFinite(value)
            ? Math.Max(value, 0d)
            : 0d;
        var (divisor, unit) = formatIndex == 1
            ? GetWanUnit(normalized)
            : GetKmbtUnit(normalized);

        return unit is null
            ? normalized.ToString(unscaledFormat, CultureInfo.CurrentCulture)
            : (normalized / divisor).ToString(scaledFormat, CultureInfo.CurrentCulture) + unit;
    }

    private static (double Divisor, string? Unit) GetKmbtUnit(double value)
    {
        var localization = LocalizationManager.Instance;
        return value switch
        {
            >= 1_000_000_000_000d => (1_000_000_000_000d, localization.GetString("NumberUnit_Kmbt_Trillion")),
            >= 1_000_000_000d => (1_000_000_000d, localization.GetString("NumberUnit_Kmbt_Billion")),
            >= 1_000_000d => (1_000_000d, localization.GetString("NumberUnit_Kmbt_Million")),
            >= 1_000d => (1_000d, localization.GetString("NumberUnit_Kmbt_Thousand")),
            _ => (1d, null)
        };
    }

    private static (double Divisor, string? Unit) GetWanUnit(double value)
    {
        var localization = LocalizationManager.Instance;
        return value switch
        {
            >= 1_000_000_000_000d => (1_000_000_000_000d, localization.GetString("NumberUnit_Wan_Trillion")),
            >= 100_000_000d => (100_000_000d, localization.GetString("NumberUnit_Wan_HundredMillion")),
            >= 10_000d => (10_000d, localization.GetString("NumberUnit_Wan_TenThousand")),
            _ => (1d, null)
        };
    }
}
