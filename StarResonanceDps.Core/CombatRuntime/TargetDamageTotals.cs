using ProtoBuf;
using System.Runtime.CompilerServices;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using Zproto;
namespace StarResonanceDps.Core.CombatRuntime;
[ProtoContract]
public sealed class TargetDamageTotal
{
    [ProtoMember(1)] public long EnemyUuid {get;set;}
    [ProtoMember(2)] public long PlayerUuid {get;set;}
    [ProtoMember(3)] public ulong Damage {get;set;}
}
public sealed record TargetDamageRank(long CharacterId,ulong Damage);
public static class TargetDamageTotals
{
    private static readonly ConditionalWeakTable<Encounter,Dictionary<(long,long),TargetDamageTotal>> Indexes=new();
    public static void Record(Encounter encounter,long enemy,long player,long damage)
    {
        if(damage<=0 || Utils.UuidToEntityType(player)!=(long)EEntityType.EntChar
            || (Utils.UuidToEntityType(enemy)!=(long)EEntityType.EntMonster && Utils.UuidToEntityType(enemy)!=(long)EEntityType.EntDummy)
            || (encounter.Entities.TryGetValue(player,out var actor) && actor.IsNpc))return;
        lock(encounter.ExData.TargetDamages)
        {
            var index=Indexes.GetValue(encounter,e=>e.ExData.TargetDamages.ToDictionary(x=>(x.EnemyUuid,x.PlayerUuid)));
            if(!index.TryGetValue((enemy,player),out var total))
            {total=new(){EnemyUuid=enemy,PlayerUuid=player};index[(enemy,player)]=total;encounter.ExData.TargetDamages.Add(total);}
            total.Damage=ulong.MaxValue-total.Damage<(ulong)damage?ulong.MaxValue:total.Damage+(ulong)damage;
        }
        NearbyEntityStore.Instance.TryRefresh(enemy,row=>row with{DamageRanking=Read(encounter,enemy)});
        if(encounter.ExData.DefeatedEnemies.Any(x=>x.Uuid==enemy))EntityHistoryProjection.Publish();
    }
    public static IReadOnlyList<TargetDamageRank> Read(Encounter encounter,long enemy)
    {
        lock(encounter.ExData.TargetDamages)return encounter.ExData.TargetDamages.Where(x=>x.EnemyUuid==enemy && x.Damage>0)
            .OrderByDescending(x=>x.Damage).ThenBy(x=>x.PlayerUuid).Take(5)
            .Select(x=>new TargetDamageRank(Utils.UuidToEntityId(x.PlayerUuid),x.Damage)).ToArray();
    }
}
