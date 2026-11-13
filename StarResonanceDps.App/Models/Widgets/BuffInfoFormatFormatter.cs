// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Text.RegularExpressions;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// バフ・デバフカードとバフ・デバフ一覧の行のバフ名を組み立てる。
///
/// <para>
/// <b>プレイヤーとモンスターで同じ関数を通す。</b>どちらも名前とレベルを持つので、
/// 呼ぶ側が値を取り出して渡すだけでよい。
/// </para>
///
/// <para>
/// 値が空になって残った <c>()</c> <c>[]</c> は消し、空白を詰める(メーターのプレイヤー名の書式と同じ決まり)。
/// 改行はその後に入れるので、詰める対象にならない。
/// </para>
/// </summary>
public static partial class BuffInfoFormatFormatter
{
    /// <param name="casterName">付与者プレイヤー名。出さないときは空。</param>
    public static string Format(string buffName, string targetName, string casterName, int level, string? formatString)
    {
        var result = string.IsNullOrEmpty(formatString)
            ? buffName
            : formatString;

        result = GetBuffNameRegex().Replace(result, buffName);
        result = GetNameRegex().Replace(result, targetName);
        result = GetCasterRegex().Replace(result, casterName);
        result = GetLevelRegex().Replace(result, level.ToString());

        result = GetCollapseWhitespaceRegex().Replace(result, " ");
        result = GetEmptyParenthesisRegex().Replace(result, string.Empty);
        result = GetEmptyBracketRegex().Replace(result, string.Empty);
        result = result.Trim();

        result = GetNewLineRegex().Replace(result, Environment.NewLine);
        return result;
    }

    /// <param name="showsCaster">見本の付与者プレイヤー名を入れるか。一覧の「付与者プレイヤー名の表示」が非表示なら偽。</param>
    public static string FormatPreview(string? formatString, bool showsCaster = true)
    {
        return Format(
            LocalizationManager.Instance.GetString("Settings_BuffInfo_PreviewBuffName"),
            LocalizationManager.Instance.GetString("Settings_BuffInfo_PreviewName"),
            showsCaster ? LocalizationManager.Instance.GetString("Settings_BuffInfo_PreviewCaster") : string.Empty,
            50,
            formatString);
    }

    [GeneratedRegex(@"\{BuffName\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetBuffNameRegex();

    [GeneratedRegex(@"\{TargetName\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetNameRegex();

    [GeneratedRegex(@"\{CasterName\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetCasterRegex();

    [GeneratedRegex(@"\{Level\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetLevelRegex();

    [GeneratedRegex(@"\{NewLine\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetNewLineRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex GetCollapseWhitespaceRegex();

    [GeneratedRegex(@"\(\s*\)")]
    private static partial Regex GetEmptyParenthesisRegex();

    [GeneratedRegex(@"\[\s*\]")]
    private static partial Regex GetEmptyBracketRegex();
}
