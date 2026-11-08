using System.Runtime.CompilerServices;
using ProtoBuf;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

[ProtoContract]
public sealed class HateObservation
{
    [ProtoMember(1)] public long EnemyUuid { get; set; }
    [ProtoMember(2)] public long PlayerUuid { get; set; }
    [ProtoMember(3)] public DateTime FromUtc { get; set; }
    [ProtoMember(4)] public DateTime Timestamp { get; set; }
    [ProtoMember(5)] public long Change { get; set; }
    [ProtoMember(6)] public bool IsBaseline { get; set; }
}

public static class HateObservationRecorder
{
    private sealed class State
    {
        public Dictionary<long, (DateTime Time, Dictionary<long, uint> Values)> Enemies = [];
        public bool LoggedBaseline, LoggedIncrease;
    }
    private static readonly ConditionalWeakTable<Encounter, State> States = new();

    // Packet thread only. A new encounter, a missing entry, or a cleared list starts a new baseline.
    public static void Record(Encounter encounter, long enemyUuid, IEnumerable<HateInfo> values, DateTime utc)
    {
        if (Utils.UuidToEntityType(enemyUuid) != (long)EEntityType.EntMonster) return;
        var state = States.GetOrCreateValue(encounter);
        var next = values.Where(x => Utils.UuidToEntityType(x.Uuid) == (long)EEntityType.EntChar)
            .GroupBy(x => x.Uuid).ToDictionary(g => g.Key, g => g.Last().HateVal);
        var hasPrevious = state.Enemies.TryGetValue(enemyUuid, out var previous);
        if (hasPrevious && utc < previous.Time) return;
        state.Enemies[enemyUuid] = (utc, next);
        foreach (var item in next)
        {
            var baseline = !hasPrevious || !previous.Values.TryGetValue(item.Key, out _);
            var change = baseline ? 0 : (long)item.Value - previous.Values[item.Key];
            if (!baseline && change == 0 || !encounter.IsRecorded(utc, item.Key)) continue;
            lock (encounter.ExData.HateObservations)
                encounter.ExData.HateObservations.Add(new() { EnemyUuid = enemyUuid, PlayerUuid = item.Key,
                    Timestamp = utc, FromUtc = hasPrevious ? previous.Time : utc, Change = change, IsBaseline = baseline });
            // Log only the first observation and increase per encounter, without player identities or packet payloads.
            if (baseline && !state.LoggedBaseline)
            {
                state.LoggedBaseline = true;
                Serilog.Log.Information("Hate baseline received (encounter {EncounterId})", encounter.EncounterId);
            }
            if (change > 0 && !state.LoggedIncrease)
            {
                state.LoggedIncrease = true;
                Serilog.Log.Information("Hate increase recorded (encounter {EncounterId})", encounter.EncounterId);
            }
        }
    }
}
