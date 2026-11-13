using Zproto;
namespace StarResonanceDps.Core.CombatRuntime;

public sealed record EncounterParticipant(string Server, long CharacterId, string Name);

public static class EncounterParticipants
{
    public static IReadOnlyList<EncounterParticipant> Read(Encounter encounter)
    {
        var hate = encounter.ExData.HateObservations.Where(x => !x.IsBaseline && x.Change > 0).Select(x => x.PlayerUuid).ToHashSet();
        return encounter.Entities.Where(pair => Utils.UuidToEntityType(pair.Key) == (long)EEntityType.EntChar)
            .Where(pair => pair.Value.TotalDamage > 0 || pair.Value.TotalHealing > 0
                || pair.Value.TotalHate > 0 || pair.Value.TotalBreak > 0 || pair.Value.TakenStats.HasSkillSnapshots() || pair.Value.IsNamedInCombatEventLog || hate.Contains(pair.Key))
            .Select(pair => Utils.UuidToEntityId(pair.Key)).Distinct()
            .Select(id => MeterSnapshotProvider.GetBuildPlayer(id, encounter)?.Player)
            .Where(player => player is not null && !player.IsNpc)
            .Select(player => new EncounterParticipant(encounter.ExData.GameServer.ToString(), player!.CharacterId, player.Name)).ToArray();
    }
}
