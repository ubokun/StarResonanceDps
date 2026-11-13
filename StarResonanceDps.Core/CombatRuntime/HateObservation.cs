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

// Retained for reading older encounter records; no new records are collected.
