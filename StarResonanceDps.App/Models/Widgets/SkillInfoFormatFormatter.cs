// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Text.RegularExpressions;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// スキル詳細の行名の書式。作りはメーターのプレイヤー名(<see cref="PlayerInfoFormatFormatter"/>)と同じで、
/// 差し込める項目が スキル名 / 属性 / タイプ / ヒット数 / 会心率 になる。
/// 空になった括弧・連続したハイフンの後始末も同じ。
/// </summary>
public static partial class SkillInfoFormatFormatter
{
    public static string Format(
        string skillName,
        string elementText,
        string damageModeText,
        ulong hitCount,
        double critRate,
        string? formatString)
    {
        if (string.IsNullOrEmpty(formatString))
        {
            return string.Empty;
        }

        var result = formatString;

        result = GetSkillNameRegex().Replace(result, skillName);
        result = GetElementRegex().Replace(result, elementText);
        result = GetTypeRegex().Replace(result, damageModeText);
        result = GetHitsRegex().Replace(result, hitCount.ToString(CultureInfo.CurrentCulture));
        result = GetCritRateRegex().Replace(result, critRate.ToString("F2", CultureInfo.CurrentCulture) + "%");

        result = GetCollapseWhitespaceRegex().Replace(result, " ");
        result = GetEmptyParenthesisRegex().Replace(result, string.Empty);
        result = GetEmptyBracketRegex().Replace(result, string.Empty);
        result = GetRepeatedHyphensRegex().Replace(result, " - ");
        result = GetLeadingOrTrailingHyphenRegex().Replace(result, string.Empty);

        return result.Trim();
    }

    /// <summary>設定画面の見本。値は実データではなく、書式の形を見せるためのもの。</summary>
    public static string FormatPreview(string? formatString)
    {
        var localization = LocalizationManager.Instance;
        return Format(
            localization.GetString("Settings_SkillInfo_PreviewName"),
            localization.GetString("DamageProperty_Fire"),
            localization.GetString("Metric_DamageMode_Physical"),
            484UL,
            16.42d,
            formatString);
    }

    [GeneratedRegex(@"\{SkillName\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetSkillNameRegex();

    [GeneratedRegex(@"\{Element\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetElementRegex();

    [GeneratedRegex(@"\{Type\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetTypeRegex();

    [GeneratedRegex(@"\{Hits\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetHitsRegex();

    [GeneratedRegex(@"\{CritRate\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetCritRateRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex GetCollapseWhitespaceRegex();

    [GeneratedRegex(@"\(\s*\)")]
    private static partial Regex GetEmptyParenthesisRegex();

    [GeneratedRegex(@"\[\s*\]")]
    private static partial Regex GetEmptyBracketRegex();

    [GeneratedRegex(@"\s*-\s*-\s*")]
    private static partial Regex GetRepeatedHyphensRegex();

    [GeneratedRegex(@"^\s*-\s*|\s*-\s*$")]
    private static partial Regex GetLeadingOrTrailingHyphenRegex();
}
