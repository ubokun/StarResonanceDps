// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Config;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>詠唱1つ。周りの HP バーのある敵(エンティティリストのフィルター「オブジェクト以外」に載る実体)がしている詠唱。</summary>
/// <param name="EntityName">詠唱している敵の名前。</param>
public readonly record struct EntityCastObservation(long EntityUuid, NearbyEntityCast Cast, string EntityName);

/// <summary>
/// 通知「エンティティ詠唱」の判定。周りの敵の一覧が届くたびに、詠唱している敵(<see cref="NearbyEntityEntry.Cast"/> のあるもの)を渡す。
/// ウィジェットを開いていなくても動く(<see cref="NotificationMonitor"/>)。
///
/// <para>
/// 敵ごとに前の一覧の詠唱と比べ、新しく始まった詠唱の技が通知タブの対象(<see cref="NotificationWatchTarget"/>)に当たれば、その場で1回出す。
/// 技の名前はタイムラインの詠唱の行と同じ名前(<see cref="MeterSnapshotProvider.GetCastSkillName"/>)で比べる。
/// 詠唱はエンティティリストの詠唱ゲージと同じ値で、周りに現れたときにすでに詠唱していた敵は値を持たないので出さない。
/// 同じ一覧の中で同じ名前の敵がいくつ詠唱を始めても1回(同じ文が並ぶだけになるため。エンティティ出現と同じ)。
/// オンにした・対象を変えたときは <see cref="Rebaseline"/> で、その時点で詠唱中の敵には出さない。
/// </para>
/// </summary>
public sealed class EntityCastNotificationTracker
{
    /// <summary>敵ごとに最後に見た詠唱。鍵は敵の UUID。最後の一覧で詠唱していなかった敵は持たない。</summary>
    private Dictionary<long, NearbyEntityCast> _castsByEntityUuid = [];
    private Dictionary<long, NearbyEntityCast> _nextCastsByEntityUuid = [];
    private readonly HashSet<string> _notifiedNames = new(StringComparer.Ordinal);

    /// <summary>今の詠唱を、出さずに「既に見た詠唱」として控え直す。オフのときは空で呼ぶ。</summary>
    public void Rebaseline(IReadOnlyList<EntityCastObservation> casts)
    {
        _castsByEntityUuid.Clear();
        foreach (var cast in casts)
        {
            _castsByEntityUuid[cast.EntityUuid] = cast.Cast;
        }
    }

    /// <param name="target">見張りが持っている、この項目の対象。</param>
    public void Observe(IReadOnlyList<EntityCastObservation> casts, NotificationWatchTarget target)
    {
        _nextCastsByEntityUuid.Clear();
        _notifiedNames.Clear();
        foreach (var cast in casts)
        {
            _nextCastsByEntityUuid[cast.EntityUuid] = cast.Cast;
            if ((_castsByEntityUuid.TryGetValue(cast.EntityUuid, out var previous) && previous == cast.Cast)
                || !target.Matches(
                    cast,
                    static observation => observation.Cast.SkillId,
                    static observation => MeterSnapshotProvider.GetCastSkillName(observation.Cast.SkillId))
                || !_notifiedNames.Add(cast.EntityName))
            {
                continue;
            }

            NotificationService.Instance.Notify(
                NotificationKind.EntityCast,
                NotificationTextFormatter.FormatEntityCast(
                    NotificationTextFormatter.ResolveFormat(NotificationKind.EntityCast),
                    cast.EntityName));
        }

        (_castsByEntityUuid, _nextCastsByEntityUuid) = (_nextCastsByEntityUuid, _castsByEntityUuid);
    }
}
