using System.Text.RegularExpressions;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// 被ダメログの右の書式(設定「スキル名」)。差し込める項目は スキル名 / エンティティ名 / タイプ。
/// </summary>
public static partial class DamageTakenLogHitInfoFormatter
{
    /// <summary>設定の差し込む項目の一覧。項目名は既存の文言(スキル詳細の列の見出し・エンティティリストの名前)を使う。</summary>
    public static IReadOnlyList<(string Key, string LabelResourceKey, string Placeholder)> FieldDefinitions { get; } =
    [
        ("SkillName", "Metric_SkillName", "{SkillName}"),
        ("EntityName", "Settings_EntityInfo_Field_Name", "{EntityName}"),
        ("Type", "Metric_DamageMode", "{Type}")
    ];

    /// <summary>
    /// 書式に値を差し込む。項目の名前は大文字と小文字を区別しない(スキル詳細の書式と同じ)。
    /// 1回の置き換えで全部の項目を埋めるので、値の中の <c>{Type}</c> などは置き換えない。
    /// </summary>
    public static string Format(string? formatString, string skillName, string entityName, string typeText)
    {
        if (string.IsNullOrEmpty(formatString))
        {
            return string.Empty;
        }

        return GetFieldRegex().Replace(formatString, match => match.Groups[1].Value.ToUpperInvariant() switch
        {
            "SKILLNAME" => skillName,
            "ENTITYNAME" => entityName,
            _ => typeText
        });
    }

    /// <summary>被弾1回の右の文字。エンティティ名は加害者のパーティでの表示名(タイムラインと同じ)。</summary>
    public static string Format(string? formatString, DamageTakenLogHit hit)
    {
        return Format(
            formatString,
            FormatSourceName(hit),
            PlayerInfoFormatFormatter.GetPartyDisplayName(hit.Attacker),
            FormatDamageModeText(hit.DamageMode));
    }

    /// <summary>
    /// 技(またはバフ)の名前。落下は「落下」、名前が無ければ「攻撃」(タイムラインのダメージの技の行と同じ)、内部ID注記は全体設定に従う。
    /// </summary>
    private static string FormatSourceName(DamageTakenLogHit hit)
    {
        var localization = LocalizationManager.Instance;
        if (hit.IsFall)
        {
            return localization.GetString("DamageTakenLog_FallSkill");
        }

        var name = string.IsNullOrEmpty(hit.SourceName)
            ? localization.GetString("CombatEventLog_UnnamedSkill")
            : hit.SourceName;
        return hit.IsBuffSource
            ? CombatDataCatalog.AppendBuffInternalId(name, hit.SourceId)
            : CombatDataCatalog.AppendSkillInternalId(name, hit.SourceId);
    }

    /// <summary>書式の <c>{Type}</c>。値はスキル詳細の書式の <c>{Type}</c> と同じ(物理 / 魔法 / 無分類)。</summary>
    private static string FormatDamageModeText(Zproto.EDamageMode mode)
    {
        var localization = LocalizationManager.Instance;
        return mode switch
        {
            Zproto.EDamageMode.DamagePhysical => localization.GetString("Metric_DamageMode_Physical"),
            Zproto.EDamageMode.DamageMagical => localization.GetString("Metric_DamageMode_Magical"),
            _ => localization.GetString("Metric_DamageMode_Normal")
        };
    }

    /// <summary>設定画面の見本。値は実データではなく、書式の形を見せるためのもの(スキル詳細・エンティティリストの見本と同じ)。</summary>
    public static string FormatPreview(string? formatString)
    {
        var localization = LocalizationManager.Instance;
        return Format(
            formatString,
            localization.GetString("Settings_SkillInfo_PreviewName"),
            localization.GetString("Settings_EntityInfo_PreviewName"),
            localization.GetString("Metric_DamageMode_Physical"));
    }

    [GeneratedRegex(@"\{(SkillName|EntityName|Type)\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetFieldRegex();
}
