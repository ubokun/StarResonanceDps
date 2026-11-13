// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// 与ダメ詳細・ヒール詳細・被ダメ詳細。上に合計・通常・会心・幸運の表、下に属性と物理魔法の割合(種類ごとのバーの行)。
///
/// <para>
/// 窓の高さを中身が全部入る高さに合わせる(<c>Behaviors.AutoFitHeightBehavior</c>)。
/// 縦スクロールはウィンドウ枠側の細いスクロールバーに繋ぎ、出るのは手で高さを小さくしたときだけ(ステータス詳細と同じ)。
/// </para>
/// </summary>
public partial class PlayerMetricSummaryWidgetView : UserControl, IWidgetVerticalScrollContent
{
    public PlayerMetricSummaryWidgetView()
    {
        InitializeComponent();
    }

    public event EventHandler? VerticalScrollMetricsChanged;

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(SummaryScrollViewer.ScrollableHeight, 0d);
        var viewport = Math.Max(SummaryScrollViewer.ViewportHeight, 0d);
        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(SummaryScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9d, 1d),
            // 表の行1つぶん。行の高さ(22)と合わせる。
            22d);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(SummaryScrollViewer.ScrollableHeight, 0d);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0d, maximum)
            : 0d;
        SummaryScrollViewer.ScrollToVerticalOffset(offset);
    }

    private void PlayerMetricSummaryWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerMetricSummaryWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerMetricSummaryWidgetView_DataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void SummaryScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
