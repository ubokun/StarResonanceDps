// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using StarResonanceDps.App.Behaviors;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.Core.Logging;

namespace StarResonanceDps.App.Views;

/// <summary>
/// ログタブ。見えている間だけ行を読み直す。隠れている間に来た通知は印だけ付けて、次に見えたときに1回読む。
///
/// <para>
/// <b>末尾に居るときだけ追記に追従する</b>(タイムライン・チャットと同じ)。途中を読んでいる間に行が増えても位置は動かさない。
/// 追従するかは、利用者のスクロール(内容の高さが変わっていない回)で決める。
/// </para>
///
/// <para>
/// 文字は行をまたいで選べ、選んだ文字は右クリックの「コピー」(窓がアクティブなら Ctrl+C も)でクリップボードへ入れる。
/// 選ぶ・コピーするのは <see cref="TextRowsSelectionBehavior"/>、文字にするのは <see cref="LogsViewModel.CreateCopyText"/>。
/// </para>
/// </summary>
public partial class LogsView : UserControl
{
    private readonly PacketDiagnosticLogStore _diagnosticLog = PacketDiagnosticLogStore.Instance;
    private readonly LogsViewModel _viewModel = new();
    private readonly ScrollViewer _scrollViewer;
    private bool _isSubscribed;
    private bool _followsTail = true;

    /// <summary>画面のスレッドへの読み直しを予約済みか(0 / 1)。通知が続けて来ても予約は1回にまとめる。</summary>
    private int _isRefreshQueued;

    /// <summary>まだ読んでいない通知がある。画面のスレッドだけが触る。</summary>
    private bool _needsRefresh = true;

    /// <summary>メイン画面の外のバーが追いかけるスクロール。メイン画面が作られるときに要るので、テンプレートはここで当てる。</summary>
    internal ScrollViewer ContentScrollViewer => _scrollViewer;

    public LogsView()
    {
        InitializeComponent();
        DataContext = _viewModel;

        LogItemsControl.ApplyTemplate();
        _scrollViewer = (ScrollViewer)LogItemsControl.Template.FindName("LogScrollViewer", LogItemsControl);
        _scrollViewer.ScrollChanged += LogScrollViewer_ScrollChanged;

        Loaded += LogsView_Loaded;
        Unloaded += LogsView_Unloaded;
        IsVisibleChanged += LogsView_IsVisibleChanged;
    }

    private void LogsView_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_isSubscribed)
        {
            _diagnosticLog.EntriesChanged += DiagnosticLog_EntriesChanged;
            _isSubscribed = true;
        }

        // 外れていた間の通知は受けていないので、見えていれば読み直す。
        _needsRefresh = true;
        RefreshIfNeeded();
    }

    private void LogsView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!_isSubscribed)
        {
            return;
        }

        _diagnosticLog.EntriesChanged -= DiagnosticLog_EntriesChanged;
        _isSubscribed = false;
    }

    private void LogsView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        RefreshIfNeeded();
    }

    /// <summary>
    /// ほかのスレッドからも続けて来る。予約が残っている間の通知は、その予約の読み直しに含まれる
    /// (読むのは読んだ時点の全部)。
    /// </summary>
    private void DiagnosticLog_EntriesChanged(object? sender, EventArgs e)
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
        _viewModel.Refresh(_diagnosticLog.Snapshot);
    }

    private void LogScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeightChange == 0)
        {
            _followsTail = _scrollViewer.VerticalOffset >= _scrollViewer.ScrollableHeight - 1;
        }
        else if (_followsTail)
        {
            _scrollViewer.ScrollToEnd();
        }
    }
}
