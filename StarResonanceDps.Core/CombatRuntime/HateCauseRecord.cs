using ProtoBuf;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

public enum HateCauseKind { Hit = 1, EffectDamage = 2, Healing = 3 }

/// <summary>An observed effect, not a claim about how much threat it generated. Uses the same source key as DPS/HPS.</summary>
[ProtoContract]
public record struct HateCauseRecord
{
    [ProtoMember(1)] public long PlayerUuid { get; set; }
    [ProtoMember(2)] public long TargetUuid { get; set; }
    [ProtoMember(3)] public DateTime Timestamp { get; set; }
    [ProtoMember(4)] public long SourceKey { get; set; }
    [ProtoMember(5)] public bool IsBuffSource { get; set; }
    [ProtoMember(6)] public HateCauseKind Kind { get; set; }
    [ProtoMember(7)] public long Value { get; set; }
    [ProtoMember(8)] public int OriginSkillId { get; set; }
}

public static class HateCauseRecorder
{
    // Called on the packet thread only, after the normal aggregation gates accepted the effect.
    public static void Record(Encounter encounter, HateCauseRecord effect)
    {
        if (effect.Value <= 0 || !Enum.IsDefined(effect.Kind) || Utils.UuidToEntityType(effect.PlayerUuid) != (long)EEntityType.EntChar
            || !encounter.IsRecorded(effect.Timestamp, effect.PlayerUuid)) return;
        var targetType = Utils.UuidToEntityType(effect.TargetUuid);
        // Friendly training dummies and summons are not EntChar. The accepted healing notification is the evidence.
        if (effect.Kind == HateCauseKind.Healing ? effect.TargetUuid == 0 : targetType != (long)EEntityType.EntMonster) return;
        var records = encounter.ExData.HateCauses;
        // Identical multi-hit entries in one packet have the same attribution evidence; retain their combined amount.
        if (records.Count > 0)
        {
            var last = records[^1];
            if (last.PlayerUuid == effect.PlayerUuid && last.TargetUuid == effect.TargetUuid && last.Timestamp == effect.Timestamp
                && last.SourceKey == effect.SourceKey && last.IsBuffSource == effect.IsBuffSource && last.Kind == effect.Kind
                && last.OriginSkillId == effect.OriginSkillId && last.Value <= long.MaxValue - effect.Value)
            { last.Value += effect.Value; records[^1] = last; return; }
        }
        records.Add(effect);
    }
}
