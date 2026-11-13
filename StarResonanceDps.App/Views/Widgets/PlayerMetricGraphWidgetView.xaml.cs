// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StarResonanceDps.App.Controls;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// 推移グラフ。横のスクロールバーは窓の枠の層の1本(<see cref="IWidgetHorizontalScrollContent"/>)、
/// 格子と軸の線は枠の層(<see cref="WidgetWindow.FrameOverlayHost"/>)へ差し込む。どちらも分割線と同じく窓の不透明度で合成される。
/// 窓の上のホイールは横軸の長さを変える(<see cref="IWidgetMouseWheelContent"/>)。
/// </summary>
public partial class PlayerMetricGraphWidgetView : UserControl, IWidgetHorizontalScrollContent, IWidgetMouseWheelContent
{
    private MetricGraphGridLayer? _gridLayer;
    private WidgetWindow? _frameOverlayOwner;

    /// <summary>1回分(<see cref="Mouse.MouseWheelDeltaForOneLine"/>)に満たないホイールの量。細かく回るタッチパッドの分を溜める。</summary>
    private int _pendingWheelDelta;

    public PlayerMetricGraphWidgetView()
    {
        InitializeComponent();
        GraphChart.ScrollMetricsChanged += GraphChart_ScrollMetricsChanged;
        Loaded += PlayerMetricGraphWidgetView_Loaded;
        Unloaded += PlayerMetricGraphWidgetView_Unloaded;
    }

    public event EventHandler? HorizontalScrollMetricsChanged;

    public WidgetHorizontalScrollMetrics GetHorizontalScrollMetrics()
    {
        return new WidgetHorizontalScrollMetrics(
            GraphChart.ScrollableSeconds,
            GraphChart.VisibleSeconds,
            GraphChart.HorizontalOffset,
            GraphChart.VisibleSeconds,
            GraphChart.GridStepSeconds);
    }

    public void SetHorizontalScrollOffset(double horizontalOffset)
    {
        GraphChart.HorizontalOffset = horizontalOffset;
    }

    public bool HandleWidgetMouseWheel(int delta)
    {
        if (DataContext is not PlayerMetricWidgetViewModel viewModel)
        {
            return false;
        }

        _pendingWheelDelta += delta;
        var notches = _pendingWheelDelta / Mouse.MouseWheelDeltaForOneLine;
        if (notches != 0)
        {
            _pendingWheelDelta -= notches * Mouse.MouseWheelDeltaForOneLine;
            viewModel.ChangeGraphVisibleSecondsByWheel(notches);
        }

        return true;
    }

    private void GraphChart_ScrollMetricsChanged(object? sender, EventArgs e)
    {
        HorizontalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PlayerMetricGraphWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        AttachGridLayer();
    }

    private void PlayerMetricGraphWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachGridLayer();
    }

    private void AttachGridLayer()
    {
        if (_gridLayer is not null)
        {
            return;
        }

        if (Window.GetWindow(this) is not WidgetWindow owner
            || TryFindResource("GridLayer") is not MetricGraphGridLayer gridLayer)
        {
            return;
        }

        _gridLayer = gridLayer;
        _frameOverlayOwner = owner;
        gridLayer.Chart = GraphChart;
        owner.FrameOverlayHost.Content = gridLayer;
    }

    private void DetachGridLayer()
    {
        if (_frameOverlayOwner is not null
            && ReferenceEquals(_frameOverlayOwner.FrameOverlayHost.Content, _gridLayer))
        {
            _frameOverlayOwner.FrameOverlayHost.Content = null;
        }

        if (_gridLayer is not null)
        {
            _gridLayer.Chart = null;
        }

        _gridLayer = null;
        _frameOverlayOwner = null;
    }
}
