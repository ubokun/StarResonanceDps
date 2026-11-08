using StarResonanceDps.Core.CombatRuntime;
using Zproto;

namespace StarResonanceDps.App.Services;

public sealed record HateMeterBar(long Uuid, int SkillId, string Name, double Amount, double PerSecond, double Share, string? IconPath = null)
{
    public double ShareRatio => Share / 100;
    public string ValueText => $"{PerSecond:N1} /秒 · {Amount:N0} · {Share:0.0}%";
    public string Action => Name;
    public string Track => "スキル";
    public SkillIconFrame Frame => SkillId > 0 ? CombatDataCatalog.GetSkillActivationDisplay(SkillId).Frame : SkillIconFrame.Standard;
    public string FallbackGlyph => "•";
}
public sealed record HatePlayerMeter(HateMeterBar Bar, IReadOnlyList<HateMeterBar> Skills);
public sealed record HateMeterSnapshot(ulong Id, string Caption, double Duration, IReadOnlyList<HatePlayerMeter> Players,
    IReadOnlyList<TimelineActor> Enemies, int Baselines, double Reductions);

public static class HateMeterReader
{
    public static Task<HateMeterSnapshot> ReadAsync(long enemyUuid, bool partyOnly)
    {
        var result = new TaskCompletionSource<HateMeterSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        MessageManager.RunOnPacketThread(() =>
        {
            try { result.SetResult(Build(MeterSnapshotProvider.GetTimeline(null, 0).Encounter, enemyUuid, partyOnly)); }
            catch (Exception ex) { result.SetException(ex); }
        });
        return result.Task;
    }
    public static HateMeterSnapshot Build(Encounter? encounter, long enemyUuid = 0, bool partyOnly = false)
    {
        if (encounter is null) return new(0,"集計なし",0,[],[],0,0);
        HateObservation[] observations;
        lock (encounter.ExData.HateObservations) observations = encounter.ExData.HateObservations.ToArray();
        var enemies = encounter.Entities.Values.Where(x => x.EntityType == EEntityType.EntMonster)
            .Select(x => new TimelineActor(x.UUID, CombatTimelineReader.EnemyName(encounter,x.UUID) + $" [{x.UUID}]",true)).ToArray();
        enemies = enemies.Concat(encounter.ExData.DefeatedEnemies.Where(x=>!enemies.Any(e=>e.Uuid==x.Uuid))
            .Select(x=>new TimelineActor(x.Uuid,$"{CombatDataCatalog.GetMonsterName(x.TypeId)}（討伐済み） [{x.Uuid}]",true))).ToArray();
        var clock = encounter.ReadCombatClock();
        var duration = clock.Elapsed.TotalSeconds;
        var first = observations.Select(x=>x.Timestamp).DefaultIfEmpty(DateTime.UtcNow).Min();
        if (clock.StartUtc is null) duration = Math.Max(0, (observations.Select(x=>x.Timestamp).DefaultIfEmpty(first).Max()-first).TotalSeconds);
        var allowed = encounter.ExData.PartyCharacterIds.ToHashSet();
        if (partyOnly && ReferenceEquals(encounter,EncounterManager.Current))
            foreach(var self in MeterSnapshotProvider.GetSnapshot(MeterSnapshotKind.Damage).Players.Where(x=>x.IsSelf)) allowed.Add(self.CharacterId);
        var items = observations.Where(x => (enemyUuid == 0 || x.EnemyUuid == enemyUuid)
            && (!partyOnly || allowed.Contains(Utils.UuidToEntityId(x.PlayerUuid)))
            && (clock.StartUtc is null || x.Timestamp >= clock.StartUtc)).ToArray();
        var gains = items.Where(x=>!x.IsBaseline && x.Change > 0).ToArray();
        var total = gains.Sum(x=>(double)x.Change);
        var players = gains.GroupBy(x=>x.PlayerUuid).Select(group =>
        {
            var sum = group.Sum(x=>(double)x.Change);
            var entity = encounter.Entities.GetValueOrDefault(group.Key);
            var name = string.IsNullOrWhiteSpace(entity?.Name) ? $"プレイヤー {Utils.UuidToEntityId(group.Key)}" : entity.Name;
            var activations = encounter.GetSkillActivationsFrom(group.Key,0,out _);
            var skills = group.GroupBy(gain => EstimateSkill(gain, activations.Select(x=>(x.Timestamp,x.SkillId))))
                .Select(g => { var amount=g.Sum(x=>(double)x.Change); var display=CombatDataCatalog.GetSkillActivationDisplay(g.Key);
                    return new HateMeterBar(group.Key,g.Key,g.Key==0 ? "帰属不明" : (string.IsNullOrWhiteSpace(display.Name) ? $"技 #{g.Key}" : display.Name) + "（推定）", amount,
                        duration > 0 ? amount/duration : 0,sum>0 ? amount/sum*100 : 0,g.Key==0 ? null : CombatIconResolver.ResolveSkillIcon(display.IconName)); })
                .OrderByDescending(x=>x.Amount).ToArray();
            return new HatePlayerMeter(new(group.Key,0,name,sum,duration>0 ? sum/duration : 0,total>0 ? sum/total*100 : 0),skills);
        }).OrderByDescending(x=>x.Bar.Amount).ToArray();
        var scope = partyOnly ? allowed.Count==0 ? "PT情報未受信（対象なし）" : "PTのみ" : "全プレイヤー";
        return new(encounter.EncounterId,$"{encounter.SceneName} · #{encounter.EncounterId} · {scope}" + (clock.StartUtc is null ? " · 観測時間基準" : " · 集計時間基準"),
            duration,players,enemies,items.Count(x=>x.IsBaseline),items.Where(x=>x.Change<0).Sum(x=>-(double)x.Change));
    }
    public static int EstimateSkill(HateObservation gain, IEnumerable<(DateTime Timestamp,int SkillId)> activations)
    {
        if (gain.IsBaseline || gain.Change <= 0 || gain.Timestamp <= gain.FromUtc || (gain.Timestamp-gain.FromUtc).TotalSeconds > 2) return 0;
        var candidates = activations.Where(x=>x.Timestamp>gain.FromUtc && x.Timestamp<=gain.Timestamp).Select(x=>x.SkillId).Distinct().ToArray();
        return candidates.Length==1 ? candidates[0] : 0;
    }
}
