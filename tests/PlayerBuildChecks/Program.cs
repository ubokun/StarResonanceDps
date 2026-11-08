using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StarResonanceDps.App.Models;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.Views;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.CombatRuntime;
using static StarResonanceDps.Core.CombatRuntime.DataTypes.Enums.Professions;

internal static class Program
{
 [STAThread] private static void Main(string[] args)
 {
  if (args is ["--ui-demo"])
  {
   DB.Init();
   var demoApp = new Application(); DemoResources.Load(demoApp);
   var dashboard = new StarResonanceDps.App.Views.CombatTimelineWindow { Title = "検証用：拡張タイムライン" };
   demoApp.MainWindow = dashboard;
   dashboard.Loaded += (_, _) =>
   {
    ((System.Windows.Threading.DispatcherTimer)typeof(StarResonanceDps.App.Views.CombatTimelineWindow).GetField("_timer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dashboard)!).Stop();
    var demo = new CombatTimelineSnapshot(123, 180, "検証データ · PT DPS · クエスト時間", [new(60,61,100),new(70,71,300),new(100,101,150)],
      [new(70,"スキル",1,"検証プレイヤー","スキルA"),new(80,"イマジン",1,"検証プレイヤー","イマジンB"),new(90,"因子効果",1,"検証プレイヤー","因子C"),new(100,"敵行動",2,"検証ボス","敵の大技")],
      [new(1,"検証プレイヤー"),new(2,"検証ボス")]);
    typeof(StarResonanceDps.App.Views.CombatTimelineWindow).GetMethod("SetSnapshot", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(dashboard, [demo]);
    var library = new UserBuildWindow { Title = "検証用：ユーザービルド", Owner = dashboard };
    library.Show();
   };
   demoApp.Run(dashboard); return;
  }
  if (args is ["--inspect-targets-db", var targetsFile]) { HistoryReadback.Targets(targetsFile); return; }
  if (args is ["--inspect-history-db", var inspectedFile]) { HistoryReadback.Run(inspectedFile); return; }
  if (args is ["--analyze-hate-db", var analyzedFile]) { HistoryReadback.Run(analyzedFile, true); return; }
  if (args is ["--check-history-db", var historyFile])
  {
   SQLitePCL.Batteries_V2.Init();
   using var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
    { DataSource = Path.GetFullPath(historyFile), Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
   connection.Open();
   using var command = connection.CreateCommand(); command.CommandText = "SELECT ExDataBlob FROM Encounters WHERE ExDataBlob IS NOT NULL ORDER BY EncounterId DESC LIMIT 10";
   using var reader = command.ExecuteReader(); using var decompressor = new ZstdSharp.Decompressor(); var count = 0;
   while (reader.Read())
   {
    using var bytes = new MemoryStream(decompressor.Unwrap((byte[])reader[0]).ToArray());
    _ = ProtoBuf.Serializer.Deserialize<EncounterExData>(bytes); count++;
   }
   Console.WriteLine($"Historical metadata compatible ({count} records checked)."); return;
  }
  if (args is ["--backup-db", var sourceFile, var destinationFile])
  {
   sourceFile = Path.GetFullPath(sourceFile); destinationFile = Path.GetFullPath(destinationFile);
   if (!File.Exists(sourceFile) || File.Exists(destinationFile) || sourceFile.Equals(destinationFile, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Backup requires an existing source and a new destination.");
   Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
   SQLitePCL.Batteries_V2.Init();
   using var source = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
    { DataSource = sourceFile, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
   using var destination = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
    { DataSource = destinationFile, Pooling = false }.ToString());
   source.Open(); destination.Open(); source.BackupDatabase(destination);
   using var command = destination.CreateCommand(); command.CommandText = "PRAGMA quick_check";
   if ((string?)command.ExecuteScalar() != "ok") throw new InvalidDataException("Backup validation failed.");
   Console.WriteLine("Database backup verified."); return;
  }
  var testDir = Path.Combine(Path.GetTempPath(), "PlayerBuildChecks-" + Guid.NewGuid().ToString("N"));
  var store = new SavedPlayerBuildStore(testDir);
  void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
  SavedPlayerBuild Make(ulong encounter, string fingerprint = "build-A", long user = 123, string server = "Asia") => new()
  { CharacterId = user, PlayerName = "検証用ユーザー", Server = server, Fingerprint = fingerprint,
    CapturedUtc = DateTime.UtcNow, Details = "【アビリティ】\n取得：検証スキル\n不明：未観測ノード\n【心相】確認済み\n【イマジン】使用を確認",
    SourceData = "{\"test\":true}", Encounters = [new() { EncounterId = encounter, StartTime = DateTime.Now,
     SceneName = "検証用クエスト", Timeline = "00:00:01.200 検証スキル", CapturedUtc = DateTime.UtcNow }] };
  var first = store.Save(Make(100));
  Check(store.Load(out var failed).Single().Details == first.Details && failed == 0, "snapshot round trip");
  store.Save(Make(101)); store.Save(Make(101));
  Check(store.Load(out failed).Single().Encounters.Count == 2, "same build links encounters without duplicate");
  store.Save(Make(102, "build-B"));
  Check(store.Load(out failed).Count == 2, "changed build preserves prior snapshot");
  store.Save(Make(103, user: 456)); store.Save(Make(104, server: "China"));
  Check(store.Load(out failed).Count == 4, "separate character and server identity");
  File.WriteAllText(Path.Combine(testDir, "broken.json"), "{");
  Check(store.Load(out failed).Count == 4 && failed == 1 && File.Exists(Path.Combine(testDir, "broken.json")), "isolate and preserve corrupt record");
  Check(!Directory.GetFiles(testDir, "*.tmp").Any(), "atomic save cleanup");
  var invalid = Make(0);
  try { store.Save(invalid); throw new Exception("invalid encounter accepted"); } catch (InvalidDataException) { Console.WriteLine("PASS reject invalid encounter"); }
  var abilityMethod = typeof(PlayerTalentTreeWidgetViewModel).GetMethod("ResolveAbilityKind", BindingFlags.Static | BindingFlags.NonPublic)!;
  var node = new TalentTreeNode(1, 0, 0, [], false, 1, true, "", ERoleType.None, []);
  var player = new PlayerRosterEntry(123, "test", 1);
  TalentTreeNodeKind Resolve(TalentTreeNode n, PlayerRosterEntry p) => (TalentTreeNodeKind)abilityMethod.Invoke(null, [n, PlayerRootBuffData.FromSorted([]), p])!;
  Check(Resolve(node, player) == TalentTreeNodeKind.Unknown, "other player unobservable ability stays unknown");
  Check(Resolve(node, player with { TalentNodeIds = [1] }) == TalentTreeNodeKind.Taken, "known acquired ability");
  Check(Resolve(node with { Buffs = [999] }, player) == TalentTreeNodeKind.Dot, "known absent buff ability");
  Check(Resolve(node with { SkillReplacements = [new(10,20)] }, player with { LastReplaceableSkills = new Dictionary<int,int> { [10] = 20 } }) == TalentTreeNodeKind.Taken, "observed replaced skill");
  var resolverType = typeof(MeterSnapshotProvider).Assembly.GetType("StarResonanceDps.Core.CombatRuntime.PlayerDataSourceResolver")!;
  var resolver = resolverType.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(x => x.Name == "Resolve" && x.GetParameters().Length == 5);
  StarResonanceDps.Core.Services.MeterPlayerSpecCache.Instance.SetSubProfession(777, 999);
  StarResonanceDps.Core.Services.MeterPlayerSpecCache.Instance.SetSeasonTalent(777, 888);
  var historicalEntity = new Entity(777, "記録の名前");
  var recorded = resolver.Invoke(null, [777L, null, historicalEntity, false, true])!;
  Check((int)recorded.GetType().GetProperty("SubProfessionId")!.GetValue(recorded)! == 0, "history never inherits live specialization cache");
  Check((int)recorded.GetType().GetProperty("SeasonTalentBuffId")!.GetValue(recorded)! == 0, "history never inherits live mindscape cache");
  // Isolated executable data directory, not the running user's application directory.
  var uiStore = new SavedPlayerBuildStore(); uiStore.Save(Make(90001));
  var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; DemoResources.Load(app);
  ExtensionChecks.Run(Check);
  NewFeatureChecks.Run(Check);
  HateAttributionChecks.Run(Check);
  WidgetIntegrationChecks.Run(Check);
  RecordedBuildChecks.Run(Check);
  EntityDisplayChecks.Run(Check);
  var window = new UserBuildWindow();
  var userList = (ListBox)window.FindName("Users"); userList.SelectedItem = userList.Items.Cast<object>().First(x => x.GetType().GetProperty("Label")!.GetValue(x)!.ToString()!.Contains("検証用ユーザー"));
  Check(((ListBox)window.FindName("Builds")).Items.Count > 0, "user to builds navigation");
  Check(((TextBox)window.FindName("Details")).Text.Contains("不明"), "build details preserve unknown state");
  Check(window.FindName("Encounters") is null, "user build widget removes aggregate association list");
  Check(((TextBox)window.FindName("Timeline")).Text.Contains("検証スキル"), "encounter-specific timeline display");
  var content = (FrameworkElement)window.Content;
  content.Measure(new Size(980, 650)); content.Arrange(new Rect(0,0,980,650)); content.UpdateLayout();
  var bitmap = new RenderTargetBitmap(980,650,96,96,PixelFormats.Pbgra32); bitmap.Render(content);
  var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
  var imagePath = Path.Combine(AppContext.BaseDirectory, "player-build-preview.png");
  using (var output = File.Create(imagePath)) png.Save(output);
  Console.WriteLine("PREVIEW " + imagePath);
  window.Close(); app.Shutdown();
 }
}
