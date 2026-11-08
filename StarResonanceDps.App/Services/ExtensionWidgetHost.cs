using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Views;
namespace StarResonanceDps.App.Services;

/// <summary>Hosts extension bodies within the shared widget chrome and persisted window lifecycle.</summary>
public sealed class ExtensionWidgetHost : ContentControl, StarResonanceDps.App.ViewModels.IWidgetDataStatusSource
{
    public WidgetDataStatus DataStatus { get; private set; }
    public event EventHandler? DataStatusChanged;
    public void RefreshDataStatus()
    {
        var status=WidgetDataStatusRules.ForTimeline(StarResonanceDps.Core.CombatRuntime.GameReceptionStateStore.Current,
            StarResonanceDps.Core.CombatRuntime.AppState.OpenedHistoricalEncounter is not null,
            StarResonanceDps.Core.CombatRuntime.EncounterHistoryProvider.HasLoadFailed);
        if(status==DataStatus)return;
        DataStatus=status; DataStatusChanged?.Invoke(this,EventArgs.Empty);
    }
    public static Action<WidgetKind,long>? OpenRequested { get; set; }
    internal static readonly Dictionary<WidgetKind,long> Targets = new();
    private static readonly Dictionary<WidgetKind,ExtensionWidgetHost> Active = new();
    private readonly Window _controller;
    private readonly WidgetKind _kind;
    private bool _started, _stopped;
    public static void Open(WidgetKind kind,long target=0) => OpenRequested?.Invoke(kind,target);
    public ExtensionWidgetHost(WidgetKind kind)
    {
        _kind=kind;
        HorizontalContentAlignment=HorizontalAlignment.Stretch;
        VerticalContentAlignment=VerticalAlignment.Stretch;
        _controller=kind switch {
            WidgetKind.PartyDps => new PartyDpsWindow(),
            WidgetKind.UserBuildLibrary => new UserBuildWindow(),
            WidgetKind.CombatExtensionTimeline => new CombatTimelineWindow(),
            WidgetKind.HatePs => new HateMeterWindow(Targets.GetValueOrDefault(kind)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)) };
        var frame=(ContentControl)_controller.Content;
        var grid=(Grid)frame.Content;
        // AppHeader is itself a UserControl/ContentControl. Extract the body panel, never its header content.
        var panel=grid.Children.OfType<ContentControl>().First(x=>x is not UserControl);
        var body=(FrameworkElement)panel.Content;
        panel.Content=null;
        Resources.MergedDictionaries.Add(_controller.Resources);
        System.Windows.Data.BindingOperations.SetBinding(this, Behaviors.ThemePaletteScope.PaletteProperty,
            new System.Windows.Data.Binding(nameof(ViewModels.WidgetListItemViewModel.ThemePalette)));
        SetResourceReference(ForegroundProperty,"Brush.TextPrimary");
        Content=body;
        Loaded += (_,_) =>
        {
            if(_started)return; _started=true;
            if(Window.GetWindow(this) is { } owner) owner.Closed += (_,_) => Stop();
            Active[kind]=this;
            switch(_controller)
            {
                case UserBuildWindow builds: builds.StartWidget(); break;
                case CombatTimelineWindow timeline: timeline.StartWidget(); break;
                case HateMeterWindow hate: hate.StartWidget(); break;
                case PartyDpsWindow graph: graph.StartWidget(); break;
            }
            ApplyTarget(kind);
        };
        Unloaded += (_,_) => { if(Active.GetValueOrDefault(kind)==this) Active.Remove(kind); Stop(); };
    }
    private void Stop()
    {
        if(_stopped)return; _stopped=true;
        if(Active.GetValueOrDefault(_kind)==this)Active.Remove(_kind);
        switch(_controller)
        {
            case UserBuildWindow builds: builds.StopWidget(); break;
            case CombatTimelineWindow timeline: timeline.StopWidget(); break;
            case HateMeterWindow hate: hate.StopWidget(); break;
            case PartyDpsWindow graph: graph.StopWidget(); break;
        }
        _controller.Close();
    }
    internal static void ApplyTarget(WidgetKind kind)
    {
        if(!Active.TryGetValue(kind,out var host))return;
        var target=Targets.GetValueOrDefault(kind);
        if(host._controller is UserBuildWindow builds && target>0) _=builds.PreviewPlayerAsync(target);
        if(host._controller is HateMeterWindow hate) hate.SelectEnemy(target);
    }
}
