// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Text.RegularExpressions;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// 装備詳細の行の書式。作りはスキル詳細の行名(<see cref="SkillInfoFormatFormatter"/>)と同じで、
/// 差し込める項目が 装備名 / 装備レベル / メインステータス になる。
/// 空になった括弧・連続したハイフンの後始末も同じ。
/// </summary>
public static partial class EquipmentInfoFormatFormatter
{
    /// <summary>設定画面の見本に使う装備。</summary>
    private const int PreviewEquipmentId = 2011001;

    public static string Format(
        string equipName,
        string equipLevel,
        string mainStat,
        string? formatString)
    {
        if (string.IsNullOrEmpty(formatString))
        {
            return string.Empty;
        }

        var result = formatString;

        result = GetEquipNameRegex().Replace(result, equipName);
        result = GetEquipLevelRegex().Replace(result, equipLevel);
        result = GetMainStatRegex().Replace(result, mainStat);

        result = GetCollapseWhitespaceRegex().Replace(result, " ");
        result = GetEmptyParenthesisRegex().Replace(result, string.Empty);
        result = GetEmptyBracketRegex().Replace(result, string.Empty);
        result = GetRepeatedHyphensRegex().Replace(result, " - ");
        result = GetLeadingOrTrailingHyphenRegex().Replace(result, string.Empty);

        return result.Trim();
    }

    /// <summary>装備1つの行の文字。値は表示中の言語で引く。</summary>
    /// <param name="breakThroughTime">突破の段階が分かっていれば(自分の装備)その段階。<see cref="FormatLevel"/> と同じ。</param>
    public static string Format(EquipmentDefinition equipment, string? formatString, int? breakThroughTime = null)
    {
        return Format(
            CombatDataCatalog.GetItemName(equipment.Id),
            FormatLevel(equipment, breakThroughTime),
            CreateMainStatText(equipment),
            formatString);
    }

    /// <summary>
    /// 設定画面の見本。実在の装備の値で作る。表に無ければ行と同じく <c>(装備ID)</c>。
    /// </summary>
    public static string FormatPreview(string? formatString)
    {
        var equipment = CombatDataCatalog.GetEquipment(PreviewEquipmentId);
        return equipment is null
            ? $"({PreviewEquipmentId})"
            : Format(equipment, formatString);
    }

    /// <summary>
    /// 装備レベル(書式の <c>{EquipLevel}</c> と行の右の文字)。
    /// 突破の段階が分かれば(自分の装備)その段階の装備Lv。他人の突破段階は届かないので、
    /// 突破がある装備は段階0 〜 最大の段階の範囲で出す。
    /// </summary>
    /// <param name="breakThroughTime">突破の段階。装備の段階の範囲に収まっていること(呼び出し側が確かめる)。</param>
    public static string FormatLevel(EquipmentDefinition equipment, int? breakThroughTime = null)
    {
        var first = equipment.Stages[0].Gs.ToString(CultureInfo.CurrentCulture);
        var gearScore = breakThroughTime is { } stage
            ? equipment.Stages[stage].Gs.ToString(CultureInfo.CurrentCulture)
            : equipment.Stages.Count >= 2
                ? $"{first}~{equipment.Stages[^1].Gs.ToString(CultureInfo.CurrentCulture)}"
                : first;

        return LocalizationManager.Instance.Format("PlayerEquipment_Level", gearScore);
    }

    /// <summary>メインステータス(筋力 / 知力 / 敏捷)の名前。基礎に無い装備は空。</summary>
    private static string CreateMainStatText(EquipmentDefinition equipment)
    {
        return equipment.MainStat == 0
            ? string.Empty
            : LocalizationManager.Instance.GetString($"PlayerStatus_{equipment.MainStat}");
    }

    [GeneratedRegex(@"\{EquipName\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetEquipNameRegex();

    [GeneratedRegex(@"\{EquipLevel\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetEquipLevelRegex();

    [GeneratedRegex(@"\{MainStat\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetMainStatRegex();

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
