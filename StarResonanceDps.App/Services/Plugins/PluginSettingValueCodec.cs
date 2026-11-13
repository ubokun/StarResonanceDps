// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Windows.Media;
using StarResonanceDps.App.Config;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグインの設定の値の保存の形。保存は文字(スイッチは true / false、選択リストは選択肢の ID、数は整数、スライダーは数、どれも言語に依らない書き方)。
/// 色は枠の色と選んでいる枠の番号で「番号;#RRGGBB,#RRGGBB」(本体の色の設定と同じく、枠は5つまで)。
/// 読むときは定義に合わせる: 読めない値と無くなった選択肢は既定、範囲の外は近いほうの端。
/// </summary>
internal static class PluginSettingValueCodec
{
    private const char ColorIndexSeparator = ';';
    private const char ColorListSeparator = ',';

    /// <summary>
    /// 保存した値を定義に合わせて読む。型はスイッチ <see cref="bool"/>、選択リスト <see cref="PluginChoiceValue"/>、
    /// 数 <see cref="long"/>、文字 <see cref="string"/>、スライダー <see cref="double"/>、色 <see cref="PluginColorPaletteValue"/>。
    /// </summary>
    public static object Read(PluginSettingDefinition definition, string? saved)
    {
        return definition switch
        {
            PluginColorSetting colorSetting => ReadColor(colorSetting, saved),
            PluginSwitchSetting switchSetting => bool.TryParse(saved, out var isOn) ? isOn : switchSetting.DefaultValue,
            PluginChoiceSetting choiceSetting => new PluginChoiceValue(
                choiceSetting.Options.FirstOrDefault(option => string.Equals(option.Id, saved, StringComparison.OrdinalIgnoreCase))?.Id
                ?? choiceSetting.DefaultOptionId),
            PluginNumberSetting numberSetting => long.TryParse(saved, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                ? Math.Clamp(number, numberSetting.Minimum, numberSetting.Maximum)
                : numberSetting.DefaultValue,
            PluginTextSetting textSetting => saved ?? textSetting.DefaultValue,
            PluginSliderSetting sliderSetting => double.TryParse(saved, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                && double.IsFinite(value)
                    ? Math.Clamp(value, sliderSetting.Minimum, sliderSetting.Maximum)
                    : sliderSetting.DefaultValue,
            _ => throw new ArgumentOutOfRangeException(nameof(definition), definition.GetType().FullName, "Unsupported plugin setting type.")
        };
    }

    /// <summary><see cref="Read"/> が返す型の値を、保存する文字にする。</summary>
    public static string Write(object value)
    {
        return value switch
        {
            bool isOn => isOn ? bool.TrueString : bool.FalseString,
            PluginChoiceValue choice => choice.OptionId,
            long number => number.ToString(CultureInfo.InvariantCulture),
            string text => text,
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            PluginColorPaletteValue palette => palette.SelectedIndex.ToString(CultureInfo.InvariantCulture)
                + ColorIndexSeparator
                + string.Join(ColorListSeparator, palette.Colors),
            _ => throw new ArgumentOutOfRangeException(nameof(value), value.GetType().FullName, "Unsupported plugin setting value.")
        };
    }

    /// <summary>
    /// 設定の全部を、保存した値から読む(保存が無い設定は既定)。プラグインへ渡す形(<see cref="PluginSettingValues"/>)で、色は選んでいる色。
    /// 鍵は設定の ID(大文字と小文字を区別しない)。
    /// </summary>
    public static IReadOnlyDictionary<string, object> ReadAll(
        IEnumerable<PluginSettingDefinition> definitions,
        IReadOnlyDictionary<string, string>? saved)
    {
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            string? savedValue = null;
            saved?.TryGetValue(definition.Id, out savedValue);
            var value = Read(definition, savedValue);
            values[definition.Id] = value is PluginColorPaletteValue palette ? palette.SelectedColor : value;
        }

        return values;
    }

    /// <summary>色の枠と選んでいる番号。枠が読めなければ既定の色で、番号は枠の範囲へ収める。</summary>
    private static PluginColorPaletteValue ReadColor(PluginColorSetting definition, string? saved)
    {
        var separatorIndex = saved?.IndexOf(ColorIndexSeparator) ?? -1;
        if (separatorIndex > 0
            && int.TryParse(saved.AsSpan(0, separatorIndex), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
        {
            var colors = new List<string>();
            foreach (var hex in saved![(separatorIndex + 1)..].Split(ColorListSeparator))
            {
                if (ColorUtilities.TryParseHex(hex, out var color))
                {
                    var normalized = ColorUtilities.ToHex(color);
                    if (!colors.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    {
                        colors.Add(normalized);
                    }
                }
            }

            if (colors.Count > 0)
            {
                var kept = colors.Take(WidgetConfigDefaults.MaxPaletteColorCount).ToArray();
                return new PluginColorPaletteValue(kept, Math.Clamp(index, 0, kept.Length - 1));
            }
        }

        return new PluginColorPaletteValue(definition.DefaultColors, 0);
    }
}

/// <summary>色の設定の値。枠の色(#RRGGBB)と選んでいる枠の番号。</summary>
internal sealed record PluginColorPaletteValue(IReadOnlyList<string> Colors, int SelectedIndex)
{
    public Color SelectedColor => ColorUtilities.TryParseHex(Colors[SelectedIndex], out var color)
        ? color
        : throw new InvalidOperationException($"The color '{Colors[SelectedIndex]}' is not #RRGGBB.");
}
