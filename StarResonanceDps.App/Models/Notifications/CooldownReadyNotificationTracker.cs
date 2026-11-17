// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>
/// 通知「リキャスト完了」の判定。ウィジェットを開いていなくても、通知がオンで対象があれば <see cref="NotificationMonitor"/> が一定の間隔で <see cref="Poll"/> を呼ぶ。
///
/// <para>
/// 対象は自分のスキルだけ(値はゲームが送る自分の CD、<see cref="SkillCooldownStateStore"/>)。ほかのプレイヤーの CD はアプリの推定で、知らせる時刻がずれるので見ない。
/// 対象のスキル(<see cref="NotificationWatchTarget"/>)の CD が、使えるようになるまで通知条件の秒数より長い間に構え、秒数を切ったときに1回出す。
/// チャージのスキルは、残りの回数が 0 の間だけ構える(0回から1回に戻ったときに出す)。
/// 判定を始めた時点(通知をオンにした・アプリを起動した・対象か通知条件を選び直した)で、既に秒数を切っている CD には出さない。
/// </para>
///
/// <para>
/// 名前は表示言語のスキル名で、内部ID注記と内部名は使わない(読み上げで日本語以外を読めないため。予兆技と同じ)。名前の無いスキルは「何か」。
/// 構えた CD の控えはスキルの番号だけで、数は対象の自分のスキルの数まで。自分が分からなくなった(ログアウト・キャプチャの停止・キャラクターの切り替え)ら、出さずに捨てる。
/// </para>
/// </summary>
public sealed class CooldownReadyNotificationTracker
{
    /// <summary>構えている(使えるようになるまで通知条件の秒数より長かった)スキルの番号。</summary>
    private readonly HashSet<int> _armedSkillIds = [];

    private readonly List<int> _coolingSkillIds = [];

    /// <summary>構えを解くスキルと、使えるようになったか(false は控えが消えて出さないもの)。</summary>
    private readonly List<(int SkillId, bool IsReady)> _finishedSkills = [];

    private long _selfUuid;

    /// <param name="leadSeconds">使えるようになる何秒前に出すか。0 は使えるようになったとき。</param>
    public CooldownReadyNotificationTracker(NotificationWatchTarget target, int leadSeconds)
    {
        Target = target;
        LeadSeconds = leadSeconds;
    }

    public NotificationWatchTarget Target { get; }

    public int LeadSeconds { get; }

    public void Poll()
    {
        var selfUuid = AppState.PlayerUUID;
        if (selfUuid != _selfUuid)
        {
            _armedSkillIds.Clear();
            _selfUuid = selfUuid;
        }

        if (selfUuid == 0)
        {
            return;
        }

        _coolingSkillIds.Clear();
        SkillCooldownStateStore.CopySelfCooldownSkillIds(selfUuid, _coolingSkillIds);
        foreach (var skillId in _coolingSkillIds)
        {
            if (!_armedSkillIds.Contains(skillId)
                && SkillCooldownStateStore.GetSelfSecondsUntilUsable(selfUuid, skillId) > LeadSeconds
                && Target.Matches(skillId, static id => id, CombatDataCatalog.GetSkillNameWithoutInternalId))
            {
                _armedSkillIds.Add(skillId);
            }
        }

        _finishedSkills.Clear();
        foreach (var skillId in _armedSkillIds)
        {
            // null は控えが消えた(自分が分からなくなった)ので、使えるようになったとは言えない。出さずに捨てる。
            var secondsUntilUsable = SkillCooldownStateStore.GetSelfSecondsUntilUsable(selfUuid, skillId);
            if (secondsUntilUsable is null || secondsUntilUsable <= LeadSeconds)
            {
                _finishedSkills.Add((skillId, secondsUntilUsable is not null));
            }
        }

        foreach (var (skillId, isReady) in _finishedSkills)
        {
            _armedSkillIds.Remove(skillId);
            if (isReady)
            {
                Notify(skillId);
            }
        }
    }

    private static void Notify(int skillId)
    {
        var skillName = CombatDataCatalog.GetSkillNameWithoutInternalId(skillId);
        if (string.IsNullOrEmpty(skillName))
        {
            skillName = LocalizationManager.Instance.GetString("Timeline_UnnamedCastSkill");
        }

        NotificationService.Instance.Notify(
            NotificationKind.CooldownReady,
            NotificationTextFormatter.FormatCooldownReady(
                NotificationTextFormatter.ResolveFormat(NotificationKind.CooldownReady),
                skillName));
    }
}
