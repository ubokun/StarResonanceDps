// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// アビリティ情報(タレントの木)。縦横のスクロールはウィンドウ枠側の細いスクロールバーに繋ぐ。
/// </summary>
public partial class PlayerTalentTreeWidgetView : UserControl, IWidgetVerticalScrollContent, IWidgetHorizontalScrollContent
{
    public PlayerTalentTreeWidgetView()
    {
        InitializeComponent();
    }

    public event EventHandler? VerticalScrollMetricsChanged;

    public event EventHandler? HorizontalScrollMetricsChanged;

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(TreeScrollViewer.ScrollableHeight, 0);
        var viewport = Math.Max(TreeScrollViewer.ViewportHeight, 0);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(TreeScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9, 1),
            42);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(TreeScrollViewer.ScrollableHeight, 0);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0, maximum)
            : 0;

        TreeScrollViewer.ScrollToVerticalOffset(offset);
    }

    public WidgetHorizontalScrollMetrics GetHorizontalScrollMetrics()
    {
        var maximum = Math.Max(TreeScrollViewer.ScrollableWidth, 0);
        var viewport = Math.Max(TreeScrollViewer.ViewportWidth, 0);

        return new WidgetHorizontalScrollMetrics(
            maximum,
            viewport,
            Math.Min(TreeScrollViewer.HorizontalOffset, maximum),
            Math.Max(viewport * 0.9, 1),
            42);
    }

    public void SetHorizontalScrollOffset(double horizontalOffset)
    {
        var maximum = Math.Max(TreeScrollViewer.ScrollableWidth, 0);
        var offset = double.IsFinite(horizontalOffset)
            ? Math.Clamp(horizontalOffset, 0, maximum)
            : 0;

        TreeScrollViewer.ScrollToHorizontalOffset(offset);
    }

    private void PlayerTalentTreeWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        NotifyScrollMetricsChanged();
    }

    private void PlayerTalentTreeWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyScrollMetricsChanged();
    }

    private void PlayerTalentTreeWidgetView_DataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        NotifyScrollMetricsChanged();
    }

    private void TreeScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyScrollMetricsChanged();
    }

    private void NotifyScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
        HorizontalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
