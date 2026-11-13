using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

[Flags]
public enum HateEvidence { None = 0, Start = 1, Hit = 2, EffectDamage = 4, Healing = 8 }
public enum HateUnassignedReason { None, NoEvidence, SparseUpdate, MultipleSources, UnknownSource, OtherTarget, InvalidInterval }
public readonly record struct HateAttribution(long SourceKey, bool IsBuffSource, HateEvidence Evidence, HateUnassignedReason Reason)
{
    public static HateAttribution Unknown(HateUnassignedReason reason) => new(0, false, HateEvidence.None, reason);
}

/// <summary>Immutable per-player time index. Every positive delta is assigned once or kept unknown; no damage-proportional allocation.</summary>
public sealed class HateAttributionIndex
{
    private readonly HateCauseRecord[] _effects;
    private readonly (DateTime Timestamp, int SkillId)[] _starts;
    public HateAttributionIndex(IEnumerable<HateCauseRecord> effects, IEnumerable<(DateTime Timestamp, int SkillId)> starts)
    {
        _effects = effects.Where(x=>x.Value>0).OrderBy(x=>x.Timestamp).ToArray();
        _starts = starts.Where(x=>x.SkillId>0).OrderBy(x=>x.Timestamp).ToArray();
    }

    public HateAttribution Match(HateObservation gain)
    {
        if (gain.IsBaseline || gain.Change<=0 || gain.Timestamp<=gain.FromUtc)
            return HateAttribution.Unknown(HateUnassignedReason.InvalidInterval);
        if ((gain.Timestamp-gain.FromUtc).TotalSeconds>2)
            return HateAttribution.Unknown(HateUnassignedReason.SparseUpdate);
        var candidates = new Dictionary<(long Key,bool Buff),HateEvidence>();
        var origins = new HashSet<int>();
        var sawEffect = false;
        for (var i=AfterEffect(gain.FromUtc); i<_effects.Length && _effects[i].Timestamp<=gain.Timestamp; i++)
        {
            var effect=_effects[i];
            if(effect.PlayerUuid!=gain.PlayerUuid)continue;
            sawEffect=true;
            if(effect.Kind!=HateCauseKind.Healing && effect.TargetUuid!=gain.EnemyUuid)continue;
            // Unresolved effects must not be silently discarded in favor of a known candidate.
            if(effect.SourceKey<=0 || CombatDataCatalog.SourceKeyOwnerId(effect.SourceKey)<=0)
                return HateAttribution.Unknown(HateUnassignedReason.UnknownSource);
            // Explicit DPS/HPS rows identify one source, including skill/buff aliases. Raw ids still need separate namespaces.
            var hasCanonicalRow=CombatDataCatalog.TryResolveRecountRow(effect.SourceKey,out var row) && row==effect.SourceKey;
            var key=(effect.SourceKey,effect.IsBuffSource && !hasCanonicalRow);
            var evidence=effect.Kind switch { HateCauseKind.Healing=>HateEvidence.Healing,
                HateCauseKind.EffectDamage=>HateEvidence.EffectDamage,_=>HateEvidence.Hit };
            candidates[key]=candidates.GetValueOrDefault(key)|evidence;
            if(effect.OriginSkillId>0)origins.Add(effect.OriginSkillId);
        }
        if(candidates.Count>1) return HateAttribution.Unknown(HateUnassignedReason.MultipleSources);

        var starts = new List<int>();
        for(var i=AfterStart(gain.FromUtc); i<_starts.Length && _starts[i].Timestamp<=gain.Timestamp; i++) starts.Add(_starts[i].SkillId);
        if(candidates.Count==1)
        {
            var candidate=candidates.Single();
            // An unrelated skill beginning here could itself change hate (e.g. taunt), even before its first hit.
            if(starts.Any(id=>!origins.Contains(id) && (candidate.Key.Buff || CombatDataCatalog.GetSkillActivationSourceKey(id)!=candidate.Key.Key)))
                return HateAttribution.Unknown(HateUnassignedReason.MultipleSources);
            return new(candidate.Key.Key,candidate.Key.Buff,candidate.Value,HateUnassignedReason.None);
        }
        if(sawEffect) return HateAttribution.Unknown(HateUnassignedReason.OtherTarget);
        // Old histories do not contain effects. Preserve the older bounded start-only estimate and identify its weaker basis.
        var sources=starts.Select(CombatDataCatalog.GetSkillActivationSourceKey).Distinct().ToArray();
        return sources.Length==1 ? new(sources[0],false,HateEvidence.Start,HateUnassignedReason.None)
            : HateAttribution.Unknown(sources.Length>1 ? HateUnassignedReason.MultipleSources : HateUnassignedReason.NoEvidence);
    }

    private int AfterEffect(DateTime time)
    {
        var lo=0;var hi=_effects.Length;
        while(lo<hi) {var mid=lo+(hi-lo)/2;if(_effects[mid].Timestamp<=time)lo=mid+1;else hi=mid;}
        return lo;
    }
    private int AfterStart(DateTime time)
    {
        var lo=0;var hi=_starts.Length;
        while(lo<hi) {var mid=lo+(hi-lo)/2;if(_starts[mid].Timestamp<=time)lo=mid+1;else hi=mid;}
        return lo;
    }
}
