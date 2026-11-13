// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>狙われ状態1つ。敵(エンティティリストの実体)が、通知タブの対象プレイヤーに入るプレイヤーを狙っている。</summary>
/// <param name="PlayerName">狙われている人の名前(プレイヤーリストの行と同じ)。</param>
/// <param name="EntityName">狙っている敵の名前。</param>
public readonly record struct TargetedState(long EntityUuid, long CharacterId, string PlayerName, string EntityName);

/// <summary>
/// 通知「狙われるプレイヤー」の判定。周りの敵の一覧か名簿が届くたびに、今の狙われ状態(通知タブの対象エンティティ・対象プレイヤーで絞ったもの)を渡す。
/// ウィジェットを開いていなくても動く(<see cref="NotificationMonitor"/>)。
///
/// <para>
/// 状態は「敵と狙われている人」の組ごとに数え、通知条件の秒数続いたら1回出す。通知条件が無条件(0秒)なら、狙われたその場で出す。
/// 続く間に敵が対象プレイヤーに入らない人に向いた(狙う相手がいなくなったも含む)・敵が周りから外れたら、その状態は終わりで出さない。
/// 同じ敵が対象プレイヤーに入る別の人に向いたら、その人の状態を初めから数える。
/// </para>
///
/// <para>
/// 続く秒数は時計で数える。敵の値が変わらない間は一覧が届かないため(HP低下と同じ)。
/// </para>
/// </summary>
public sealed class TargetedNotificationTracker
{
    /// <summary>控えた状態の経過を確かめる間隔。HP低下と同じ。</summary>
    private const int CheckIntervalMilliseconds = 250;

    /// <summary>今の狙われ状態。鍵は敵の UUID。<c>IsNotified</c> はこの状態でもう出したか(出さないと決めたものも含む)。</summary>
    private readonly Dictionary<long, (TargetedState Target, long Since, bool IsNotified)> _targetsByEntityUuid = [];

    private readonly DispatcherTimer _checkTimer;

    public TargetedNotificationTracker()
    {
        _checkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(CheckIntervalMilliseconds)
        };
        _checkTimer.Tick += CheckTimer_Tick;
    }

    /// <summary>
    /// 通知をオンにした・設定を選び直した。今の狙われ状態は「既に出したもの」として控え直す(その時点で狙われている人には出さない)。
    /// オフのときは空で呼ぶ。
    /// </summary>
    public void Rebaseline(IReadOnlyList<TargetedState> targets)
    {
        _targetsByEntityUuid.Clear();
        var now = Environment.TickCount64;
        foreach (var target in targets)
        {
            _targetsByEntityUuid[target.EntityUuid] = (target, now, true);
        }

        UpdateCheckTimer();
    }

    public void Observe(IReadOnlyList<TargetedState> targets)
    {
        var notifyDelayMilliseconds = GetNotifyDelayMilliseconds();
        var now = Environment.TickCount64;
        var entityUuids = new HashSet<long>();
        foreach (var target in targets)
        {
            entityUuids.Add(target.EntityUuid);
            if (_targetsByEntityUuid.TryGetValue(target.EntityUuid, out var current)
                && current.Target.CharacterId == target.CharacterId)
            {
                _targetsByEntityUuid[target.EntityUuid] = (target, current.Since, current.IsNotified);
                continue;
            }

            if (notifyDelayMilliseconds == 0)
            {
                Notify(target);
                _targetsByEntityUuid[target.EntityUuid] = (target, now, true);
            }
            else
            {
                _targetsByEntityUuid[target.EntityUuid] = (target, now, false);
            }
        }

        foreach (var entityUuid in _targetsByEntityUuid.Keys.Where(uuid => !entityUuids.Contains(uuid)).ToArray())
        {
            _targetsByEntityUuid.Remove(entityUuid);
        }

        UpdateCheckTimer();
    }

    /// <summary>狙われてから通知条件の秒数たった状態に出す(最後に届いた一覧で、まだ同じ人を狙っている)。</summary>
    private void CheckTimer_Tick(object? sender, EventArgs e)
    {
        var now = Environment.TickCount64;
        var notifyDelayMilliseconds = GetNotifyDelayMilliseconds();
        foreach (var (entityUuid, state) in _targetsByEntityUuid.ToArray())
        {
            if (state.IsNotified || now - state.Since < notifyDelayMilliseconds)
            {
                continue;
            }

            _targetsByEntityUuid[entityUuid] = (state.Target, state.Since, true);
            Notify(state.Target);
        }

        UpdateCheckTimer();
    }

    /// <summary>まだ出していない状態がある間だけ時計を動かす。</summary>
    private void UpdateCheckTimer()
    {
        if (!_targetsByEntityUuid.Values.Any(state => !state.IsNotified))
        {
            _checkTimer.Stop();
            return;
        }

        if (!_checkTimer.IsEnabled)
        {
            _checkTimer.Start();
        }
    }

    /// <summary>狙われ状態が続いたら出すまでの時間。通知タブの「通知条件」。0 は無条件。</summary>
    private static long GetNotifyDelayMilliseconds()
    {
        var index = ConfigManager.Instance.GetNotificationItem(NotificationKind.Targeted).TargetedConditionIndex;
        return AppConfigDefaults.TargetedDurationSeconds[index] * 1000L;
    }

    private static void Notify(TargetedState target)
    {
        NotificationService.Instance.Notify(
            NotificationKind.Targeted,
            NotificationTextFormatter.FormatTargeted(
                NotificationTextFormatter.ResolveFormat(NotificationKind.Targeted),
                target.PlayerName,
                target.EntityName));
    }
}
