// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>
/// 通知「HP低下」の判定。名簿が届くたびに、通知タブの「対象プレイヤー」で絞った人(自分も含む)を渡す。
/// ウィジェットを開いていなくても動く(<see cref="NotificationMonitor"/>)。
/// 割合(HP低下判定)と続く秒数(通知条件)は通知タブで選ぶ。選び直したら <see cref="NotificationMonitor"/> が控えを捨てて見始め直す。
///
/// <para>
/// HP が最大HPの割合を超えていたか 0(倒れた)だった人が割合以下(0より大きい)になったら、その時刻を控え、割合以下が通知条件の秒数続いたら1回出す。
/// 通知条件が無条件(0秒)なら、割合以下になったその場で出す。
/// 続く間に割合を超えるか 0 になった(倒れた)ら出さない(すぐ回復した人と、通知しても間に合わない人)。割合を超えたら次も出す。
/// 割合を超えたところから 0 になった(即死)ときは出さない。0 から割合以下で復帰したとき(見え始めた時点で 0 だった人も)も、続いたら出す。
/// 見え始めた時点で既に割合以下(0より大きい)の人と、最大HP が分からない人は出さない(下がったところを見ていないため)。
/// バリアは数えない。対象から外れた人の状態は捨てる。
/// </para>
///
/// <para>
/// 続く秒数は時計で数える。HP が割合以下のまま変わらないと名簿が届かず、名簿を待つと判定の機会が来ないため。
/// 判定に使うのは最後に届いた名簿の HP。
/// </para>
/// </summary>
public sealed class HealthLowNotificationTracker
{
    /// <summary>控えた人の経過を確かめる間隔。プレイヤーリストの更新と同じ間隔。</summary>
    private const int CheckIntervalMilliseconds = 250;

    /// <summary>人ごとの「次に割合以下(0より大きい)になったら控えるか」(割合を超えていたか 0 だった)。鍵は CharacterId。</summary>
    private readonly Dictionary<long, bool> _isArmedByCharacterId = [];

    /// <summary>
    /// 割合以下になって、まだ出していない人。割合以下に入った時刻(<see cref="Environment.TickCount64"/>)と、出すときの名前
    /// (最後に届いた名簿の値)。鍵は CharacterId。
    /// </summary>
    private readonly Dictionary<long, (long Since, string Name)> _lowSinceByCharacterId = [];

    private readonly DispatcherTimer _checkTimer;

    public HealthLowNotificationTracker()
    {
        _checkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(CheckIntervalMilliseconds)
        };
        _checkTimer.Tick += CheckTimer_Tick;
    }

    /// <summary>通知をオフにした・マップが替わった・HP低下判定か通知条件を選び直した。今までの観測を捨てる(通知しない)。</summary>
    public void Reset()
    {
        _isArmedByCharacterId.Clear();
        _lowSinceByCharacterId.Clear();
        _checkTimer.Stop();
    }

    public void Observe(IReadOnlyList<PlayerRosterEntry> targets, PlayerNameDisplayMode nameDisplayMode)
    {
        var thresholdPercent = GetThresholdPercent();
        var notifyDelayMilliseconds = GetNotifyDelayMilliseconds();
        var targetCharacterIds = new HashSet<long>();
        var now = Environment.TickCount64;
        foreach (var player in targets)
        {
            targetCharacterIds.Add(player.CharacterId);
            if (player.MaxHp <= 0)
            {
                _isArmedByCharacterId.Remove(player.CharacterId);
                _lowSinceByCharacterId.Remove(player.CharacterId);
                continue;
            }

            var isDown = player.CurrentHp <= 0;
            var isAboveThreshold = player.CurrentHp * 100 > player.MaxHp * thresholdPercent;
            if (isAboveThreshold || isDown)
            {
                _lowSinceByCharacterId.Remove(player.CharacterId);
            }
            else if (_lowSinceByCharacterId.TryGetValue(player.CharacterId, out var pending))
            {
                _lowSinceByCharacterId[player.CharacterId] = (pending.Since, GetDisplayName(player, nameDisplayMode));
            }
            else if (_isArmedByCharacterId.TryGetValue(player.CharacterId, out var wasArmed) && wasArmed)
            {
                if (notifyDelayMilliseconds == 0)
                {
                    Notify(GetDisplayName(player, nameDisplayMode));
                }
                else
                {
                    _lowSinceByCharacterId[player.CharacterId] = (now, GetDisplayName(player, nameDisplayMode));
                }
            }

            _isArmedByCharacterId[player.CharacterId] = isAboveThreshold || isDown;
        }

        foreach (var characterId in _isArmedByCharacterId.Keys.Where(id => !targetCharacterIds.Contains(id)).ToArray())
        {
            _isArmedByCharacterId.Remove(characterId);
        }

        foreach (var characterId in _lowSinceByCharacterId.Keys.Where(id => !targetCharacterIds.Contains(id)).ToArray())
        {
            _lowSinceByCharacterId.Remove(characterId);
        }

        UpdateCheckTimer();
    }

    /// <summary>割合以下に入ってから通知条件の秒数たった人に出す(最後に届いた名簿で、まだ割合以下のまま)。</summary>
    private void CheckTimer_Tick(object? sender, EventArgs e)
    {
        var now = Environment.TickCount64;
        var notifyDelayMilliseconds = GetNotifyDelayMilliseconds();
        foreach (var (characterId, pending) in _lowSinceByCharacterId.ToArray())
        {
            if (now - pending.Since < notifyDelayMilliseconds)
            {
                continue;
            }

            _lowSinceByCharacterId.Remove(characterId);
            Notify(pending.Name);
        }

        UpdateCheckTimer();
    }

    /// <summary>控えた人が居る間だけ時計を動かす。</summary>
    private void UpdateCheckTimer()
    {
        if (_lowSinceByCharacterId.Count == 0)
        {
            _checkTimer.Stop();
            return;
        }

        if (!_checkTimer.IsEnabled)
        {
            _checkTimer.Start();
        }
    }

    /// <summary>HP低下とみなす最大HPに対する割合(%)。通知タブの「HP低下判定」。</summary>
    private static long GetThresholdPercent()
    {
        var index = ConfigManager.Instance.GetNotificationItem(NotificationKind.HealthLow).HealthLowThresholdIndex;
        return AppConfigDefaults.HealthLowThresholdPercents[index];
    }

    /// <summary>割合以下が続いたら出すまでの時間。通知タブの「通知条件」。0 は無条件。</summary>
    private static long GetNotifyDelayMilliseconds()
    {
        var index = ConfigManager.Instance.GetNotificationItem(NotificationKind.HealthLow).HealthLowConditionIndex;
        return AppConfigDefaults.HealthLowDurationSeconds[index] * 1000L;
    }

    private static string GetDisplayName(PlayerRosterEntry player, PlayerNameDisplayMode nameDisplayMode)
    {
        return PlayerInfoFormatFormatter.GetDisplayName(
            player.Name,
            player.CharacterId,
            player.IsSelf,
            player.IsNpc,
            player.ProfessionId,
            nameDisplayMode);
    }

    private static void Notify(string name)
    {
        NotificationService.Instance.Notify(
            NotificationKind.HealthLow,
            NotificationTextFormatter.FormatHealthLow(NotificationTextFormatter.ResolveFormat(NotificationKind.HealthLow), name));
    }
}
