using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Config;
using System.IO;
using Zproto;
using Entity=StarResonanceDps.Core.CombatRuntime.Entity;
internal static class TargetDamageChecks
{
    public static void Run(Action<bool,string> check)
    {
        long U(long id,EEntityType kind)=>Utils.EntityIdToUuid(id,(long)kind,false,false);
        var enemy=U(81001,EEntityType.EntMonster);var second=U(81002,EEntityType.EntMonster);
        var old=EncounterManager.Current;var encounter=new Encounter();var start=DateTime.UtcNow.AddSeconds(-10);
        encounter.ExData.FirstDamageTimeStamp=start;encounter.SetEndTime(start.AddSeconds(10).ToLocalTime());
        try
        {
            EncounterManager.Current=encounter;
            for(var i=1;i<=7;i++)TargetDamageTotals.Record(encounter,enemy,U(82000+i,EEntityType.EntChar),i*100);
            TargetDamageTotals.Record(encounter,second,U(82001,EEntityType.EntChar),5000);
            TargetDamageTotals.Record(encounter,enemy,second,99999);TargetDamageTotals.Record(encounter,enemy,U(82001,EEntityType.EntChar),-1);
            var ranks=TargetDamageTotals.Read(encounter,enemy);
            check(ranks.Count==5 && ranks.Select(x=>x.Damage).SequenceEqual(new ulong[]{700,600,500,400,300}),"per-enemy damage ranks show only the highest five players");
            check(TargetDamageTotals.Read(encounter,second).Single().Damage==5000 && TargetDamageTotals.Read(encounter,999).Count==0,"enemy damage ledgers do not mix enemies or substitute a global total");
            using var bytes=new MemoryStream();ProtoBuf.Serializer.Serialize(bytes,encounter.ExData);bytes.Position=0;
            var restored=new Encounter{ExData=ProtoBuf.Serializer.Deserialize<EncounterExData>(bytes)};
            check(TargetDamageTotals.Read(restored,enemy).SequenceEqual(ranks),"per-enemy damage totals survive historical metadata serialization");
            var entry=new NearbyEntityEntry(enemy,81001,"検証敵",EEntityType.EntMonster,default,1,50,100,0,0,false,false,EntityCampRelation.Hostile,0,true,0,false,0,NearbyEntityHateRanking.Empty,null,DamageRanking:ranks);
            var names=new EntityHatePlayerResolver();names.SetRoster(Enumerable.Range(1,7).Select(i=>new PlayerRosterEntry(82000+i,"検証"+i,1)).ToArray());var settings=new MeterWidgetSettingsConfig();names.Prepare(EntityRowSettings.FromEntityList(settings),PlayerNameDisplayMode.Show);
            var row=EntityListEntry.Create(entry,EntityRowSettings.FromEntityList(settings),names,false,6);
            check(row.DamageRows.Count==5 && row.DamageRows[0].Dps=="70 DPS" && row.DamageRows[0].Ratio==1,"inline target DPS uses the encounter clock and the leading player as bar reference");
            row.Update(entry with{EntityUuid=second,DamageRanking=TargetDamageTotals.Read(encounter,second)},EntityRowSettings.FromEntityList(settings),names,false);
            check(row.DamageRows.Count==1 && row.DamageRows[0].Dps=="500 DPS","recycled entity rows replace the previous target damage ranks");
            row.Update(entry with{DamageRanking=null},EntityRowSettings.FromEntityList(settings),names,false);
            check(row.DamageRows.Count==0,"older history without target records remains unknown rather than showing global DPS");
            var enemyEntity=new Entity(enemy,"検証敵");enemyEntity.SetEntityType(EEntityType.EntMonster);enemyEntity.SetHpValuesNoUpdate(0,100);encounter.Entities[enemy]=enemyEntity;
            DefeatedEnemyRecorder.Observe(encounter,enemyEntity,start.AddSeconds(9));
            var historical=EntityHistoryProjection.Build(encounter,true);
            check(historical.Defeated.Single(x=>x.EntityUuid==enemy).DamageRanking!.SequenceEqual(ranks),"defeated entity history retains its target-specific top five damage totals");
            var config=new WidgetConfig{Window=new(){Width=457}};WidgetConfigDefaults.Normalize(WidgetKind.EntityList,config);var cloned=config.Clone();WidgetConfigDefaults.Normalize(WidgetKind.EntityList,cloned);
            check(config.Window.Width==637 && cloned.Window.Width==637,"inline target DPS width migration runs once and survives config cloning");
        }
        finally {EncounterManager.Current=old;}
    }
}
