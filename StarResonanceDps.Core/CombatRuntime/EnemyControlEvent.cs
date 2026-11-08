using ProtoBuf;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

public enum EnemyControlKind { Silence = 1, SilenceRemoved = 2, CastEnded = 3, SkillStarted = 4 }

[ProtoContract]
public sealed class EnemyControlEvent
{
    [ProtoMember(1)] public DateTime Timestamp { get; set; }
    [ProtoMember(2)] public long EntityUuid { get; set; }
    [ProtoMember(3)] public EnemyControlKind Kind { get; set; }
    [ProtoMember(4)] public int BuffId { get; set; }
    [ProtoMember(5)] public int SkillId { get; set; }
}

public static class EnemyControlRecorder
{
    public static void RecordState(Encounter encounter, long uuid, DateTime utc, int previousSkill, int currentSkill,
        bool skillChanged, bool? previousSilence, bool? currentSilence, bool silenceChanged)
    {
        if (Utils.UuidToEntityType(uuid) != (long)EEntityType.EntMonster) return;
        if (silenceChanged && currentSilence == true && previousSilence != true)
            Add(encounter, new() { Timestamp = utc, EntityUuid = uuid, Kind = EnemyControlKind.Silence });
        if (silenceChanged && previousSilence == true && currentSilence == false)
            Add(encounter, new() { Timestamp = utc, EntityUuid = uuid, Kind = EnemyControlKind.SilenceRemoved });
        if (skillChanged && currentSkill > 0 && currentSkill != previousSkill)
            Add(encounter, new() { Timestamp = utc, EntityUuid = uuid, Kind = EnemyControlKind.SkillStarted, SkillId = currentSkill });
        // A cleared skill ID proves only that the cast ended, not why it ended.
        if (skillChanged && previousSkill > 0 && currentSkill == 0)
            Add(encounter, new() { Timestamp = utc, EntityUuid = uuid, Kind = EnemyControlKind.CastEnded, SkillId = previousSkill });
    }

    public static void RecordBuff(Encounter encounter, long uuid, int buffId, DateTime utc)
    {
        if (Utils.UuidToEntityType(uuid) != (long)EEntityType.EntMonster || !CombatDataCatalog.IsSilenceBuff(buffId)) return;
        Add(encounter, new() { Timestamp = utc, EntityUuid = uuid, Kind = EnemyControlKind.Silence, BuffId = buffId });
    }

    private static void Add(Encounter encounter, EnemyControlEvent item)
    {
        if (!encounter.IsRecorded(item.Timestamp, item.EntityUuid)) return;
        encounter.GetOrCreateEntity(item.EntityUuid).IsNamedInCombatEventLog = true;
        encounter.RecordCombatEvent(item.Timestamp);
        lock (encounter.ExData.EnemyControlEvents)
        {
            var existing = encounter.ExData.EnemyControlEvents.LastOrDefault(x => x.Timestamp == item.Timestamp
                && x.EntityUuid == item.EntityUuid && x.Kind == item.Kind);
            if (existing is not null) { if (item.BuffId > 0) existing.BuffId = item.BuffId; return; }
            encounter.ExData.EnemyControlEvents.Add(item);
        }
    }
}
