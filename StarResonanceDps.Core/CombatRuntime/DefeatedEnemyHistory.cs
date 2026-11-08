using ProtoBuf;
using StarResonanceDps.Core.Models;
using Zproto;
namespace StarResonanceDps.Core.CombatRuntime;

[ProtoContract]
public sealed class DefeatedEnemyRecord
{
    [ProtoMember(1)] public long Uuid {get;set;}
    [ProtoMember(2)] public long TypeId {get;set;}
    [ProtoMember(3)] public string Name {get;set;} = "";
    [ProtoMember(4)] public int Level {get;set;}
    [ProtoMember(5)] public long MaxHp {get;set;}
    [ProtoMember(6)] public EMonsterType MonsterType {get;set;}
    [ProtoMember(7)] public DateTime Timestamp {get;set;}
    public NearbyEntityEntry ToEntry() => new(Uuid,TypeId,Name,EEntityType.EntMonster,MonsterType,Level,0,MaxHp,0,0,false,false,EntityCampRelation.Hostile,0,true,0,false,0,NearbyEntityHateRanking.Empty,null);
}
public sealed record EntityHistorySnapshot(ulong EncounterId,bool IsHistorical,string SceneName,IReadOnlyList<NearbyEntityEntry> Alive,IReadOnlyList<NearbyEntityEntry> Defeated)
{
    public static EntityHistorySnapshot Empty {get;} = new(0,false,"",[],[]);
}
public static class DefeatedEnemyRecorder
{
    public static void Observe(Encounter encounter,Entity entity,DateTime utc)
    {
        if(entity.EntityType!=EEntityType.EntMonster || entity.MaxHp<=0) return;
        var changed=false;
        lock(encounter.ExData.DefeatedEnemies)
        {
            var existing=encounter.ExData.DefeatedEnemies.FirstOrDefault(x=>x.Uuid==entity.UUID);
            if(entity.Hp>0 && existing is not null){encounter.ExData.DefeatedEnemies.Remove(existing);changed=true;}
            else if(entity.Hp==0 && existing is not null && existing.TypeId==0 && EnemyCatalogStore.TypeFor(entity)>0)
            { existing.TypeId=EnemyCatalogStore.TypeFor(entity);existing.Name=entity.Name??"";existing.MaxHp=entity.MaxHp;existing.MonsterType=entity.MonsterType;changed=true; }
            else if(entity.Hp==0 && existing is null && encounter.IsRecorded(utc,null))
            {
                encounter.ExData.DefeatedEnemies.Add(new(){Uuid=entity.UUID,TypeId=EnemyCatalogStore.TypeFor(entity),Name=entity.Name??"",Level=entity.GetAttrKV("AttrLevel") is int level ? level : 0,MaxHp=entity.MaxHp,MonsterType=entity.MonsterType,Timestamp=utc});changed=true;
            }
        }
        if(changed) EntityHistoryProjection.Publish();
    }
}
public static class EntityHistoryProjection
{
    private static bool _initialized;
    private static EntityHistorySnapshot _current=EntityHistorySnapshot.Empty;
    public static EntityHistorySnapshot Current => Volatile.Read(ref _current);
    public static event Action<EntityHistorySnapshot>? Changed;
    public static void Initialize()
    {
        if(_initialized)return;_initialized=true;
        EncounterHistoryProvider.SelectionChanged+=Publish;
        EncounterManager.EncounterStart+=_=>Publish();
        Publish();
    }
    public static EntityHistorySnapshot Build(Encounter encounter,bool historical)
    {
        DefeatedEnemyRecord[] saved; lock(encounter.ExData.DefeatedEnemies)saved=encounter.ExData.DefeatedEnemies.ToArray();
        var dead=saved.Select(x=>x.ToEntry()).ToList();
        // Older histories can only expose corpses actually retained in their entity blob.
        foreach(var entity in encounter.Entities.Values.Where(x=>x.EntityType==EEntityType.EntMonster && x.Hp==0 && x.MaxHp>0))
            if(!dead.Any(x=>x.EntityUuid==entity.UUID)) dead.Add(NearbyEntityProjection.CreateEntry(entity.UUID,entity,null) with {CurrentHp=0,IsInCombat=false,Cast=null,CampRelation=EntityCampRelation.Hostile});
        var alive=historical ? encounter.Entities.Values.Where(x=>x.EntityType==EEntityType.EntMonster && x.Hp!=0 && !dead.Any(d=>d.EntityUuid==x.UUID))
            .Select(x=>NearbyEntityProjection.CreateEntry(x.UUID,x,null) with {IsInCombat=false,Cast=null,CampRelation=EntityCampRelation.Hostile}).ToArray() : [];
        return new(encounter.EncounterId,historical,encounter.SceneName??"",alive,dead);
    }
    public static void Publish()
    {
        if(!_initialized)return;
        var historical=AppState.OpenedHistoricalEncounter;
        var snapshot=Build(historical??EncounterManager.Current,historical is not null);
        Volatile.Write(ref _current,snapshot);Changed?.Invoke(snapshot);
    }
}
