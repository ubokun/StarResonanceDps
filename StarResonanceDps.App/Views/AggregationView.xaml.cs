// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Views;

/// <summary>
/// 集計タブ。保存済みエンカウンターの一覧と、現在の戦闘への操作(計測・リセット)。
/// </summary>
public partial class AggregationView : UserControl
{
    private readonly AggregationViewModel _viewModel = new();
    private readonly DispatcherTimer _benchmarkTimer;

    private bool _isSubscribed;
    private bool _needsReload = true;

    internal ScrollViewer ContentScrollViewer => HistoryScrollViewer;

    internal void FilterUser(string server, long characterId, string name) => _viewModel.FilterUser(server, characterId, name);

    public AggregationView()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.LoadFailed += ViewModel_LoadFailed;

        // 計測の状態は Core が通知を出さない。メーターウィジェットと
        // 同じく定期的に読み直す。動かすのはこのタブが見えている間だけ。
        _benchmarkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _benchmarkTimer.Tick += BenchmarkTimer_Tick;

        Loaded += AggregationView_Loaded;
        Unloaded += AggregationView_Unloaded;
        IsVisibleChanged += AggregationView_IsVisibleChanged;
    }

    private void AggregationView_Loaded(object sender, RoutedEventArgs e)
    {
        // 起動できないときに見せるだけのメイン窓では Core が初期化されていない(DB も開いていない)。
        // EncounterManager に触ると静的コンストラクタが DB に書きに行って落ちるので、購読もタイマーも始めない。
        if (_isSubscribed || !CombatRuntimeHost.Instance.IsInitialized)
        {
            return;
        }

        // 前のエンカウンターが DB に入るのは EnterDungeon の中で、EncounterStart は
        // その直後に上がる。つまりこれが「1件増えた」の合図。
        EncounterManager.EncounterStart += EncounterManager_EncounterStart;

        // 選択はこのタブの外からも変わる(戦闘とエンカウンターの作り直しによる自動解除)。
        // 購読しないと「表示中」が残る。
        EncounterHistoryProvider.SelectionChanged += EncounterHistoryProvider_SelectionChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        // 内部IDの表示切り替えは行名(シーン名)に効くのに、一覧を組み直す合図が
        // EncounterStart しか無く、言語切替か再起動まで古い文字列が残っていた。
        // プレビューでも保存でも同じイベントが上がるので購読はこれ1つでよい。
        ConfigManager.Instance.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        _isSubscribed = true;

        UpdateActiveState();
    }

    private void AggregationView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!_isSubscribed)
        {
            return;
        }

        EncounterManager.EncounterStart -= EncounterManager_EncounterStart;
        EncounterHistoryProvider.SelectionChanged -= EncounterHistoryProvider_SelectionChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
        ConfigManager.Instance.SettingsPreviewChanged -= ConfigManager_SettingsPreviewChanged;
        _isSubscribed = false;

        _benchmarkTimer.Stop();
    }

    private void AggregationView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateActiveState();
    }

    private void UpdateActiveState()
    {
        if (!CombatRuntimeHost.Instance.IsInitialized)
        {
            return;
        }

        if (!IsVisible)
        {
            _benchmarkTimer.Stop();
            return;
        }

        if (_needsReload)
        {
            _needsReload = false;
            _viewModel.Reload();
        }

        _viewModel.SyncSelection();
        _viewModel.RefreshBenchmarkState();
        _benchmarkTimer.Start();
    }

    private void BenchmarkTimer_Tick(object? sender, EventArgs e)
    {
        _viewModel.RefreshBenchmarkState();
    }

    /// <summary>
    /// パケット処理スレッドから上がる。UIスレッドへ渡してから読み直す。
    /// 隠れている間は印だけ付けて、次に表示されたときにまとめて読む。
    /// </summary>
    private void EncounterManager_EncounterStart(EncounterStartEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(MarkHistoryChanged, DispatcherPriority.Background);
            return;
        }

        MarkHistoryChanged();
    }

    private void MarkHistoryChanged()
    {
        _needsReload = true;

        if (!IsVisible)
        {
            return;
        }

        _needsReload = false;
        _viewModel.Reload();
    }

    /// <summary>
    /// パケットのスレッドから上がる(キャプチャが止まっていれば読み込みのスレッドのこともある)ので、UIスレッドへ渡し直す。
    /// </summary>
    private void EncounterHistoryProvider_SelectionChanged()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(_viewModel.SyncSelection);
            return;
        }

        _viewModel.SyncSelection();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        _viewModel.RefreshTexts();
    }

    private void ViewModel_LoadFailed(object? sender, EventArgs e)
    {
        HistoryLoadFailureMessage.Show(Window.GetWindow(this));
    }

    /// <summary>
    /// 設定のプレビュー適用と保存。<b>内部IDの表示切り替えを言語切替と同じ扱いで即反映させる。</b>
    /// シーン名は <c>CombatDataCatalog.GetSceneName</c> が表示時に組み立てているので、
    /// 値は正しく変わる。足りていなかったのは再通知のほう。
    /// </summary>
    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(_viewModel.RefreshTexts);
            return;
        }

        _viewModel.RefreshTexts();
    }
}
