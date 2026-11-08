using System.Reflection;
using Dapper;
using Microsoft.Data.Sqlite;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using Zproto;

internal static class HistoryReadback
{
    public static void Run(string path, bool analyzeAttribution = false)
    {
        SQLitePCL.Batteries_V2.Init();
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
            if(analyzeAttribution)
            {
                if(positive.Length==0)continue;
                var reasons=new Dictionary<string,(long Count,double Amount)>();
                var playersAnalysis=new List<object>();
                foreach(var group in positive.GroupBy(x=>x.PlayerUuid))
                {
                    var activations=encounter.GetSkillActivationsFrom(group.Key,0,out _).OrderBy(x=>x.Timestamp).ToArray();
                    double unknown=0, total=0; var missingRecent=new Dictionary<string,double>();
                    foreach(var gain in group)
                    {
                        total+=gain.Change;
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
                SharesConserved=hate.Players.All(x=>Math.Abs(x.Skills.Sum(s=>s.Amount)-x.Bar.Amount)<0.001),
                DefeatedEnemies=encounter.ExData.DefeatedEnemies.Count,
                EnemyControlEvents=encounter.ExData.EnemyControlEvents.Count,
                encounter.ExData.UseQuestClock,encounter.ExData.QuestStartUtc
            }));
        }
    }
}
