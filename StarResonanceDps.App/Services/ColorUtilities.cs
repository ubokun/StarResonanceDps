// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Windows.Media;

namespace StarResonanceDps.App.Services;

public static class ColorUtilities
{
    public static Color GetReadableTextColor(Color background)
    {
        return IsLight(background) ? Colors.Black : Colors.White;
    }

    public static bool IsLight(Color color)
    {
        return GetRelativeLuminance(color) > 0.5;
    }

    public static double GetRelativeLuminance(Color color)
    {
        static double ToLinear(byte channel)
        {
            var value = channel / 255.0;
            return value <= 0.03928
                ? value / 12.92
                : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * ToLinear(color.R)
            + 0.7152 * ToLinear(color.G)
            + 0.0722 * ToLinear(color.B);
    }

    public static Color Blend(Color from, Color to, double toWeight)
    {
        var weight = Math.Clamp(toWeight, 0d, 1d);
        return Color.FromRgb(
            BlendChannel(from.R, to.R, weight),
            BlendChannel(from.G, to.G, weight),
            BlendChannel(from.B, to.B, weight));
    }

    /// <summary>
    /// 重みを付けて複数の色を混ぜる(重みの合計で割った加重平均)。
    ///
    /// <para>
    /// <b>重みが1つも無ければ混ぜられないので false を返す。</b>
    /// 呼び出し側は、そのとき色を作ったことにせず、出すのをやめる。
    /// </para>
    /// </summary>
    public static bool TryBlendWeighted(IReadOnlyList<(Color Color, double Weight)> parts, out Color color)
    {
        color = Colors.White;

        var totalWeight = 0d;
        foreach (var part in parts)
        {
            if (part.Weight > 0d)
            {
                totalWeight += part.Weight;
            }
        }

        if (totalWeight <= 0d)
        {
            return false;
        }

        var red = 0d;
        var green = 0d;
        var blue = 0d;
        foreach (var part in parts)
        {
            if (part.Weight <= 0d)
            {
                continue;
            }

            var weight = part.Weight / totalWeight;
            red += part.Color.R * weight;
            green += part.Color.G * weight;
            blue += part.Color.B * weight;
        }

        color = Color.FromRgb(ToChannel(red), ToChannel(green), ToChannel(blue));
        return true;
    }

    public static string ToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static bool TryParseHex(string? raw, out Color color)
    {
        color = Colors.White;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var text = raw.Trim();
        if (text.StartsWith("#", StringComparison.Ordinal))
        {
            text = text[1..];
        }

        if (text.Length != 6)
        {
            return false;
        }

        foreach (var ch in text)
        {
            if (!Uri.IsHexDigit(ch))
            {
                return false;
            }
        }

        if (!byte.TryParse(text.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(text.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(text.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        color = Color.FromRgb(r, g, b);
        return true;
    }

    private static byte BlendChannel(byte from, byte to, double toWeight)
    {
        var value = from + ((to - from) * toWeight);
        return ToChannel(value);
    }

    private static byte ToChannel(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), byte.MinValue, byte.MaxValue);
    }


}
