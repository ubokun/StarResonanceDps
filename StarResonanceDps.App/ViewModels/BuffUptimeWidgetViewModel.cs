// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Controls;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// バフ稼働率。1人のバフ(デバフは出さない)を1行ずつ、付いていた区間を戦闘の時計の横軸に並べ、稼働率を出す。
///
/// <para>
/// 稼働率 = 付いていた時間 ÷(戦闘の時計の経過 − その人が周りにいなかった時間)。材料は Core のバフ稼働率の記録(<see cref="MeterSnapshotProvider.GetPlayerBuffUptime"/>)。
/// 横軸(スクロール・最新を追いかける・数字)は推移グラフと同じ決まり(<see cref="MetricTimeAxis"/>・<see cref="MetricTimeAxisScroll"/>)。
/// ただし幅と時間の比は設定「1秒の幅」で固定し、見える秒数はバーの列の幅 ÷ 1秒の幅(窓の幅を変えたらバーを伸ばさず見える範囲を変える)。
/// </para>
///
/// <para>
/// 行の並びは 優先表示リストのオンの行(その並び)→ その回で最初に付いた順。表示設定で常駐バフ(持続が負の実体)と料理・薬剤の行を出すかを決め、非表示リストのバフは出さない。
/// 付与者の名前は出さない。行は番号(料理・薬剤はまとまり)ごとに作り直さず値だけ入れ替える。
/// </para>
/// </summary>
public sealed partial class BuffUptimeWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable, IWidgetWindowHiddenAware
{
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly DispatcherTimer _refreshTimer;
    private readonly ObservableCollection<BuffUptimeEntry> _entries = [];
    private readonly Dictionary<(int BuffId, BuffGroup Group), BuffUptimeEntry> _entriesByKey = [];
    private readonly List<BuffUptimeEntry> _orderedEntries = [];
    private readonly List<(BuffUptimeEntry Entry, int PriorityRank, int FirstOrder)> _sortBuffer = [];
    private readonly HashSet<(int BuffId, BuffGroup Group)> _seenKeys = [];
    private readonly MetricTimeAxisScroll _scroll = new();
    private BuffUptimeWidgetSettingsConfig _settings;

    /// <summary>優先表示リストのオンの行(料理・薬剤のまとまりの行は見ない、バフリストと同じ)の、番号 → 並び。設定が変わったときに作り直す。</summary>
    private Dictionary<int, int> _priorityRanks;
    private Encounter? _encounter;
    private double _totalSeconds;

    /// <summary>バーの列の幅(画面が知らせる)。見える秒数はこれ ÷ 1秒の幅。</summary>
    private double _plotWidth;
    private double _visibleSeconds = 1d;

    /// <summary>手で決めた横軸の位置。範囲が狭まって収めても、範囲が広がればこの位置へ戻す(推移グラフの依存プロパティの強制と同じ)。</summary>
    private double _requestedOffset;

    private double _horizontalOffset;
    private double[] _absentSpans = [];
    private Brush? _barBrush;
    private BuffUptimeEntry? _menuEntry;
    private bool _namesDirty = true;
    private bool _isDisposed;

    public BuffUptimeWidgetViewModel(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer)
        : base(playerWidget, requestedCharacterId)
    {
        _settings = playerWidget.GetBuffUptimeSettingsSnapshot();
        _priorityRanks = CreatePriorityRanks(_settings);
        ApplyVisibleSeconds();
        Entries = new ReadOnlyObservableCollection<BuffUptimeEntry>(_entries);
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _configManager.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        playerWidget.BuffUptimeSettingsChanged += Widget_BuffUptimeSettingsChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        InitializePlayer(initialPlayer);
        Refresh();
        _refreshTimer.Start();
    }

    public ReadOnlyObservableCollection<BuffUptimeEntry> Entries { get; }

    /// <summary>右クリックのメニューが色を引くウィジェット(バフリストと同じ)。</summary>
    public WidgetListItemViewModel Widget => PlayerWidget;

    /// <summary>見える秒数(バーの列の幅 ÷ 1秒の幅)。</summary>
    public double VisibleSeconds => _visibleSeconds;

    /// <summary>見ている窓の始まり(戦闘の秒)。</summary>
    public double HorizontalOffset
    {
        get => _horizontalOffset;
        private set => SetProperty(ref _horizontalOffset, value);
    }

    /// <summary>今の時刻(戦闘の経過の秒、横軸の全体の長さ)。履歴の回は回の終わり。今の時刻の縦線が使う(変わると <see cref="AxisChanged"/>)。</summary>
    public double ElapsedSeconds => _totalSeconds;

    /// <summary>ずらせる秒数。スクロールバーの最大値で、0 ならバーを出さない。</summary>
    public double ScrollableSeconds => _scroll.ScrollableSeconds;

    /// <summary>横軸の数字の間隔。スクロールバーの1段の移動もこれ。</summary>
    public double LabelStepSeconds => MetricTimeAxis.GetLabelStepSeconds(VisibleSeconds);

    /// <summary>その人が周りにいなかった区間(戦闘の経過の秒、[始まり, 終わり] を交互)。中身が変わったときだけ差し替える。</summary>
    public IReadOnlyList<double> AbsentSpans => _absentSpans;

    /// <summary>バーの色。グラフカラーのその人のクラスの色。</summary>
    public Brush? BarBrush
    {
        get => _barBrush;
        private set => SetProperty(ref _barBrush, value);
    }

    /// <summary>横軸(長さ・位置・ずらせる秒数)か、周りにいなかった区間が変わった(スクロールバー・レール・今の時刻の縦線を合わせる合図)。</summary>
    public event EventHandler? AxisChanged;

    /// <summary>右クリックのメニューを開いた行。メニューの文言はこの行の今の状態で決める。</summary>
    public BuffUptimeEntry? MenuEntry
    {
        get => _menuEntry;
        set
        {
            if (SetProperty(ref _menuEntry, value))
            {
                OnPropertyChanged(nameof(PriorityMenuText));
            }
        }
    }

    public string PriorityMenuText => BuffDebuffCardContent.GetPriorityMenuText(
        isDebuff: false,
        MenuEntry is { } entry && IsPrioritized(entry.BuffId));

    public string HideMenuText => LocalizationManager.Instance.GetString("BuffList_Menu_HideBuff");

    /// <summary>メニューを開く直前に文言を今の状態にそろえる。</summary>
    public void RefreshMenuText()
    {
        OnPropertyChanged(nameof(PriorityMenuText));
        OnPropertyChanged(nameof(HideMenuText));
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _configManager.SettingsPreviewChanged -= ConfigManager_SettingsPreviewChanged;
        PlayerWidget.BuffUptimeSettingsChanged -= Widget_BuffUptimeSettingsChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    /// <summary>バーの列の幅が変わった(窓の大きさ・名前の列の幅)。見える秒数を決め直す(バーは伸ばさない)。</summary>
    public void SetPlotWidth(double plotWidth)
    {
        if (_plotWidth == plotWidth)
        {
            return;
        }

        _plotWidth = plotWidth;
        if (ApplyVisibleSeconds())
        {
            AxisChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>横のスクロールバーで動かした。右端にいる間だけ最新を追いかける。</summary>
    public void SetHorizontalOffsetByUser(double horizontalOffset)
    {
        _requestedOffset = _scroll.Clamp(horizontalOffset);
        _scroll.OnOffsetSettled(_requestedOffset);
        if (ApplyHorizontalOffset(_requestedOffset))
        {
            AxisChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        Refresh();
    }

    /// <summary>何を読むかは推移グラフと同じ(戦闘の時計と、その人の素性)。</summary>
    protected override WidgetDataStatus CreateDataStatus(GameReceptionSnapshot reception, PlayerRosterEntry? player)
        => WidgetDataStatusRules.ForPlayerMetric(
            reception,
            EncounterHistoryProvider.SelectedEncounterId is not null,
            EncounterHistoryProvider.HasLoadFailed,
            IsSelfTarget,
            SelectedCharacterId is { } characterId ? MeterSnapshotProvider.GetPlayerIdentity(characterId) : null,
            PartyStateStore.Instance.Current.IsMembershipPending,
            countsSkillLevels: false,
            countsHealingSources: false);

    [RelayCommand]
    private void TogglePriority(BuffUptimeEntry? entry)
    {
        if (entry is null || entry.IsGroup)
        {
            return;
        }

        PlayerWidget.SetBuffUptimePriorityBuffEnabledFromWidget(entry.BuffId, !IsPrioritized(entry.BuffId));
    }

    [RelayCommand]
    private void HideBuff(BuffUptimeEntry? entry)
    {
        if (entry is null || entry.IsGroup)
        {
            return;
        }

        PlayerWidget.HideBuffInBuffUptime(entry.BuffId);
    }

    private bool IsPrioritized(int buffId)
    {
        return _priorityRanks.ContainsKey(buffId);
    }

    /// <summary>
    /// 見える秒数をバーの列の幅 ÷ 1秒の幅で決め直し、位置も決め直す。変わったら true。
    /// 幅がまだ分からない(0)間は 1px 分で数える(バーが無いので描くものは無い)。
    /// </summary>
    private bool ApplyVisibleSeconds()
    {
        var visibleSeconds = Math.Max(_plotWidth, 1d) / _settings.PixelsPerSecond;
        if (_visibleSeconds == visibleSeconds)
        {
            return false;
        }

        _visibleSeconds = visibleSeconds;
        OnPropertyChanged(nameof(VisibleSeconds));
        OnPropertyChanged(nameof(LabelStepSeconds));
        ApplyTotalSeconds(_totalSeconds);
        return true;
    }

    /// <summary>窓が隠れたら定期の読み直しを止め、見えたら1回読み直してから再開する(<see cref="IWidgetWindowHiddenAware"/>)。</summary>
    public void SetWindowHidden(bool isHidden)
    {
        if (isHidden)
        {
            _refreshTimer.Stop();
            return;
        }

        RefreshTimer_Tick(this, EventArgs.Empty);
        _refreshTimer.Start();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
        // 相手の素性・パーティの名簿は名簿の行を変えずに届くことがあるので、読み直すたびに見る。
        RefreshDataStatus();
    }

    /// <summary>全体設定(内部IDの表示など)のプレビュー。行の名前を引き直す。</summary>
    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        _namesDirty = true;
        Refresh();
    }

    private void Widget_BuffUptimeSettingsChanged(object? sender, EventArgs e)
    {
        _settings = PlayerWidget.GetBuffUptimeSettingsSnapshot();
        _priorityRanks = CreatePriorityRanks(_settings);
        if (ApplyVisibleSeconds())
        {
            AxisChanged?.Invoke(this, EventArgs.Empty);
        }

        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        _namesDirty = true;
        Refresh();
    }

    private void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        if (SelectedCharacterId is not { } characterId)
        {
            ApplyEmptyData();
            return;
        }

        var playerIdentity = MeterSnapshotProvider.GetPlayerIdentity(characterId);
        if (playerIdentity is not null)
        {
            SetHeaderText(playerIdentity.Name, playerIdentity.UserId, playerIdentity.IsNpc, playerIdentity.ProfessionId);
            ApplyBarBrush(PlayerProfession.GetKey(playerIdentity.ProfessionId, playerIdentity.ClassSpec));
        }

        if (MeterSnapshotProvider.GetPlayerBuffUptime(characterId) is not { } snapshot)
        {
            ApplyEmptyData();
            return;
        }

        if (!ReferenceEquals(snapshot.Encounter, _encounter))
        {
            // 別の回になった(区切り・履歴)。最新を追いかけ直す。
            _encounter = snapshot.Encounter;
            _scroll.RestartFollowing();
        }

        var axisChanged = ApplyTotalSeconds(snapshot.ElapsedSeconds);
        if (!_absentSpans.AsSpan().SequenceEqual(snapshot.AbsentSpans))
        {
            _absentSpans = snapshot.AbsentSpans;
            axisChanged = true;
        }

        ApplyRows(snapshot);
        _namesDirty = false;

        if (axisChanged)
        {
            AxisChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>何も届いていない(相手が分からない・戦闘の時計が始まっていない)間は本文を空にする。</summary>
    private void ApplyEmptyData()
    {
        if (_entries.Count != 0)
        {
            _entries.Clear();
        }

        _entriesByKey.Clear();
        _encounter = null;
        _scroll.RestartFollowing();
        var axisChanged = ApplyTotalSeconds(0d);
        if (_absentSpans.Length != 0)
        {
            _absentSpans = [];
            axisChanged = true;
        }

        if (axisChanged)
        {
            AxisChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>横軸の全体の長さを置き直し、位置を決め直す(推移グラフの <c>UpdateScrollRange</c> と同じ決まり)。長さ・位置・ずらせる秒数のどれかが変わったら true。</summary>
    private bool ApplyTotalSeconds(double totalSeconds)
    {
        var previousScrollable = _scroll.ScrollableSeconds;
        var totalChanged = _totalSeconds != totalSeconds;
        _totalSeconds = totalSeconds;

        var scrollable = _scroll.SetRange(_totalSeconds, VisibleSeconds);
        double offset;
        if (_scroll.IsFollowingEnd)
        {
            offset = scrollable;
            _requestedOffset = offset;
        }
        else
        {
            offset = _scroll.Clamp(_requestedOffset);
            _scroll.OnOffsetSettled(offset);
        }

        var offsetChanged = ApplyHorizontalOffset(offset);
        if (previousScrollable != scrollable)
        {
            OnPropertyChanged(nameof(ScrollableSeconds));
            return true;
        }

        return offsetChanged || totalChanged;
    }

    private bool ApplyHorizontalOffset(double offset)
    {
        if (HorizontalOffset == offset)
        {
            return false;
        }

        HorizontalOffset = offset;
        return true;
    }

    private void ApplyRows(BuffUptimeSnapshot snapshot)
    {
        var denominator = snapshot.ElapsedSeconds - snapshot.AbsentSeconds;
        var priorityRanks = _priorityRanks;
        var hiddenBuffIds = _settings.HiddenBuffIds;

        _sortBuffer.Clear();
        _seenKeys.Clear();
        foreach (var row in snapshot.Rows)
        {
            if ((row.Group != BuffGroup.None && !_settings.ShowsCuisinePotion)
                || (row.IsPermanent && !_settings.ShowsPermanentBuffs)
                || (row.Group == BuffGroup.None && hiddenBuffIds.Contains(row.BuffId)))
            {
                continue;
            }

            var key = (row.BuffId, row.Group);
            if (!_entriesByKey.TryGetValue(key, out var entry))
            {
                entry = new BuffUptimeEntry(row.BuffId, row.Group);
                entry.Name = ResolveName(entry);
                _entriesByKey.Add(key, entry);
            }
            else if (_namesDirty)
            {
                entry.Name = ResolveName(entry);
            }

            if (!entry.Spans.AsSpan().SequenceEqual(row.Spans))
            {
                entry.Spans = row.Spans;
            }

            entry.RateText = denominator > 0d
                ? PlayerMetricSummaryWidgetViewModel.FormatPercent(row.ActiveSeconds / denominator * 100d)
                : string.Empty;

            _seenKeys.Add(key);
            var rank = row.Group == BuffGroup.None && priorityRanks.TryGetValue(row.BuffId, out var priorityRank)
                ? priorityRank
                : int.MaxValue;
            _sortBuffer.Add((entry, rank, row.FirstOrder));
        }

        // 回が替わって出なくなった行は手放す(行は番号ごとに使い回すので、残すと回をまたいで増え続ける)。
        if (_entriesByKey.Count != _seenKeys.Count)
        {
            foreach (var key in _entriesByKey.Keys.Where(key => !_seenKeys.Contains(key)).ToArray())
            {
                _entriesByKey.Remove(key);
            }
        }

        _sortBuffer.Sort(static (left, right) =>
        {
            var byPriority = left.PriorityRank.CompareTo(right.PriorityRank);
            return byPriority != 0 ? byPriority : left.FirstOrder.CompareTo(right.FirstOrder);
        });

        _orderedEntries.Clear();
        foreach (var (entry, _, _) in _sortBuffer)
        {
            _orderedEntries.Add(entry);
        }

        CollectionOrder.Apply(_entries, _orderedEntries);
        _orderedEntries.Clear();
        _sortBuffer.Clear();
    }

    private static Dictionary<int, int> CreatePriorityRanks(BuffUptimeWidgetSettingsConfig settings)
    {
        var ranks = new Dictionary<int, int>();
        if (settings.PriorityBuffs is not { } priorityBuffs)
        {
            return ranks;
        }

        foreach (var entry in priorityBuffs)
        {
            if (entry.IsEnabled && entry.Group == BuffGroup.None)
            {
                ranks.TryAdd(entry.BuffId, ranks.Count);
            }
        }

        return ranks;
    }

    private static string ResolveName(BuffUptimeEntry entry)
    {
        return entry.IsGroup
            ? BuffDebuffCardContent.GetGroupName(entry.Group)
            : CombatDataCatalog.GetBuffName(entry.BuffId);
    }

    /// <summary>バーの色。グラフカラーのクラスの鍵の選んでいる色を不透明にしたもの。同じ色なら差し替えない。</summary>
    private void ApplyBarBrush(string classKey)
    {
        var color = PlayerMetricWidgetViewModel.ResolveGraphColor(_settings.ClassColorIndexes, _settings.ClassColorPalettes, classKey);
        if (BarBrush is SolidColorBrush current && current.Color == color)
        {
            return;
        }

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        BarBrush = brush;
    }
}
