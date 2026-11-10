using System.Diagnostics.CodeAnalysis;
using StarResonanceDps.App.Config;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグインが定義した設定(通知の項目の設定・ウィジェットの設定)を確かめ、本体が持つ写しを作る。
/// 決まりは SDK の <see cref="PluginSettingDefinition"/> の説明と同じ。理由はログに書く英語で返す。
/// </summary>
internal static class PluginSettingValidator
{
    /// <param name="allowsColor">色の設定を認めるか(ウィジェットだけ)。</param>
    /// <param name="usedIds">使った設定の ID(大文字と小文字を区別しない)。同じ所に並ぶ設定どうしで共有し、重なりを見る。</param>
    public static bool TryCopySettings(
        IReadOnlyList<PluginSettingDefinition>? settings,
        bool allowsColor,
        HashSet<string> usedIds,
        out IReadOnlyList<PluginSettingDefinition> copies,
        [NotNullWhen(false)] out string? error)
    {
        var result = new List<PluginSettingDefinition>();
        copies = result;
        foreach (var setting in settings ?? [])
        {
            if (setting is null)
            {
                error = "a setting is null.";
                return false;
            }

            if (!PluginIdRules.IsValid(setting.Id))
            {
                error = $"the setting ID '{setting.Id}' is missing or contains unsupported characters.";
                return false;
            }

            var id = setting.Id;
            if (!usedIds.Add(id))
            {
                error = $"the setting ID '{id}' is defined more than once.";
                return false;
            }

            if (!TryCopyRequiredTexts(setting.Names, out var names))
            {
                error = $"the setting '{id}' needs at least one name, and each culture only once.";
                return false;
            }

            IReadOnlyDictionary<string, string>? notes = null;
            if (setting.Notes is not null && (notes = PluginTexts.TryCopy(setting.Notes)) is null)
            {
                error = $"the setting '{id}' has a culture more than once in its notes.";
                return false;
            }

            if (!TryCopySetting(setting, id, names, notes, allowsColor, out var copy, out error))
            {
                return false;
            }

            result.Add(copy);
        }

        error = null;
        return true;
    }

    /// <summary>1言語以上あり、同じ言語が2つ無い文字の写し。</summary>
    public static bool TryCopyRequiredTexts(
        IReadOnlyDictionary<string, string>? texts,
        [NotNullWhen(true)] out IReadOnlyDictionary<string, string>? copy)
    {
        copy = PluginTexts.TryCopy(texts);
        return copy is { Count: > 0 };
    }

    private static bool TryCopySetting(
        PluginSettingDefinition setting,
        string id,
        IReadOnlyDictionary<string, string> names,
        IReadOnlyDictionary<string, string>? notes,
        bool allowsColor,
        [NotNullWhen(true)] out PluginSettingDefinition? copy,
        [NotNullWhen(false)] out string? error)
    {
        copy = null;
        switch (setting)
        {
            case PluginSwitchSetting switchSetting:
                copy = new PluginSwitchSetting { Id = id, Names = names, Notes = notes, DefaultValue = switchSetting.DefaultValue };
                break;

            case PluginChoiceSetting choiceSetting:
                if (!TryCopyOptions(choiceSetting.Options, out var options, out var optionError))
                {
                    error = $"the setting '{id}': {optionError}";
                    return false;
                }

                var defaultOption = options.FirstOrDefault(option =>
                    string.Equals(option.Id, choiceSetting.DefaultOptionId, StringComparison.OrdinalIgnoreCase));
                if (defaultOption is null)
                {
                    error = $"the setting '{id}' has the default option '{choiceSetting.DefaultOptionId}' that is not one of its options.";
                    return false;
                }

                copy = new PluginChoiceSetting
                {
                    Id = id,
                    Names = names,
                    Notes = notes,
                    Options = options,
                    DefaultOptionId = defaultOption.Id
                };
                break;

            case PluginNumberSetting numberSetting:
                if (numberSetting.Minimum < 0
                    || numberSetting.Maximum < numberSetting.Minimum
                    || numberSetting.DefaultValue < numberSetting.Minimum
                    || numberSetting.DefaultValue > numberSetting.Maximum)
                {
                    error = $"the setting '{id}' needs 0 <= minimum <= default <= maximum.";
                    return false;
                }

                copy = new PluginNumberSetting
                {
                    Id = id,
                    Names = names,
                    Notes = notes,
                    Minimum = numberSetting.Minimum,
                    Maximum = numberSetting.Maximum,
                    DefaultValue = numberSetting.DefaultValue
                };
                break;

            case PluginTextSetting textSetting:
                copy = new PluginTextSetting { Id = id, Names = names, Notes = notes, DefaultValue = textSetting.DefaultValue ?? string.Empty };
                break;

            case PluginSliderSetting sliderSetting:
                if (!double.IsFinite(sliderSetting.Minimum)
                    || !double.IsFinite(sliderSetting.Maximum)
                    || !double.IsFinite(sliderSetting.DefaultValue)
                    || !double.IsFinite(sliderSetting.Step)
                    || sliderSetting.Minimum >= sliderSetting.Maximum
                    || sliderSetting.Step <= 0
                    || sliderSetting.DefaultValue < sliderSetting.Minimum
                    || sliderSetting.DefaultValue > sliderSetting.Maximum)
                {
                    error = $"the setting '{id}' needs finite numbers with minimum < maximum, minimum <= default <= maximum and step > 0.";
                    return false;
                }

                copy = new PluginSliderSetting
                {
                    Id = id,
                    Names = names,
                    Notes = notes,
                    Minimum = sliderSetting.Minimum,
                    Maximum = sliderSetting.Maximum,
                    DefaultValue = sliderSetting.DefaultValue,
                    Step = sliderSetting.Step
                };
                break;

            case PluginColorSetting colorSetting:
                if (!allowsColor)
                {
                    error = $"the setting '{id}' is a color, which is only allowed in widget settings.";
                    return false;
                }

                if (!TryCopyColors(colorSetting.DefaultColors, out var colors))
                {
                    error = $"the setting '{id}' needs 1 to {WidgetConfigDefaults.MaxPaletteColorCount} different default colors written as #RRGGBB.";
                    return false;
                }

                copy = new PluginColorSetting { Id = id, Names = names, Notes = notes, DefaultColors = colors };
                break;

            default:
                error = $"the setting '{id}' is of an unsupported type '{setting.GetType().FullName}'.";
                return false;
        }

        error = null;
        return true;
    }

    private static bool TryCopyOptions(
        IReadOnlyList<PluginChoiceOption>? options,
        out IReadOnlyList<PluginChoiceOption> copies,
        [NotNullWhen(false)] out string? error)
    {
        var result = new List<PluginChoiceOption>();
        copies = result;
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in options ?? [])
        {
            if (option is null)
            {
                error = "an option is null.";
                return false;
            }

            if (!PluginIdRules.IsValid(option.Id))
            {
                error = $"the option ID '{option.Id}' is missing or contains unsupported characters.";
                return false;
            }

            var id = option.Id;
            if (!ids.Add(id))
            {
                error = $"the option ID '{id}' is defined more than once.";
                return false;
            }

            if (!TryCopyRequiredTexts(option.Names, out var names))
            {
                error = $"the option '{id}' needs at least one name, and each culture only once.";
                return false;
            }

            result.Add(new PluginChoiceOption { Id = id, Names = names });
        }

        if (result.Count == 0)
        {
            error = "a choice needs at least one option.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>#RRGGBB の色が1〜5個で、重ならないか。写しは本体の書き方(<see cref="ColorUtilities.ToHex"/>)にそろえる。</summary>
    private static bool TryCopyColors(IReadOnlyList<string>? colors, out IReadOnlyList<string> copies)
    {
        var result = new List<string>();
        copies = result;
        foreach (var hex in colors ?? [])
        {
            var trimmed = hex?.Trim() ?? string.Empty;
            if (trimmed.Length != 7 || trimmed[0] != '#' || !ColorUtilities.TryParseHex(trimmed, out var color))
            {
                return false;
            }

            var normalized = ColorUtilities.ToHex(color);
            if (result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            result.Add(normalized);
        }

        return result.Count is > 0 and <= WidgetConfigDefaults.MaxPaletteColorCount;
    }
}
