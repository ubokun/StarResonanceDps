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
        var events = new[] { new ExtensionTimelineEvent(1,"スキル",1,"A","技"), new ExtensionTimelineEvent(11,"スキル",1,"A","技") };
        check(CombatTimelineReader.IsFrequentAction(events, 600), "idle quest time does not dilute frequent action detection");
        check(!CombatTimelineReader.IsFrequentAction(events.Select(x => x with { Seconds = x.Seconds * 2 }), 600), "slow actions remain enabled by default");
        var encounter = new Encounter { EncounterId = 99010 };
        var uuid = Utils.EntityIdToUuid(99010, (long)EEntityType.EntChar, false, false);
        var entity = new StarResonanceDps.Core.CombatRuntime.Entity(uuid, "未保存プレイヤー"); entity.SetEntityType(EEntityType.EntChar); encounter.Entities[uuid] = entity;
        var capture = typeof(PlayerBuildCapture).GetMethod("CaptureObserved", BindingFlags.NonPublic | BindingFlags.Static)!;
        var build = (StarResonanceDps.App.Models.SavedPlayerBuild)capture.Invoke(null, [99010L, encounter, true])!;
        check(build.CharacterId == 99010 && build.Encounters.Count == 0, "observed noncombat build can be saved without false encounter association");
    }
}
