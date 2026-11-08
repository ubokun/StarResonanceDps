using System.Reflection;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.Core.CombatRuntime;
using SkillLevelInfo = StarResonanceDps.Core.CombatRuntime.DataTypes.Skills.SkillLevelInfo;
using StarResonanceDps.Core.Models;
using Zproto;
using Entity = StarResonanceDps.Core.CombatRuntime.Entity;

internal static class RecordedBuildChecks
{
    public static void Run(Action<bool,string> check)
    {
        const long id=95123;
        var uuid=Utils.EntityIdToUuid(id,(long)EEntityType.EntChar,false,false);
        var entity=new Entity(uuid,"記録プレイヤー") { TotalDamage=1 };
        entity.SetEntityType(EEntityType.EntChar); entity.SetProfessionId(1);
        var roots=new[]{123,456}; var nodes=new[]{10,20}; var replacements=new Dictionary<int,int>{{30,31}};
        var skill=new SkillLevelInfo { SkillId=12345, CurrentLevel=5, Tier=2, Icon="recorded-icon" };
        entity.SetAttrKV("AttrSkillLevelIdList",new List<SkillLevelInfo>{skill});
        var displayed=new PlayerRosterEntry(id,"記録プレイヤー",1,SubProfessionId:2,
            SeasonTalentBuffId:100,RootBuffData:PlayerRootBuffData.FromSorted(roots),TalentNodeIds:nodes,
            LastReplaceableSkills:replacements,EquipmentData:PlayerEquipmentData.Create([new(1,222)]));
        var capture=typeof(RecordedPlayerBuildSnapshot).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static)!;
        var build=(RecordedPlayerBuildSnapshot)capture.Invoke(null,[entity,displayed])!;
        entity.RecordedBuild=build;
        roots[0]=999; nodes[0]=999; replacements[30]=99; skill.CurrentLevel=99;
        check(build.RootBuffIds![0]==123 && build.TalentNodeIds![0]==10 && build.LastReplaceableSkills![30]==31 && build.SkillLevels![0].CurrentLevel==5,
            "recorded build copies mutable buffers instead of following later live updates");
        build.ModuleEffects=[new(1234,6)]; build.Factors=[new(10,1234)];
        var encounter=new Encounter { SceneName="保存ビルド検証" }; encounter.Entities[uuid]=entity;
        encounter.ExData.GameServer=GameServer.Asia;
        var unknownUuid=Utils.EntityIdToUuid(id+1,(long)EEntityType.EntChar,false,false);
        var unknown=new Entity(unknownUuid,"未取得プレイヤー") { TotalHealing=1 }; unknown.SetEntityType(EEntityType.EntChar); encounter.Entities[unknownUuid]=unknown;
        var savedId=DB.InsertEncounter(encounter);
        try
        {
            var loaded=DB.LoadEncounter(savedId)!;
            var restored=loaded.Entities[uuid].RecordedBuild!;
            check(restored.RootBuffIds!.SequenceEqual(new[]{123,456}) && restored.TalentNodeIds!.SequenceEqual(new[]{10,20}) && restored.SeasonTalentBuffId==100,
                "encounter database round trip preserves ability and season build snapshot");
            check(restored.EquipmentItems!.Single().EquipmentId==222 && restored.SkillLevels!.Single().CurrentLevel==5
                && restored.ModuleEffects!.Single().LinkCount==6 && restored.Factors!.Single().ItemId==1234,
                "encounter database round trip preserves equipment skills module links and factors");
            check(loaded.Entities[unknownUuid].RecordedBuild is { RootBuffIds:null, TalentNodeIds:null, EquipmentState:null },
                "unreceived build fields remain unknown after saving and reloading");
            var player=MeterSnapshotProvider.GetBuildPlayer(id,loaded)!.Value.Player;
            check(player.RootBuffData!.BuffIds[0]==123 && player.RecordedBuild==restored && player.EquipmentData!.Items[0].EquipmentId==222,
                "historical build reader uses saved snapshot rather than missing entity attributes");
            var oldHistory=AppState.OpenedHistoricalEncounter;
            try
            {
                var live=displayed with { RootBuffData=PlayerRootBuffData.FromSorted([888]) };
                AppState.OpenedHistoricalEncounter=loaded;
                var probe=new BuildProbe(id,live);
                check(probe.Last is { IsFromRecord:true } && probe.Last.RootBuffData!.BuffIds[0]==123 && probe.HeaderText.Contains($"集計#{savedId}"),
                    "existing build widget shows selected historical build even when same player is live");
                var otherHistory=new Encounter { EncounterId=savedId+1 };
                AppState.OpenedHistoricalEncounter=otherHistory;
                probe.UpdatePlayerFromRoster(new Dictionary<long,PlayerRosterEntry>{{id,live}},null);
                check(probe.Last is null,"missing player in selected history never falls back to live build");
                AppState.OpenedHistoricalEncounter=null;
                probe.UpdatePlayerFromRoster(new Dictionary<long,PlayerRosterEntry>{{id,live}},null);
                check(probe.Last==live && !probe.HeaderText.Contains("集計#"),"build widget returns to live data when historical selection ends");
            }
            finally { AppState.OpenedHistoricalEncounter=oldHistory; }
            var next=new Entity(uuid,"次の戦闘"); next.CopyIdentityFrom(entity);
            check(next.RecordedBuild is null,"new encounter does not inherit frozen build snapshot from previous encounter");
            loaded.Entities[uuid].RecordedBuild=null;
            check(MeterSnapshotProvider.GetBuildPlayer(id,loaded) is not null,"legacy encounters without build snapshot remain readable");
        }
        finally { DB.DeleteEncounter(savedId); }
    }

    private sealed class BuildProbe : PlayerWidgetWindowViewModel
    {
        public PlayerRosterEntry? Last { get; private set; }
        public BuildProbe(long id,PlayerRosterEntry live):base(new WidgetListItemViewModel { Kind=WidgetKind.PlayerTalent },id) => InitializePlayer(live);
        protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player) => Last=player;
        protected override WidgetDataStatus CreateDataStatus(GameReceptionSnapshot reception,PlayerRosterEntry? player) => default;
    }
}
