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
    public static long ObservedTarget(Entity entity) => ReadTargetValue(entity.GetAttrKV("ClientSelectedTargetUuid") ?? entity.GetAttrKV("AttrTargetId") ?? entity.GetAttrKV("AttrTargetUuid"));
    private static long ReadTargetValue(object? value) => value switch
    { long number=>number, int number=>number, uint number=>number, ulong number when number<=long.MaxValue=>(long)number, _=>0 };

    private static readonly Dictionary<string,long> AttributeCounts=[];
    private static readonly Dictionary<string,HashSet<long>> AttributeActors=[];
    private static long _lastDiagnosticTicks;
    private static readonly Dictionary<string,long> ClientCalls=[];
    private static readonly Dictionary<string,long> ServerNotifications=[];
    private static long _clientTargetChanges,_clientClears,_skillTargetChanges,_changesBeforeSelf;
    private static long _selfDeltaFallbacks,_selfTargetFallbacks;
    public static void ObserveSelfDeltaFallback(bool containsTarget)
    {
        lock(Gate) { _selfDeltaFallbacks++; if(containsTarget)_selfTargetFallbacks++; }
    }
    public static void ObserveClientCall(Protocols.ObservedMessage message)
    {
        if(message.Kind==Protocols.ObservedMessageKind.Notify)
        {
            var name=$"{message.ServiceId}:{message.MethodId:X}";
            lock(Gate) if(ServerNotifications.Count<256 || ServerNotifications.ContainsKey(name))ServerNotifications[name]=ServerNotifications.GetValueOrDefault(name)+1;
        }
        if(message.Kind!=Protocols.ObservedMessageKind.Call)return;
        var key=$"{message.ServiceId}:{message.MethodId:X}";
        lock(Gate) if(ClientCalls.Count<128 || ClientCalls.ContainsKey(key))ClientCalls[key]=ClientCalls.GetValueOrDefault(key)+1;
    }
    public static void ApplyClientTargetChange(long selfUuid,long targetUuid,int skillId)
    {
        lock(Gate)
        {
            _clientTargetChanges++;
            if(targetUuid==0)_clientClears++;
            if(skillId>0)_skillTargetChanges++;
            if(selfUuid==0)_changesBeforeSelf++;
        }
        if(selfUuid==0 || Utils.UuidToEntityType(selfUuid)!=(long)EEntityType.EntChar)return;
        EncounterManager.Current.GetOrCreateEntity(selfUuid).SetAttrKV("ClientSelectedTargetUuid",targetUuid);
        Update(selfUuid,targetUuid);
    }
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
            ServerNotificationCounts=new Dictionary<string,long>(ServerNotifications),
            ClientCallCounts=new Dictionary<string,long>(ClientCalls), ClientTargetChanges=_clientTargetChanges,
            ClientTargetClears=_clientClears, SkillContextTargetChanges=_skillTargetChanges, ClientChangesBeforeSelfKnown=_changesBeforeSelf,
            SelfDeltaUuidFallbacks=_selfDeltaFallbacks, SelfTargetDeltaUuidFallbacks=_selfTargetFallbacks,
            CurrentRelations=Targets.Count,
            RelationsMatchingKnownEnemies=Targets.Count(x=>encounter.Entities.TryGetValue(x.Value,out var target) && target.EntityType==EEntityType.EntMonster) };
        try
        {
            var path=Path.Combine(CombatRuntimePaths.DataDirectory,"TargetingDiagnostics.json");
            File.WriteAllText(path+".tmp",System.Text.Json.JsonSerializer.Serialize(data)); File.Move(path+".tmp",path,true);
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { Serilog.Log.Debug(ex,"Could not write target reception diagnostics"); }
    }
    public static void Clear() { lock(Gate) { Targets.Clear(); Players.Clear(); AttributeCounts.Clear(); AttributeActors.Clear(); ClientCalls.Clear(); ServerNotifications.Clear(); _clientTargetChanges=0; _clientClears=0; _skillTargetChanges=0; _changesBeforeSelf=0; _selfDeltaFallbacks=0; _selfTargetFallbacks=0; _lastDiagnosticTicks=0; } }
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
