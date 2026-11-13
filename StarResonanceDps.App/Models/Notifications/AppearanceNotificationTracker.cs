// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Config;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>
/// 通知「プレイヤー出現」「エンティティ出現」の判定。見張り(<see cref="NotificationMonitor"/>)が、周りに見えている相手の一覧を届くたびに渡す。
/// ウィジェットを開いていなくても動く。
///
/// <para>
/// 前の一覧に無かった相手のうち、通知タブの対象(<see cref="NotificationWatchTarget"/>)に当たるものが現れたら出す。視界を出入りすると、入るたびに出す。
/// 同じ一覧の中で同じ名前の相手がいくつ現れても1回(同じ文が並ぶだけになるため)。
/// オンにした・対象を変えたときは <see cref="Rebaseline"/> で、その時点で既にいる相手には出さない。
/// </para>
/// </summary>
public sealed class AppearanceNotificationTracker(NotificationKind kind)
{
    private HashSet<long> _presentKeys = [];
    private HashSet<long> _nextPresentKeys = [];
    private readonly HashSet<string> _notifiedNames = new(StringComparer.Ordinal);
    private bool _isBaselinePending;

    /// <summary>次の一覧を、出さずに「既にいる相手」として控える。</summary>
    public void Rebaseline()
    {
        _isBaselinePending = true;
    }

    /// <param name="items">いま周りに見えている相手。</param>
    /// <param name="target">見張りが持っている、この項目の対象。</param>
    /// <param name="getKey">相手を見分ける番号(プレイヤーはキャラクターID、エンティティは実体の UUID)。</param>
    /// <param name="getMatchId">対象のIDと比べる番号(プレイヤーはキャラクターID、エンティティは種別ID)。</param>
    /// <param name="getMatchName">対象の名前と比べる名前。名前が無ければ空。</param>
    /// <param name="getDisplayName">通知の文に入れる名前。当たったときだけ引く。</param>
    public void Observe<T>(
        IReadOnlyList<T> items,
        NotificationWatchTarget target,
        Func<T, long> getKey,
        Func<T, long> getMatchId,
        Func<T, string> getMatchName,
        Func<T, string> getDisplayName)
    {
        _nextPresentKeys.Clear();
        _notifiedNames.Clear();
        foreach (var item in items)
        {
            var key = getKey(item);
            _nextPresentKeys.Add(key);
            if (_isBaselinePending
                || _presentKeys.Contains(key)
                || !target.Matches(item, getMatchId, getMatchName))
            {
                continue;
            }

            var displayName = getDisplayName(item);
            if (_notifiedNames.Add(displayName))
            {
                NotificationService.Instance.Notify(
                    kind,
                    NotificationTextFormatter.FormatAppeared(kind, NotificationTextFormatter.ResolveFormat(kind), displayName));
            }
        }

        (_presentKeys, _nextPresentKeys) = (_nextPresentKeys, _presentKeys);
        _isBaselinePending = false;
    }
}
