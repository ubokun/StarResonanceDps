using StarResonanceDps.Core.Services;
using Zproto;
namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>Only observed player target attributes; never inferred from damage or hate.</summary>
public static class EntityTargetingPlayers
{
    private static readonly object Gate = new();
    private static readonly Dictionary<long,long> Targets = [];
    private static readonly Dictionary<long,long[]> Players = [];
    public static IReadOnlyList<long> Get(long targetUuid) { lock(Gate) return Players.GetValueOrDefault(targetUuid) ?? []; }
    public static long ObservedTarget(Entity entity) => entity.GetAttrKV("AttrTargetUuid") switch
    { long value=>value, int value=>value, uint value=>value, ulong value when value<=long.MaxValue=>(long)value, _=>0 };
    public static void Clear() { lock(Gate) { Targets.Clear(); Players.Clear(); } }
    public static void RemoveEntity(long uuid)
    {
        if (Utils.UuidToEntityType(uuid) == (long)EEntityType.EntChar) { Update(uuid,0); return; }
        long[] actors; lock(Gate) actors=Targets.Where(x=>x.Value==uuid).Select(x=>x.Key).ToArray();
        foreach(var actor in actors) Update(actor,0);
    }
    public static void Update(long playerUuid, long targetUuid)
    {
        if (Utils.UuidToEntityType(playerUuid) != (long)EEntityType.EntChar) return;
        long previous;
        lock(Gate)
        {
            previous=Targets.GetValueOrDefault(playerUuid);
            if(previous==targetUuid)return;
            if(targetUuid==0) Targets.Remove(playerUuid); else Targets[playerUuid]=targetUuid;
            foreach(var target in new[]{previous,targetUuid}.Where(x=>x!=0).Distinct())
            {
                var ids=Targets.Where(x=>x.Value==target).Select(x=>Utils.UuidToEntityId(x.Key)).Distinct().Order().ToArray();
                if(ids.Length==0)Players.Remove(target);else Players[target]=ids;
            }
        }
        foreach(var target in new[]{previous,targetUuid}.Where(x=>x!=0).Distinct())
            NearbyEntityStore.Instance.TryRefresh(target,row=>row with {TargetingCharacterIds=Get(target)});
    }
}
