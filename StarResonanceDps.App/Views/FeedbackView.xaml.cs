// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.Core.Diagnostics;

namespace StarResonanceDps.App.Views;

/// <summary>フィードバックタブ。見えている間だけ行を読み直す。隠れている間に来た通知は印だけ付けて、次に見えたときに1回読む。</summary>
public partial class FeedbackView : UserControl
{
    private readonly FeedbackViewModel _viewModel = new();
    private bool _isSubscribed;

    /// <summary>画面のスレッドへの読み直しを予約済みか(0 / 1)。通知が続けて来ても予約は1回にまとめる。</summary>
    private int _isRefreshQueued;

    /// <summary>まだ読んでいない通知がある。画面のスレッドだけが触る。</summary>
    private bool _needsRefresh = true;

    public FeedbackView()
    {
        InitializeComponent();
        DataContext = _viewModel;

        Loaded += FeedbackView_Loaded;
        Unloaded += FeedbackView_Unloaded;
        IsVisibleChanged += FeedbackView_IsVisibleChanged;
    }

    private void FeedbackView_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_isSubscribed)
        {
            ResidentDetectionStore.Changed += ResidentDetectionStore_Changed;
            LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
            _isSubscribed = true;
        }

        // 外れていた間の通知は受けていないので、見えていれば読み直す。
        _viewModel.RefreshTexts();
        _needsRefresh = true;
        RefreshIfNeeded();
    }

    private void FeedbackView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!_isSubscribed)
        {
            return;
        }

        ResidentDetectionStore.Changed -= ResidentDetectionStore_Changed;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
        _isSubscribed = false;
    }

    private void FeedbackView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        RefreshIfNeeded();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        _viewModel.RefreshTexts();
    }

    /// <summary>検知したスレッドから来る。予約が残っている間の通知は、その予約の読み直しに含まれる。</summary>
    private void ResidentDetectionStore_Changed(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _isRefreshQueued, 1) == 1)
        {
            return;
        }

        Dispatcher.BeginInvoke(ApplyQueuedRefresh, DispatcherPriority.Background);
    }

    private void ApplyQueuedRefresh()
    {
        // 先に下ろす。読んでいる間に来た通知は次の予約になる。
        Interlocked.Exchange(ref _isRefreshQueued, 0);
        _needsRefresh = true;
        RefreshIfNeeded();
    }

    private void RefreshIfNeeded()
    {
        if (!_needsRefresh || !IsVisible)
        {
            return;
        }

        _needsRefresh = false;
        _viewModel.Refresh();
    }
}
