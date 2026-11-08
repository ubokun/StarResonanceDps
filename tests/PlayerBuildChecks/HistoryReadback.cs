using System.Reflection;
using Dapper;
using Microsoft.Data.Sqlite;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using Zproto;

internal static class HistoryReadback
{
    public static void Targets(string path)
    {
        SQLitePCL.Batteries_V2.Init(); AppState.LoadDataTables();
        using var connection=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=System.IO.Path.GetFullPath(path), Mode=SqliteOpenMode.ReadOnly, Pooling=false }.ToString());
        connection.Open();
        typeof(DB).GetField("DbConn",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,connection);
        typeof(DB).GetField("Log",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,Serilog.Log.Logger);
        foreach(var id in connection.Query<long>("SELECT EncounterId FROM Encounters ORDER BY EncounterId DESC LIMIT 6").ToArray())
        {
            var encounter=DB.LoadEncounter((ulong)id)!;
            var players=encounter.Entities.Values.Where(x=>x.EntityType==EEntityType.EntChar).ToArray();
            var enemies=encounter.Entities.Values.Where(x=>x.EntityType==EEntityType.EntMonster).ToArray();
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Id=id, Players=players.Length,
                PlayerTargetFields=players.Count(x=>x.Attributes.ContainsKey("AttrTargetUuid")),
                PositivePlayerTargets=players.Count(x=>EntityTargetingPlayers.ObservedTarget(x)!=0),
                MonsterTargets=players.Count(x=>Utils.UuidToEntityType(EntityTargetingPlayers.ObservedTarget(x))==(long)EEntityType.EntMonster),
                Enemies=enemies.Length,EnemyTargetFields=enemies.Count(x=>x.Attributes.ContainsKey("AttrTargetUuid")),
                PositiveEnemyTargets=enemies.Count(x=>EntityTargetingPlayers.ObservedTarget(x)!=0) }));
        }
    }

    public static void Run(string path, bool analyzeAttribution = false)
    {
        SQLitePCL.Batteries_V2.Init();
        AppState.LoadDataTables();
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = System.IO.Path.GetFullPath(path), Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        typeof(DB).GetField("DbConn",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,connection);
        typeof(DB).GetField("Log",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,Serilog.Log.Logger);
        var ids=connection.Query<long>("SELECT EncounterId FROM Encounters ORDER BY EncounterId DESC LIMIT 6").ToArray();
        foreach(var id in ids)
        {
            var encounter=DB.LoadEncounter((ulong)id)!;
            var hate=HateMeterReader.Build(encounter);
            var players=encounter.Entities.Values.Where(x=>x.EntityType==EEntityType.EntChar).ToArray();
            var positive=encounter.ExData.HateObservations.Where(x=>!x.IsBaseline&&x.Change>0).ToArray();
            var clock=encounter.ReadCombatClock();
            var compared=positive.Where(x=>clock.StartUtc is null||x.Timestamp>=clock.StartUtc).ToArray();
            double legacyUnknown=0;
            foreach(var group in compared.GroupBy(x=>x.PlayerUuid))
            {
                var starts=encounter.GetSkillActivationsFrom(group.Key,0,out _).Select(x=>(x.Timestamp,x.SkillId)).ToArray();
                legacyUnknown+=group.Where(x=>HateMeterReader.EstimateSkill(x,starts)==0).Sum(x=>(double)x.Change);
            }
            var comparedAmount=compared.Sum(x=>(double)x.Change);
            var legacyShare=comparedAmount>0 ? legacyUnknown/comparedAmount*100 : 0;
            if(analyzeAttribution)
            {
                if(positive.Length==0)continue;
                var reasons=new Dictionary<string,(long Count,double Amount)>();
                var effectReasons=new Dictionary<string,(long Count,double Amount)>();
                var playersAnalysis=new List<object>();
                foreach(var group in positive.GroupBy(x=>x.PlayerUuid))
                {
                    var activations=encounter.GetSkillActivationsFrom(group.Key,0,out _).OrderBy(x=>x.Timestamp).ToArray();
                    var effectIndex=new HateAttributionIndex(encounter.ExData.HateCauses.Where(x=>x.PlayerUuid==group.Key),activations.Select(x=>(x.Timestamp,x.SkillId)));
                    double unknown=0, total=0; var missingRecent=new Dictionary<string,double>();
                    foreach(var gain in group)
                    {
                        total+=gain.Change;
                        var effectMatch=effectIndex.Match(gain);
                        var effectReason=effectMatch.SourceKey>0 ? effectMatch.Evidence.ToString() : effectMatch.Reason.ToString();
                        if(effectMatch.Reason==HateUnassignedReason.MultipleSources)
                        {
                            var relevant=encounter.ExData.HateCauses.Where(x=>x.PlayerUuid==gain.PlayerUuid && x.Timestamp>gain.FromUtc && x.Timestamp<=gain.Timestamp
                                && (x.Kind==HateCauseKind.Healing || x.TargetUuid==gain.EnemyUuid)).ToArray();
                            effectReason=relevant.Select(x=>(x.SourceKey,Buff:x.IsBuffSource && !(CombatDataCatalog.TryResolveRecountRow(x.SourceKey,out var row)&&row==x.SourceKey))).Distinct().Take(2).Count()>1
                                ? "MultipleEffectSources" : "EffectAndOtherSkillStart";
                        }
                        var oldEffect=effectReasons.GetValueOrDefault(effectReason);effectReasons[effectReason]=(oldEffect.Count+1,oldEffect.Amount+gain.Change);
                        var interval=(gain.Timestamp-gain.FromUtc).TotalSeconds;
                        var candidates=activations.Where(x=>x.Timestamp>gain.FromUtc&&x.Timestamp<=gain.Timestamp).Select(x=>x.SkillId).Distinct().Count();
                        var reason=interval<=0 ? "same_timestamp" : interval>2 ? "interval_over_2s" : candidates==0 ? "no_activation_in_interval" : candidates>1 ? "multiple_activations" : "assigned";
                        var old=reasons.GetValueOrDefault(reason);reasons[reason]=(old.Count+1,old.Amount+gain.Change);
                        if(reason!="assigned")unknown+=gain.Change;
                        if(reason=="no_activation_in_interval")
                        {
                            var prior=activations.LastOrDefault(x=>x.Timestamp<=gain.Timestamp);
                            var age=prior.SkillId==0 ? double.PositiveInfinity : (gain.Timestamp-prior.Timestamp).TotalSeconds;
                            var bucket=age<=0.1 ? "last_start_within_100ms" : age<=0.5 ? "last_start_within_500ms" : age<=2 ? "last_start_within_2s" : age<=5 ? "last_start_within_5s" : "last_start_older_or_missing";
                            missingRecent[bucket]=missingRecent.GetValueOrDefault(bucket)+gain.Change;
                        }
                    }
                    playersAnalysis.Add(new {PlayerId=Utils.UuidToEntityId(group.Key),Activations=activations.Length,UnknownShare=total>0?unknown/total*100:0,MissingActivationAmounts=missingRecent});
                }
                var amount=positive.Sum(x=>(double)x.Change);
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {encounter.EncounterId,PositiveHate=amount,
                    CauseRecords=encounter.ExData.HateCauses.Count,ComparisonPositiveHate=comparedAmount,LegacyUnknownShare=legacyShare,NewUnknownShare=hate.UnknownShare,
                    hate.EffectMatches,hate.StartMatches,
                    EffectReasons=effectReasons.Select(x=>new {Reason=x.Key,x.Value.Count,x.Value.Amount,Share=x.Value.Amount/amount*100}),
                    Reasons=reasons.Select(x=>new {Reason=x.Key,x.Value.Count,x.Value.Amount,Share=x.Value.Amount/amount*100}),Players=playersAnalysis}));
                continue;
            }
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {
                encounter.EncounterId,encounter.SceneName,encounter.StartTime,encounter.TotalDamage,
                Players=players.Length,
                PlayersWithSkillLevels=players.Count(x=>x.GetAttrKV("AttrSkillLevelIdList") is not null),
                HateRecords=encounter.ExData.HateObservations.Count,
                PositiveHateRecords=positive.Length,
                HateParticipants=encounter.ExData.HateObservations.Select(x=>x.PlayerUuid).Distinct().Count(),
                PositiveHate=positive.Sum(x=>(double)x.Change),
                MeterPlayers=hate.Players.Count,
                EstimatedSkillRows=hate.Players.Sum(x=>x.Skills.Count(s=>s.SkillId>0)),
                UnknownSkillRows=hate.Players.Sum(x=>x.Skills.Count(s=>s.SkillId==0)),
                CauseRecords=encounter.ExData.HateCauses.Count,HitRecords=encounter.ExData.HateCauses.Count(x=>x.Kind==HateCauseKind.Hit),
                EffectDamageRecords=encounter.ExData.HateCauses.Count(x=>x.Kind==HateCauseKind.EffectDamage),HealingRecords=encounter.ExData.HateCauses.Count(x=>x.Kind==HateCauseKind.Healing),
                LegacyUnknownShare=legacyShare,NewUnknownShare=hate.UnknownShare,hate.EffectMatches,hate.StartMatches,
                SharesConserved=hate.Players.All(x=>Math.Abs(x.Skills.Sum(s=>s.Amount)-x.Bar.Amount)<0.001),
                DefeatedEnemies=encounter.ExData.DefeatedEnemies.Count,
                EnemyControlEvents=encounter.ExData.EnemyControlEvents.Count,
                encounter.ExData.UseQuestClock,encounter.ExData.QuestStartUtc
            }));
        }
    }
}
