// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StarResonanceDps.App.Controls;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// バフ稼働率。縦のスクロールバーと横のスクロールバーは窓の枠の層の1本ずつ(<see cref="IWidgetVerticalScrollContent"/>・<see cref="IWidgetHorizontalScrollContent"/>)、
/// 行のレールと今の時刻の縦線は枠の層(<see cref="WidgetWindow.FrameOverlayHost"/>)へ差し込む。
///
/// <para>
/// ホイールは一覧を縦に流す(一覧のテンプレートの <see cref="ScrollViewer"/> が受ける)。横軸の縮尺は設定「1秒の幅」だけで変え、
/// 見える秒数はバーの列の幅から VM が決める(<see cref="BuffUptimeWidgetViewModel.SetPlotWidth"/>)。
/// 一覧は仮想化して見えている行だけを作るので、名前と稼働率の列の幅(SharedSizeGroup)は見えている行で決まり、スクロールで変わる。
/// </para>
///
/// <para>
/// 行の右クリックはメニュー(優先表示にする/やめる・非表示)。料理・薬剤のまとまりの行は優先表示・非表示の対象にならないので開かない。
/// 行の部品は別の行に使い回すので、メニューを開いた行の部品が外されるときはメニューを閉じる。
/// </para>
/// </summary>
public partial class BuffUptimeWidgetView : UserControl, IWidgetVerticalScrollContent, IWidgetHorizontalScrollContent, IBuffUptimeRailSource
{
    /// <summary>行の作りの中のバーの名前。</summary>
    private const string UptimeBarName = "UptimeBar";

    private BuffUptimeRailLayer? _railLayer;
    private WidgetWindow? _frameOverlayOwner;
    private BuffUptimeWidgetViewModel? _viewModel;
    private ScrollViewer? _scrollViewer;
    private ContextMenu? _openMenu;
    private BuffUptimeEntry? _openMenuEntry;

    /// <summary>最後に知った、バーの列の幅。VM が入れ替わったときにも渡す。</summary>
    private double _plotWidth;

    public BuffUptimeWidgetView()
    {
        InitializeComponent();
    }

    public event EventHandler? VerticalScrollMetricsChanged;

    public event EventHandler? HorizontalScrollMetricsChanged;

    public FrameworkElement? RailViewport => _scrollViewer;

    public double HorizontalOffset => _viewModel?.HorizontalOffset ?? 0d;

    public double VisibleSeconds => _viewModel?.VisibleSeconds ?? 1d;

    public double ElapsedSeconds => _viewModel?.ElapsedSeconds ?? 0d;

    public IReadOnlyList<double> AbsentSpans => _viewModel?.AbsentSpans ?? Array.Empty<double>();

    public IEnumerable<FrameworkElement> GetRailElements()
    {
        var generator = RowsItemsControl.ItemContainerGenerator;
        for (var index = 0; index < RowsItemsControl.Items.Count; index++)
        {
            if (generator.ContainerFromIndex(index) is ContentPresenter presenter
                && presenter.ContentTemplate?.FindName(UptimeBarName, presenter) is FrameworkElement bar)
            {
                yield return bar;
            }
        }
    }

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        if (_scrollViewer is null)
        {
            return new WidgetVerticalScrollMetrics(0, 0, 0, 1, 31);
        }

        var maximum = Math.Max(_scrollViewer.ScrollableHeight, 0);
        var viewport = Math.Max(_scrollViewer.ViewportHeight, 0);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(_scrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9, 1),
            31);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        if (_scrollViewer is null)
        {
            return;
        }

        var maximum = Math.Max(_scrollViewer.ScrollableHeight, 0);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0, maximum)
            : 0;

        _scrollViewer.ScrollToVerticalOffset(offset);
    }

    public WidgetHorizontalScrollMetrics GetHorizontalScrollMetrics()
    {
        if (_viewModel is not { } viewModel)
        {
            return new WidgetHorizontalScrollMetrics(0d, 1d, 0d, 1d, 1d);
        }

        return new WidgetHorizontalScrollMetrics(
            viewModel.ScrollableSeconds,
            viewModel.VisibleSeconds,
            viewModel.HorizontalOffset,
            viewModel.VisibleSeconds,
            viewModel.LabelStepSeconds);
    }

    public void SetHorizontalScrollOffset(double horizontalOffset)
    {
        _viewModel?.SetHorizontalOffsetByUser(horizontalOffset);
    }

    private void AxisLabels_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _plotWidth = e.NewSize.Width;
        _viewModel?.SetPlotWidth(_plotWidth);
    }

    private void BuffUptimeWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_scrollViewer is null)
        {
            RowsItemsControl.ApplyTemplate();
            _scrollViewer = (ScrollViewer)RowsItemsControl.Template.FindName("RowsScrollViewer", RowsItemsControl);
            _scrollViewer.ScrollChanged += RowsScrollViewer_ScrollChanged;
        }

        AttachRailLayer();
        NotifyVerticalScrollMetricsChanged();
        HorizontalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BuffUptimeWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachRailLayer();
    }

    private void BuffUptimeWidgetView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.AxisChanged -= ViewModel_AxisChanged;
        }

        _viewModel = DataContext as BuffUptimeWidgetViewModel;
        if (_viewModel is not null)
        {
            _viewModel.AxisChanged += ViewModel_AxisChanged;
            _viewModel.SetPlotWidth(_plotWidth);
        }

        _railLayer?.InvalidateVisual();
        HorizontalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ViewModel_AxisChanged(object? sender, EventArgs e)
    {
        _railLayer?.InvalidateVisual();
        HorizontalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RowsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>行の部品が別の行に使い回される(仮想化)。メニューを開いた行なら閉じる(被ダメログと同じ)。</summary>
    private void RowsItemsControl_CleanUpVirtualizedItem(object sender, CleanUpVirtualizedItemEventArgs e)
    {
        if (_openMenu is { IsOpen: true } menu && ReferenceEquals(e.Value, _openMenuEntry))
        {
            menu.IsOpen = false;
        }
    }

    private void Row_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is not FrameworkElement row
            || row.ContextMenu is not ContextMenu menu
            || row.DataContext is not BuffUptimeEntry entry
            || entry.IsGroup
            || _viewModel is not { } viewModel)
        {
            return;
        }

        viewModel.MenuEntry = entry;
        viewModel.RefreshMenuText();
        _openMenu = menu;
        _openMenuEntry = entry;
        menu.PlacementTarget = row;
        menu.IsOpen = true;
    }

    private void AttachRailLayer()
    {
        if (_railLayer is not null)
        {
            return;
        }

        if (Window.GetWindow(this) is not WidgetWindow owner
            || TryFindResource("RailLayer") is not BuffUptimeRailLayer railLayer)
        {
            return;
        }

        _railLayer = railLayer;
        _frameOverlayOwner = owner;
        railLayer.Source = this;
        owner.FrameOverlayHost.Content = railLayer;
    }

    private void DetachRailLayer()
    {
        if (_frameOverlayOwner is not null
            && ReferenceEquals(_frameOverlayOwner.FrameOverlayHost.Content, _railLayer))
        {
            _frameOverlayOwner.FrameOverlayHost.Content = null;
        }

        if (_railLayer is not null)
        {
            _railLayer.Source = null;
        }

        _railLayer = null;
        _frameOverlayOwner = null;
    }
}
