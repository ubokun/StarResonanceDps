using System.IO;
using ProtoBuf;
using System.Collections.Frozen;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using ExtraPacketData = StarResonanceDps.Core.CombatRuntime.Protocols.ExtraPacketData;
using Zproto;

internal static class HateAttributionChecks
{
    public static void Run(Action<bool,string> check)
    {
        var t=DateTime.UtcNow.AddSeconds(-20);
        long U(long id,EEntityType type)=>Utils.EntityIdToUuid(id,(long)type,false,false);
        var player=U(87101,EEntityType.EntChar);var ally=U(87102,EEntityType.EntChar);
        var enemy=U(87103,EEntityType.EntMonster);var otherEnemy=U(87104,EEntityType.EntMonster);
        var key=CombatDataCatalog.MakeSourceKey(123456,1);var otherKey=CombatDataCatalog.MakeSourceKey(123456,2);
        var gain=new HateObservation {PlayerUuid=player,EnemyUuid=enemy,FromUtc=t,Timestamp=t.AddMilliseconds(200),Change=150};
        HateCauseRecord Effect(HateCauseKind kind=HateCauseKind.Hit)=>new() {PlayerUuid=player,TargetUuid=enemy,Timestamp=t.AddMilliseconds(100),
            SourceKey=key,Kind=kind,Value=90,OriginSkillId=123456};
        HateAttribution Match(IEnumerable<HateCauseRecord> effects,params (DateTime,int)[] starts)=>new HateAttributionIndex(effects,starts).Match(gain);
        check(HateMeterReader.EstimateSkill(gain,[(t.AddSeconds(-1),123456)])==0 && Match([Effect()]).SourceKey==key,
            "actual hit reduces unknown even when skill started before the hate interval");
        check(Match([Effect(HateCauseKind.EffectDamage)]).Evidence==HateEvidence.EffectDamage,"periodic and buff damage is matched at effect time");
        check(Match([Effect(HateCauseKind.Healing) with {TargetUuid=ally}]).Evidence==HateEvidence.Healing,"actual healing can explain an enemy hate gain without hitting that enemy");
        check(Match([Effect(HateCauseKind.Healing) with {TargetUuid=otherEnemy}]).Evidence==HateEvidence.Healing,"healing a friendly non-character training dummy remains valid evidence");
        check(Match([Effect() with {TargetUuid=otherEnemy}]).SourceKey==0,"damage to another enemy is never used as the selected enemy hit evidence");
        check(Match([Effect() with {TargetUuid=otherEnemy}],(t.AddMilliseconds(90),123456)).Reason==HateUnassignedReason.OtherTarget,
            "another enemy hit cannot be bypassed with a skill start fallback");
        check(Match([Effect(),Effect() with {SourceKey=otherKey}]).Reason==HateUnassignedReason.MultipleSources,
            "different hit branches remain distinct rather than collapsing by owner id");
        check(Match([Effect(),Effect(HateCauseKind.Healing) with {TargetUuid=ally,SourceKey=otherKey}]).SourceKey==0,
            "overlapping damage and healing from different sources remains unknown");
        check(Match([Effect(),Effect() with {IsBuffSource=true}]).SourceKey==0,"buff and skill source identity is not mixed");
        var rowField=typeof(CombatDataCatalog).GetField("_recountRows",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
        var previousRows=rowField.GetValue(null);
        try
        {
            rowField.SetValue(null,new Dictionary<long,long> {{key,key}}.ToFrozenDictionary());
            check(Match([Effect(),Effect(HateCauseKind.EffectDamage) with {IsBuffSource=true}]).SourceKey==key,
                "explicit DPS row aliases count as one source while preserving branch identity");
        }
        finally {rowField.SetValue(null,previousRows);}
        check(Match([Effect(),Effect()]).SourceKey==key,"multiple hits from the same source remain one candidate");
        check(Match([Effect()],(t.AddMilliseconds(80),123456)).SourceKey==key,"same-origin start does not conflict with its hit");
        check(Match([Effect()],(t.AddMilliseconds(80),987654)).SourceKey==0,"unrelated concurrent skill start keeps potential taunt effects ambiguous");
        check(Match([Effect() with {PlayerUuid=ally}]).SourceKey==0,"another player's damage is not attributed to this player's hate");
        check(Match([Effect() with {Timestamp=t}]).SourceKey==0 && Match([Effect() with {Timestamp=gain.Timestamp}]).SourceKey==key,
            "effect interval is open at previous update and closed at current update");
        check(Match([Effect() with {Timestamp=gain.Timestamp.AddTicks(1)}]).SourceKey==0,"future effects are not borrowed for an earlier hate gain");
        check(new HateAttributionIndex([Effect()],[]).Match(withCloneEnd(t.AddSeconds(3))).SourceKey==0,"long notification gaps stay ambiguous despite hit evidence");
        check(Match([Effect(),Effect() with {SourceKey=0}]).Reason==HateUnassignedReason.UnknownSource,"unknown observed source blocks a misleading single known candidate");
        check(Match([], (t.AddMilliseconds(80),123456)).Evidence==HateEvidence.Start,"old history preserves explicitly marked start-only estimation");

        var encounter=new Encounter();encounter.ExData.FirstDamageTimeStamp=t;
        HateCauseRecorder.Record(encounter,Effect());HateCauseRecorder.Record(encounter,Effect());
        HateCauseRecorder.Record(encounter,Effect() with {Value=0});
        HateCauseRecorder.Record(encounter,Effect() with {PlayerUuid=enemy});
        HateCauseRecorder.Record(encounter,Effect(HateCauseKind.Healing) with {TargetUuid=0});
        check(encounter.ExData.HateCauses.Count==1 && encounter.ExData.HateCauses[0].Value==180,"effect recorder coalesces identical packet hits and rejects invalid causes");
        var dummyHealing=new Encounter(); HateCauseRecorder.Record(dummyHealing,Effect(HateCauseKind.Healing) with {TargetUuid=otherEnemy});
        check(dummyHealing.ExData.HateCauses.Count==1,"healing training dummy notifications are recorded without requiring a player target");
        using(var stream=new MemoryStream())
        {
            Serializer.Serialize(stream,encounter.ExData);stream.Position=0;
            var restored=Serializer.Deserialize<EncounterExData>(stream);
            check(restored.HateCauses.Single()==encounter.ExData.HateCauses[0],"effect evidence survives protobuf with full source branch and target");
        }
        encounter.ExData.HateObservations.Add(gain);
        encounter.SetEndTime(t.AddSeconds(1).ToLocalTime());
        var snapshot=HateMeterReader.Build(encounter);
        check(snapshot.EffectMatches==1 && snapshot.UnknownShare==0 && snapshot.Players.Single().Skills.Single().Evidence==HateEvidence.Hit,
            "meter uses hit evidence and reports reduced unknown share");
        check(snapshot.Players.Single().Skills.Sum(x=>x.Amount)==150 && snapshot.Players.Single().Bar.Amount==150,
            "effect attribution preserves total hate without allocating threat by damage amount");
        encounter.ExData.HateCauses.Add(Effect() with {SourceKey=otherKey});
        var ambiguous=HateMeterReader.Build(encounter);
        check(ambiguous.UnknownShare==100 && ambiguous.Players.Single().Skills.Single().SkillId==0,"ambiguous effect amount remains in the unknown bucket");
        DB.InsertEncounter(encounter);var loaded=DB.LoadEncounter(encounter.EncounterId)!;
        check(loaded.ExData.HateCauses.SequenceEqual(encounter.ExData.HateCauses),"effect evidence persists in the actual history database");

        var hooks=new Encounter();hooks.ExData.FirstDamageTimeStamp=t;
        void Damage(EDamageType type,bool miss=false)=>hooks.AddDamage(player,enemy,key,123456,1,100,100,0,default,type,default,
            false,false,false,miss,false,new ExtraPacketData(t.AddMilliseconds(100)),false,true,123456);
        Damage(default);Damage(EDamageType.Immune);Damage(default,true);
        hooks.AddHealing(player,ally,otherKey,1,50,100,0,0,default,EDamageType.Heal,default,false,false,false,false,false,new ExtraPacketData(t.AddMilliseconds(120)),false,123456);
        hooks.AddHealing(player,ally,otherKey,1,0,100,0,0,default,EDamageType.Heal,default,false,false,false,false,false,new ExtraPacketData(t.AddMilliseconds(140)));
        check(hooks.ExData.HateCauses.Count==2 && hooks.ExData.HateCauses[0].Kind==HateCauseKind.EffectDamage && hooks.ExData.HateCauses[1].Value==50,
            "aggregation hooks record accepted hits and actual healing but exclude misses immunity and pure overheal");

        HateObservation withCloneEnd(DateTime end)=>new() {PlayerUuid=player,EnemyUuid=enemy,FromUtc=t,Timestamp=end,Change=150};
    }
}
