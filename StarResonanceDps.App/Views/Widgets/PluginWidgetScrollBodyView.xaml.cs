// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// プラグインのウィジェットの本体を、窓の枠の層のバーでスクロールする置き場(メーターなど本体のリストと同じ動き)。余白は付けない。
/// </summary>
public partial class PluginWidgetScrollBodyView : UserControl, IWidgetVerticalScrollContent, IPluginWidgetElementHost
{
    private const double SmallChange = 31d;

    /// <param name="element">中身。空の窓は null で、後から <see cref="SetElement"/> で入れる。</param>
    public PluginWidgetScrollBodyView(FrameworkElement? element)
    {
        InitializeComponent();
        ElementHost.Content = element;
        Loaded += PluginWidgetScrollBodyView_Loaded;
        SizeChanged += PluginWidgetScrollBodyView_SizeChanged;
    }

    public void SetElement(FrameworkElement element)
    {
        ElementHost.Content = element;
    }

    public event EventHandler? VerticalScrollMetricsChanged;

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(BodyScrollViewer.ScrollableHeight, 0d);
        var viewport = Math.Max(BodyScrollViewer.ViewportHeight, 0d);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(BodyScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9d, 1d),
            SmallChange);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(BodyScrollViewer.ScrollableHeight, 0d);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0d, maximum)
            : 0d;

        BodyScrollViewer.ScrollToVerticalOffset(offset);
    }

    private void PluginWidgetScrollBodyView_Loaded(object sender, RoutedEventArgs e)
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PluginWidgetScrollBodyView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BodyScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
