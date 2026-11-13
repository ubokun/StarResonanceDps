// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Config;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

/// <summary>
/// バフ・デバフ一覧の行の名前を、一覧の表示設定「バフ名」の書式と「付与者プレイヤー名の表示」で組む。
/// プレイヤーから開いた一覧とエンティティリストから開いた一覧、エンティティ情報のバフ・デバフの枠が使う。内部ID注記は書式を当てた後ろに付ける。
/// </summary>
public static class BuffListRowName
{
    /// <param name="targetName">対象者名(<c>{TargetName}</c>)。</param>
    /// <param name="level">対象者のレベル(<c>{Level}</c>)。</param>
    public static string Format(
        BuffListWidgetSettingsConfig settings,
        PlayerBuffSnapshot snapshot,
        string targetName,
        int level)
    {
        return Format(settings.BuffInfoFormatString, settings.CasterNameDisplayModeIndex, snapshot, targetName, level);
    }

    /// <param name="buffInfoFormatString">設定「バフ名」の書式。</param>
    /// <param name="casterNameDisplayModeIndex">設定「付与者プレイヤー名の表示」(<see cref="WidgetConfigDefaults.CasterNameDisplayShowIndex"/> ほか)。</param>
    /// <param name="targetName">対象者名(<c>{TargetName}</c>)。</param>
    /// <param name="level">対象者のレベル(<c>{Level}</c>)。</param>
    public static string Format(
        string? buffInfoFormatString,
        int casterNameDisplayModeIndex,
        PlayerBuffSnapshot snapshot,
        string targetName,
        int level)
    {
        var casterName = ShowsCaster(casterNameDisplayModeIndex, snapshot)
            ? BuffDebuffCardContent.GetCasterName(snapshot.Caster)
            : string.Empty;
        var name = BuffInfoFormatFormatter.Format(
            snapshot.NameWithoutInternalId,
            targetName,
            casterName,
            level,
            buffInfoFormatString);
        return CombatDataCatalog.AppendBuffInternalId(name, snapshot.BaseId);
    }

    /// <summary>「他者付与のみ」は、付与者が対象者本人(本人の召喚物を含む)なら出さない。</summary>
    private static bool ShowsCaster(int casterNameDisplayModeIndex, PlayerBuffSnapshot snapshot)
        => casterNameDisplayModeIndex switch
        {
            WidgetConfigDefaults.CasterNameDisplayHideIndex => false,
            WidgetConfigDefaults.CasterNameDisplayAppliedByOthersIndex => !snapshot.IsCasterTarget,
            _ => true,
        };
}
