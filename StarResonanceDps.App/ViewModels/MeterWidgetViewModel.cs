using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class MeterWidgetViewModel : ViewModelBase, IDisposable, IWidgetDataStatusSource
{
    private readonly WidgetListItemViewModel _widget;
    private readonly MeterSnapshotKind _kind;
    private readonly ObservableCollection<MeterPlayerEntry> _entries = [];
    private readonly Dictionary<long, MeterPlayerEntry> _entriesByCharacterId = [];
    private readonly List<MeterPlayerEntry> _orderedEntries = [];
    private readonly DispatcherTimer _refreshTimer;
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly Action<WidgetKind, long> _requestPlayerWindow;

    /// <summary>直前の描き直しで出した行。受信の状態が変わったときの状態の印の計算し直しに使う。</summary>
    private IReadOnlyList<MeterPlayerSnapshot> _shownPlayers = Array.Empty<MeterPlayerSnapshot>();

    /// <summary>直前の描き直しの名前の書式が潜在心相晶(<c>{Psych}</c>)を出すか。</summary>
    private bool _showsSeasonTalent;

    [ObservableProperty]
    private string _elapsedText = "00:00:00";

    [ObservableProperty]
    private string _partyMetricLabel = string.Empty;

    [ObservableProperty]
    private string _partyMetricValueText = string.Empty;

    [ObservableProperty]
    private string _totalLabel = string.Empty;

    [ObservableProperty]
    private string _totalValueText = string.Empty;

    [ObservableProperty]
    private bool _isBenchmarkActive;

    [ObservableProperty]
    private string _benchmarkStatusText = string.Empty;

    /// <summary>ヘッダーのボタンに出す文言。計測中は「計測停止」に変わる。</summary>
    [ObservableProperty]
    private string _benchmarkActionText = string.Empty;

    public MeterWidgetViewModel(
        WidgetListItemViewModel widget,
        MeterSnapshotKind kind,
        Action<WidgetKind, long> requestPlayerWindow)
    {
        _widget = widget;
        _kind = kind;
        _requestPlayerWindow = requestPlayerWindow;
        Entries = new ReadOnlyObservableCollection<MeterPlayerEntry>(_entries);
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _widget.MeterSettingsChanged += Widget_MeterSettingsChanged;
        _configManager.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        Refresh();
        _refreshTimer.Start();
    }

    public WidgetListItemViewModel Widget => _widget;

    public ReadOnlyObservableCollection<MeterPlayerEntry> Entries { get; }

    public WidgetDataStatus DataStatus { get; private set; }

    public event EventHandler? DataStatusChanged;

    /// <summary>HPS メーターだけ、料理と自然回復を数えられない近くの人(名簿の行)も見る。</summary>
    public void RefreshDataStatus()
    {
        var status = WidgetDataStatusRules.ForMeter(
            GameReceptionStateStore.Current,
            EncounterHistoryProvider.SelectedEncounterId is not null,
            EncounterHistoryProvider.HasLoadFailed,
            PartyStateStore.Instance.Current.IsMembershipPending,
            _shownPlayers,
            _showsSeasonTalent,
            _kind == MeterSnapshotKind.Healing ? PlayerRosterStore.Instance.Current.Entries : null);
        if (status == DataStatus)
        {
            return;
        }

        DataStatus = status;
        DataStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _widget.MeterSettingsChanged -= Widget_MeterSettingsChanged;
        _configManager.SettingsPreviewChanged -= ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    public string SkillDetailMenuText => LocalizationManager.Instance.GetString(
        _kind == MeterSnapshotKind.Damage
            ? "Widget_DamageSkillDetail"
            : "Widget_HealingSkillDetail");

    public string SummaryMenuText => LocalizationManager.Instance.GetString(
        _kind == MeterSnapshotKind.Damage
            ? "Widget_DamageSummary"
            : "Widget_HealingSummary");

    public string GraphMenuText => LocalizationManager.Instance.GetString(
        _kind == MeterSnapshotKind.Damage
            ? "Widget_DpsGraph"
            : "Widget_HpsGraph");

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void Widget_MeterSettingsChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(SkillDetailMenuText));
        OnPropertyChanged(nameof(SummaryMenuText));
        OnPropertyChanged(nameof(GraphMenuText));
        Refresh();
    }

    private void Refresh()
    {
        // 計測の完了後も描き直してよい。時計が窓の終わりで止まり、窓の後は記録されない(Core)。
        var benchmarkState = MeterSnapshotProvider.GetBenchmarkState();
        var settings = _widget.MeterSettings;
        var globalSettings = _configManager.CurrentSettings;
        var numberDisplayFormatIndex = globalSettings.NumberDisplayFormatIndex;
        var playerNameDisplayMode = (PlayerNameDisplayMode)globalSettings.PlayerNameDisplayModeIndex;
        var snapshot = MeterSnapshotProvider.GetSnapshot(
            _kind,
            (PartyDisplayMode)settings.PartyDisplayModeIndex);

        ElapsedText = FormatDuration(snapshot.Duration);
        IsBenchmarkActive = benchmarkState.IsActive;
        BenchmarkStatusText = LocalizationManager.Instance.GetString(
            benchmarkState.IsCompleted
                ? "Meter_BenchmarkCompleted"
                : "Meter_BenchmarkInProgress");
        BenchmarkActionText = LocalizationManager.Instance.GetString(
            benchmarkState.IsActive
                ? "Meter_StopBenchmark"
                : "Meter_Benchmark");
        PartyMetricLabel = $"{LocalizationManager.Instance.GetString(_kind == MeterSnapshotKind.Damage ? "Metric_EncounterDps" : "Metric_EncounterHps")}:";
        PartyMetricValueText = MeterNumberFormatter.Format(snapshot.ValuePerSecond, numberDisplayFormatIndex);
        TotalLabel = $"{LocalizationManager.Instance.GetString("Meter_Total")}:";
        TotalValueText = MeterNumberFormatter.Format(snapshot.TotalValue, numberDisplayFormatIndex);

        var activeCharacterIds = snapshot.Players
            .Select(player => player.CharacterId)
            .ToHashSet();

        for (var index = _entries.Count - 1; index >= 0; index--)
        {
            var entry = _entries[index];
            if (activeCharacterIds.Contains(entry.CharacterId))
            {
                continue;
            }

            entry.IsPlayerSelectionMenuOpen = false;
            _entriesByCharacterId.Remove(entry.CharacterId);
            _entries.RemoveAt(index);
        }

        for (var index = 0; index < snapshot.Players.Count; index++)
        {
            var player = snapshot.Players[index];
            if (_entriesByCharacterId.TryGetValue(player.CharacterId, out var entry))
            {
                entry.Update(player, index + 1, settings, _widget.Kind, numberDisplayFormatIndex, playerNameDisplayMode);
            }
            else
            {
                entry = MeterPlayerEntry.Create(player, index + 1, settings, _widget.Kind, numberDisplayFormatIndex, playerNameDisplayMode);
                _entriesByCharacterId.Add(player.CharacterId, entry);
            }

            _orderedEntries.Add(entry);
        }

        CollectionOrder.Apply(_entries, _orderedEntries);
        _orderedEntries.Clear();
        _shownPlayers = snapshot.Players;
        _showsSeasonTalent = PlayerInfoFormatFormatter.ShowsSeasonTalent(settings.PlayerInfoFormatString);
        RefreshDataStatus();
    }

    [RelayCommand]
    private void ToggleBenchmark()
    {
        MeterSnapshotProvider.ToggleBenchmark();
        Refresh();
    }

    [RelayCommand]
    private void ResetEncounter()
    {
        MeterSnapshotProvider.ResetCurrentEncounter();
    }

    [RelayCommand]
    private void RequestSkillDetail(MeterPlayerEntry? player)
    {
        RequestPlayerWindow(
            _kind == MeterSnapshotKind.Damage
                ? WidgetKind.DamageSkillDetail
                : WidgetKind.HealingSkillDetail,
            player);
    }

    [RelayCommand]
    private void RequestSummary(MeterPlayerEntry? player)
    {
        RequestPlayerWindow(
            _kind == MeterSnapshotKind.Damage
                ? WidgetKind.DamageSummary
                : WidgetKind.HealingSummary,
            player);
    }

    [RelayCommand]
    private void RequestGraph(MeterPlayerEntry? player)
    {
        RequestPlayerWindow(
            _kind == MeterSnapshotKind.Damage
                ? WidgetKind.DpsGraph
                : WidgetKind.HpsGraph,
            player);
    }

    [RelayCommand]
    private void RequestPlayerTalent(MeterPlayerEntry? player) => RequestPlayerWindow(WidgetKind.PlayerTalent, player);
    [RelayCommand]
    private void RequestPlayerSeasonTalent(MeterPlayerEntry? player) => RequestPlayerWindow(WidgetKind.PlayerSeasonTalent, player);
    [RelayCommand]
    private void RequestPlayerModule(MeterPlayerEntry? player) => RequestPlayerWindow(WidgetKind.PlayerModule, player);

    private void RequestPlayerWindow(WidgetKind widgetKind, MeterPlayerEntry? player)
    {
        if (player is not null)
        {
            _requestPlayerWindow(widgetKind, player.PlayerId);
        }
    }

    /// <summary>
    /// メーターのタイマーの書式。タイムライン・被ダメログの時刻も同じ書式で出す。
    /// 秒は床関数で丸め、負は符号を付けて出す(接続をまたいで到着の順が前後し、起点より前の時刻になったタイムライン・被ダメログの行。-0.4 秒は -00:00:01)。
    /// </summary>
    internal static string FormatDuration(TimeSpan duration)
    {
        var totalSeconds = (long)Math.Floor(duration.TotalSeconds);
        var sign = totalSeconds < 0 ? "-" : string.Empty;
        var magnitude = Math.Abs(totalSeconds);
        var hours = magnitude / 3600;
        var minutes = magnitude / 60 % 60;
        var seconds = magnitude % 60;

        return hours >= 100
            ? string.Create(CultureInfo.InvariantCulture, $"{sign}{hours:000}:{minutes:00}:{seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{sign}{hours:00}:{minutes:00}:{seconds:00}");
    }
}
