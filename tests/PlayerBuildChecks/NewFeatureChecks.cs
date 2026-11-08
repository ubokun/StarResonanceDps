using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ProtoBuf;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.Views;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.Core.CombatRuntime;
using Zproto;
using Entity = StarResonanceDps.Core.CombatRuntime.Entity;

internal static class NewFeatureChecks
{
    public static void Run(Action<bool,string> check)
    {
        var utc=DateTime.UtcNow.AddMinutes(-1);
        long Uuid(long id,EEntityType type)=>Utils.EntityIdToUuid(id,(long)type,false,false);
        var enemy=Uuid(44001,EEntityType.EntMonster); var player=Uuid(77001,EEntityType.EntChar); var other=Uuid(77002,EEntityType.EntChar);
        var encounter=new Encounter();
        HateInfo[] Values(uint a,uint b)=>[new(){Uuid=player,HateVal=a},new(){Uuid=other,HateVal=b}];
        HateObservationRecorder.Record(encounter,enemy,Values(1000,2000),utc);
        check(encounter.ExData.HateObservations.All(x=>x.IsBaseline && x.Change==0),"initial hate values establish baseline without phantom gains");
        HateObservationRecorder.Record(encounter,enemy,Values(1100,2200),utc.AddSeconds(1));
        HateObservationRecorder.Record(encounter,enemy,Values(1100,2200),utc.AddSeconds(1));
        check(encounter.ExData.HateObservations.Where(x=>x.Change>0).Sum(x=>x.Change)==300,"hate deltas counted once");
        HateObservationRecorder.Record(encounter,enemy,Values(5,6),utc.AddSeconds(2));
        HateObservationRecorder.Record(encounter,enemy,Values(15,16),utc.AddSeconds(3));
        check(encounter.ExData.HateObservations.Where(x=>x.Change>0).Sum(x=>x.Change)==320,"hate decrease rebases instead of adding negative generation");
        HateObservationRecorder.Record(encounter,enemy,[],utc.AddSeconds(4));
        HateObservationRecorder.Record(encounter,enemy,Values(9000,9000),utc.AddSeconds(5));
        check(encounter.ExData.HateObservations.Where(x=>x.Change>0).Sum(x=>x.Change)==320,"missing hate list and reappearance require fresh baseline");
        HateObservationRecorder.Record(encounter,enemy,Values(99999,99999),utc.AddSeconds(1));
        check(encounter.ExData.HateObservations.Where(x=>x.Change>0).Sum(x=>x.Change)==320,"out of order hate updates do not manufacture gains");
        var baselineEncounter=new Encounter(); HateObservationRecorder.Record(baselineEncounter,enemy,Values(1000,2000),utc);
        var baselineMeter=HateMeterReader.Build(baselineEncounter);
        check(baselineMeter.Players.Count==2 && baselineMeter.Players.All(x=>x.Bar.Amount==0) && baselineMeter.Observations==2,"baseline reception displays zero PS players while waiting for increases");
        baselineEncounter.ExData.UseQuestClock=true; baselineEncounter.ExData.QuestStartUtc=utc.AddSeconds(1);
        check(HateMeterReader.Build(baselineEncounter).Players.Count==2 && HateMeterReader.Build(baselineEncounter).Players.All(x=>x.Bar.Amount==0),"precombat baselines remain visible without becoming combat hate gains");
        var newEncounter=new Encounter();HateObservationRecorder.Record(newEncounter,enemy,Values(9999,9999),utc);
        check(newEncounter.ExData.HateObservations.All(x=>x.IsBaseline),"new encounter does not inherit previous hate baseline");
        using(var stream=new MemoryStream()){Serializer.Serialize(stream,encounter.ExData);stream.Position=0;check(Serializer.Deserialize<EncounterExData>(stream).HateObservations.Count==encounter.ExData.HateObservations.Count,"hate observations persist through protobuf");}
        var gain=encounter.ExData.HateObservations.First(x=>x.PlayerUuid==player && x.Change>0);
        check(HateMeterReader.EstimateSkill(gain,[(utc.AddMilliseconds(500),100)])==100,"single skill in bounded observation interval is estimated");
        check(HateMeterReader.EstimateSkill(gain,[(utc.AddMilliseconds(400),100),(utc.AddMilliseconds(700),200)])==0,"overlapping skills remain unassigned");
        check(HateMeterReader.EstimateSkill(new(){Change=10,FromUtc=utc,Timestamp=utc.AddSeconds(3)},[(utc.AddSeconds(1),100)])==0,"sparse hate update is not attributed to one skill");
        encounter.Entities[player]=new Entity(player,"検証プレイヤーA");encounter.Entities[other]=new Entity(other,"検証プレイヤーB");
        encounter.AddSkillActivation(player,100,utc.AddMilliseconds(500));
        encounter.ExData.FirstDamageTimeStamp=utc;encounter.SetEndTime(utc.AddSeconds(10).ToLocalTime());
        var snapshot=HateMeterReader.Build(encounter);
        check(snapshot.Players.Sum(x=>x.Bar.Amount)==320 && Math.Abs(snapshot.Players.Sum(x=>x.Bar.Share)-100)<1e-8,"meter shares preserve all observed hate including unknown attribution");
        check(snapshot.Players.Sum(x=>x.Bar.PerSecond)==32,"hate PS uses encounter duration");
        check(snapshot.Players.All(x=>x.Skills.Sum(s=>s.Amount)==x.Bar.Amount),"skill estimates plus unknown reconcile to player total");
        check(HateMeterReader.Build(encounter,123).Players.Count==0,"hate meter enemy filter isolates selected target");
        var path=Path.Combine(Path.GetTempPath(),"EnemyCatalogChecks-"+Guid.NewGuid().ToString("N"),"EnemyCatalog.json");
        var store=new EnemyCatalogStore(path);var monster=new Entity(enemy,"raw name");monster.SetEntityType(EEntityType.EntMonster);monster.SetAttrKV("AttrId",12345);monster.SetAttrKV("AttrSkillId",23456);encounter.Entities[enemy]=monster;
        store.Observe(encounter,enemy,utc);store.Observe(encounter,enemy,utc.AddSeconds(1));
        check(store.Read().Count==1 && !store.Read()[0].Enabled && store.Read()[0].SkillIds.SequenceEqual(new[]{23456}),"enemy catalog records stable type once and defaults disabled");
        var key=store.Read()[0].Key;store.SetEnabled(key,true);store.Save();var restored=new EnemyCatalogStore(path);
        check(restored.Read().Single().Enabled && restored.IsEnabled(encounter,enemy),"enemy tracking selection persists after restart");
        var enemy2=Uuid(44002,EEntityType.EntMonster);var monster2=new Entity(enemy2);monster2.SetEntityType(EEntityType.EntMonster);monster2.SetAttrKV("AttrId",12345);encounter.Entities[enemy2]=monster2;
        check(restored.IsEnabled(encounter,enemy2),"same enemy type shares tracking selection across instances");
        var unknown=Uuid(44003,EEntityType.EntMonster);encounter.Entities[unknown]=new Entity(unknown);restored.Observe(encounter,unknown,utc);
        check(restored.Read().Count==1 && !restored.IsEnabled(encounter,unknown),"unknown enemy type is neither merged nor implicitly tracked");
        var corrupt=Path.Combine(Path.GetDirectoryName(path)!,"broken.json");File.WriteAllText(corrupt,"{");var broken=new EnemyCatalogStore(corrupt);broken.Save();check(broken.LastIssue is not null && File.ReadAllText(corrupt)=="{","corrupt enemy catalog is preserved");
        EnemyControlRecorder.RecordState(encounter,enemy,utc.AddSeconds(6),0,23456,true,false,false,false);
        check(encounter.ExData.EnemyControlEvents.Any(x=>x.Kind==EnemyControlKind.SkillStarted),"enemy skill attribute start is recorded without inferring damage");
        var meter=new HateMeterWindow(); typeof(HateMeterWindow).GetMethod("SetSnapshot",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(meter,[snapshot]);
        check(((ListBox)meter.FindName("Players")).Items.Count==2 && ((ListBox)meter.FindName("Skills")).Items.Count>0,"hate meter selects a player and shows skill proportions");
        var enemyCombo=(ComboBox)meter.FindName("Enemy");
        typeof(HateMeterWindow).GetField("_targetUuid",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(meter,enemy);
        typeof(HateMeterWindow).GetMethod("SetSnapshot",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(meter,[snapshot]);
        check((enemyCombo.SelectedItem as TimelineActor)?.Uuid==enemy,"changing enemy target refreshes combo selection even when enemy choices are unchanged");
        ((ListBox)meter.FindName("Players")).SelectedIndex=1;
        Render(meter,"hate-meter-preview.png");meter.Close();
        EnemyCatalogStore.Current.Observe(encounter,enemy,utc);var catalog=new EnemyCatalogWindow();Render(catalog,"enemy-catalog-preview.png");catalog.Close();
        monster.SetHpValuesNoUpdate(0,1000);DefeatedEnemyRecorder.Observe(encounter,monster,utc.AddSeconds(8));DefeatedEnemyRecorder.Observe(encounter,monster,utc.AddSeconds(9));
        encounter.Entities.Remove(enemy,out _);
        var deadSnapshot=EntityHistoryProjection.Build(encounter,true);
        check(deadSnapshot.Defeated.Count==1 && deadSnapshot.Defeated[0].CurrentHp==0,"defeated enemy remains after despawn without duplicate rows");
        check(!deadSnapshot.Alive.Any(x=>x.EntityUuid==enemy),"defeated enemies are excluded from alive historical section");
        var listVm=new WidgetListItemViewModel {Kind=StarResonanceDps.App.Models.Widgets.WidgetKind.EntityList};
        listVm.ApplyMeterSettingsPreview(StarResonanceDps.App.Config.WidgetConfigDefaults.CreateMeterSettings(StarResonanceDps.App.Models.Widgets.WidgetKind.EntityList));
        listVm.UpdateEntityHistory(deadSnapshot);
        var previousOpen=ExtensionWidgetHost.OpenRequested;
        long clickedEnemy=0; StarResonanceDps.App.Models.Widgets.WidgetKind? clickedKind=null;
        ExtensionWidgetHost.OpenRequested=(kind,target)=>{clickedKind=kind;clickedEnemy=target;};
        var clickView=new StarResonanceDps.App.Views.Widgets.EntityListWidgetView {DataContext=listVm};
        var clickedRow=listVm.EntityListEntries.Last();
        typeof(StarResonanceDps.App.Views.Widgets.EntityListWidgetView).GetMethod("EntityListItem_Click",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!
            .Invoke(clickView,[new Button {DataContext=clickedRow},new RoutedEventArgs()]);
        check(clickedKind==StarResonanceDps.App.Models.Widgets.WidgetKind.HatePs && clickedEnemy==enemy,"mob row left click opens the selected enemy hate PS widget");
        ExtensionWidgetHost.OpenRequested=previousOpen;

        check(listVm.EntityListEntries.Last().EntityUuid==enemy && listVm.EntityListEntries.Any(x=>x.IsFirstDefeated),"entity list places defeated history below alive enemies");
        var deadView=new StarResonanceDps.App.Views.Widgets.EntityListWidgetView {DataContext=listVm};
        var deadHost=new Border {Background=new SolidColorBrush(Color.FromRgb(32,32,32)),Padding=new Thickness(12),Child=deadView};
        deadHost.Measure(new Size(470,250));deadHost.Arrange(new Rect(0,0,470,250));deadHost.UpdateLayout();
        var deadBitmap=new RenderTargetBitmap(470,250,96,96,PixelFormats.Pbgra32);deadBitmap.Render(deadHost);var deadEncoder=new PngBitmapEncoder();deadEncoder.Frames.Add(BitmapFrame.Create(deadBitmap));
        using(var deadOutput=File.Create(Path.Combine(AppContext.BaseDirectory,"defeated-enemies-preview.png")))deadEncoder.Save(deadOutput);
        var revivedEncounter=new Encounter();var revived=new Entity(enemy);revived.SetEntityType(EEntityType.EntMonster);revived.SetHpValuesNoUpdate(0,1000);revivedEncounter.Entities[enemy]=revived;
        DefeatedEnemyRecorder.Observe(revivedEncounter,revived,utc);revived.SetHpValuesNoUpdate(1000,1000);DefeatedEnemyRecorder.Observe(revivedEncounter,revived,utc.AddSeconds(1));
        check(revivedEncounter.ExData.DefeatedEnemies.Count==0,"revived enemy is removed from defeated section");
        listVm.UpdateEntityHistory(EntityHistorySnapshot.Empty);
        check(!listVm.EntityListEntries.Any(x=>x.IsFirstDefeated),"switching encounter clears previous defeated section");
        var targetMeter=new HateMeterWindow(enemy);typeof(HateMeterWindow).GetMethod("SetSnapshot",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(targetMeter,[HateMeterReader.Build(encounter,enemy)]);
        check(((TimelineActor)((ComboBox)targetMeter.FindName("Enemy")).SelectedItem).Uuid==enemy,"entity context opens hate meter for selected defeated enemy");targetMeter.Close();
        var missingMeter=new HateMeterWindow(999999);typeof(HateMeterWindow).GetMethod("SetSnapshot",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(missingMeter,[HateMeterReader.Build(encounter,999999)]);
        check(((TimelineActor)((ComboBox)missingMeter.FindName("Enemy")).SelectedItem).Uuid==999999 && ((ListBox)missingMeter.FindName("Players")).Items.Count==0,"missing target never silently switches hate meter to all enemies");missingMeter.Close();
        var savedId=DB.InsertEncounter(encounter);
        var restoredEncounter=DB.LoadEncounter(savedId)!;
        check(restoredEncounter.ExData.DefeatedEnemies.Count==1 && restoredEncounter.ExData.HateObservations.Count>0,"defeated history and hate deltas survive database round trip");
        void Wait(Task task){if(!task.IsCompleted){var frame=new DispatcherFrame();var dispatcher=Dispatcher.CurrentDispatcher;task.ContinueWith(_=>dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)));Dispatcher.PushFrame(frame);}task.GetAwaiter().GetResult();}
        var vm=new AggregationViewModel();vm.Reload();var item=vm.Entries.Single(x=>x.EncounterId==savedId);
        // No SelectionChanged subscription: the command must still clear its loading state.
        Wait(vm.SelectEntryCommand.ExecuteAsync(item));
        check(item.IsSelected && !item.IsLoading,"history spinner clears even when UI selection event is not subscribed");
        EncounterHistoryProvider.SelectLive();
        Action fail=()=>throw new InvalidOperationException("intentional apply failure");EncounterHistoryProvider.SelectionChanged+=fail;
        var failed=EncounterHistoryProvider.SelectAsync(savedId);Wait(failed);EncounterHistoryProvider.SelectionChanged-=fail;
        check(failed.Result==HistorySelectResult.Failed,"history apply failure settles selection result instead of stranding loading");
        check(EncounterHistoryProvider.SelectedEncounterId is null,"failed history application restores previous selection");
        var opened=EncounterHistoryProvider.SelectAsync(savedId);Wait(opened);check(opened.Result==HistorySelectResult.Opened && EncounterHistoryProvider.SelectedEncounterId==savedId,"history can be retried successfully after apply failure");
        var autoClearField=typeof(CombatRuntimeSettings).GetField("<ClearHistorySelectionOnNextEvent>k__BackingField",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
        var previousAutoClear=autoClearField.GetValue(null);
        try
        {
            autoClearField.SetValue(null,true);
            Wait(EncounterHistoryProvider.SelectAsync(savedId,true));
            EncounterHistoryProvider.NotifyLiveEncounterEvent(); EncounterHistoryProvider.NotifyLiveEncounterEvent();
            check(EncounterHistoryProvider.SelectedEncounterId==savedId,"timeline-retained history survives incoming combat and encounter events");
            var missing=EncounterHistoryProvider.SelectAsync(ulong.MaxValue,true); Wait(missing);
            check(missing.Result==HistorySelectResult.NotFound && EncounterHistoryProvider.SelectedEncounterId==savedId,"failed history request preserves displayed retained history");
            Action pinFailure=()=>throw new InvalidOperationException("retained selection rollback");
            EncounterHistoryProvider.SelectionChanged+=pinFailure;
            var rollback=EncounterHistoryProvider.SelectAsync(savedId,false); Wait(rollback);
            EncounterHistoryProvider.SelectionChanged-=pinFailure;
            EncounterHistoryProvider.NotifyLiveEncounterEvent();
            check(rollback.Result==HistorySelectResult.Failed && EncounterHistoryProvider.SelectedEncounterId==savedId,"failed application restores retained mode as well as previous history");
            EncounterHistoryProvider.SelectLive();
            check(EncounterHistoryProvider.SelectedEncounterId is null,"explicit live action releases retained timeline history");
            Wait(EncounterHistoryProvider.SelectAsync(savedId)); EncounterHistoryProvider.NotifyLiveEncounterEvent();
            check(EncounterHistoryProvider.SelectedEncounterId is null,"ordinary history still follows existing auto-return setting after retained mode ends");
            using (EncounterHistoryProvider.RetainWhileTimelineOpen())
            {
                Wait(EncounterHistoryProvider.SelectAsync(savedId)); EncounterHistoryProvider.NotifyLiveEncounterEvent();
                check(EncounterHistoryProvider.SelectedEncounterId==savedId,"timeline opened before aggregation selection also prevents automatic live return");
            }
            EncounterHistoryProvider.SelectLive();
            Wait(EncounterHistoryProvider.SelectAsync(savedId)); EncounterHistoryProvider.RetainCurrentSelection(); EncounterHistoryProvider.NotifyLiveEncounterEvent();
            check(EncounterHistoryProvider.SelectedEncounterId==savedId,"opening timeline can retain history originally selected in aggregation");
        }
        finally { autoClearField.SetValue(null,previousAutoClear); EncounterHistoryProvider.SelectLive(); }
        EncounterHistoryProvider.SelectLive();DB.DeleteEncounter(savedId);
    }
    private static void Render(Window window,string name)
    {
        var content=(FrameworkElement)window.Content;content.Measure(new Size(1060,760));content.Arrange(new Rect(0,0,1060,760));content.UpdateLayout();
        var bitmap=new RenderTargetBitmap(1060,760,96,96,PixelFormats.Pbgra32);bitmap.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output=File.Create(Path.Combine(AppContext.BaseDirectory,name));encoder.Save(output);Console.WriteLine("PREVIEW "+name);
    }
}
