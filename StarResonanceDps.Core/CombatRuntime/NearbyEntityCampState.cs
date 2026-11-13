// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.Models;

namespace StarResonanceDps.Core.CombatRuntime;

internal static class NearbyEntityCampState
{
    private static readonly object Sync = new();
    private static readonly Dictionary<long, int> Camps = [];
    private static long _selfEntityUuid;

    public static void BeginMap()
    {
        lock (Sync)
        {
            var hasSelfCamp = Camps.TryGetValue(_selfEntityUuid, out var selfCamp);
            Camps.Clear();

            // 自分の陣営は EnterScene でしか届かないので、EnterScene を伴わない切替で消すと判定できなくなる。
            // 自分のUUIDと陣営だけ残す。変わるときは AttrCamp が上書きする。
            if (hasSelfCamp)
            {
                Camps[_selfEntityUuid] = selfCamp;
            }
        }
    }

    /// <summary>
    /// 起動時の値に戻す。<see cref="BeginMap"/> と違い、自分の実体と陣営も消す(ログアウトでキャラが替わりうるため)。
    /// </summary>
    public static void ResetToStartup()
    {
        lock (Sync)
        {
            Camps.Clear();
            _selfEntityUuid = 0;
        }
    }

    public static bool SetSelfEntity(long entityUuid)
    {
        if (entityUuid == 0)
        {
            return false;
        }

        lock (Sync)
        {
            if (_selfEntityUuid == entityUuid)
            {
                return false;
            }

            _selfEntityUuid = entityUuid;
            return true;
        }
    }

    public static bool SetCamp(long entityUuid, bool isPresent, int camp)
    {
        if (entityUuid == 0)
        {
            return false;
        }

        lock (Sync)
        {
            var changed = isPresent
                ? !Camps.TryGetValue(entityUuid, out var existingCamp) || existingCamp != camp
                : Camps.Remove(entityUuid);

            if (isPresent)
            {
                Camps[entityUuid] = camp;
            }

            return changed && entityUuid == _selfEntityUuid;
        }
    }

    public static void Remove(long entityUuid)
    {
        if (entityUuid == 0)
        {
            return;
        }

        lock (Sync)
        {
            Camps.Remove(entityUuid);
        }
    }

    public static EntityCampRelation GetRelation(long entityUuid)
    {
        lock (Sync)
        {
            return GetRelationNoLock(entityUuid);
        }
    }

    public static IReadOnlyDictionary<long, EntityCampRelation> GetRelations(IEnumerable<long> entityUuids)
    {
        ArgumentNullException.ThrowIfNull(entityUuids);

        lock (Sync)
        {
            return entityUuids
                .Distinct()
                .ToDictionary(entityUuid => entityUuid, GetRelationNoLock);
        }
    }

    private static EntityCampRelation GetRelationNoLock(long entityUuid)
    {
        // 3つ揃わないと判定できない。自分の陣営は EnterScene でしか届かないため、
        // ロード済みマップで起動するとマップ移動まで揃わない。
        if (_selfEntityUuid == 0
            || !Camps.TryGetValue(_selfEntityUuid, out var selfCamp)
            || !Camps.TryGetValue(entityUuid, out var entityCamp))
        {
            return EntityCampRelation.NonHostile;
        }

        return entityCamp == selfCamp
            ? EntityCampRelation.Friendly
            : EntityCampRelation.Hostile;
    }
}
