// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Media;

namespace StarResonanceDps.App.Controls;

/// <summary>
/// バフ稼働率の1行のバー。付いていた区間を、見ている窓(<see cref="HorizontalOffset"/> から <see cref="VisibleSeconds"/> 秒)の中だけ塗る。
/// 横の位置の決まりは推移グラフと同じ(<see cref="MetricTimeAxis.ToX"/>)。高さは画面の作りが決める。
/// </summary>
public sealed class BuffUptimeBar : FrameworkElement
{
    /// <summary>付いていた区間。戦闘の経過の秒で [始まり, 終わり] を交互に並べ、始まりの昇順。</summary>
    public static readonly DependencyProperty SpansProperty = DependencyProperty.Register(
        nameof(Spans),
        typeof(IReadOnlyList<double>),
        typeof(BuffUptimeBar),
        new FrameworkPropertyMetadata(Array.Empty<double>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HorizontalOffsetProperty = DependencyProperty.Register(
        nameof(HorizontalOffset),
        typeof(double),
        typeof(BuffUptimeBar),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VisibleSecondsProperty = DependencyProperty.Register(
        nameof(VisibleSeconds),
        typeof(double),
        typeof(BuffUptimeBar),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender),
        static value => value is double seconds && seconds > 0d && !double.IsInfinity(seconds));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill),
        typeof(Brush),
        typeof(BuffUptimeBar),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double> Spans
    {
        get => (IReadOnlyList<double>)GetValue(SpansProperty);
        set => SetValue(SpansProperty, value);
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

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        if (Fill is not { } fill || Spans is not { Count: > 1 } spans || width <= 0d || height <= 0d)
        {
            return;
        }

        for (var index = 0; index + 1 < spans.Count; index += 2)
        {
            var left = Math.Max(MetricTimeAxis.ToX(0d, width, HorizontalOffset, VisibleSeconds, spans[index]), 0d);
            var right = Math.Min(MetricTimeAxis.ToX(0d, width, HorizontalOffset, VisibleSeconds, spans[index + 1]), width);
            if (right > left)
            {
                drawingContext.DrawRectangle(fill, null, new Rect(left, 0d, right - left, height));
            }
        }
    }
}
