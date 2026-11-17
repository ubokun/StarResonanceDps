// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Media;

namespace StarResonanceDps.App.Controls;

/// <summary>バフ稼働率のレールと今の時刻の縦線を引く相手(ビュー)。</summary>
public interface IBuffUptimeRailSource
{
    /// <summary>行のバー(できている分)。レールはバーの高さの中央に、バーと同じ左右で引く。今の時刻の縦線の左右もバーの列で決める。</summary>
    IEnumerable<FrameworkElement> GetRailElements();

    /// <summary>行の見えている範囲(縦のスクロールの窓)。この外の行には引かない。今の時刻の縦線はこの上端から下端まで引く。</summary>
    FrameworkElement? RailViewport { get; }

    double HorizontalOffset { get; }

    double VisibleSeconds { get; }

    /// <summary>今の時刻(戦闘の経過の秒)。履歴の回は回の終わり。</summary>
    double ElapsedSeconds { get; }

    /// <summary>その人が周りにいなかった区間(戦闘の経過の秒、[始まり, 終わり] を交互)。この間は引かない。</summary>
    IReadOnlyList<double> AbsentSpans { get; }
}

/// <summary>
/// バフ稼働率の行のレールと、今の時刻の縦線。推移グラフの軸と同じく窓の枠の層(<c>WidgetWindow.FrameOverlayHost</c>)へ差し込み、分割線と同じく窓の不透明度で合成する。
/// 縦線は、まだ来ていない時刻が見えている間だけ(今の時刻が見えている窓の左端より右・右端より左)、見えている範囲の上端から下端まで引く。
/// バーの終わりが「バフが切れた」のか「時間がまだそこまで来ていない」のかを見分けるため。
/// 線は画素にそろえる。行の位置は縦のスクロールで動くので、配置が変わるたびに位置を見て、変わっていれば描き直す。
/// 枠の層は中身の層の切り取りを受けないので、見えている範囲の外の行は自分で除く。
/// </summary>
public sealed class BuffUptimeRailLayer : FrameworkElement
{
    public static readonly DependencyProperty BrushProperty = DependencyProperty.Register(
        nameof(Brush),
        typeof(Brush),
        typeof(BuffUptimeRailLayer),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnPenSourceChanged));

    public static readonly DependencyProperty LineThicknessProperty = DependencyProperty.Register(
        nameof(LineThickness),
        typeof(double),
        typeof(BuffUptimeRailLayer),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender, OnPenSourceChanged));

    private readonly List<Rect> _placements = [];
    private readonly List<Rect> _renderedPlacements = [];
    private Rect _renderedViewport = Rect.Empty;
    private IBuffUptimeRailSource? _source;

    /// <summary>色と太さが変わったときだけ作り直す(描くたびに作らない)。</summary>
    private Pen? _pen;

    public BuffUptimeRailLayer()
    {
        IsHitTestVisible = false;
        LayoutUpdated += RailLayer_LayoutUpdated;
    }

    public Brush? Brush
    {
        get => (Brush?)GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    public double LineThickness
    {
        get => (double)GetValue(LineThicknessProperty);
        set => SetValue(LineThicknessProperty, value);
    }

    /// <summary>引く相手。置くのはビュー。</summary>
    public IBuffUptimeRailSource? Source
    {
        get => _source;
        set
        {
            _source = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        _renderedViewport = CollectPlacements(_renderedPlacements);
        if (Source is not { } source || _renderedPlacements.Count == 0 || GetPen() is not { } pen)
        {
            return;
        }

        var offset = source.HorizontalOffset;
        var visibleSeconds = source.VisibleSeconds;
        var absent = source.AbsentSpans;
        var elapsed = source.ElapsedSeconds;
        var halfThickness = pen.Thickness / 2d;
        var guidelines = new GuidelineSet();
        foreach (var rect in _renderedPlacements)
        {
            guidelines.GuidelinesY.Add(rect.Top + rect.Height / 2d + halfThickness);
        }

        // 今の時刻の縦線。左右はバーの列(どの行のバーも同じ列)。
        var showsNowLine = elapsed > offset && elapsed < offset + visibleSeconds;
        var nowX = 0d;
        if (showsNowLine)
        {
            var column = _renderedPlacements[0];
            nowX = MetricTimeAxis.ToX(column.Left, column.Width, offset, visibleSeconds, elapsed);
            guidelines.GuidelinesX.Add(nowX + halfThickness);
        }

        drawingContext.PushGuidelineSet(guidelines);
        foreach (var rect in _renderedPlacements)
        {
            var y = rect.Top + rect.Height / 2d;
            var cursor = offset;
            var end = offset + visibleSeconds;
            for (var index = 0; index + 1 < absent.Count && cursor < end; index += 2)
            {
                if (absent[index + 1] <= cursor)
                {
                    continue;
                }

                DrawSegment(drawingContext, pen, rect, y, offset, visibleSeconds, cursor, Math.Min(absent[index], end));
                cursor = Math.Max(cursor, absent[index + 1]);
            }

            DrawSegment(drawingContext, pen, rect, y, offset, visibleSeconds, cursor, end);
        }

        if (showsNowLine)
        {
            drawingContext.DrawLine(pen, new Point(nowX, _renderedViewport.Top), new Point(nowX, _renderedViewport.Bottom));
        }

        drawingContext.Pop();
    }

    private Pen? GetPen()
    {
        if (_pen is null && Brush is { } brush && LineThickness > 0d)
        {
            _pen = new Pen(brush, LineThickness);
            if (_pen.CanFreeze)
            {
                _pen.Freeze();
            }
        }

        return _pen;
    }

    private static void OnPenSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        ((BuffUptimeRailLayer)sender)._pen = null;
    }

    private static void DrawSegment(DrawingContext drawingContext, Pen pen, Rect rect, double y, double offset, double visibleSeconds, double startSeconds, double endSeconds)
    {
        if (endSeconds <= startSeconds)
        {
            return;
        }

        var left = Math.Max(MetricTimeAxis.ToX(rect.Left, rect.Width, offset, visibleSeconds, startSeconds), rect.Left);
        var right = Math.Min(MetricTimeAxis.ToX(rect.Left, rect.Width, offset, visibleSeconds, endSeconds), rect.Right);
        if (right > left)
        {
            drawingContext.DrawLine(pen, new Point(left, y), new Point(right, y));
        }
    }

    /// <summary>見えている範囲の中の行のバーの、この要素から見た四角を集め、見えている範囲の四角を返す(見えている範囲が無ければ空)。</summary>
    private Rect CollectPlacements(List<Rect> target)
    {
        target.Clear();
        if (Source?.RailViewport is not { } viewport || viewport.FindCommonVisualAncestor(this) is null)
        {
            return Rect.Empty;
        }

        var viewportRect = viewport.TransformToVisual(this).TransformBounds(new Rect(viewport.RenderSize));
        foreach (var element in Source.GetRailElements())
        {
            if (!element.IsVisible || element.FindCommonVisualAncestor(this) is null)
            {
                continue;
            }

            var rect = element.TransformToVisual(this).TransformBounds(new Rect(element.RenderSize));
            var centerY = rect.Top + rect.Height / 2d;
            if (centerY >= viewportRect.Top && centerY <= viewportRect.Bottom && rect.Width > 0d)
            {
                target.Add(rect);
            }
        }

        return viewportRect;
    }

    private void RailLayer_LayoutUpdated(object? sender, EventArgs e)
    {
        if (Source is null)
        {
            return;
        }

        // 行が少ないと、窓の高さだけ変えてもバーは動かず、縦線の長さ(見えている範囲)だけが変わる。
        var viewport = CollectPlacements(_placements);
        if (viewport == _renderedViewport && _placements.SequenceEqual(_renderedPlacements))
        {
            return;
        }

        InvalidateVisual();
    }
}
