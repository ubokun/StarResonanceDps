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

// Retained for reading older encounter records; no new records are collected.
