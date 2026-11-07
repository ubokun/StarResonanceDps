using StarResonanceDps.Core.CombatRuntime;
using Zproto;

namespace StarResonanceDps.App.Services;

public sealed record TimelineActor(long Uuid, string Name);
public sealed record ExtensionTimelineEvent(double Seconds, string Track, long EntityUuid, string Actor, string Action)
{
    public string Time => TimeSpan.FromSeconds(Math.Max(0, Seconds)).ToString(@"hh\:mm\:ss\.fff");
}
public sealed record CombatTimelineSnapshot(ulong EncounterId, double Duration, string Caption,
    IReadOnlyList<MetricGraphPoint> Points, IReadOnlyList<ExtensionTimelineEvent> Events, IReadOnlyList<TimelineActor> Actors);

public static class CombatTimelineReader
{
    public static Task<CombatTimelineSnapshot> ReadAsync(CombatExtensionOptions options)
    {
        var result = new TaskCompletionSource<CombatTimelineSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        MessageManager.RunOnPacketThread(() =>
        {
            try { result.SetResult(Read(options)); }
            catch (Exception ex) { result.SetException(ex); }
        });
        return result.Task;
    }

    private static CombatTimelineSnapshot Read(CombatExtensionOptions options)
    {
        var timeline = MeterSnapshotProvider.GetTimeline(null, 0);
        var encounter = timeline.Encounter;
        if (encounter is null) return new(0, 0, "集計なし", [], [], []);
        var meters = MeterSnapshotProvider.GetSnapshot(MeterSnapshotKind.Damage);
        var partyIds = encounter.ExData.PartyCharacterIds.ToHashSet();
        foreach (var player in meters.Players.Where(x => x.IsSelf)) partyIds.Add(player.CharacterId);
        var partyKnown = partyIds.Count > 0;
        var players = encounter.Entities.Values.Where(x => x.EntityType == EEntityType.EntChar && !x.IsNpc)
            .Where(x => !options.PartyOnly || !partyKnown || partyIds.Contains(x.UID != 0 ? x.UID : Utils.UuidToEntityId(x.UUID))).ToArray();
        var buckets = new Dictionary<double, (double End, double Value)>();
        var events = new List<ExtensionTimelineEvent>();
        foreach (var player in players)
        {
            var id = player.UID != 0 ? player.UID : Utils.UuidToEntityId(player.UUID);
            var buffer = new MetricGraphPointBuffer();
            MeterSnapshotProvider.ReadPlayerGraph(MeterSnapshotKind.Damage, id, 1, buffer);
            foreach (var point in buffer.CreateView())
            {
                var prior = buckets.GetValueOrDefault(point.StartSeconds);
                buckets[point.StartSeconds] = (Math.Max(prior.End, point.EndSeconds), prior.Value + point.ValuePerSecond);
            }
            foreach (var skill in encounter.GetSkillActivationsFrom(player.UUID, 0, out _))
            {
                var (name, _, _, imagine) = CombatDataCatalog.GetSkillActivationDisplay(skill.SkillId);
                if (imagine ? !options.ShowImagines : !options.ShowSkills) continue;
                events.Add(new(encounter.ToCombatOffset(skill.Timestamp)?.TotalSeconds ?? 0,
                    imagine ? "イマジン" : "スキル", player.UUID, player.Name ?? id.ToString(), $"{name} [#{skill.SkillId}]"));
            }
        }
        if (options.ShowEnemies)
        {
            foreach (var line in timeline.Lines)
                events.Add(new(encounter.ToCombatOffset(line.Timestamp)?.TotalSeconds ?? 0, "敵行動",
                    line.Attacker.Uuid, line.Attacker.Name, $"{line.SourceName} {line.BuffName}"
                        + (line.Target is null ? "" : $" → {line.Target.Name}") + (line.Value == 0 ? "" : $" {line.Value:N0}")));
            foreach (var hit in MeterSnapshotProvider.GetDamageTakenLog(null, 0).Hits)
            {
                if (hit.IsFall || hit.Attacker.IsPlayer || hit.Attacker.IsSystem
                    || (options.PartyOnly && partyKnown && !partyIds.Contains(hit.CharacterId))) continue;
                events.Add(new(encounter.ToCombatOffset(hit.Timestamp)?.TotalSeconds ?? 0, "敵行動",
                    hit.Attacker.Uuid, hit.Attacker.Name, $"{hit.SourceName} → プレイヤー {hit.CharacterId}：{hit.Value:N0}ダメージ"));
            }
        }
        if (options.ShowFactors)
        {
            lock (encounter.ExData.FactorActivations)
                foreach (var factor in encounter.ExData.FactorActivations)
                {
                    if (options.PartyOnly && partyKnown && !players.Any(x => x.UUID == factor.EntityUuid)) continue;
                    var actor = encounter.Entities.GetValueOrDefault(factor.EntityUuid)?.Name ?? "不明";
                    events.Add(new(encounter.ToCombatOffset(factor.Timestamp)?.TotalSeconds ?? 0, "因子効果",
                        factor.EntityUuid, actor, $"{CombatDataCatalog.GetRootBuffName(factor.BuffId)} [#{factor.BuffId}]（付与元 #{factor.SourceId}）"));
                }
        }
        var actors = events.Select(x => new TimelineActor(x.EntityUuid, x.Actor)).DistinctBy(x => x.Uuid).ToArray();
        var points = buckets.OrderBy(x => x.Key).Select(x => new MetricGraphPoint(x.Key, x.Value.End, x.Value.Value)).ToArray();
        var scope = options.PartyOnly && partyKnown ? "PT DPS" : options.PartyOnly ? "全員DPS（PT情報未記録）" : "全員DPS";
        var clock = encounter.ReadCombatClock();
        return new(encounter.EncounterId, clock.Elapsed.TotalSeconds,
            $"{scope} · {encounter.SceneName} · #{encounter.EncounterId} · {(encounter.ExData.UseQuestClock
                ? encounter.ExData.QuestStartFromServer ? "クエスト時間（サーバー開始時刻）" : "クエスト時間（開始通知基準）" : "戦闘時間")}",
            points, events.OrderBy(x => x.Seconds).ToArray(), actors);
    }
}
