using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ProtoBuf;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.Views;
using StarResonanceDps.Core.CombatRuntime;
using Zproto;

internal static class ExtensionChecks
{
 public static void Run(Action<bool,string> check)
 {
  var origin = DateTime.UtcNow.AddMinutes(-3);
  var data = new EncounterExData { LastCombatEventTimeStamp = origin, IdleResetSeconds = 10 };
  check(!CustomCombatFeatures.ShouldReset(data, origin.AddSeconds(9)) && CustomCombatFeatures.ShouldReset(data, origin.AddSeconds(10)), "idle reset boundary");
  data.IdleResetSeconds = 0; data.IntervalResetSeconds = 20; data.FirstDamageTimeStamp = origin;
  check(!CustomCombatFeatures.ShouldReset(data, origin.AddSeconds(19)) && CustomCombatFeatures.ShouldReset(data, origin.AddSeconds(20)), "periodic reset boundary");
  data.FirstDamageTimeStamp = null;
  check(!CustomCombatFeatures.ShouldReset(data, origin.AddHours(1)), "blank encounter is not periodically reset");
  check(CustomCombatFeatures.ResolveQuestStart(origin, 1) == origin, "invalid server timer falls back to receipt time");
  var epoch = (int)new DateTimeOffset(origin).ToUnixTimeSeconds();
  check(Math.Abs((CustomCombatFeatures.ResolveQuestStart(origin.AddMinutes(1), epoch) - origin).TotalSeconds) < 1, "server quest origin accepted when plausible");
  var encounter = new Encounter();
  encounter.ExData.UseQuestClock = true; encounter.ExData.QuestStartUtc = origin;
  encounter.ExData.FirstDamageTimeStamp = origin.AddSeconds(5);
  encounter.ExData.CombatExitSeconds = 1; encounter.ExData.LastCombatEventTimeStamp = origin.AddSeconds(5);
  var clockMethod = typeof(Encounter).GetMethod("ReadCombatClock", BindingFlags.NonPublic | BindingFlags.Instance, [typeof(DateTime)])!;
  var clock = (CombatClockReading)clockMethod.Invoke(encounter, [origin.AddSeconds(20)])!;
  check(clock.Elapsed == TimeSpan.FromSeconds(20) && encounter.ToCombatOffset(origin.AddSeconds(8)) == TimeSpan.FromSeconds(8), "meter and events share quest clock without idle pauses");
  encounter.SetEndTime(origin.AddSeconds(30).ToLocalTime());
  clock = (CombatClockReading)clockMethod.Invoke(encounter, [origin.AddMinutes(10)])!;
  check(clock.Elapsed == TimeSpan.FromSeconds(30) && clock.IsClosed, "closed quest clock freezes in history");
  encounter.ExData.FactorActivations.Add(new() { Timestamp = origin, EntityUuid = 1, BuffId = 2, SourceId = 3 });
  encounter.ExData.PartyCharacterIds.Add(123); encounter.ExData.IntervalResetSeconds = 90;
  using (var bytes = new MemoryStream())
  {
   Serializer.Serialize(bytes, encounter.ExData); bytes.Position = 0;
   var restored = Serializer.Deserialize<EncounterExData>(bytes);
   check(restored.UseQuestClock && restored.QuestStartUtc == origin && restored.FactorActivations.Single().BuffId == 2 && restored.PartyCharacterIds.Single() == 123 && restored.IntervalResetSeconds == 90, "quest factor and party metadata persist through protobuf");
  }
  try { CombatExtensionSettings.Validate(new() { VisibleSeconds = 0 }); throw new Exception("invalid width accepted"); }
  catch (ArgumentOutOfRangeException) { Console.WriteLine("PASS reject invalid extension settings"); }
  check(WidgetConfigDefaults.HateRankDisplayCounts[WidgetConfigDefaults.DefaultHateRankDisplayModeIndex] == 5, "entity list defaults to five hate gauges");
  // Exercise real transition logic against the isolated test executable's database.
  DB.Init();
  CustomCombatFeatures.SyncQuestClock = true;
  BattleStateMachine.DungeonStateHistory.Clear();
  BattleStateMachine.DungeonStateHistoryAdd(EDungeonState.DungeonStatePlaying, origin);
  var first = EncounterManager.Current;
  BattleStateMachine.DungeonStateHistoryAdd(EDungeonState.DungeonStatePlaying, origin.AddSeconds(1));
  check(ReferenceEquals(first, EncounterManager.Current) && first.ExData.QuestStartUtc == origin, "duplicate quest start does not reset or shift origin");
  EncounterManager.EnterDungeon(true, force: true, reason: EncounterStartReason.NewObjective);
  check(ReferenceEquals(first, EncounterManager.Current), "quest synchronization keeps automatic phase totals together");
  // Save a known user, then invoke the closing handler for a second shared encounter.
  var playerUuid = Utils.EntityIdToUuid(81234, (long)EEntityType.EntChar, false, false);
  var observed = new StarResonanceDps.Core.CombatRuntime.Entity(playerUuid, "自動紐づけ検証") { IsNamedInCombatEventLog = true };
  observed.SetEntityType(EEntityType.EntChar);
  first.Entities[playerUuid] = observed; first.EncounterId = 81101;
  first.ExData.PartyCharacterIds.Add(81234);
  var captureMethod = typeof(PlayerBuildCapture).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)!;
  var captured = (StarResonanceDps.App.Models.SavedPlayerBuild)captureMethod.Invoke(null, [81234L, first])!;
  var autoStore = new SavedPlayerBuildStore(); var original = autoStore.Save(captured);
  var second = new Encounter { EncounterId = 81102 };
  second.Entities[playerUuid] = observed; second.ExData.PartyCharacterIds.Add(81234);
  var autoHandler = typeof(SavedPlayerBuildAutoLink).GetMethod("OnEncounterClosing", BindingFlags.NonPublic | BindingFlags.Static)!;
  autoHandler.Invoke(null, [second]);
  check(autoStore.Load(out _).Single(x => x.Id == original.Id).Encounters.Any(x => x.EncounterId == 81102), "known user auto-links a shared party encounter");
  SavedPlayerBuildAutoLink.Initialize();
  second.ExData.QuestStartUtc = origin; second.ExData.UseQuestClock = true;
  second.ExData.FactorActivations.Add(new() { Timestamp = origin, EntityUuid = playerUuid, BuffId = 321 });
  second.AddSkillActivation(playerUuid, 12345, origin.AddSeconds(3));
  var actualId = DB.InsertEncounter(second);
  check(autoStore.Load(out _).Single(x => x.Id == original.Id).Encounters.Any(x => x.EncounterId == actualId), "database commit triggers auto-link with actual saved encounter id");
  check(autoStore.Load(out _).Single(x => x.Id == original.Id).Encounters.Any(x => x.EncounterId == actualId && x.Timeline.Contains("#12345")), "auto-linked snapshot retains skill timeline after database save");
  var loaded = DB.LoadEncounter(actualId)!;
  check(loaded.ExData.UseQuestClock && loaded.ExData.FactorActivations.Single().BuffId == 321, "database restores extension metadata");
  DB.DeleteEncounter(actualId);
  check(DB.GetNextEncounterId() > actualId, "deleted latest history never reuses predicted encounter id");
  check(autoStore.Load(out _).Any(x => x.Id == original.Id), "history deletion preserves saved build");
  CustomCombatFeatures.ClearQuest();
  check(CustomCombatFeatures.QuestStartUtc is null, "map or logout clears quest origin");
  CustomCombatFeatures.SyncQuestClock = false;
  var enemyUuid = Utils.EntityIdToUuid(901, (long)EEntityType.EntMonster, false, false);
  var controlEncounter = new Encounter();
  EnemyControlRecorder.RecordState(controlEncounter, enemyUuid, origin, 100, 0, true, false, true, true);
  check(controlEncounter.ExData.EnemyControlEvents.Any(x => x.Kind == EnemyControlKind.Silence), "packet silence state is recorded");
  check(controlEncounter.ExData.EnemyControlEvents.Any(x => x.Kind == EnemyControlKind.CastEnded) && controlEncounter.ExData.EnemyControlEvents.Count == 2, "cast clearing is recorded without asserting interruption");
  EnemyControlRecorder.RecordBuff(controlEncounter, enemyUuid, 55426, origin);
  check(controlEncounter.ExData.EnemyControlEvents.Count == 2 && controlEncounter.ExData.EnemyControlEvents.Single(x => x.Kind == EnemyControlKind.Silence).BuffId == 55426, "silence buff and attribute notifications are deduplicated");
  EnemyControlRecorder.RecordState(controlEncounter, enemyUuid, origin.AddSeconds(1), 0, 0, false, true, false, true);
  check(controlEncounter.ExData.EnemyControlEvents.Any(x => x.Kind == EnemyControlKind.SilenceRemoved), "silence removal is recorded");
  using (var controlBytes = new MemoryStream())
  {
    Serializer.Serialize(controlBytes, controlEncounter.ExData); controlBytes.Position = 0;
    check(Serializer.Deserialize<EncounterExData>(controlBytes).EnemyControlEvents.Count == 3, "enemy control events persist in history");
  }
  var window = new CombatTimelineWindow();
  var skillIcon = Directory.EnumerateFiles(Path.Combine(CombatRuntimePaths.DataDirectory,"Images","Skills"),"*.png").First();
  var factorIcon = Directory.EnumerateFiles(Path.Combine(CombatRuntimePaths.DataDirectory,"Images","Factors"),"*.png").First();
  var snapshot = new CombatTimelineSnapshot(123, 180, "PT DPS · 検証クエスト · クエスト時間", [new(60,61,100),new(70,71,300),new(100,101,150)],
   [new(70,"スキル",1,"検証プレイヤー","スキルA",skillIcon),new(80,"イマジン",1,"検証プレイヤー","イマジンB",skillIcon),new(90,"因子効果",1,"検証プレイヤー","因子C",factorIcon),new(100,"敵行動",2,"検証ボス","敵の大技")],
   [new(1,"検証プレイヤー"),new(2,"検証ボス",true),new(3,"待機中の敵",true)]);
  var set = typeof(CombatTimelineWindow).GetMethod("SetSnapshot", BindingFlags.NonPublic | BindingFlags.Instance)!;
  set.Invoke(window, [snapshot]);
  var plot = (Canvas)window.FindName("Plot");
  check(plot.Children.OfType<Border>().Count(x => x.Child is ContentControl { Content: ExtensionTimelineEvent { IconPath: not null } }) == 3, "skills imagines and factors use icon images");
  check(plot.Children.OfType<TextBlock>().Any(x => x.Text.Contains("待機中の敵")), "each enemy gets a track even without action records");
  var list = (DataGrid)window.FindName("EventList");
  check(list.Items.Count == 4, "skills imagines factors and enemy events share timeline");
  var actors = (ListBox)window.FindName("Actors"); actors.SelectedItems.Add(actors.Items[0]);
  check(list.Items.Count == 3, "tracked entity selection filters timeline");
  actors.SelectedItems.Clear();
  var content = (FrameworkElement)window.Content;
  content.Measure(new Size(1140,780)); content.Arrange(new Rect(0,0,1140,780)); content.UpdateLayout();
  var bitmap = new RenderTargetBitmap(1140,780,96,96,PixelFormats.Pbgra32); bitmap.Render(content);
  var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
  var imagePath = Path.Combine(AppContext.BaseDirectory,"combat-timeline-preview.png");
  using (var output = File.Create(imagePath)) encoder.Save(output);
  Console.WriteLine("PREVIEW " + imagePath);
  ((ContentControl)window.FindName("MeasurementSettings")).Visibility = Visibility.Visible;
  content.Measure(new Size(1140,780)); content.Arrange(new Rect(0,0,1140,780)); content.UpdateLayout();
  var settingsBitmap = new RenderTargetBitmap(1140,780,96,96,PixelFormats.Pbgra32); settingsBitmap.Render(content);
  var settingsEncoder = new PngBitmapEncoder(); settingsEncoder.Frames.Add(BitmapFrame.Create(settingsBitmap));
  var settingsImagePath = Path.Combine(AppContext.BaseDirectory,"combat-settings-preview.png");
  using (var settingsOutput = File.Create(settingsImagePath)) settingsEncoder.Save(settingsOutput);
  Console.WriteLine("PREVIEW " + settingsImagePath);
  window.Close();
 }
}
