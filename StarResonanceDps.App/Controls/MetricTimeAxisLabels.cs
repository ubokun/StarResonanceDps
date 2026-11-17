// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StarResonanceDps.App.Controls;

/// <summary>
/// 横軸の数字(M:SS)の行。数字の間隔と置く秒は推移グラフと同じ(<see cref="MetricTimeAxis"/>)で、数字の中心をその秒の位置に置く。
/// 端の数字は要素の外へはみ出す(推移グラフの右端の数字と同じ)。文字はウィジェットの窓の文字のスタイル。
/// </summary>
public sealed class MetricTimeAxisLabels : FrameworkElement
{
    public static readonly DependencyProperty HorizontalOffsetProperty = DependencyProperty.Register(
        nameof(HorizontalOffset),
        typeof(double),
        typeof(MetricTimeAxisLabels),
        new FrameworkPropertyMetadata(0d, OnAxisChanged));

    public static readonly DependencyProperty VisibleSecondsProperty = DependencyProperty.Register(
        nameof(VisibleSeconds),
        typeof(double),
        typeof(MetricTimeAxisLabels),
        new FrameworkPropertyMetadata(1d, OnAxisChanged),
        static value => value is double seconds && seconds > 0d && !double.IsInfinity(seconds));

    private readonly VisualCollection _children;
    private readonly TextBlock[] _labels;
    private readonly List<double> _labelSeconds = [];

    public MetricTimeAxisLabels()
    {
        _children = new VisualCollection(this);
        _labels = new TextBlock[MetricTimeAxis.MaxLabelIntervals + 1];
        for (var index = 0; index < _labels.Length; index++)
        {
            var label = new TextBlock { IsHitTestVisible = false };
            label.SetResourceReference(StyleProperty, "Text.WidgetWindow");
            _children.Add(label);
            _labels[index] = label;
        }

        UpdateLabels();
    }

    public double HorizontalOffset
    {
        get => (double)GetValue(HorizontalOffsetProperty);
        set => SetValue(HorizontalOffsetProperty, value);
    }

    public double VisibleSeconds
    {
        get => (double)GetValue(VisibleSecondsProperty);
        set => SetValue(VisibleSecondsProperty, value);
    }

    protected override int VisualChildrenCount => _children.Count;

    protected override Visual GetVisualChild(int index) => _children[index];

    protected override Size MeasureOverride(Size availableSize)
    {
        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);
        var height = 0d;
        foreach (var label in _labels)
        {
            label.Measure(unbounded);
            height = Math.Max(height, label.DesiredSize.Height);
        }

        return new Size(0d, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var index = 0; index < _labels.Length; index++)
        {
            var label = _labels[index];
            if (index >= _labelSeconds.Count)
            {
                label.Arrange(new Rect());
                continue;
            }

            var x = MetricTimeAxis.ToX(0d, finalSize.Width, HorizontalOffset, VisibleSeconds, _labelSeconds[index]);
            label.Arrange(new Rect(new Point(x - label.DesiredSize.Width / 2d, 0d), label.DesiredSize));
        }

        return finalSize;
    }

    private static void OnAxisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MetricTimeAxisLabels)d).UpdateLabels();
    }

    private void UpdateLabels()
    {
        MetricTimeAxis.FillLabelSeconds(HorizontalOffset, VisibleSeconds, _labelSeconds, _labels.Length);
        for (var index = 0; index < _labels.Length; index++)
        {
            var label = _labels[index];
            if (index < _labelSeconds.Count)
            {
                label.Text = MetricTimeAxis.FormatSeconds(_labelSeconds[index]);
                label.Visibility = Visibility.Visible;
            }
            else
            {
                label.Visibility = Visibility.Collapsed;
            }
        }

        InvalidateMeasure();
        InvalidateArrange();
    }
}
