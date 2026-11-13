// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>
/// 通知「予兆技」の判定。ウィジェットを開いていなくても、通知がオンの間 <see cref="NotificationMonitor"/> が一定の間隔で <see cref="Poll"/> を呼ぶ。
///
/// <para>
/// <b>予告</b>はゲームの予告のバーが消える時刻(<see cref="BossDbmBarStore"/>)の、通知タブの通知条件の秒数前に、
/// <b>警告</b>は警告の技の開始が届いたとき(<see cref="WarningSkillCastStore"/>)に出す(警告に通知条件は使わない)。
/// 予告のバーがその秒数より短ければ、届いた時点で出す。同じ技に予告と警告の両方があっても、それぞれの時機に別々に出す。
/// どちらもライブの戦闘で決め、タイムラインが履歴を表示していても、計測の窓の外で記録が止まっていても止まらない。
/// ダンジョンが終わった後は、どちらの控えにも入らないので出ない。
/// 判定を始めた時点(通知をオンにした・アプリを起動した・通知条件を選び直した)で時機を過ぎたものは出さない。
/// </para>
///
/// <para>
/// 名前とスキル名はタイムラインの行と同じ引き方の公式の名前(予告は予告の行、警告は詠唱の行と同じ)で、名前の無い技は「何か」。
/// 内部名は使わない(読み上げで日本語以外を読めないため。タイムラインの行は内部名になることがある)。内部ID注記は付けない。
/// </para>
/// </summary>
public sealed class TelegraphedSkillNotificationTracker
{
    private DateTime _lastBarCheckUtc;

    /// <summary>
    /// もう出した予告(または判定を始めた時点で時機を過ぎていた予告)。同じ番号の予告が来直すと終わる時刻が変わるので、番号と終わる時刻の組で持つ。
    /// 終わる時刻が前回の確かめより前のものは、もう拾わないので捨てる。
    /// </summary>
    private readonly HashSet<(int DbmId, DateTime EndTimeUtc)> _notifiedBars = [];

    // 判定を始めた時点までに控えた警告は出さない。
    private long _lastWarningSequence = WarningSkillCastStore.Instance.LastSequence;

    /// <param name="leadSeconds">予告のバーが終わる何秒前に出すか。0 は終わったとき。</param>
    public TelegraphedSkillNotificationTracker(int leadSeconds)
    {
        Rebaseline(leadSeconds);
    }

    public int LeadSeconds { get; private set; }

    /// <summary>通知条件を選び直した。その時点で時機を過ぎた予告は出さない。警告はそのまま続けて見る。</summary>
    public void Rebaseline(int leadSeconds)
    {
        LeadSeconds = leadSeconds;
        _lastBarCheckUtc = DateTime.UtcNow;
        _notifiedBars.Clear();
        foreach (var bar in BossDbmBarStore.Instance.GetEndedBetween(DateTime.MinValue, _lastBarCheckUtc + TimeSpan.FromSeconds(leadSeconds)))
        {
            _notifiedBars.Add((bar.DbmId, bar.EndTimeUtc));
        }
    }

    public void Poll()
    {
        var now = DateTime.UtcNow;

        // 前回の確かめより後に終わり、今から通知条件の秒数のうちに終わる予告。前回の後に届いて、届いた時点で既に秒数を切っていた予告もここで拾う。
        foreach (var bar in BossDbmBarStore.Instance.GetEndedBetween(_lastBarCheckUtc, now + TimeSpan.FromSeconds(LeadSeconds)))
        {
            if (!_notifiedBars.Add((bar.DbmId, bar.EndTimeUtc)))
            {
                continue;
            }

            Notify(
                PlayerInfoFormatFormatter.GetPartyDisplayName(MeterSnapshotProvider.CreateCombatEventLogAnnouncementParty(bar.OwnerMonsterId)),
                MeterSnapshotProvider.GetTelegraphedAnnouncementSkillName(bar.SkillId));
        }

        _lastBarCheckUtc = now;
        _notifiedBars.RemoveWhere(bar => bar.EndTimeUtc <= now);

        foreach (var cast in WarningSkillCastStore.Instance.GetAfter(_lastWarningSequence))
        {
            _lastWarningSequence = cast.Sequence;
            Notify(PlayerInfoFormatFormatter.GetPartyDisplayName(cast.Caster), MeterSnapshotProvider.GetTelegraphedCastSkillName(cast.SkillId));
        }
    }

    private static void Notify(string name, string skillName)
    {
        if (string.IsNullOrEmpty(skillName))
        {
            skillName = LocalizationManager.Instance.GetString("Timeline_UnnamedCastSkill");
        }

        NotificationService.Instance.Notify(
            NotificationKind.TelegraphedSkill,
            NotificationTextFormatter.FormatTelegraphedSkill(
                NotificationTextFormatter.ResolveFormat(NotificationKind.TelegraphedSkill),
                name,
                skillName));
    }
}
