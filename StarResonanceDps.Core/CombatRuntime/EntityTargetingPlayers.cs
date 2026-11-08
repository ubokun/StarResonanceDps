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
    public static long ObservedTarget(Entity entity) => ReadTargetValue(entity.GetAttrKV("AttrTargetId") ?? entity.GetAttrKV("AttrTargetUuid"));
    private static long ReadTargetValue(object? value) => value switch
    { long number=>number, int number=>number, uint number=>number, ulong number when number<=long.MaxValue=>(long)number, _=>0 };

    private static readonly Dictionary<string,long> AttributeCounts=[];
    private static readonly Dictionary<string,HashSet<long>> AttributeActors=[];
    private static long _lastDiagnosticTicks;
    public static void ObserveAttribute(long actorUuid,string key,object value)
    {
        var kind=Utils.UuidToEntityType(actorUuid)==(long)EEntityType.EntChar ? "Player" : "Other";
        var name=kind+key;
        lock(Gate)
        {
            AttributeCounts[name]=AttributeCounts.GetValueOrDefault(name)+1;
            if(!AttributeActors.TryGetValue(name,out var actors))AttributeActors[name]=actors=[];
            actors.Add(actorUuid);
            if(ReadTargetValue(value)!=0)AttributeCounts[name+"Positive"]=AttributeCounts.GetValueOrDefault(name+"Positive")+1;
        }
    }
    public static void PublishDiagnostics(Encounter encounter)
    {
        var now=DateTime.UtcNow;
        if(now.Ticks-_lastDiagnosticTicks<TimeSpan.TicksPerSecond*2)return;
        _lastDiagnosticTicks=now.Ticks;
        object data;
        lock(Gate) data=new { UpdatedUtc=now, EncounterId=encounter.EncounterId,
            AttributeUpdates=new Dictionary<string,long>(AttributeCounts),
            AttributeActorCounts=AttributeActors.ToDictionary(x=>x.Key,x=>x.Value.Count),
            CurrentRelations=Targets.Count,
            RelationsMatchingKnownEnemies=Targets.Count(x=>encounter.Entities.TryGetValue(x.Value,out var target) && target.EntityType==EEntityType.EntMonster) };
        try
        {
            var path=Path.Combine(CombatRuntimePaths.DataDirectory,"TargetingDiagnostics.json");
            File.WriteAllText(path+".tmp",System.Text.Json.JsonSerializer.Serialize(data)); File.Move(path+".tmp",path,true);
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { Serilog.Log.Debug(ex,"Could not write target reception diagnostics"); }
    }
    public static void Clear() { lock(Gate) { Targets.Clear(); Players.Clear(); AttributeCounts.Clear(); AttributeActors.Clear(); _lastDiagnosticTicks=0; } }
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
