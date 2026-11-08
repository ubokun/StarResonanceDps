using System.Reflection;
using Dapper;
using Microsoft.Data.Sqlite;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using Zproto;

internal static class HistoryReadback
{
    public static void Run(string path)
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
