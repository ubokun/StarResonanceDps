using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels.Notifications;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    private readonly ObservableCollection<WidgetListItemViewModel> _widgetItems = new();
    private readonly GlobalHotkeyService _hotkeyService = GlobalHotkeyService.Instance;
    private readonly ObservableCollection<PluginListItemViewModel> _pluginItems = new();
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly WidgetStateManager _widgetStateManager = WidgetStateManager.Instance;
    private readonly WidgetWindowManager _widgetWindowManager = WidgetWindowManager.Instance;
    private readonly PluginManager _pluginManager = PluginManager.Instance;
    private readonly PlayerRosterPresentationStore _playerRosterStore = PlayerRosterPresentationStore.Instance;
    private readonly PartyStateStore _partyStateStore = PartyStateStore.Instance;
    private readonly NearbyEntityStore _nearbyEntityStore = NearbyEntityStore.Instance;
    private readonly DungeonScoreStore _dungeonScoreStore = DungeonScoreStore.Instance;
    private readonly object _playerRosterUpdateSync = new();
    private readonly object _nearbyEntityUpdateSync = new();

    private PlayerRosterSnapshot? _pendingPlayerRosterSnapshot;
    private NearbyEntitySnapshot? _pendingNearbyEntitySnapshot;
    private bool _isPlayerRosterUpdateQueued;
    private bool _isNearbyEntityUpdateQueued;

    private readonly WidgetListItemViewModel _playerListWidget;
    private readonly WidgetListItemViewModel _entityListWidget;

    /// <summary>ウィジェットを開いていなくても通知する項目の判定。起動できないときに見せるだけのメイン窓では作らない。</summary>
    private readonly NotificationMonitor? _notificationMonitor;

    /// <summary>複数開けるウィジェットのカードで、窓の相手を選んで開く・閉じる。</summary>
    private readonly WidgetTargetSelection _targetSelection;

    /// <summary>
    /// 複数開けるウィジェットのカードを全部当て直す1秒の時計。選択リストの元のうち、バフの行とメーターの行には変わった合図が無い
    /// (バフは時間だけでも消え、それを出すウィジェットも時計で読み直している)ので、1秒ごとに読み直す。
    /// 合図のある元(名簿・周りの実体)は、その合図でもすぐ当て直す。
    /// </summary>
    private readonly DispatcherTimer _targetSelectionTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _widgetSortIndex;

    [ObservableProperty]
    private int _widgetActionIndex = -1;

    private bool _isBulkUpdatingWidgets;
    private bool _isLoadingWidgets;

    /// <summary>
    /// ウィジェットのカードの一覧の並び。並べ替えに使う値が変わったら、そのカードだけを動かす(live sorting。一覧を組み直すとカードが全部作り直される)。
    /// 検索に当たらないカードは一覧から外さず隠す(<see cref="WidgetListItemViewModel.IsSearchMatch"/>)。
    /// </summary>
    public ListCollectionView Widgets { get; }

    public ReadOnlyObservableCollection<PluginListItemViewModel> PluginItems { get; }

    /// <summary>通知タブ。</summary>
    public NotificationsViewModel Notifications { get; } = new();

    // 三点メニューの項目。登録できているホットキーを右に半角括弧で添える。
    // 1つのキーで切り替える2項目(起動/停止など)には同じキーが付く。

    public string StartFavoritesActionText => FormatWidgetActionText("Manager_WidgetAction_StartAllFavorites", HotkeyAction.StartFavoritesOrStopAll);

    public string StopAllActionText => FormatWidgetActionText("Manager_WidgetAction_StopAll", HotkeyAction.StartFavoritesOrStopAll);

    public string PinRunningActionText => FormatWidgetActionText("Manager_WidgetAction_PinAllRunning", HotkeyAction.PinRunningOrUnpinAll);

    public string UnpinAllActionText => FormatWidgetActionText("Manager_WidgetAction_UnpinAll", HotkeyAction.PinRunningOrUnpinAll);

    public string ClickThroughPinnedActionText => FormatWidgetActionText("Manager_WidgetAction_ClickThroughAllPinned", HotkeyAction.ClickThroughPinnedOrClearAll);

    public string ClearClickThroughActionText => FormatWidgetActionText("Manager_WidgetAction_ClearAllClickThrough", HotkeyAction.ClickThroughPinnedOrClearAll);

    public MainViewModel()
    {
        _targetSelection = new WidgetTargetSelection(_widgetWindowManager, FindWidget);

        _playerListWidget = AddWidget(WidgetKind.PlayerList, "Widget_PlayerList");
        _entityListWidget = AddWidget(WidgetKind.EntityList, "Widget_EntityList");
        AddWidget(WidgetKind.PlayerInfo, "Widget_PlayerInfo");
        AddWidget(WidgetKind.EntityInfo, "Widget_EntityInfo");
        AddWidget(WidgetKind.PlayerStatus, "Widget_PlayerStatus");
        AddWidget(WidgetKind.PlayerEquipment, "Widget_PlayerEquipment");
        AddWidget(WidgetKind.PlayerTalent, "Widget_PlayerTalent");
        AddWidget(WidgetKind.PlayerSeasonTalent, "Widget_PlayerSeasonTalent");
        AddWidget(WidgetKind.PlayerModule, "Widget_PlayerModule");
        AddWidget(WidgetKind.BuffList, "Widget_BuffList");
        AddWidget(WidgetKind.DebuffList, "Widget_DebuffList");
        AddWidget(WidgetKind.BuffDebuffCard, "Widget_BuffDebuffCard");
        AddWidget(WidgetKind.BuffDebuffGroup, "Widget_BuffDebuffGroup");
        AddWidget(WidgetKind.ReviveList, "Widget_ReviveList");
        AddWidget(WidgetKind.Timeline, "Widget_Timeline");
        AddWidget(WidgetKind.DpsMeter, "Widget_DpsMeter");
        AddWidget(WidgetKind.HpsMeter, "Widget_HpsMeter");
        AddWidget(WidgetKind.DamageTakenLog, "Widget_DamageTakenLog");
        AddWidget(WidgetKind.DamageSkillDetail, "Widget_DamageSkillDetail");
        AddWidget(WidgetKind.DamageSummary, "Widget_DamageSummary");
        AddWidget(WidgetKind.DpsGraph, "Widget_DpsGraph");
        AddWidget(WidgetKind.HealingSkillDetail, "Widget_HealingSkillDetail");
        AddWidget(WidgetKind.HealingSummary, "Widget_HealingSummary");
        AddWidget(WidgetKind.HpsGraph, "Widget_HpsGraph");
        AddWidget(WidgetKind.DamageTakenSummary, "Widget_DamageTakenSummary");
        AddWidget(WidgetKind.Chat, "Widget_Chat");
        AddWidget(WidgetKind.UserBuildLibrary, "ユーザービルド");
        AddWidget(WidgetKind.CombatExtensionTimeline, "拡張タイムライン");
        AddWidget(WidgetKind.HatePs, "ヘイトPSメーター");
        AddWidget(WidgetKind.PartyDps, "PT DPS推移");
        ExtensionWidgetHost.OpenRequested = (kind, target) =>
        {
            var widget = _widgetItems.First(x => x.Kind == kind);
            ExtensionWidgetHost.Targets[kind] = target;
            WidgetWindowManager.Instance.OpenExtension(widget);
        };

        _playerListWidget.PlayerWindowRequested += PlayerListWidget_PlayerWindowRequested;
        _entityListWidget.EntityWindowRequested += EntityListWidget_EntityWindowRequested;
        _entityListWidget.EntityListTargetsChanged += EntityListWidget_EntityListTargetsChanged;
        _widgetWindowManager.TargetWindowsChanged += WidgetWindowManager_TargetWindowsChanged;
        _widgetWindowManager.UnspecifiedBuffCardResolved += WidgetWindowManager_UnspecifiedBuffCardResolved;
        // 起動できないときに見せるだけのメイン窓では Core が初期化されていない(DB も開いていない)。
        // EncounterManager に触ると静的コンストラクタが回を作り直しに行くので、Core の購読も選択リストを読み直す時計も始めない(集計タブと同じ)。
        if (CombatRuntimeHost.Instance.IsInitialized)
        {
            EncounterManager.EncounterStart += EncounterManager_EncounterStart;
            EncounterHistoryProvider.SelectionChanged += EncounterHistoryProvider_SelectionChanged;
            MessageManager.ResetToStartupCompleted += MessageManager_ResetToStartupCompleted;
            _targetSelectionTimer.Tick += TargetSelectionTimer_Tick;
            _targetSelectionTimer.Start();
            _notificationMonitor = new NotificationMonitor();
        }

        Widgets = (ListCollectionView)CollectionViewSource.GetDefaultView(_widgetItems);
        Widgets.IsLiveSorting = true;
        ApplyWidgetSort();

        // プラグインのウィジェットは、プラグインを初期化したとき(起動の処理と「＋」)に足し、止めたときに外す。
        foreach (var group in PluginWidgetRegistry.Instance.Groups)
        {
            PluginWidgetRegistry_GroupAdded(this, group);
        }

        PluginWidgetRegistry.Instance.GroupAdded += PluginWidgetRegistry_GroupAdded;
        PluginWidgetRegistry.Instance.GroupRemoved += PluginWidgetRegistry_GroupRemoved;

        foreach (var manifest in _pluginManager.DiscoverPlugins())
        {
            AddPluginItem(manifest);
        }

        PluginItems = new ReadOnlyObservableCollection<PluginListItemViewModel>(_pluginItems);
        _pluginManager.PluginRegistered += PluginManager_PluginRegistered;
        _pluginManager.PluginStatusChanged += PluginManager_PluginStatusChanged;

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        _hotkeyService.Pressed += HotkeyService_Pressed;
        _hotkeyService.RegistrationChanged += HotkeyService_RegistrationChanged;
        _configManager.SettingsChanged += ConfigManager_SettingsChanged;
        _configManager.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;

        _playerRosterStore.RosterChanged += PlayerRosterPresentationStore_RosterChanged;
        _partyStateStore.Changed += PartyStateStore_Changed;
        PlayerDirectory.Changed += PlayerDirectory_Changed;
        SocialDataStore.Cleared += SocialDataStore_Cleared;
        SelfEquipmentStore.Changed += SelfEquipmentStore_Changed;
        SelfModuleStore.Changed += SelfModuleStore_Changed;
        SelfSeasonCultivateStore.Changed += SelfSeasonCultivateStore_Changed;
        _nearbyEntityStore.EntitiesChanged += NearbyEntityStore_EntitiesChanged;
        EntityHistoryProjection.Changed += EntityHistory_Changed;
        _dungeonScoreStore.Changed += DungeonScoreStore_Changed;
        MessageManager.MatchFound += MessageManager_MatchFound;
        GrpcTeamManager.TeamEntryVoteStarted += GrpcTeamManager_TeamEntryVoteStarted;
        MessageManager.PrivateChatMessageReceived += MessageManager_PrivateChatMessageReceived;
        MessageManager.PrivateChatRead += MessageManager_PrivateChatRead;
        MessageManager.WorldChatMessageReceived += MessageManager_WorldChatMessageReceived;

        var roster = _playerRosterStore.Current;
        ApplyPlayerRosterSnapshot(roster);
        ApplyNearbyEntitySnapshot(_nearbyEntityStore.Current);
        _entityListWidget.UpdateEntityHistory(EntityHistoryProjection.Current);
        _entityListWidget.UpdateDungeonScore(_dungeonScoreStore.Current);
    }

    private void AddPluginItem(PluginInfo pluginInfo)
    {
        _pluginItems.Add(CreatePluginItem(pluginInfo, _pluginItems.Count));
    }

    // 「＋ プラグインを追加」から足したプラグイン。一覧は入れた順なので末尾に足す。
    private void PluginManager_PluginRegistered(object? sender, PluginInfo pluginInfo)
    {
        AddPluginItem(pluginInfo);
    }

    private void PluginManager_PluginStatusChanged(object? sender, string pluginKey)
    {
        _pluginItems
            .FirstOrDefault(plugin => string.Equals(plugin.PluginKey, pluginKey, StringComparison.OrdinalIgnoreCase))
            ?.RefreshStatus();
    }

    private static PluginListItemViewModel CreatePluginItem(PluginInfo pluginInfo, int originalIndex)
    {
        var plugin = new PluginListItemViewModel
        {
            Info = pluginInfo,
            Icon = PluginIconLoader.Load(pluginInfo),
            OriginalIndex = originalIndex
        };

        plugin.RefreshLocalizedText();
        plugin.RefreshStatus();
        return plugin;
    }

    [RelayCommand]
    private void AddPlugin()
    {
        _pluginManager.AddFromFile();
    }

    private WidgetListItemViewModel AddWidget(WidgetKind kind, string displayNameResourceKey)
    {
        return AddWidget(new WidgetListItemViewModel
        {
            Kind = kind,
            DisplayNameResourceKey = displayNameResourceKey,
            State = WidgetState.Stopped,
            OriginalIndex = _widgetItems.Count
        });
    }

    /// <summary>動き出したプラグインのウィジェットのカードを、本体のカードの後ろに足す(並びの番号は足した順)。</summary>
    private void PluginWidgetRegistry_GroupAdded(object? sender, PluginWidgetGroup group)
    {
        foreach (var registration in group.Widgets)
        {
            AddWidget(new WidgetListItemViewModel
            {
                Kind = WidgetKind.Plugin,
                PluginWidget = registration,
                State = WidgetState.Stopped,
                OriginalIndex = _widgetItems.Count
            });
        }
    }

    /// <summary>
    /// 止めたプラグインのウィジェットのカードを外す。開いている窓は保存した状態(起動中)を変えずに閉じ、次の起動で開き直す。
    /// プラグインはカードの一覧を回している途中の例外でも止まるので、一覧から外すのは今の処理が終わってから。
    /// </summary>
    private void PluginWidgetRegistry_GroupRemoved(object? sender, PluginWidgetGroup group)
    {
        var removedWidgets = _widgetItems
            .Where(widget => widget.PluginWidget is { } registration && group.Widgets.Contains(registration))
            .ToArray();
        foreach (var widget in removedWidgets)
        {
            _widgetWindowManager.CloseKeepingState(widget);
            widget.PropertyChanged -= OnWidgetPropertyChanged;
            widget.BuffListHiddenBuffsChanged -= Widget_BuffListHiddenBuffsChanged;
            widget.EntityInfoHiddenBuffsChanged -= Widget_EntityInfoHiddenBuffsChanged;
            widget.BuffListPriorityBuffChangedFromWidget -= Widget_BuffListPriorityBuffChangedFromWidget;
            widget.EntityInfoPriorityBuffChangedFromWidget -= Widget_EntityInfoPriorityBuffChangedFromWidget;
            widget.BuffDebuffGroupEntryChangedFromWidget -= Widget_BuffDebuffGroupEntryChangedFromWidget;
            widget.BuffCardShortcutChangedFromWidget -= Widget_BuffCardShortcutChangedFromWidget;
            widget.BuffCardNotificationTargetChangedFromWidget -= Widget_BuffCardNotificationTargetChangedFromWidget;
            widget.BuffDebuffGroupNotificationTargetChangedFromWidget -= Widget_BuffDebuffGroupNotificationTargetChangedFromWidget;
            if (widget.SupportsTargetSelection)
            {
                widget.TargetOptionsRequested -= Widget_TargetOptionsRequested;
                widget.SelectedTargetChanged -= Widget_SelectedTargetChanged;
                widget.SelectedTargetToggleRequested -= Widget_SelectedTargetToggleRequested;
                widget.BuffCardShortcutsChanged -= Widget_BuffCardShortcutsChanged;
            }
        }

        if (Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher)
        {
            dispatcher.BeginInvoke(() =>
            {
                foreach (var widget in removedWidgets)
                {
                    _widgetItems.Remove(widget);
                }
            });
        }
    }

    private WidgetListItemViewModel AddWidget(WidgetListItemViewModel widget)
    {
        widget.RefreshLocalizedText();

        _isLoadingWidgets = true;
        try
        {
            var persistedConfig = _widgetStateManager.GetWidgetSnapshot(widget.Key);
            widget.ApplyWidgetConfig(persistedConfig);

            if (persistedConfig.State is null)
            {
                _widgetStateManager.SaveWidgetState(widget.Key, widget.State);
            }
        }
        finally
        {
            _isLoadingWidgets = false;
        }

        widget.PropertyChanged += OnWidgetPropertyChanged;
        widget.BuffListHiddenBuffsChanged += Widget_BuffListHiddenBuffsChanged;
        widget.EntityInfoHiddenBuffsChanged += Widget_EntityInfoHiddenBuffsChanged;
        widget.BuffListPriorityBuffChangedFromWidget += Widget_BuffListPriorityBuffChangedFromWidget;
        widget.EntityInfoPriorityBuffChangedFromWidget += Widget_EntityInfoPriorityBuffChangedFromWidget;
        widget.BuffDebuffGroupEntryChangedFromWidget += Widget_BuffDebuffGroupEntryChangedFromWidget;
        widget.BuffCardShortcutChangedFromWidget += Widget_BuffCardShortcutChangedFromWidget;
        widget.BuffCardNotificationTargetChangedFromWidget += Widget_BuffCardNotificationTargetChangedFromWidget;
        widget.BuffDebuffGroupNotificationTargetChangedFromWidget += Widget_BuffDebuffGroupNotificationTargetChangedFromWidget;
        if (widget.SupportsTargetSelection)
        {
            widget.SelectedTargetOption = _targetSelection.CreateDefault(widget);
            widget.TargetOptionsRequested += Widget_TargetOptionsRequested;
            widget.SelectedTargetChanged += Widget_SelectedTargetChanged;
            widget.SelectedTargetToggleRequested += Widget_SelectedTargetToggleRequested;
            widget.BuffCardShortcutsChanged += Widget_BuffCardShortcutsChanged;
        }

        widget.IsSearchMatch = MatchesSearchText(widget);
        _widgetItems.Add(widget);
        _widgetWindowManager.RegisterPlayerWindowWidget(widget);
        return widget;
    }

    private WidgetListItemViewModel? FindWidget(WidgetKind kind)
    {
        return _widgetItems.FirstOrDefault(widget => widget.Kind == kind);
    }

    private void Widget_TargetOptionsRequested(object? sender, EventArgs e)
    {
        if (sender is WidgetListItemViewModel widget)
        {
            _targetSelection.BuildOptions(widget);
        }
    }

    /// <summary>選んでいる項目が変わった。バフ・デバフカードの相手はバフリスト・デバフリストの選択で決まるので、全部のカードを当て直す。</summary>
    private void Widget_SelectedTargetChanged(object? sender, EventArgs e)
    {
        RefreshTargetSelections();
    }

    private void Widget_SelectedTargetToggleRequested(object? sender, EventArgs e)
    {
        if (sender is WidgetListItemViewModel widget)
        {
            _targetSelection.Toggle(widget);
        }
    }

    private void WidgetWindowManager_TargetWindowsChanged(WidgetListItemViewModel widget)
    {
        _targetSelection.Refresh(widget);
    }

    private void TargetSelectionTimer_Tick(object? sender, EventArgs e)
    {
        RefreshTargetSelections();
    }

    /// <summary>バフ・デバフカードの「指定なし」で開くカードが決まった。選択を指定なしからそのカードに置き換える。</summary>
    private void WidgetWindowManager_UnspecifiedBuffCardResolved(WidgetListItemViewModel widget, WidgetTargetOption card)
    {
        _targetSelection.ResolveUnspecified(widget, card);
    }

    /// <summary>バフ・デバフカードのショートカット(保存かプレビュー)が変わった。並べるものの有無と既定が変わるので、そのカードを当て直す。</summary>
    private void Widget_BuffCardShortcutsChanged(object? sender, EventArgs e)
    {
        if (sender is WidgetListItemViewModel widget)
        {
            _targetSelection.Refresh(widget);
        }
    }

    /// <summary>回を作り直した(メーターの行が空に戻る)。パケットの処理の側から上がるので、画面の側で当て直す。</summary>
    private void EncounterManager_EncounterStart(EncounterStartEventArgs e)
    {
        RefreshTargetSelectionsOnDispatcher();
    }

    /// <summary>履歴の回を開いた・閉じた(メーターの行が履歴の回のものに替わる)。</summary>
    private void EncounterHistoryProvider_SelectionChanged()
    {
        RefreshTargetSelectionsOnDispatcher();
    }

    /// <summary>ログアウトで起動時の状態へ戻した。パケットの処理の側から上がるので、画面の側でプラグインのカードの選択を戻す。</summary>
    private void MessageManager_ResetToStartupCompleted()
    {
        Application.Current?.Dispatcher.BeginInvoke(() => _targetSelection.ResetPluginSelections(_widgetItems));
    }

    private void RefreshTargetSelectionsOnDispatcher()
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(RefreshTargetSelections);
            return;
        }

        RefreshTargetSelections();
    }

    /// <summary>
    /// カードの名前の欄(選んでいる項目で開く窓のタイトル)と起動ボタンを当て直す。
    /// 名簿・周りの実体・言語・表示設定(名前を伏せるか)が変わったときと、1秒ごとの時計(合図の無いバフの行・メーターの行のため)で呼ぶ。
    /// 値が同じならカードは変えない。
    /// </summary>
    private void RefreshTargetSelections()
    {
        _targetSelection.RefreshAll(_widgetItems);
    }

    /// <summary>
    /// 窓の右クリックでバフを非表示にした。<b>保存済みの一覧に足して</b>その項目だけ書く。
    /// ウィジェットが持つ一覧は設定ウィンドウのプレビューを含むので、そのまま書くと未保存の削除まで保存してしまう。
    /// </summary>
    private void Widget_BuffListHiddenBuffsChanged(WidgetKind kind, int baseId)
    {
        var hiddenBuffIds = _widgetStateManager.GetWidgetSnapshot(kind).BuffList?.HiddenBuffIds ?? [];
        if (hiddenBuffIds.Contains(baseId))
        {
            return;
        }

        _widgetStateManager.SaveBuffListHiddenBuffs(kind, [.. hiddenBuffIds, baseId]);
    }

    /// <summary>エンティティ情報の枠の右クリックでバフを非表示にした。バフ・デバフ一覧と同じく、保存済みの一覧に足してその項目だけ書く。</summary>
    private void Widget_EntityInfoHiddenBuffsChanged(WidgetKind kind, int baseId)
    {
        var hiddenBuffIds = _widgetStateManager.GetWidgetSnapshot(kind).EntityInfo?.HiddenBuffIds ?? [];
        if (hiddenBuffIds.Contains(baseId))
        {
            return;
        }

        _widgetStateManager.SaveEntityInfoHiddenBuffs(kind, [.. hiddenBuffIds, baseId]);
    }

    /// <summary>
    /// 窓の右クリックでバフを優先表示にした・やめた。グループの行と同じく、<b>保存済みの行に同じ決まりで当てて</b>
    /// (<see cref="WidgetConfigDefaults.SetBuffEntryEnabled"/>)その項目だけ書く。
    /// </summary>
    private void Widget_BuffListPriorityBuffChangedFromWidget(WidgetKind kind, BuffEntryKey key, bool isEnabled)
    {
        var priorityBuffs = WidgetConfigDefaults
            .CloneNormalizedBuffList(kind, _widgetStateManager.GetWidgetSnapshot(kind).BuffList)
            .PriorityBuffs ?? [];
        WidgetConfigDefaults.SetBuffEntryEnabled(priorityBuffs, key, isEnabled, WidgetConfigDefaults.GetBuffListPriorityDefaults(kind));
        _widgetStateManager.SaveBuffListPriorityBuffs(kind, priorityBuffs);
    }

    /// <summary>エンティティ情報の枠の右クリックでバフを優先表示にした・やめた。バフ・デバフ一覧と同じく、保存済みの行に同じ決まりで当ててその項目だけ書く。</summary>
    private void Widget_EntityInfoPriorityBuffChangedFromWidget(WidgetKind kind, BuffEntryKey key, bool isEnabled)
    {
        var priorityBuffs = WidgetConfigDefaults
            .CloneNormalizedEntityInfo(_widgetStateManager.GetWidgetSnapshot(kind).EntityInfo)
            .PriorityBuffs ?? [];
        WidgetConfigDefaults.SetBuffEntryEnabled(priorityBuffs, key, isEnabled, WidgetConfigDefaults.DefaultEntityInfoPriorityBuffEntries);
        _widgetStateManager.SaveEntityInfoPriorityBuffs(kind, priorityBuffs);
    }

    /// <summary>
    /// 右クリックでバフ・デバフグループの行をオン/オフにした。<b>保存済みの行に同じ決まりで当てて</b>
    /// (<see cref="WidgetConfigDefaults.SetBuffEntryEnabled"/>)その項目だけ書く。
    /// ウィジェットが持つ行は設定ウィンドウのプレビューを含むので、そのまま書くと未保存の変更まで保存してしまう。
    /// </summary>
    private void Widget_BuffDebuffGroupEntryChangedFromWidget(WidgetKind kind, BuffEntryKey key, bool isEnabled)
    {
        var entries = WidgetConfigDefaults
            .CloneNormalizedBuffDebuffGroup(_widgetStateManager.GetWidgetSnapshot(kind).BuffDebuffGroup)
            .Entries ?? [];
        WidgetConfigDefaults.SetBuffEntryEnabled(entries, key, isEnabled, WidgetConfigDefaults.DefaultBuffDebuffGroupEntries);
        _widgetStateManager.SaveBuffDebuffGroupEntries(kind, entries);
    }

    /// <summary>
    /// 右クリックでバフ・デバフカードのショートカットの行をオンにした。グループの行と同じく、<b>保存済みの行に同じ決まりで当てて</b>その項目だけ書く。
    /// </summary>
    private void Widget_BuffCardShortcutChangedFromWidget(WidgetKind kind, BuffEntryKey key, bool isEnabled)
    {
        var shortcuts = WidgetConfigDefaults
            .CloneNormalizedBuffCard(_widgetStateManager.GetWidgetSnapshot(kind).BuffCard)
            .Shortcuts ?? [];
        WidgetConfigDefaults.SetBuffEntryEnabled(shortcuts, key, isEnabled, WidgetConfigDefaults.DefaultBuffCardShortcutEntries);
        _widgetStateManager.SaveBuffCardShortcuts(kind, shortcuts);
    }

    /// <summary>
    /// 右クリックでバフ・デバフカードの通知対象の行をオン/オフにした。ショートカットと同じく、<b>保存済みの行に同じ決まりで当てて</b>その項目だけ書く。
    /// </summary>
    private void Widget_BuffCardNotificationTargetChangedFromWidget(WidgetKind kind, BuffEntryKey key, bool isEnabled)
    {
        var targets = WidgetConfigDefaults
            .CloneNormalizedBuffCard(_widgetStateManager.GetWidgetSnapshot(kind).BuffCard)
            .NotificationTargets ?? [];
        WidgetConfigDefaults.SetBuffEntryEnabled(targets, key, isEnabled, WidgetConfigDefaults.DefaultBuffCardNotificationTargetEntries);
        _widgetStateManager.SaveBuffCardNotificationTargets(kind, targets);
    }

    /// <summary>
    /// 右クリックでバフ・デバフグループの通知対象の行をオン/オフにした。グループの行と同じく、<b>保存済みの行に同じ決まりで当てて</b>その項目だけ書く。
    /// </summary>
    private void Widget_BuffDebuffGroupNotificationTargetChangedFromWidget(WidgetKind kind, BuffEntryKey key, bool isEnabled)
    {
        var targets = WidgetConfigDefaults
            .CloneNormalizedBuffDebuffGroup(_widgetStateManager.GetWidgetSnapshot(kind).BuffDebuffGroup)
            .NotificationTargets ?? [];
        WidgetConfigDefaults.SetBuffEntryEnabled(targets, key, isEnabled, WidgetConfigDefaults.DefaultBuffDebuffGroupNotificationTargetEntries);
        _widgetStateManager.SaveBuffDebuffGroupNotificationTargets(kind, targets);
    }

    private void PlayerListWidget_PlayerWindowRequested(WidgetKind kind, long characterId)
    {
        _widgetWindowManager.OpenPlayerWindow(kind, characterId);
    }

    private void EntityListWidget_EntityWindowRequested(WidgetKind kind, EntityTargetInfo entity)
    {
        _widgetWindowManager.OpenEntityWindow(kind, entity);
    }

    private void EntityListWidget_EntityListTargetsChanged(object? sender, EventArgs e)
    {
        _widgetWindowManager.ApplyEntityTargets(_entityListWidget.EntityListEntries);
    }

    private void PlayerRosterPresentationStore_RosterChanged(object? sender, PlayerRosterChangedEventArgs e)
    {
        QueuePlayerRosterSnapshot(new PlayerRosterSnapshot(
            e.Snapshot,
            e.MapName,
            e.MapChannel,
            e.MapGeneration,
            e.SeasonId));
    }

    /// <summary>
    /// 名簿の外の値(名刺照会の値・チャットの発言者の値)が変わった相手。受信のスレッドから来るので UI のスレッドへ移して、
    /// プレイヤーの窓へ渡す。
    /// </summary>
    private void PlayerDirectory_Changed(long characterId)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            _widgetWindowManager.UpdatePlayerWindowPlayerData(characterId);
            return;
        }

        dispatcher.BeginInvoke(() => _widgetWindowManager.UpdatePlayerWindowPlayerData(characterId));
    }

    /// <summary>名刺の値の控えが全部消えた(キャプチャの停止とログアウト)。UI のスレッドへ移して全部の窓へ渡す。</summary>
    private void SocialDataStore_Cleared()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            _widgetWindowManager.ClearPlayerWindowSocialData();
            return;
        }

        dispatcher.BeginInvoke(() => _widgetWindowManager.ClearPlayerWindowSocialData());
    }

    private void SelfEquipmentStore_Changed()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            _widgetWindowManager.UpdatePlayerWindowSelfEquipment();
            return;
        }

        dispatcher.BeginInvoke(() => _widgetWindowManager.UpdatePlayerWindowSelfEquipment());
    }

    private void SelfModuleStore_Changed()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            _widgetWindowManager.UpdatePlayerWindowSelfModules();
            return;
        }

        dispatcher.BeginInvoke(() => _widgetWindowManager.UpdatePlayerWindowSelfModules());
    }

    private void SelfSeasonCultivateStore_Changed()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            _widgetWindowManager.UpdatePlayerWindowSelfSeasonCultivate();
            return;
        }

        dispatcher.BeginInvoke(() => _widgetWindowManager.UpdatePlayerWindowSelfSeasonCultivate());
    }

    /// <summary>マッチングが成立した。受信のスレッドから来るので UI のスレッドへ移して通知の判定へ渡す。</summary>
    private void MessageManager_MatchFound(Zproto.EMatchType matchType, long matchTypeUuid)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            _notificationMonitor?.NotifyMatchFound(matchType, matchTypeUuid);
            return;
        }

        dispatcher.BeginInvoke(() => _notificationMonitor?.NotifyMatchFound(matchType, matchTypeUuid));
    }

    /// <summary>パーティでコンテンツに入るときの承認が始まった。受信のスレッドから来るので UI のスレッドへ移して通知の判定へ渡す。</summary>
    private void GrpcTeamManager_TeamEntryVoteStarted(int sceneId, int masterModeDifficulty)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            _notificationMonitor?.NotifyTeamEntryVoteStarted(sceneId, masterModeDifficulty);
            return;
        }

        dispatcher.BeginInvoke(() => _notificationMonitor?.NotifyTeamEntryVoteStarted(sceneId, masterModeDifficulty));
    }

    /// <summary>ほかの人からプライベートトークが届いた。受信のスレッドから来るので UI のスレッドへ移して通知の判定へ渡す。</summary>
    private void MessageManager_PrivateChatMessageReceived(long partnerId, long messageId, string senderName)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            _notificationMonitor?.NotifyPrivateChatMessage(partnerId, messageId, senderName);
            return;
        }

        dispatcher.BeginInvoke(() => _notificationMonitor?.NotifyPrivateChatMessage(partnerId, messageId, senderName));
    }

    /// <summary>ゲームがプライベートトークを既読にした。受信のスレッドから来るので UI のスレッドへ移して通知の判定へ渡す。</summary>
    private void MessageManager_PrivateChatRead(long partnerId, long maxReadMessageId)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            _notificationMonitor?.NotifyPrivateChatRead(partnerId, maxReadMessageId);
            return;
        }

        dispatcher.BeginInvoke(() => _notificationMonitor?.NotifyPrivateChatRead(partnerId, maxReadMessageId));
    }

    /// <summary>ワールドチャットにほかの人の文の発言が届いた。受信のスレッドから来るので UI のスレッドへ移して通知の判定へ渡す。</summary>
    private void MessageManager_WorldChatMessageReceived(long senderId, string senderName, string message)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            _notificationMonitor?.NotifyWorldChatMessage(senderId, senderName, message);
            return;
        }

        dispatcher.BeginInvoke(() => _notificationMonitor?.NotifyWorldChatMessage(senderId, senderName, message));
    }

    private void PartyStateStore_Changed(object? sender, EventArgs e)
    {
        QueuePlayerRosterSnapshot(_playerRosterStore.Current);
    }

    private void QueuePlayerRosterSnapshot(PlayerRosterSnapshot roster)
    {
        lock (_playerRosterUpdateSync)
        {
            _pendingPlayerRosterSnapshot = roster;
            if (_isPlayerRosterUpdateQueued)
            {
                return;
            }

            _isPlayerRosterUpdateQueued = true;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            ProcessPendingPlayerRosterSnapshot();
            return;
        }

        dispatcher.BeginInvoke(ProcessPendingPlayerRosterSnapshot);
    }

    private void ProcessPendingPlayerRosterSnapshot()
    {
        PlayerRosterSnapshot? roster;

        lock (_playerRosterUpdateSync)
        {
            roster = _pendingPlayerRosterSnapshot;
            _pendingPlayerRosterSnapshot = null;
            _isPlayerRosterUpdateQueued = false;
        }

        if (roster is not null)
        {
            ApplyPlayerRosterSnapshot(roster);
        }
    }

    private void ApplyPlayerRosterSnapshot(PlayerRosterSnapshot roster)
    {
        _playerListWidget.UpdatePlayerRoster(
            roster.Entries,
            roster.MapName,
            roster.MapChannel,
            roster.MapGeneration);
        _entityListWidget.UpdateEntityListRoster(roster.Entries);
        _widgetWindowManager.UpdateEntityInfoRosters(roster.Entries);
        _widgetWindowManager.UpdatePlayerWindowPresentations(roster);
        _widgetWindowManager.UpdateListWidgetRosters(roster);
        _notificationMonitor?.ObserveRoster(roster);
        RefreshTargetSelections();
    }

    private void NearbyEntityStore_EntitiesChanged(object? sender, NearbyEntitiesChangedEventArgs e)
    {
        QueueNearbyEntitySnapshot(new NearbyEntitySnapshot(
            e.Snapshot,
            e.MapName,
            e.MapChannel,
            e.MapGeneration));
    }

    /// <summary>ダンジョンの得点が変わった。受信のスレッドから来るので UI のスレッドへ移し、その時点の値をエンティティリストへ渡す。</summary>
    private void DungeonScoreStore_Changed(object? sender, EventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            _entityListWidget.UpdateDungeonScore(_dungeonScoreStore.Current);
            return;
        }

        dispatcher.BeginInvoke(() => _entityListWidget.UpdateDungeonScore(_dungeonScoreStore.Current));
    }

    private void QueueNearbyEntitySnapshot(NearbyEntitySnapshot snapshot)
    {
        lock (_nearbyEntityUpdateSync)
        {
            _pendingNearbyEntitySnapshot = snapshot;
            if (_isNearbyEntityUpdateQueued)
            {
                return;
            }

            _isNearbyEntityUpdateQueued = true;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            ProcessPendingNearbyEntitySnapshot();
            return;
        }

        dispatcher.BeginInvoke(ProcessPendingNearbyEntitySnapshot);
    }

    private void ProcessPendingNearbyEntitySnapshot()
    {
        NearbyEntitySnapshot? snapshot;

        lock (_nearbyEntityUpdateSync)
        {
            snapshot = _pendingNearbyEntitySnapshot;
            _pendingNearbyEntitySnapshot = null;
            _isNearbyEntityUpdateQueued = false;
        }

        if (snapshot is not null)
        {
            ApplyNearbyEntitySnapshot(snapshot);
        }
    }

    private void EntityHistory_Changed(EntityHistorySnapshot snapshot)
    {
        var dispatcher=Application.Current?.Dispatcher;
        if(dispatcher is not null && !dispatcher.CheckAccess()) { dispatcher.BeginInvoke(()=>ApplyEntityHistory(snapshot)); return; }
        ApplyEntityHistory(snapshot);
    }
    private void ApplyEntityHistory(EntityHistorySnapshot snapshot)
    {
        _entityListWidget.UpdateEntityHistory(snapshot);
        var entities=(snapshot.IsHistorical ? snapshot.Alive : _nearbyEntityStore.Current.Entries).Concat(snapshot.Defeated).DistinctBy(x=>x.EntityUuid).ToArray();
        _widgetWindowManager.UpdateEntityWindowPresentations(_entityListWidget.EntityListEntries,entities);
        RefreshTargetSelections();
    }

    private void ApplyNearbyEntitySnapshot(NearbyEntitySnapshot snapshot)
    {
        // エンティティ出現の通知はエンティティリストを開いていなくても見る。
        _notificationMonitor?.ObserveNearbyEntities(snapshot.Entries);
        _entityListWidget.UpdateNearbyEntities(
            snapshot.Entries,
            snapshot.MapName,
            snapshot.MapChannel,
            snapshot.MapGeneration);
        var history=EntityHistoryProjection.Current;
        var entities=(history.IsHistorical ? history.Alive : snapshot.Entries).Concat(history.Defeated).DistinctBy(x=>x.EntityUuid).ToArray();
        _widgetWindowManager.UpdateEntityWindowPresentations(_entityListWidget.EntityListEntries, entities);
        RefreshTargetSelections();
    }

    private void ConfigManager_SettingsChanged(object? sender, EventArgs e)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(RefreshPlayerRosterPresentation);
            return;
        }

        RefreshPlayerRosterPresentation();
    }

    private void RefreshPlayerRosterPresentation()
    {
        QueuePlayerRosterSnapshot(_playerRosterStore.Current);
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        RefreshLocalizedPresentation();
    }

    /// <summary>
    /// 設定のプレビュー適用。<b>内部IDの表示切り替えを言語切替と同じ扱いで即反映させる</b>ため、
    /// 保存だけでなくプレビューでも組み直す(保存時も同じイベントが上がる)。
    /// </summary>
    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(RefreshLocalizedPresentation);
            return;
        }

        RefreshLocalizedPresentation();
    }

    /// <summary>
    /// 表示中の文字列を組み直す。言語切替と、内部IDの表示切り替えの両方から呼ぶ。
    /// </summary>
    private void RefreshLocalizedPresentation()
    {
        // シーン名は Core が解決済みの文字列で持っているので、言語や表示設定を変えただけでは
        // 追従しない。引き直して投影へ流し直させる。チャンネルの接尾辞は App 側で組むので、
        // これでマップ表示が丸ごと組み直される。
        EncounterManager.RefreshSceneName();

        foreach (var widget in _widgetItems)
        {
            widget.RefreshLocalizedText();
        }

        foreach (var plugin in _pluginItems)
        {
            plugin.RefreshLocalizedText();
        }

        RaiseWidgetActionTextsChanged();
        RefreshTargetSelections();
    }

    private void OnWidgetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not WidgetListItemViewModel widget || _isLoadingWidgets)
        {
            return;
        }

        if (e.PropertyName is nameof(WidgetListItemViewModel.IsFavorite)
            or nameof(WidgetListItemViewModel.IsPinned)
            or nameof(WidgetListItemViewModel.IsClickThrough))
        {
            _widgetStateManager.SaveWidgetFlags(widget.Key, widget.IsFavorite, widget.IsPinned, widget.IsClickThrough);

            if (e.PropertyName == nameof(WidgetListItemViewModel.IsPinned))
            {
                _widgetWindowManager.ApplyWidgetPinState(
                    widget,
                    bringToFront: !_isBulkUpdatingWidgets);
            }
        }

        if (e.PropertyName == nameof(WidgetListItemViewModel.State))
        {
            _widgetStateManager.SaveWidgetState(widget.Key, widget.State);
            _widgetWindowManager.ApplyWidgetState(widget);
        }

        // 名前の欄に出している文字が変わったら、そのカードだけ検索に当たるかを決め直す。
        if (e.PropertyName is nameof(WidgetListItemViewModel.DisplayName)
            or nameof(WidgetListItemViewModel.TargetSelectorText)
            or nameof(WidgetListItemViewModel.ShowsTargetSelector))
        {
            widget.IsSearchMatch = MatchesSearchText(widget);
        }
    }

    public void RestoreRunningWidgetWindows()
    {
        foreach (var widget in _widgetItems.Where(widget => widget.State == WidgetState.Running))
        {
            _widgetWindowManager.ApplyWidgetState(widget);
        }
    }

    /// <summary>
    /// 起動設定に従って補助を起動し、保存してあるホットキーを登録する(起動時)。登録できなかったものを返す。
    /// 全体設定での登録(プレビュー・リセット・保存・保存せずに閉じる)は全体設定の画面が行う。
    /// </summary>
    public IReadOnlyList<HotkeyRegistrationFailure> ApplyHotkeys()
    {
        var settings = _configManager.GetSettingsSnapshot();
        _hotkeyService.Start(settings.StartHotkeyHelperOnLaunch);
        return _hotkeyService.Apply(settings.Hotkeys);
    }

    private void HotkeyService_Pressed(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.StartFavoritesOrStopAll:
                if (_widgetItems.Any(widget => widget.State == WidgetState.Running))
                {
                    StopAllWidgets();
                }
                else
                {
                    StartFavoriteWidgets();
                }

                break;

            case HotkeyAction.PinRunningOrUnpinAll:
                if (_widgetItems.Any(widget => widget.IsPinned))
                {
                    UnpinAllWidgets();
                }
                else
                {
                    PinAllRunningWidgets();
                }

                break;

            case HotkeyAction.ClickThroughPinnedOrClearAll:
                if (_widgetItems.Any(widget => widget.IsClickThrough))
                {
                    ClearAllWidgetClickThrough();
                }
                else
                {
                    ClickThroughAllPinnedWidgets();
                }

                break;

            case HotkeyAction.PlayerNameShowOrHide:
                _configManager.SavePlayerNameDisplayModeIndex(
                    _configManager.CurrentSettings.PlayerNameDisplayModeIndex == (int)PlayerNameDisplayMode.Show
                        ? (int)PlayerNameDisplayMode.Hide
                        : (int)PlayerNameDisplayMode.Show);
                break;

            case HotkeyAction.WidgetWindowTopmostAlwaysOrPinnedOnly:
                _configManager.SaveWidgetWindowTopmostModeIndex(
                    _configManager.CurrentSettings.WidgetWindowTopmostModeIndex == AppConfigDefaults.AlwaysWidgetWindowTopmostModeIndex
                        ? AppConfigDefaults.PinnedOnlyWidgetWindowTopmostModeIndex
                        : AppConfigDefaults.AlwaysWidgetWindowTopmostModeIndex);
                break;

            case HotkeyAction.Benchmark:
                MeterSnapshotProvider.ToggleBenchmark();
                break;

            case HotkeyAction.ResetEncounter:
                MeterSnapshotProvider.ResetCurrentEncounter();
                break;
        }
    }

    private void HotkeyService_RegistrationChanged(object? sender, EventArgs e)
    {
        RaiseWidgetActionTextsChanged();
    }

    private string FormatWidgetActionText(string resourceKey, HotkeyAction action)
    {
        var text = LocalizationManager.Instance.GetString(resourceKey);
        return _hotkeyService.IsRegistered(action)
            ? $"{text} ({HotkeyText.Format(_hotkeyService.GetBinding(action))})"
            : text;
    }

    private void RaiseWidgetActionTextsChanged()
    {
        OnPropertyChanged(nameof(StartFavoritesActionText));
        OnPropertyChanged(nameof(StopAllActionText));
        OnPropertyChanged(nameof(PinRunningActionText));
        OnPropertyChanged(nameof(UnpinAllActionText));
        OnPropertyChanged(nameof(ClickThroughPinnedActionText));
        OnPropertyChanged(nameof(ClearClickThroughActionText));
    }

    /// <summary>
    /// 全部のカードの、検索欄の文字に当たるかを決め直す。当たらないカードは隠すだけで、一覧は組み直さない
    /// (一覧の絞り込みを組み直すと、カードが全部作り直される)。
    /// </summary>
    private void ApplySearch()
    {
        foreach (var widget in _widgetItems)
        {
            widget.IsSearchMatch = MatchesSearchText(widget);
        }
    }

    /// <summary>カードの名前の欄に出している文字(<see cref="WidgetListItemViewModel.ShownName"/>)に、検索欄の文字が入っているか。</summary>
    private bool MatchesSearchText(WidgetListItemViewModel widget)
    {
        var searchText = SearchText?.Trim();
        return string.IsNullOrEmpty(searchText)
            || widget.ShownName.Contains(searchText, StringComparison.CurrentCultureIgnoreCase);
    }

    private void ApplyWidgetSort()
    {
        using (Widgets.DeferRefresh())
        {
            Widgets.SortDescriptions.Clear();

            switch (WidgetSortIndex)
            {
                case 1:
                    Widgets.SortDescriptions.Add(new SortDescription(nameof(WidgetListItemViewModel.DisplayName), ListSortDirection.Ascending));
                    break;

                case 2:
                    Widgets.SortDescriptions.Add(new SortDescription(nameof(WidgetListItemViewModel.IsFavorite), ListSortDirection.Descending));
                    Widgets.SortDescriptions.Add(new SortDescription(nameof(WidgetListItemViewModel.OriginalIndex), ListSortDirection.Ascending));
                    break;

                case 3:
                    Widgets.SortDescriptions.Add(new SortDescription(nameof(WidgetListItemViewModel.IsRunning), ListSortDirection.Descending));
                    Widgets.SortDescriptions.Add(new SortDescription(nameof(WidgetListItemViewModel.OriginalIndex), ListSortDirection.Ascending));
                    break;

                case 4:
                    Widgets.SortDescriptions.Add(new SortDescription(nameof(WidgetListItemViewModel.IsPinned), ListSortDirection.Descending));
                    Widgets.SortDescriptions.Add(new SortDescription(nameof(WidgetListItemViewModel.OriginalIndex), ListSortDirection.Ascending));
                    break;

                case 5:
                    Widgets.SortDescriptions.Add(new SortDescription(nameof(WidgetListItemViewModel.IsClickThrough), ListSortDirection.Descending));
                    Widgets.SortDescriptions.Add(new SortDescription(nameof(WidgetListItemViewModel.OriginalIndex), ListSortDirection.Ascending));
                    break;

                default:
                    Widgets.SortDescriptions.Add(new SortDescription(nameof(WidgetListItemViewModel.OriginalIndex), ListSortDirection.Ascending));
                    break;
            }

            // 並べ替えに使う値だけを見張る(変わったカードだけが動く)。
            Widgets.LiveSortingProperties.Clear();
            foreach (var sortDescription in Widgets.SortDescriptions)
            {
                Widgets.LiveSortingProperties.Add(sortDescription.PropertyName);
            }
        }
    }

    private void StartFavoriteWidgets()
    {
        UpdateWidgetStates(_widgetItems.Where(widget => widget.IsFavorite), WidgetState.Running);
    }

    private void StopAllWidgets()
    {
        UpdateWidgetStates(_widgetItems, WidgetState.Stopped);
    }

    private void PinAllRunningWidgets()
    {
        UpdateWidgetPinStates(
            _widgetItems.Where(widget => widget.State == WidgetState.Running),
            isPinned: true);
    }

    private void UnpinAllWidgets()
    {
        UpdateWidgetPinStates(_widgetItems, isPinned: false);
    }

    /// <summary>ピン留め中のウィジェットを全部クリック透過にする。起動中かどうかは問わない(閉じているものは開いたときに効く)。</summary>
    private void ClickThroughAllPinnedWidgets()
    {
        UpdateWidgetClickThroughStates(
            _widgetItems.Where(widget => widget.IsPinned),
            isClickThrough: true);
    }

    private void ClearAllWidgetClickThrough()
    {
        UpdateWidgetClickThroughStates(_widgetItems, isClickThrough: false);
    }

    private void UpdateWidgetClickThroughStates(IEnumerable<WidgetListItemViewModel> widgets, bool isClickThrough)
    {
        foreach (var widget in widgets)
        {
            widget.IsClickThrough = isClickThrough;
        }
    }

    private void UpdateWidgetStates(IEnumerable<WidgetListItemViewModel> widgets, WidgetState state)
    {
        foreach (var widget in widgets)
        {
            widget.State = state;
        }
    }

    /// <summary>一括のピン留めでは、窓を1枚ずつ前に出さない(<c>bringToFront</c>)。</summary>
    private void UpdateWidgetPinStates(IEnumerable<WidgetListItemViewModel> widgets, bool isPinned)
    {
        _isBulkUpdatingWidgets = true;

        try
        {
            foreach (var widget in widgets)
            {
                widget.IsPinned = isPinned;
            }
        }
        finally
        {
            _isBulkUpdatingWidgets = false;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplySearch();
    }

    partial void OnWidgetSortIndexChanged(int value)
    {
        ApplyWidgetSort();
    }

    partial void OnWidgetActionIndexChanged(int value)
    {
        switch (value)
        {
            case 0:
                StartFavoriteWidgets();
                break;

            case 1:
                StopAllWidgets();
                break;

            case 2:
                PinAllRunningWidgets();
                break;

            case 3:
                UnpinAllWidgets();
                break;

            case 4:
                ClickThroughAllPinnedWidgets();
                break;

            case 5:
                ClearAllWidgetClickThrough();
                break;

            default:
                return;
        }
    }
}
