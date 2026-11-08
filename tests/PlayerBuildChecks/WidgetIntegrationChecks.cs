using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.Views;
using StarResonanceDps.App.Views.Widgets;
using StarResonanceDps.Core.CombatRuntime;
using Zproto;

internal static class WidgetIntegrationChecks
{
    public static void Run(Action<bool,string> check)
    {
        foreach (var kind in new[] { WidgetKind.UserBuildLibrary, WidgetKind.CombatExtensionTimeline, WidgetKind.HatePs, WidgetKind.PartyDps })
        {
            var vm = new WidgetListItemViewModel { Kind = kind, DisplayNameResourceKey = kind.ToString() };
            vm.RefreshLocalizedText();
            var body = new ExtensionWidgetHost(kind);
            var owner = new Window { Left = SystemParameters.WorkArea.Left, Top = SystemParameters.WorkArea.Top, Width = 800, Height = 500 };
            var window = new WidgetWindow(vm, body, body, new WidgetWindowConfig { Width = 1100, Height = 720 }, owner);
            window.Show(); window.UpdateLayout();
            var controller = (Window)typeof(ExtensionWidgetHost).GetField("_controller", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(body)!;
            check(body.IsLoaded && body.Content is FrameworkElement element && element.ActualWidth > 300, $"{kind} uses shared widget window and fills body");
            var actualControl=kind switch
            {
                WidgetKind.UserBuildLibrary => controller.FindName("Users") as FrameworkElement,
                WidgetKind.CombatExtensionTimeline => controller.FindName("Actors") as FrameworkElement,
                WidgetKind.HatePs => controller.FindName("Players") as FrameworkElement,
                _ => controller.GetType().GetField("_plot",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(controller) as FrameworkElement
            };
            check(actualControl is not null && body.IsAncestorOf(actualControl) && actualControl.IsVisible && actualControl.ActualHeight>50,
                $"{kind} displays the actual interactive body instead of extracting AppHeader");
            if(kind==WidgetKind.HatePs)
            {
                var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
                bitmap.Render(window); var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var file=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,"hate-widget-integrated-preview.png")); encoder.Save(file);
            }
            check(window.Top >= SystemParameters.WorkArea.Top && window.Top + window.Height <= SystemParameters.WorkArea.Bottom + 1,
                $"{kind} first launch stays inside monitor working area");
            check(body.Resources["Brush.TextPrimary"] is System.Windows.Media.SolidColorBrush,
                $"{kind} uses its own widget theme palette");
            window.Close(); owner.Close();
            var timer = controller.GetType().GetField("_timer", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(controller) as DispatcherTimer;
            check(timer is null || !timer.IsEnabled, $"{kind} stops updates when widget closes");
        }
        var playerWidget = new WidgetListItemViewModel { Kind = WidgetKind.PlayerList };
        var row = PlayerListEntry.Create(new(91234, "検証プレイヤー", 1), new(), StarResonanceDps.Core.Models.PlayerNameDisplayMode.Show, id => new PlayerSkillDisplayState(id));
        var playerRows = (System.Collections.ObjectModel.ObservableCollection<PlayerListEntry>)typeof(WidgetListItemViewModel).GetField("_playerListEntries", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(playerWidget)!;
        playerRows.Add(row);
        var playerView = new PlayerListWidgetView { DataContext = playerWidget };
        playerView.Measure(new Size(650, 110)); playerView.Arrange(new Rect(0, 0, 650, 110)); playerView.UpdateLayout();
        IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for(var i=0; i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            { var child=System.Windows.Media.VisualTreeHelper.GetChild(parent,i); yield return child; foreach(var nested in Descendants(child)) yield return nested; }
        }
        var infoButtons = Descendants(playerView).OfType<Button>().Where(b => b.Tag is string tag && tag is "PlayerTalent" or "PlayerSeasonTalent" or "PlayerModule").ToArray();
        check(infoButtons.Length == 3 && infoButtons.All(b => b.ActualHeight <= 44), "player list has three compact information buttons within original row height");
        var requests = new List<(WidgetKind Kind,long Id)>(); playerWidget.PlayerWindowRequested += (kind,id) => requests.Add((kind,id));
        foreach(var button in infoButtons) button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,button));
        check(requests.Select(x=>x.Kind).SequenceEqual(new[]{WidgetKind.PlayerTalent,WidgetKind.PlayerSeasonTalent,WidgetKind.PlayerModule}) && requests.All(x=>x.Id==91234), "row buttons open the three existing widgets for the exact player without opening player info");
        check(infoButtons.All(b => b.Background is System.Windows.Media.SolidColorBrush brush && brush.Color.A == 0), "player row action buttons add no opaque background");
        var playerImage = new System.Windows.Media.Imaging.RenderTargetBitmap(650, 110, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); playerImage.Render(playerView);
        var playerPng = new System.Windows.Media.Imaging.PngBitmapEncoder(); playerPng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(playerImage));
        using(var file=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,"player-list-info-buttons-preview.png"))) playerPng.Save(file);
        var events = new[] { new ExtensionTimelineEvent(1,"スキル",1,"A","技"), new ExtensionTimelineEvent(11,"スキル",1,"A","技") };
        check(CombatTimelineReader.IsFrequentAction(events, 600), "idle quest time does not dilute frequent action detection");
        check(!CombatTimelineReader.IsFrequentAction(events.Select(x => x with { Seconds = x.Seconds * 2 }), 600), "slow actions remain enabled by default");
        var encounter = new Encounter { EncounterId = 99010 };
        var uuid = Utils.EntityIdToUuid(99010, (long)EEntityType.EntChar, false, false);
        var entity = new StarResonanceDps.Core.CombatRuntime.Entity(uuid, "未保存プレイヤー"); entity.SetEntityType(EEntityType.EntChar); encounter.Entities[uuid] = entity;
        check(EncounterParticipants.Read(encounter).Count == 0, "history user filter excludes nearby noncombat players");
        entity.TotalHealing = 1;
        check(EncounterParticipants.Read(encounter).Single().CharacterId == 99010, "history user filter includes healing-only participants");
        entity.TotalHealing = 0;
        encounter.ExData.HateObservations.Add(new() { PlayerUuid = uuid, Change = 10 });
        check(EncounterParticipants.Read(encounter).Count == 1, "history user filter includes hate-only participants");
        encounter.ExData.HateObservations.Clear();
        var matchesUser = typeof(AggregationViewModel).GetMethod("MatchesUser", BindingFlags.NonPublic | BindingFlags.Static)!;
        var people = new[] { new EncounterParticipant("Asia", 11, "same name"), new EncounterParticipant("China", 22, "other") };
        check((bool)matchesUser.Invoke(null, [people, "NAME", null, null])!, "history user names match without case sensitivity");
        check((bool)matchesUser.Invoke(null, [people, "22", null, null])!, "history users can be searched by character ID");
        check(!(bool)matchesUser.Invoke(null, [people, "same name", "China", 11L])!, "row history filtering requires both exact server and character ID");
        var filterVm = new AggregationViewModel();
        filterVm.QuestFilter = "missing quest";
        check(filterVm.Entries.Count == 0, "quest filter safely handles no matching history");
        filterVm.ClearFiltersCommand.Execute(null);
        check(filterVm.UserFilter.Length == 0 && filterVm.QuestFilter.Length == 0, "clear filters resets both search fields");
        var entryA = new EncounterHistoryItem(new(901, DateTime.Now, 0, "Quest Alpha", false, 0, 0, 0));
        var entryB = new EncounterHistoryItem(new(902, DateTime.Now, 0, "Quest Beta", false, 0, 0, 0));
        typeof(AggregationViewModel).GetField("_allEntries", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(filterVm, new List<EncounterHistoryItem> { entryA, entryB });
        var cache = (Dictionary<ulong,IReadOnlyList<EncounterParticipant>>)typeof(AggregationViewModel).GetField("_participants", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(filterVm)!;
        cache[901] = people; cache[902] = [new("Asia", 33, "third")];
        filterVm.UserFilter = "same name";
        check(filterVm.Entries.Count == 1 && filterVm.Entries[0].EncounterId == 901, "user filter restricts existing history rows");
        filterVm.QuestFilter = "Beta";
        check(filterVm.Entries.Count == 0, "user and quest filters combine with AND");
        filterVm.ClearFiltersCommand.Execute(null);
        check(filterVm.Entries.Count == 2, "clearing filters restores history rows");
        var aggregation = new AggregationView { DataContext = filterVm };
        aggregation.Measure(new Size(700, 530)); aggregation.Arrange(new Rect(0, 0, 700, 530)); aggregation.UpdateLayout();
        var aggregationImage = new System.Windows.Media.Imaging.RenderTargetBitmap(700, 530, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        aggregationImage.Render(aggregation);
        var aggregationPng = new System.Windows.Media.Imaging.PngBitmapEncoder(); aggregationPng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(aggregationImage));
        using (var file = System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory, "aggregation-filter-preview.png"))) aggregationPng.Save(file);
        var capture = typeof(PlayerBuildCapture).GetMethod("CaptureObserved", BindingFlags.NonPublic | BindingFlags.Static)!;
        var build = (StarResonanceDps.App.Models.SavedPlayerBuild)capture.Invoke(null, [99010L, encounter, true])!;
        check(build.CharacterId == 99010 && build.Encounters.Count == 0, "observed noncombat build can be saved without false encounter association");
        var standaloneStore=new SavedPlayerBuildStore(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"StandaloneBuild-"+Guid.NewGuid().ToString("N")));
        var standalone=standaloneStore.Save(build);
        check(standaloneStore.Load(out var failed).Single().Encounters.Count==0 && failed==0,"unlinked observed build actually saves and reloads without a fabricated encounter");
        build.Encounters.Add(new(){EncounterId=99011,StartTime=DateTime.Now,SceneName="later battle"});
        var linked=standaloneStore.Save(build);
        check(linked.Id==standalone.Id && standaloneStore.Load(out _).Single().Encounters.Count==1,"later encounter links to the same previously unlinked build");
        var serverField=typeof(GameServerStore).GetField("_current",BindingFlags.NonPublic|BindingFlags.Static)!;
        var oldServer=serverField.GetValue(null);var oldEncounter=EncounterManager.Current;var oldHistory=AppState.OpenedHistoricalEncounter;
        try
        {
            serverField.SetValue(null,StarResonanceDps.Core.Models.GameServer.Asia);
            EncounterManager.Current=encounter;AppState.OpenedHistoricalEncounter=null;
            var live=(StarResonanceDps.App.Models.SavedPlayerBuild)capture.Invoke(null,[99010L,null,true])!;
            check(live.Server=="Asia" && live.Details.Contains("Asia"),"live build identity uses observed server before encounter metadata is stamped");
            encounter.ExData.GameServer=StarResonanceDps.Core.Models.GameServer.China;
            var historical=(StarResonanceDps.App.Models.SavedPlayerBuild)capture.Invoke(null,[99010L,encounter,true])!;
            check(historical.Server=="China","historical build identity never inherits the live server");
        }
        finally {serverField.SetValue(null,oldServer);EncounterManager.Current=oldEncounter;AppState.OpenedHistoricalEncounter=oldHistory;}
    }
}
