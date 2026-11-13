// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Serilog;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>
/// 通知「入場確認」。コンテンツに入るときの承認画面(ゲームの入場の承認画面。マッチングでもパーティでも同じ画面)が開くたびに出す(重なりは消さない)。
/// ウィジェットを開いていなくても、通知がオンなら <see cref="NotificationMonitor"/> が呼ぶ。
///
/// <para>
/// {Content} は、マッチングならマッチング先のコンテンツ名、パーティなら入る場所の名前(マスターは難易度まで)。
/// 引けなかった(表に無い番号・種類が分からない)ときは「不明」にして出し、ログに残す。
/// </para>
/// </summary>
public static class EntryConfirmationNotifier
{
    /// <summary>マッチングが成立して承諾待ちになった。</summary>
    public static void NotifyMatch(Zproto.EMatchType matchType, long matchTypeUuid)
    {
        var content = CombatDataCatalog.GetMatchTargetName(matchType, matchTypeUuid);
        if (string.IsNullOrEmpty(content))
        {
            Log.Warning("No content name for the match target: {MatchType} {MatchTypeUuid}", matchType, matchTypeUuid);
        }

        Notify(content);
    }

    /// <summary>パーティでコンテンツに入るときの承認が始まった。</summary>
    public static void NotifyTeamEntry(int sceneId, int masterModeDifficulty)
    {
        var content = CombatDataCatalog.GetSceneNameWithoutInternalId(sceneId, masterModeDifficulty);
        if (string.IsNullOrEmpty(content))
        {
            Log.Warning("No content name for the team entry: {SceneId} {Difficulty}", sceneId, masterModeDifficulty);
        }

        Notify(content);
    }

    private static void Notify(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            content = LocalizationManager.Instance.GetString("PlayerInfo_Unknown");
        }

        NotificationService.Instance.Notify(
            NotificationKind.EntryConfirmation,
            NotificationTextFormatter.FormatEntryConfirmation(
                NotificationTextFormatter.ResolveFormat(NotificationKind.EntryConfirmation),
                content));
    }
}
