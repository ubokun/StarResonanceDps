using StarResonanceDps.Core.CombatRuntime;
using Zproto;

namespace StarResonanceDps.App.Services;

public sealed record HateMeterBar(long Uuid, int SkillId, string Name, double Amount, double PerSecond, double Share, string? IconPath = null,
    long SourceKey = 0, bool IsBuffSource = false, HateEvidence Evidence = HateEvidence.None)
{
    public double ShareRatio => Share / 100;
    public string ValueText => $"{PerSecond:N1} /秒 · {Amount:N0} · {Share:0.0}%";
    public string Action => Name;
    public string Track => "スキル";
    public SkillIconFrame Frame => !IsBuffSource && SkillId > 0 ? CombatDataCatalog.GetSkillActivationDisplay(SkillId).Frame : SkillIconFrame.Standard;
    public string FallbackGlyph => "•";
    public string AttributionText => Name + (SourceKey==0 ? "" : "\n根拠：" + string.Join("・", new[] {
        Evidence.HasFlag(HateEvidence.Hit) ? "命中" : null,
        Evidence.HasFlag(HateEvidence.EffectDamage) ? "継続・効果ダメージ" : null,
        Evidence.HasFlag(HateEvidence.Healing) ? "回復" : null,
        Evidence.HasFlag(HateEvidence.Start) ? "開始通知のみ" : null }.Where(x=>x is not null)))
        + (Evidence==HateEvidence.None ? "" : "\n通知時刻の照合による推定です。サーバーが通知した技別ヘイト値ではありません。");
}
public sealed record HatePlayerMeter(HateMeterBar Bar, IReadOnlyList<HateMeterBar> Skills);
public sealed record HateMeterSnapshot(ulong Id, string Caption, double Duration, IReadOnlyList<HatePlayerMeter> Players,
    IReadOnlyList<TimelineActor> Enemies, int Baselines, double Reductions, int Observations = 0, bool IsHistorical = false,
    int EffectMatches = 0, int StartMatches = 0, double UnknownShare = 0, int CauseRecords = 0);

public static class HateMeterReader
{
    public static Task<HateMeterSnapshot> ReadAsync(long enemyUuid, bool partyOnly)
    {
        var result = new TaskCompletionSource<HateMeterSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        MessageManager.RunOnPacketThread(() =>
        {
            // Nearby entities belong to the current encounter, even while the DPS meter keeps the previous encounter visible.
            try { var history=AppState.OpenedHistoricalEncounter; result.SetResult(Build(history ?? EncounterManager.Current, enemyUuid, partyOnly, isHistorical: history is not null)); }
            catch (Exception ex) { result.SetException(ex); }
        });
        return result.Task;
    }
    public static HateMeterSnapshot Build(Encounter? encounter, long enemyUuid = 0, bool partyOnly = false, bool isHistorical = true)
    {
        if (encounter is null) return new(0,"集計なし",0,[],[],0,0);
        HateObservation[] observations;
        lock (encounter.ExData.HateObservations) observations = encounter.ExData.HateObservations.ToArray();
        var enemies = encounter.Entities.Values.Where(x => x.EntityType == EEntityType.EntMonster)
            .Select(x => new TimelineActor(x.UUID, CombatTimelineReader.EnemyName(encounter,x.UUID) + $" [{x.UUID}]",true)).ToArray();
        enemies = enemies.Concat(encounter.ExData.DefeatedEnemies.Where(x=>!enemies.Any(e=>e.Uuid==x.Uuid))
            .Select(x=>new TimelineActor(x.Uuid,$"{CombatDataCatalog.GetMonsterName(x.TypeId)}（討伐済み） [{x.Uuid}]",true))).ToArray();
        enemies = enemies.Concat(observations.Select(x=>x.EnemyUuid).Distinct().Where(uuid=>!enemies.Any(x=>x.Uuid==uuid))
            .Select(uuid=>new TimelineActor(uuid,$"記録された敵 [{uuid}]",true))).ToArray();
        var clock = encounter.ReadCombatClock();
        var duration = clock.Elapsed.TotalSeconds;
        var first = observations.Select(x=>x.Timestamp).DefaultIfEmpty(DateTime.UtcNow).Min();
        if (clock.StartUtc is null) duration = Math.Max(0, (observations.Select(x=>x.Timestamp).DefaultIfEmpty(first).Max()-first).TotalSeconds);
        var allowed = encounter.ExData.PartyCharacterIds.ToHashSet();
        if (partyOnly && !isHistorical)
            foreach(var self in MeterSnapshotProvider.GetSnapshot(MeterSnapshotKind.Damage).Players.Where(x=>x.IsSelf)) allowed.Add(self.CharacterId);
        var items = observations.Where(x => (enemyUuid == 0 || x.EnemyUuid == enemyUuid)
            && (!partyOnly || allowed.Contains(Utils.UuidToEntityId(x.PlayerUuid)))
            && (x.IsBaseline || clock.StartUtc is null || x.Timestamp >= clock.StartUtc)).ToArray();
        var gains = items.Where(x=>!x.IsBaseline && x.Change > 0).ToArray();
        var total = gains.Sum(x=>(double)x.Change);
        var causes=encounter.ExData.HateCauses.GroupBy(x=>x.PlayerUuid).ToDictionary(g=>g.Key,g=>g.ToArray());
        var effectMatches=0; var startMatches=0; double unknownAmount=0;
        var players = items.GroupBy(x=>x.PlayerUuid).Select(observed =>
        {
            var group = observed.Where(x=>!x.IsBaseline && x.Change>0).ToArray();
            var sum = group.Sum(x=>(double)x.Change);
            var entity = encounter.Entities.GetValueOrDefault(observed.Key);
            var name = string.IsNullOrWhiteSpace(entity?.Name) ? $"プレイヤー {Utils.UuidToEntityId(observed.Key)}" : entity.Name;
            var activations = encounter.GetSkillActivationsFrom(observed.Key,0,out _);
            var index=new HateAttributionIndex(causes.GetValueOrDefault(observed.Key) ?? [],activations.Select(x=>(x.Timestamp,x.SkillId)));
            var attributed=group.Select(gain=>
            {
                var match=index.Match(gain);
                if(match.SourceKey==0) unknownAmount+=gain.Change;
                else if(match.Evidence==HateEvidence.Start)startMatches++;else effectMatches++;
                return (Gain:gain,Match:match);
            }).ToArray();
            var skills = attributed.GroupBy(x=>(x.Match.SourceKey,x.Match.IsBuffSource))
                .Select(g => {
                    var amount=g.Sum(x=>(double)x.Gain.Change); var key=g.Key.SourceKey;
                    var owner=CombatDataCatalog.SourceKeyOwnerId(key);
                    var landing=entity?.SkillMetrics.GetValueOrDefault(key)?.Landing ?? default;
                    var display=key==0 ? "帰属不明" : CombatDataCatalog.GetSourceDisplayName(key,g.Key.IsBuffSource,landing);
                    var icon=g.Key.IsBuffSource ? CombatIconResolver.ResolveBuffIcon(CombatDataCatalog.GetBuffOwnIconName(owner))
                        : CombatIconResolver.ResolveSkillIcon(CombatDataCatalog.GetSkillActivationDisplay(owner).IconName);
                    icon ??= CombatIconResolver.ResolveBuffIcon(CombatDataCatalog.GetBuffOwnIconName(owner));
                    var evidence=g.Aggregate(HateEvidence.None,(e,x)=>e|x.Match.Evidence);
                    return new HateMeterBar(observed.Key,key==0 ? 0 : owner,key==0 ? display : (string.IsNullOrWhiteSpace(display) ? $"技 #{CombatDataCatalog.FormatSourceKey(key)}" : display)+"（推定）",amount,
                        duration > 0 ? amount/duration : 0,sum>0 ? amount/sum*100 : 0,key==0 ? null : icon,key,g.Key.IsBuffSource,evidence);
                })
                .OrderByDescending(x=>x.Amount).ToArray();
            return new HatePlayerMeter(new(observed.Key,0,name,sum,duration>0 ? sum/duration : 0,total>0 ? sum/total*100 : 0),skills);
        }).OrderByDescending(x=>x.Bar.Amount).ToArray();
        var scope = partyOnly ? allowed.Count==0 ? "PT情報未受信（対象なし）" : "PTのみ" : "全プレイヤー";
        return new(encounter.EncounterId,$"{encounter.SceneName} · #{encounter.EncounterId} · {scope}" + (clock.StartUtc is null ? " · 観測時間基準" : " · 集計時間基準"),
            duration,players,enemies,items.Count(x=>x.IsBaseline),items.Where(x=>x.Change<0).Sum(x=>-(double)x.Change),items.Length,
            isHistorical,effectMatches,startMatches,total>0 ? unknownAmount/total*100 : 0,encounter.ExData.HateCauses.Count);
    }
    public static int EstimateSkill(HateObservation gain, IEnumerable<(DateTime Timestamp,int SkillId)> activations)
    {
        if (gain.IsBaseline || gain.Change <= 0 || gain.Timestamp <= gain.FromUtc || (gain.Timestamp-gain.FromUtc).TotalSeconds > 2) return 0;
        var candidates = activations.Where(x=>x.Timestamp>gain.FromUtc && x.Timestamp<=gain.Timestamp).Select(x=>x.SkillId).Distinct().ToArray();
        return candidates.Length==1 ? candidates[0] : 0;
    }
}
