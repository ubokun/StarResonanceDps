// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// 状態の印の TIPS の文。理由ごとに文言の鍵が1つ決まっていて、その表はここだけに置く。
/// 当てはまる理由のうち、優先の高い理由を1つだけ出す(赤の理由が先なので、出る理由はいつも円の色と同じ色)。緑は空。
/// </summary>
public static class WidgetDataStatusText
{
    /// <summary>
    /// 優先の順。赤の後に黄。赤どうしは読めないが先(マップ移動をしても残り、赤が消えない理由になる)。
    /// </summary>
    private static readonly WidgetDataStatusReasons[] ReasonOrder =
    [
        WidgetDataStatusReasons.Unreadable,
        WidgetDataStatusReasons.MapOrRelogin,
        WidgetDataStatusReasons.PastChatRelogin,
        WidgetDataStatusReasons.PartyScreen,
        WidgetDataStatusReasons.PlayerClick,
        WidgetDataStatusReasons.PlayerApproach,
        WidgetDataStatusReasons.EnemyApproach,
    ];

    public static string Format(WidgetDataStatus status)
    {
        foreach (var reason in ReasonOrder)
        {
            if (status.Reasons.HasFlag(reason))
            {
                return LocalizationManager.Instance.GetString(GetKey(reason));
            }
        }

        return string.Empty;
    }

    private static string GetKey(WidgetDataStatusReasons reason)
    {
        return reason switch
        {
            WidgetDataStatusReasons.MapOrRelogin => "WidgetDataStatus_MapOrRelogin",
            WidgetDataStatusReasons.PartyScreen => "WidgetDataStatus_PartyScreen",
            WidgetDataStatusReasons.PlayerClick => "WidgetDataStatus_PlayerClick",
            WidgetDataStatusReasons.PlayerApproach => "WidgetDataStatus_PlayerApproach",
            WidgetDataStatusReasons.EnemyApproach => "WidgetDataStatus_EnemyApproach",
            WidgetDataStatusReasons.Unreadable => "WidgetDataStatus_Unreadable",
            WidgetDataStatusReasons.PastChatRelogin => "WidgetDataStatus_PastChatRelogin",
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "No status text for the reason."),
        };
    }
}
