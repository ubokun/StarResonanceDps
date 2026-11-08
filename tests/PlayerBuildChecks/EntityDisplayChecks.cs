using System.Reflection;
using Google.Protobuf;
using ExtraPacketData=StarResonanceDps.Core.CombatRuntime.Protocols.ExtraPacketData;
using System.Text.Json;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using Zproto;
using Entity=StarResonanceDps.Core.CombatRuntime.Entity;

internal static class EntityDisplayChecks
{
    public static void Run(Action<bool,string> check)
    {
        var settings=new MeterWidgetSettingsConfig();
        check(settings.EntityHpAsPercentage && JsonSerializer.Deserialize<MeterWidgetSettingsConfig>("{}")!.EntityHpAsPercentage,"entity HP percentage is default for both new and existing settings");
        settings.EntityHpAsPercentage=false;
        check(!settings.Clone().EntityHpAsPercentage && !JsonSerializer.Deserialize<MeterWidgetSettingsConfig>(JsonSerializer.Serialize(settings))!.EntityHpAsPercentage,"raw HP preference persists through clone and serialization");
        settings.EntityHpAsPercentage=true;
        long U(long id,EEntityType kind)=>Utils.EntityIdToUuid(id,(long)kind,false,false);
        var enemy=U(881,EEntityType.EntMonster);var otherEnemy=U(882,EEntityType.EntMonster);var player=U(991,EEntityType.EntChar);var second=U(992,EEntityType.EntChar);
        var nearby=new NearbyEntityEntry(enemy,881,"検証敵",EEntityType.EntMonster,default,60,50,200,0,0,false,false,EntityCampRelation.Hostile,20,true,0,true,0,NearbyEntityHateRanking.Empty,null,[991,992]);
        var names=new EntityHatePlayerResolver();names.SetRoster([new(991,"プレイヤーA",1),new(992,"プレイヤーB",1)]);
        names.Prepare(EntityRowSettings.FromEntityList(settings),PlayerNameDisplayMode.Show);
        var row=EntityListEntry.Create(nearby,EntityRowSettings.FromEntityList(settings),names,false,6);
        check(row.HealthText=="25.0%","HP percent excludes shield and represents current HP over maximum");
        check(row.TargetingPlayerNames.Contains("プレイヤーA") && row.TargetingPlayerNames.Contains("プレイヤーB"),"entity row lists all observed targeting players independent of hate ranking");
        row.Update(nearby with {CurrentHp=0},EntityRowSettings.FromEntityList(settings),names,false);
        check(row.HealthText=="0.0%","defeated enemy shows zero percent");
        row.Update(nearby with {MaxHp=0},EntityRowSettings.FromEntityList(settings),names,false);
        check(row.HealthText=="--%","unknown maximum HP remains unknown instead of dividing by zero");
        settings.EntityHpAsPercentage=false;settings.HealthValueDisplayModeIndex=WidgetConfigDefaults.SeparateShieldHealthValueDisplayModeIndex;
        row.Update(nearby,EntityRowSettings.FromEntityList(settings),names,false);
        check(row.HealthText=="50(20)/200","raw HP mode retains existing separate shield formatting");
        names.Prepare(EntityRowSettings.FromEntityList(settings),PlayerNameDisplayMode.Hide);
        row.Update(nearby,EntityRowSettings.FromEntityList(settings),names,false);
        check(!row.TargetingPlayerNames.Contains("プレイヤーA"),"targeting player labels respect name masking settings");
        row.Update(nearby with {TargetingCharacterIds=[]},EntityRowSettings.FromEntityList(settings),names,false);
        check(row.TargetingPlayerNames.Length==0,"recycled entity row clears old targeting player names");
        settings.EntityHpAsPercentage=true;
        names.SetRoster(Enumerable.Range(991,5).Select(id=>new PlayerRosterEntry(id,$"プレイヤー{id-990}",1)).ToArray());
        names.Prepare(EntityRowSettings.FromEntityList(settings),PlayerNameDisplayMode.Show);
        var rendered=nearby with { TargetingCharacterIds=new long[]{991,992,993,994,995},
            HateRanking=NearbyEntityHateRanking.Create(Enumerable.Range(991,5).Select((id,index)=>new NearbyEntityHateRank(id,5-index))) };
        row.Update(rendered,EntityRowSettings.FromEntityList(settings),names,false);
        check(row.TargetingPlayerNames.Contains("+2人") && row.AllTargetingPlayerNames.Contains("プレイヤー5"),"crowded targeting names fit row while preserving full tooltip list");
        var view=new StarResonanceDps.App.Views.Widgets.EntityListWidgetView { DataContext=new {EntityListEntries=new[]{row}} };
        view.Measure(new System.Windows.Size(457,100)); view.Arrange(new System.Windows.Rect(0,0,457,100)); view.UpdateLayout();
        IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject parent)
        {
            for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++)
            {var child=System.Windows.Media.VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}
        }
        var label=Descendants(view).OfType<System.Windows.Controls.TextBlock>().Single(x=>x.Text==row.TargetingPlayerNames);
        var firstHate=Descendants(view).OfType<System.Windows.Controls.ContentPresenter>().First(x=>x.Content is EntityHateSlot);
        var button=Descendants(view).OfType<System.Windows.Controls.Button>().First(x=>x.DataContext==row);
        check(button.ActualHeight==44 && label.TranslatePoint(new(),view).X+label.ActualWidth<=firstHate.TranslatePoint(new(),view).X,"targeting names appear left of hate list within original row height");
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(457,100,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(view);
        var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using(var file=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,"entity-percent-targets-preview.png")))png.Save(file);
        var oldEncounter=EncounterManager.Current;
        var projection=typeof(Encounter).Assembly.GetType("StarResonanceDps.Core.CombatRuntime.NearbyEntityProjection")!;
        var appear=projection.GetMethod("AddOrUpdateAppearedEntity",BindingFlags.Public|BindingFlags.Static)!;
        try
        {
            EntityTargetingPlayers.Clear();NearbyEntityStore.Instance.BeginMap();
            var encounter=new Encounter();EncounterManager.Current=encounter;
            foreach(var uuid in new[]{enemy,otherEnemy,player,second})
            {var actor=new Entity(uuid,"検証");actor.SetEntityType((EEntityType)Utils.UuidToEntityType(uuid));actor.SetHpValuesNoUpdate(50,200);encounter.Entities[uuid]=actor;}
            appear.Invoke(null,[enemy]);appear.Invoke(null,[otherEnemy]);
            encounter.SetAttrKV(player,"AttrTargetUuid",enemy);
            check(EntityTargetingPlayers.Get(enemy).SequenceEqual(new long[]{991}) && NearbyEntityStore.Instance.Current.Entries.Single(x=>x.EntityUuid==enemy).TargetingCharacterIds!.Contains(991),"received player target updates publish into existing entity list projection");
            encounter.SetAttrKV(second,"AttrTargetUuid",enemy);encounter.SetAttrKV(player,"AttrTargetUuid",enemy);
            check(EntityTargetingPlayers.Get(enemy).Count==2,"duplicate target notifications do not duplicate targeting players");
            encounter.SetAttrKV(player,"AttrTargetUuid",otherEnemy);
            check(EntityTargetingPlayers.Get(enemy).SequenceEqual(new long[]{992}) && EntityTargetingPlayers.Get(otherEnemy).SequenceEqual(new long[]{991}),"target switch removes player from old entity and adds to new entity");
            encounter.SetAttrKV(player,"AttrTargetUuid",0L);
            check(EntityTargetingPlayers.Get(otherEnemy).Count==0,"target release clears reverse targeting relation");
            encounter.SetAttrKV(player,"AttrTargetId",enemy);
            check(EntityTargetingPlayers.Get(enemy).Contains(991),"player selection TargetId attribute updates entity targeting names");
            encounter.SetAttrKV(player,"AttrTargetUuid",otherEnemy);
            check(EntityTargetingPlayers.Get(enemy).Contains(991) && !EntityTargetingPlayers.Get(otherEnemy).Contains(991),"explicit player selection takes precedence over alternate target UUID");
            encounter.SetAttrKV(player,"AttrTargetId",0L);
            check(!EntityTargetingPlayers.Get(enemy).Contains(991),"cleared player selection does not revive stale alternate target");
            encounter.Entities[player].Attributes.Remove("AttrTargetId");
            var oldSelf=MessageManager.currentUserUuid;
            try
            {
                MessageManager.currentUserUuid=player;
                MessageManager.ProcessClientTargetChange(new Zproto.WorldCsharp.Types.ClientTargetChange {TargetUuid=enemy}.ToByteArray(),new ExtraPacketData(DateTime.UtcNow));
                MessageManager.ProcessClientTargetChange(new Zproto.WorldCsharp.Types.ClientTargetChange {TargetUuid=otherEnemy}.ToByteArray(),new ExtraPacketData(DateTime.UtcNow));
                check(!EntityTargetingPlayers.Get(enemy).Contains(991) && EntityTargetingPlayers.Get(otherEnemy).Contains(991),"serialized client target change moves player from old enemy to new enemy");
                MessageManager.ProcessClientTargetChange(new Zproto.WorldCsharp.Types.ClientTargetChange().ToByteArray(),new ExtraPacketData(DateTime.UtcNow));
                encounter.SetAttrKV(player,"AttrTargetId",otherEnemy);
                check(!EntityTargetingPlayers.Get(otherEnemy).Contains(991),"explicit client deselection is not overwritten by stale target attribute");
            }
            finally {MessageManager.currentUserUuid=oldSelf;encounter.Entities[player].Attributes.Remove("ClientSelectedTargetUuid");encounter.Entities[player].Attributes.Remove("AttrTargetId");}
            EntityTargetingPlayers.Update(enemy,otherEnemy);
            check(EntityTargetingPlayers.Get(otherEnemy).Count==0,"enemy targeting a player is not confused with players targeting the enemy");
            EntityTargetingPlayers.RemoveEntity(second);
            check(EntityTargetingPlayers.Get(enemy).Count==0,"player disappearance clears targeting relation");
            encounter.SetAttrKV(player,"AttrTargetUuid",enemy);
            encounter.Entities[enemy].AddSkillCast(new() { SkillId=12345, Timestamp=DateTime.UtcNow });
            encounter.Entities[otherEnemy].AddSkillCast(new() { SkillId=12345, Timestamp=DateTime.UtcNow });
            var historyId=DB.InsertEncounter(encounter);
            try
            {
                EntityTargetingPlayers.Update(player,otherEnemy);
                var loadedTargets=DB.LoadEncounter(historyId)!;
                var historical=EntityHistoryProjection.Build(loadedTargets,true);
                check(historical.Alive.Single(x=>x.EntityUuid==enemy).TargetingCharacterIds!.Contains(991)
                    && historical.Alive.Single(x=>x.EntityUuid==otherEnemy).TargetingCharacterIds!.Count==0,"historical targeting uses saved packet attributes rather than live targets");
            }
            finally {DB.DeleteEncounter(historyId);}
            EntityTargetingPlayers.Clear();
            check(EntityTargetingPlayers.Get(enemy).Count==0 && EntityTargetingPlayers.Get(otherEnemy).Count==0,"map reset discards previous targeting relations");
        }
        finally {EncounterManager.Current=oldEncounter;EntityTargetingPlayers.Clear();NearbyEntityStore.Instance.BeginMap();}
    }
}
