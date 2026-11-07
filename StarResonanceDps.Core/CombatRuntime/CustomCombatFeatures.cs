using ProtoBuf;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.Core.CombatRuntime;

[ProtoContract]
public sealed class FactorActivation
{
    [ProtoMember(1)] public DateTime Timestamp { get; set; }
    [ProtoMember(2)] public long EntityUuid { get; set; }
    [ProtoMember(3)] public int BuffId { get; set; }
    [ProtoMember(4)] public int SourceId { get; set; }
}

/// <summary>Only the packet thread mutates quest state. Settings are applied from the application UI.</summary>
public static class CustomCombatFeatures
{
    public static bool SyncQuestClock { get; set; }
    public static bool ResetOnQuestTransition { get; set; } = true;
    public static int IdleResetSeconds { get; set; }
    public static int IntervalResetSeconds { get; set; }
    public static DateTime? QuestStartUtc { get; private set; }
    public static bool QuestStartFromServer { get; private set; }

    public static void ClearQuest() { QuestStartUtc = null; QuestStartFromServer = false; }
    public static void BeginQuest(DateTime utc, bool fromServer = false) { QuestStartUtc = utc; QuestStartFromServer = fromServer; }

    public static DateTime ResolveQuestStart(DateTime arrivalUtc, int? serverStartSeconds)
    {
        if (serverStartSeconds is not > 0) return arrivalUtc;
        var proposed = DateTimeOffset.FromUnixTimeSeconds(serverStartSeconds.Value).UtcDateTime;
        return proposed <= arrivalUtc.AddSeconds(5) && proposed >= arrivalUtc.AddDays(-1) ? proposed : arrivalUtc;
    }

    public static void InitializeEncounter(Encounter encounter)
    {
        encounter.ExData.QuestStartUtc = QuestStartUtc;
        encounter.ExData.QuestStartFromServer = QuestStartFromServer;
        encounter.ExData.UseQuestClock = SyncQuestClock && QuestStartUtc.HasValue && !EncounterManager.IsBenchmarkActive;
        encounter.ExData.IdleResetSeconds = Math.Clamp(IdleResetSeconds, 0, 86400);
        encounter.ExData.IntervalResetSeconds = Math.Clamp(IntervalResetSeconds, 0, 86400);
    }

    public static void BeforePacket(DateTime arrivalUtc)
    {
        CheckReset(arrivalUtc);
        EncounterManager.RecordEndedAnnouncementBars(arrivalUtc);
    }

    public static bool ShouldReset(EncounterExData data, DateTime utc) =>
        (data.IdleResetSeconds > 0 && data.LastCombatEventTimeStamp is { } last && utc >= last.AddSeconds(data.IdleResetSeconds))
        || (data.IntervalResetSeconds > 0 && data.FirstDamageTimeStamp is { } start && utc >= start.AddSeconds(data.IntervalResetSeconds));

    public static void CheckReset(DateTime arrivalUtc)
    {
        var encounter = EncounterManager.Current;
        if (encounter is not null && !EncounterManager.IsBenchmarkActive && !encounter.ExData.UseQuestClock && encounter.EndTime == DateTime.MinValue
            && ShouldReset(encounter.ExData, arrivalUtc))
            EncounterManager.EnterDungeon(keepPastEncounterInMeter: false, force: true, reason: EncounterStartReason.Force);
    }

    public static void RecordParty(Encounter encounter)
    {
        var ids = PartyStateStore.Instance.Current.OrderedCharacterIds;
        lock (encounter.ExData.PartyCharacterIds)
        {
            foreach (var id in ids)
                if (!encounter.ExData.PartyCharacterIds.Contains(id)) encounter.ExData.PartyCharacterIds.Add(id);
        }
    }

    public static void RecordFactor(Encounter encounter, long entityUuid, int buffId, int sourceId, DateTime utc)
    {
        var recognized = CombatDataCatalog.TryGetRootBuffKind(buffId, out var kind, out _) && kind == RootBuffKind.Factor;
        var fromFactor = CombatDataCatalog.TryGetRootBuffKind(sourceId, out var sourceKind, out _) && sourceKind == RootBuffKind.Factor;
        if ((!recognized && !fromFactor) || !encounter.IsRecorded(utc, entityUuid)) return;
        lock (encounter.ExData.FactorActivations)
            encounter.ExData.FactorActivations.Add(new FactorActivation { Timestamp = utc, EntityUuid = entityUuid, BuffId = buffId, SourceId = sourceId });
    }
}
