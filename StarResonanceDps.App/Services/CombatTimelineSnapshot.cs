using StarResonanceDps.Core.CombatRuntime;
using Zproto;

namespace StarResonanceDps.App.Services;

public sealed record TimelineActor(long Uuid, string Name, bool IsEnemy = false);
public sealed record ExtensionTimelineEvent(double Seconds, string Track, long EntityUuid, string Actor, string Action, string? IconPath = null, SkillIconFrame Frame = SkillIconFrame.Standard)
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
                var (name, icon, frame, imagine) = CombatDataCatalog.GetSkillActivationDisplay(skill.SkillId);
                if (imagine ? !options.ShowImagines : !options.ShowSkills) continue;
                events.Add(new(encounter.ToCombatOffset(skill.Timestamp)?.TotalSeconds ?? 0,
                    imagine ? "イマジン" : "スキル", player.UUID, player.Name ?? id.ToString(), $"{name} [#{skill.SkillId}]", CombatIconResolver.ResolveSkillIcon(icon), frame));
            }
        }
        if (options.ShowEnemies)
        {
            foreach (var line in timeline.Lines)
                events.Add(new(encounter.ToCombatOffset(line.Timestamp)?.TotalSeconds ?? 0, line.Attacker.Uuid == 0 ? "予告（対象未確定）" : "敵行動",
                    line.Attacker.Uuid, line.Attacker.Name, $"{line.SourceName} {line.BuffName}"
                        + (line.Target is null ? "" : $" → {line.Target.Name}") + (line.Value == 0 ? "" : $" {line.Value:N0}"),
                    line.IsBuffSource ? CombatIconResolver.ResolveBuffIcon(CombatDataCatalog.GetBuffOwnIconName(line.SourceId))
                        : CombatIconResolver.ResolveSkillIcon(CombatDataCatalog.GetSkillIconName(line.SourceId))));
            foreach (var hit in MeterSnapshotProvider.GetDamageTakenLog(null, 0).Hits)
            {
                if (hit.IsFall || hit.Attacker.IsPlayer || hit.Attacker.IsSystem
                    || (options.PartyOnly && partyKnown && !partyIds.Contains(hit.CharacterId))) continue;
                events.Add(new(encounter.ToCombatOffset(hit.Timestamp)?.TotalSeconds ?? 0, "敵行動",
                    hit.Attacker.Uuid, hit.Attacker.Name, $"{hit.SourceName} → プレイヤー {hit.CharacterId}：{hit.Value:N0}ダメージ",
                    hit.IsBuffSource ? CombatIconResolver.ResolveBuffIcon(CombatDataCatalog.GetBuffOwnIconName(hit.SourceId))
                        : CombatIconResolver.ResolveSkillIcon(CombatDataCatalog.GetSkillIconName(hit.SourceId))));
            }
            lock (encounter.ExData.EnemyControlEvents)
                foreach (var control in encounter.ExData.EnemyControlEvents)
                {
                    var actor = encounter.Entities.GetValueOrDefault(control.EntityUuid)?.Name ?? "敵";
                    var action = control.Kind switch
                    {
                        EnemyControlKind.Silence => control.BuffId > 0 ? $"沈黙付与：{CombatDataCatalog.GetBuffName(control.BuffId)}" : "沈黙状態を確認",
                        EnemyControlKind.SilenceRemoved => "沈黙解除を確認",
                        _ => $"詠唱終了（理由不明）：{CombatDataCatalog.GetSkillDisplayName(control.SkillId)}"
                    };
                    events.Add(new(encounter.ToCombatOffset(control.Timestamp)?.TotalSeconds ?? 0, "敵行動", control.EntityUuid, actor, action,
                        control.BuffId > 0 ? CombatIconResolver.ResolveBuffIcon(CombatDataCatalog.GetBuffOwnIconName(control.BuffId))
                            : control.SkillId > 0 ? CombatIconResolver.ResolveSkillIcon(CombatDataCatalog.GetSkillIconName(control.SkillId)) : null));
                }
        }
        if (options.ShowFactors)
        {
            lock (encounter.ExData.FactorActivations)
                foreach (var factor in encounter.ExData.FactorActivations)
                {
                    if (options.PartyOnly && partyKnown && !players.Any(x => x.UUID == factor.EntityUuid)) continue;
                    var actor = encounter.Entities.GetValueOrDefault(factor.EntityUuid)?.Name ?? "不明";
                    var rootId = CombatDataCatalog.TryGetRootBuffKind(factor.SourceId, out var kind, out _) && kind == StarResonanceDps.Core.Models.RootBuffKind.Factor
                        ? factor.SourceId : factor.BuffId;
                    events.Add(new(encounter.ToCombatOffset(factor.Timestamp)?.TotalSeconds ?? 0, "因子効果",
                        factor.EntityUuid, actor, $"{CombatDataCatalog.GetRootBuffName(rootId)} [#{factor.BuffId}]（付与元 #{factor.SourceId}）",
                        CombatIconResolver.ResolveRootBuffIcon(StarResonanceDps.Core.Models.RootBuffKind.Factor, CombatDataCatalog.GetRootBuffIconName(rootId, 0))));
                }
        }
        var enemies = options.ShowEnemies ? encounter.Entities.Values.Where(x => x.EntityType == EEntityType.EntMonster)
            .Select(x => new TimelineActor(x.UUID, string.IsNullOrWhiteSpace(x.Name) ? "敵" : x.Name, true)).ToArray() : [];
        var actors = enemies.Concat(events.Select(x => new TimelineActor(x.EntityUuid, x.Actor))).DistinctBy(x => x.Uuid).ToArray();
        var points = buckets.OrderBy(x => x.Key).Select(x => new MetricGraphPoint(x.Key, x.Value.End, x.Value.Value)).ToArray();
        var scope = options.PartyOnly && partyKnown ? "PT DPS" : options.PartyOnly ? "全員DPS（PT情報未記録）" : "全員DPS";
        var clock = encounter.ReadCombatClock();
        return new(encounter.EncounterId, clock.Elapsed.TotalSeconds,
            $"{scope} · {encounter.SceneName} · #{encounter.EncounterId} · {(encounter.ExData.UseQuestClock
                ? encounter.ExData.QuestStartFromServer ? "クエスト時間（サーバー開始時刻）" : "クエスト時間（開始通知基準）" : "戦闘時間")}",
            points, events.OrderBy(x => x.Seconds).ToArray(), actors);
    }
}
