using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Serilog;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Controls;

/// <summary>
/// 推移グラフ。横は <see cref="VisibleSeconds"/> 秒分を描く領域の幅に固定し、それより長い分は
/// <see cref="HorizontalOffset"/>(窓の横のスクロールバー、<c>IWidgetHorizontalScrollContent</c> 経由)で横にずらして見る。
/// 縦軸の数字は左に固定する。
///
/// <para>
/// 軸の数字は <c>Text.WidgetWindow</c> の TextBlock。グラフの線は <see cref="LineBrush"/>(その人のクラスのグラフカラー)で描き、
/// 文字と同じ影を線の層にだけ掛ける(文字は自分のスタイルの影を持つので、この要素に掛けると文字の影が二重になる)。
/// 格子と軸の線は分割線と同じく窓の枠の層に描く(<see cref="MetricGraphGridLayer"/>、位置はこの要素の描く領域から求める)。
/// </para>
///
/// <para>
/// 右端を見ている間は最新を追いかける。左へ戻すと追いかけるのをやめ、右端へ戻すと再開する。
/// 区切りの数が減ったとき(別の回・描画間隔の変更)も右端へ戻す。
/// </para>
///
/// <para>
/// 横軸の数字の下(窓の横のスクロールバーの上)に、押した技のアイコンの行を置く(<see cref="SkillMarkers"/>。<see cref="ShowSkillMarkers"/> で隠せる)。
/// アイコン1つは <see cref="SkillMarkerTemplate"/>(XAML)で描き、高さは <see cref="SkillMarkerRowHeight"/>。
/// 横の位置は線と同じ式で時刻から決め(アイコンの幅の中央を時刻に置く)、見ている窓の中の時刻のものだけを丸ごと出す(横軸の数字と同じ)。重なったら後の開始を上に描く。
/// 要素を持つのは見ている窓の中の分だけ(窓の外の分を畳んで持つと、畳んだ要素も配置の時間を食う)。
/// TIPS が閉じないよう、窓の中に残る要素は作り直さず使い回し、技が変わったときだけ中身を差し替える。
/// </para>
///
/// <para>
/// 大きさと位置(軸の数字の大きさ・間、線の太さ、アイコンの行)はどれも XAML から渡す。既定値は 0(軸の数字の大きさは文字のスタイルのまま)。
/// </para>
///
/// <para>
/// 縦軸の数字は全体設定「数値」の表記で縮める(<see cref="NumberDisplayFormatIndex"/>、<see cref="MeterNumberFormatter"/>)。
/// 単位の字は表示言語の文言なので、表示している間は言語が変わったら作り直す。
/// </para>
/// </summary>
public sealed class MetricGraphChart : FrameworkElement
{
    /// <summary>横軸の数字の区間の数の上限。数字の間隔(<see cref="GridStepSeconds"/>)はこれを超えない切りのいい秒にする。</summary>
    private const int MaxSecondLabelIntervals = 4;

    /// <summary>横軸の数字の間隔の候補(秒)。<see cref="VisibleSeconds"/> を <see cref="MaxSecondLabelIntervals"/> 区間以下に分ける最小のものを使う。</summary>
    private static readonly double[] SecondLabelStepCandidates = [1d, 2d, 5d, 10d, 15d, 30d, 60d, 120d, 300d, 600d];

    private const int HorizontalGridCount = 2;

    /// <summary>
    /// 03(究極スキル)の枠にイマジンの絵が来たと警告した絵のパス(パスごとに1回だけ警告する)。
    /// アイコンのテンプレートは 03 のイマジンの絵の置き方を持たず、普通の絵の置き方で描く。
    /// </summary>
    private static readonly HashSet<string> ImaginePlacementWarnedPaths = new(StringComparer.OrdinalIgnoreCase);

    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points),
        typeof(IReadOnlyList<MetricGraphPoint>),
        typeof(MetricGraphChart),
        new FrameworkPropertyMetadata(Array.Empty<MetricGraphPoint>(), OnPointsChanged));

    /// <summary>見ている窓の始まり(戦闘の秒)。0〜<see cref="ScrollableSeconds"/> に収める。</summary>
    public static readonly DependencyProperty HorizontalOffsetProperty = DependencyProperty.Register(
        nameof(HorizontalOffset),
        typeof(double),
        typeof(MetricGraphChart),
        new FrameworkPropertyMetadata(
            0d,
            OnHorizontalOffsetChanged,
            CoerceHorizontalOffset));

    /// <summary>描く領域の幅に入れる秒数(設定「横軸の長さ」)。スクロールバーの見えている幅もこれ。</summary>
    public static readonly DependencyProperty VisibleSecondsProperty = DependencyProperty.Register(
        nameof(VisibleSeconds),
        typeof(double),
        typeof(MetricGraphChart),
        new FrameworkPropertyMetadata(60d, OnVisibleSecondsChanged),
        static value => value is double seconds && seconds > 0d && !double.IsInfinity(seconds));

    /// <summary>グラフの線の色。null の間は線を描かない。</summary>
    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush),
        typeof(Brush),
        typeof(MetricGraphChart),
        new FrameworkPropertyMetadata(null, OnLineBrushChanged));

    /// <summary>横軸の下に出す技のアイコン(技の開始の届いた順)。</summary>
    public static readonly DependencyProperty SkillMarkersProperty = DependencyProperty.Register(
        nameof(SkillMarkers),
        typeof(IReadOnlyList<MetricGraphSkillMarker>),
        typeof(MetricGraphChart),
        new FrameworkPropertyMetadata(Array.Empty<MetricGraphSkillMarker>(), OnSkillMarkersChanged));

    /// <summary>横軸の下に技のアイコンの行を出すか(設定「スキルログを表示」)。出さないときは行の高さも詰め、アイコンの要素を持たない。</summary>
    public static readonly DependencyProperty ShowSkillMarkersProperty = DependencyProperty.Register(
        nameof(ShowSkillMarkers),
        typeof(bool),
        typeof(MetricGraphChart),
        new FrameworkPropertyMetadata(true, OnShowSkillMarkersChanged));

    /// <summary>
    /// 技のアイコンの TIPS の色を決めるもの。<c>ToolTip.WidgetWindowInfo</c> が TIPS を付けた要素の Tag から窓のパレットを引くので、
    /// ウィジェット(<c>WidgetListItemViewModel</c>)を渡す。アイコンのテンプレートがこのグラフから引いて Tag に入れる。
    /// </summary>
    public static readonly DependencyProperty SkillMarkerToolTipTagProperty = DependencyProperty.Register(
        nameof(SkillMarkerToolTipTag),
        typeof(object),
        typeof(MetricGraphChart),
        new FrameworkPropertyMetadata(null));

    /// <summary>技のアイコン1つのテンプレート(中身は <see cref="MetricGraphSkillMarker"/>)。</summary>
    public static readonly DependencyProperty SkillMarkerTemplateProperty = DependencyProperty.Register(
        nameof(SkillMarkerTemplate),
        typeof(DataTemplate),
        typeof(MetricGraphChart),
        new FrameworkPropertyMetadata(null, OnSkillMarkerTemplateChanged));

    /// <summary>技のアイコンの行の高さ(アイコン1つの高さ)。技が無くても空けておく(軸が上下に動かないように)。</summary>
    public static readonly DependencyProperty SkillMarkerRowHeightProperty = DependencyProperty.Register(
        nameof(SkillMarkerRowHeight),
        typeof(double),
        typeof(MetricGraphChart),
        new FrameworkPropertyMetadata(0d, OnSkillMarkerRowHeightChanged));

    /// <summary>横軸の数字の下端と技のアイコンの行の上端の間。</summary>
    public static readonly DependencyProperty SkillMarkerGapProperty = DependencyProperty.Register(
        nameof(SkillMarkerGap),
        typeof(double),
        typeof(MetricGraphChart),
        new FrameworkPropertyMetadata(0d, OnLayoutValueChanged));

    /// <summary>縦軸の数字の右端と描く領域の左端の間。</summary>
    public static readonly DependencyProperty ValueLabelGapProperty = DependencyProperty.Register(
        nameof(ValueLabelGap),
        typeof(double),
        typeof(MetricGraphChart),
        new FrameworkPropertyMetadata(0d, OnLayoutValueChanged));

    /// <summary>描く領域の下端と横軸の数字の上端の間。</summary>
    public static readonly DependencyProperty SecondLabelGapProperty = DependencyProperty.Register(
        nameof(SecondLabelGap),
        typeof(double),
        typeof(MetricGraphChart),
        new FrameworkPropertyMetadata(0d, OnLayoutValueChanged));

    /// <summary>軸の数字の大きさ。指定が無い(NaN)間は文字のスタイル(<c>Text.WidgetWindow</c>)の大きさ。</summary>
    public static readonly DependencyProperty AxisFontSizeProperty = DependencyProperty.Register(
        nameof(AxisFontSize),
        typeof(double),
        typeof(MetricGraphChart),
        new FrameworkPropertyMetadata(double.NaN, OnAxisFontSizeChanged));

    /// <summary>グラフの線の太さ。</summary>
    public static readonly DependencyProperty LineThicknessProperty = DependencyProperty.Register(
        nameof(LineThickness),
        typeof(double),
        typeof(MetricGraphChart),
        new FrameworkPropertyMetadata(0d, OnLineBrushChanged));

    /// <summary>縦軸の数字の縮め方。全体設定「数値」の番号(<see cref="MeterNumberFormatter"/>)。</summary>
    public static readonly DependencyProperty NumberDisplayFormatIndexProperty = DependencyProperty.Register(
        nameof(NumberDisplayFormatIndex),
        typeof(int),
        typeof(MetricGraphChart),
        new FrameworkPropertyMetadata(0, OnLayoutValueChanged));

    private readonly VisualCollection _children;
    private readonly LineLayer _lineLayer;
    private readonly TextBlock[] _valueLabels;
    private readonly TextBlock[] _secondLabels;
    private readonly Canvas _skillMarkerLayer;
    /// <summary>見ている窓の中の技の開始の要素(中身は <see cref="MetricGraphSkillMarker"/>)。鍵は <see cref="SkillMarkers"/> の何番目か。</summary>
    private readonly Dictionary<int, ContentPresenter> _skillMarkerElements = [];
    private readonly List<double> _secondLabelSeconds = [];
    /// <summary>今の点。並びは区切りの順(Core が並べる)。</summary>
    private IReadOnlyList<MetricGraphPoint> _points = Array.Empty<MetricGraphPoint>();
    private double _maxSeconds;
    private double _maxValuePerSecond;
    private int _lastPointCount;
    private bool _isFollowingEnd = true;
    private bool _isSettingOffset;
    private bool _isSubscribedToCulture;

    public MetricGraphChart()
    {
        _lineLayer = new LineLayer(this);
        _lineLayer.SetResourceReference(EffectProperty, "Effect.WidgetWindowTextShadow");
        _children = new VisualCollection(this) { _lineLayer };
        _valueLabels = CreateLabels(HorizontalGridCount + 1);
        _secondLabels = CreateLabels(MaxSecondLabelIntervals + 1);

        // 最後に足すので、線の層(全面で当たる)より上でマウスを受ける。
        _skillMarkerLayer = new Canvas();
        _children.Add(_skillMarkerLayer);
        UpdateLabels();

        Loaded += MetricGraphChart_Loaded;
        Unloaded += MetricGraphChart_Unloaded;
    }

    public IReadOnlyList<MetricGraphPoint> Points
    {
        get => (IReadOnlyList<MetricGraphPoint>)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
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

    public Brush? LineBrush
    {
        get => (Brush?)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public IReadOnlyList<MetricGraphSkillMarker> SkillMarkers
    {
        get => (IReadOnlyList<MetricGraphSkillMarker>)GetValue(SkillMarkersProperty);
        set => SetValue(SkillMarkersProperty, value);
    }

    public bool ShowSkillMarkers
    {
        get => (bool)GetValue(ShowSkillMarkersProperty);
        set => SetValue(ShowSkillMarkersProperty, value);
    }

    public object? SkillMarkerToolTipTag
    {
        get => GetValue(SkillMarkerToolTipTagProperty);
        set => SetValue(SkillMarkerToolTipTagProperty, value);
    }

    public DataTemplate? SkillMarkerTemplate
    {
        get => (DataTemplate?)GetValue(SkillMarkerTemplateProperty);
        set => SetValue(SkillMarkerTemplateProperty, value);
    }

    public double SkillMarkerRowHeight
    {
        get => (double)GetValue(SkillMarkerRowHeightProperty);
        set => SetValue(SkillMarkerRowHeightProperty, value);
    }

    public double SkillMarkerGap
    {
        get => (double)GetValue(SkillMarkerGapProperty);
        set => SetValue(SkillMarkerGapProperty, value);
    }

    public double ValueLabelGap
    {
        get => (double)GetValue(ValueLabelGapProperty);
        set => SetValue(ValueLabelGapProperty, value);
    }

    public double SecondLabelGap
    {
        get => (double)GetValue(SecondLabelGapProperty);
        set => SetValue(SecondLabelGapProperty, value);
    }

    public double AxisFontSize
    {
        get => (double)GetValue(AxisFontSizeProperty);
        set => SetValue(AxisFontSizeProperty, value);
    }

    public double LineThickness
    {
        get => (double)GetValue(LineThicknessProperty);
        set => SetValue(LineThicknessProperty, value);
    }

    public int NumberDisplayFormatIndex
    {
        get => (int)GetValue(NumberDisplayFormatIndexProperty);
        set => SetValue(NumberDisplayFormatIndexProperty, value);
    }

    /// <summary>横軸の数字の間隔。戦闘の秒に結び付けるので、スクロールすると数字も動く。スクロールバーの1段の移動もこれ。</summary>
    public double GridStepSeconds
    {
        get
        {
            var visibleSeconds = VisibleSeconds;
            foreach (var step in SecondLabelStepCandidates)
            {
                if (visibleSeconds / step <= MaxSecondLabelIntervals)
                {
                    return step;
                }
            }

            return Math.Ceiling(visibleSeconds / MaxSecondLabelIntervals);
        }
    }

    /// <summary>ずらせる秒数(最後の区切りの終わり − <see cref="VisibleSeconds"/>、足りなければ 0)。スクロールバーの最大値で、0 ならバーを出さない。</summary>
    public double ScrollableSeconds { get; private set; }

    /// <summary><see cref="ScrollableSeconds"/> か <see cref="HorizontalOffset"/> が変わった(スクロールバーを合わせる合図)。</summary>
    public event EventHandler? ScrollMetricsChanged;

    protected override int VisualChildrenCount => _children.Count;

    protected override Visual GetVisualChild(int index)
    {
        return _children[index];
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);
        foreach (var label in _valueLabels)
        {
            label.Measure(unbounded);
        }

        foreach (var label in _secondLabels)
        {
            label.Measure(unbounded);
        }

        _lineLayer.Measure(availableSize);
        _skillMarkerLayer.Measure(availableSize);
        return new Size(1d, 1d);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _lineLayer.Arrange(new Rect(finalSize));

        var plotBounds = GetPlotBounds(finalSize);
        var hasPlot = plotBounds.Width > 0 && plotBounds.Height > 0;

        // 縦軸の数字は右寄せ(右端を描く領域の左端の手前にそろえる)。
        for (var index = 0; index < _valueLabels.Length; index++)
        {
            var label = _valueLabels[index];
            var y = plotBounds.Top + plotBounds.Height * index / HorizontalGridCount;
            label.Arrange(hasPlot
                ? new Rect(
                    new Point(plotBounds.Left - ValueLabelGap - label.DesiredSize.Width, y - label.DesiredSize.Height / 2d),
                    label.DesiredSize)
                : new Rect());
        }

        for (var index = 0; index < _secondLabels.Length; index++)
        {
            var label = _secondLabels[index];
            if (index >= _secondLabelSeconds.Count)
            {
                continue;
            }

            var x = ToX(plotBounds, _secondLabelSeconds[index]);
            label.Arrange(hasPlot
                ? new Rect(new Point(x - label.DesiredSize.Width / 2d, plotBounds.Bottom + SecondLabelGap), label.DesiredSize)
                : new Rect());
        }

        // 技のアイコンの行は横軸の数字の下。位置を決めてから置き場を並べ直す(位置の変更で置き場の配置がやり直しになる)。
        // 要素は見ている窓の中のものだけ(SynchronizeVisibleSkillMarkers)。
        var markerTop = plotBounds.Bottom + SecondLabelGap + GetSecondLabelHeight() + SkillMarkerGap;
        foreach (var element in _skillMarkerElements.Values)
        {
            var marker = (MetricGraphSkillMarker)element.Content;
            Canvas.SetLeft(element, ToX(plotBounds, marker.Seconds) - element.DesiredSize.Width / 2d);
            Canvas.SetTop(element, markerTop);
        }

        // 描く領域が無い(窓が小さすぎる)ときは軸の数字と同じく出さない。
        _skillMarkerLayer.Clip = hasPlot ? null : Geometry.Empty;
        _skillMarkerLayer.Arrange(new Rect(finalSize));
        return finalSize;
    }

    private TextBlock[] CreateLabels(int count)
    {
        var labels = new TextBlock[count];
        for (var index = 0; index < count; index++)
        {
            var label = new TextBlock
            {
                IsHitTestVisible = false
            };
            label.SetResourceReference(StyleProperty, "Text.WidgetWindow");
            _children.Add(label);
            labels[index] = label;
        }

        return labels;
    }

    /// <summary>表示している間だけ言語の変化を受ける(二重に購読しない)。外れていた間に言語が変わっていても、ここで作り直す。</summary>
    private void MetricGraphChart_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isSubscribedToCulture)
        {
            return;
        }

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        _isSubscribedToCulture = true;
        Refresh();
    }

    private void MetricGraphChart_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!_isSubscribedToCulture)
        {
            return;
        }

        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
        _isSubscribedToCulture = false;
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MetricGraphChart)d).ApplyPoints();
    }

    private static object CoerceHorizontalOffset(DependencyObject d, object baseValue)
    {
        var value = (double)baseValue;
        return double.IsNaN(value)
            ? 0d
            : Math.Clamp(value, 0d, ((MetricGraphChart)d).ScrollableSeconds);
    }

    private static void OnHorizontalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (MetricGraphChart)d;
        if (!chart._isSettingOffset)
        {
            // 手で動かした。右端にいる間だけ最新を追いかける。
            chart._isFollowingEnd = chart.HorizontalOffset >= chart.ScrollableSeconds;
        }

        chart.Refresh();
    }

    private static void OnVisibleSecondsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (MetricGraphChart)d;
        chart.UpdateScrollRange();
        chart.Refresh();
    }

    private static void OnLineBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MetricGraphChart)d)._lineLayer.InvalidateVisual();
    }

    private static void OnSkillMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MetricGraphChart)d).ApplySkillMarkers();
    }

    private static void OnShowSkillMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 描く領域の高さが変わるので、アイコンの要素に加えて線も描き直す。
        ((MetricGraphChart)d).Refresh();
    }

    private static void OnSkillMarkerTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (MetricGraphChart)d;
        foreach (var element in chart._skillMarkerElements.Values)
        {
            element.ContentTemplate = chart.SkillMarkerTemplate;
        }

        chart.InvalidateMeasure();
        chart.InvalidateArrange();
    }

    private static void OnSkillMarkerRowHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (MetricGraphChart)d;
        foreach (var element in chart._skillMarkerElements.Values)
        {
            element.Height = chart.SkillMarkerRowHeight;
        }

        // 描く領域の高さが変わるので、アイコンの要素に加えて線も描き直す。
        chart.Refresh();
    }

    private static void OnLayoutValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MetricGraphChart)d).Refresh();
    }

    private static void OnAxisFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (MetricGraphChart)d;
        var fontSize = chart.AxisFontSize;
        foreach (var label in chart._valueLabels.Concat(chart._secondLabels))
        {
            if (double.IsFinite(fontSize) && fontSize > 0d)
            {
                label.FontSize = fontSize;
            }
            else
            {
                label.ClearValue(TextBlock.FontSizeProperty);
            }
        }

        chart.Refresh();
    }

    private void ApplySkillMarkers()
    {
        SynchronizeVisibleSkillMarkers();
        InvalidateMeasure();
        InvalidateArrange();
    }

    /// <summary>
    /// 技のアイコンの要素を、見ている窓 [<see cref="HorizontalOffset"/>, + <see cref="VisibleSeconds"/>] の中の時刻の技の開始の分だけにする
    /// (横軸の数字と同じ扱い。端のアイコンは半分が描く領域の左右の余白へはみ出す)。
    /// 要素は一覧の何番目かで持ち、窓に入った番目は作り、出た番目は外し、残る番目は使い回す(技が変わったところだけ中身を差し替える)。
    /// 作り直さないのは、マウスの下の要素が入れ替わると TIPS が閉じるため。
    /// 窓の中かは一覧の全部を見て決める(並び順に頼らない)。重なったら後の開始を上に描くよう、Z の順は番目で決める(足した順にしない)。
    /// 行を出さない設定(<see cref="ShowSkillMarkers"/>)のときは要素を全部外す。
    /// </summary>
    private void SynchronizeVisibleSkillMarkers()
    {
        var markers = ShowSkillMarkers
            ? SkillMarkers ?? Array.Empty<MetricGraphSkillMarker>()
            : Array.Empty<MetricGraphSkillMarker>();
        var start = HorizontalOffset;
        var end = start + VisibleSeconds;

        foreach (var (index, element) in _skillMarkerElements.ToArray())
        {
            if (index < markers.Count && IsInVisibleWindow(markers[index].Seconds, start, end))
            {
                continue;
            }

            _skillMarkerLayer.Children.Remove(element);
            _skillMarkerElements.Remove(index);
        }

        for (var index = 0; index < markers.Count; index++)
        {
            var marker = markers[index];
            if (!IsInVisibleWindow(marker.Seconds, start, end))
            {
                continue;
            }

            if (_skillMarkerElements.TryGetValue(index, out var element))
            {
                if (!Equals(element.Content, marker))
                {
                    WarnIfNoImaginePlacement(marker);
                    element.Content = marker;
                }

                continue;
            }

            WarnIfNoImaginePlacement(marker);
            element = new ContentPresenter
            {
                Content = marker,
                ContentTemplate = SkillMarkerTemplate,
                Height = SkillMarkerRowHeight
            };
            Panel.SetZIndex(element, index);
            _skillMarkerElements.Add(index, element);
            _skillMarkerLayer.Children.Add(element);
        }
    }

    private static bool IsInVisibleWindow(double seconds, double start, double end)
    {
        return seconds >= start && seconds <= end;
    }

    /// <summary>
    /// 03(究極スキル)の枠にイマジンの絵が来たら、絵のパスごとに1回ログに警告する(テンプレートに置き方を足す合図)。
    /// テンプレートは普通の絵の置き方で描く。
    /// </summary>
    private static void WarnIfNoImaginePlacement(MetricGraphSkillMarker marker)
    {
        if (marker.Frame != SkillIconFrame.Ultimate
            || !marker.UsesImagineAsset
            || marker.IconPath is not { } iconPath
            || !ImaginePlacementWarnedPaths.Add(iconPath))
        {
            return;
        }

        Log.Warning(
            "Skill icon {Path} is imagine art in the {Frame} frame, which has no imagine placement; drawing it with the regular placement",
            iconPath,
            marker.Frame);
    }

    private void ApplyPoints()
    {
        var points = Points ?? Array.Empty<MetricGraphPoint>();
        var maxSeconds = 0d;
        var maxValuePerSecond = 0d;
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            maxSeconds = Math.Max(maxSeconds, point.EndSeconds);
            maxValuePerSecond = Math.Max(maxValuePerSecond, point.ValuePerSecond);
        }

        _points = points;
        _maxSeconds = maxSeconds;
        _maxValuePerSecond = maxValuePerSecond;

        if (points.Count < _lastPointCount)
        {
            _isFollowingEnd = true;
        }

        _lastPointCount = points.Count;

        UpdateScrollRange();
        Refresh();
    }

    /// <summary>
    /// ずらせる秒数を最後の区切りの終わりと <see cref="VisibleSeconds"/> から決め直す。最新を追いかけている間は右端へ寄せる。
    /// 追いかけていない間は位置を範囲に収め、収めた結果が右端なら追いかけ直す(手で右端へ戻したときと同じ規則。
    /// 横軸を長くして右端へ押し戻されたのに、データが伸びても付いていかない状態を残さない)。
    /// </summary>
    private void UpdateScrollRange()
    {
        var scrollableSeconds = Math.Max(_maxSeconds - VisibleSeconds, 0d);
        ScrollableSeconds = scrollableSeconds;

        _isSettingOffset = true;
        try
        {
            if (_isFollowingEnd)
            {
                SetCurrentValue(HorizontalOffsetProperty, scrollableSeconds);
            }
            else
            {
                CoerceValue(HorizontalOffsetProperty);
                _isFollowingEnd = HorizontalOffset >= scrollableSeconds;
            }
        }
        finally
        {
            _isSettingOffset = false;
        }
    }

    private void Refresh()
    {
        UpdateLabels();
        SynchronizeVisibleSkillMarkers();
        InvalidateMeasure();
        InvalidateArrange();
        _lineLayer.InvalidateVisual();
        ScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 縦軸の数字(最大値・半分・0)と、見ている窓の中の横軸の数字の秒を決める。
    /// 最大値が無い(データが無い・全部0)ときは 0 だけを出し、ほかは空にする。
    /// </summary>
    private void UpdateLabels()
    {
        var lastValueLabelIndex = _valueLabels.Length - 1;
        for (var index = 0; index < _valueLabels.Length; index++)
        {
            _valueLabels[index].Text = _maxValuePerSecond > 0d || index == lastValueLabelIndex
                ? MeterNumberFormatter.FormatCompact(
                    _maxValuePerSecond * (1d - index / (double)HorizontalGridCount),
                    NumberDisplayFormatIndex)
                : string.Empty;
        }

        var offset = HorizontalOffset;
        var step = GridStepSeconds;
        _secondLabelSeconds.Clear();
        for (var seconds = Math.Ceiling(offset / step) * step;
             seconds <= offset + VisibleSeconds && _secondLabelSeconds.Count < _secondLabels.Length;
             seconds += step)
        {
            _secondLabelSeconds.Add(seconds);
        }

        for (var index = 0; index < _secondLabels.Length; index++)
        {
            var label = _secondLabels[index];
            if (index < _secondLabelSeconds.Count)
            {
                var totalSeconds = (int)Math.Round(_secondLabelSeconds[index]);
                label.Text = $"{totalSeconds / 60}:{totalSeconds % 60:00}";
                label.Visibility = Visibility.Visible;
            }
            else
            {
                label.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void RenderLines(DrawingContext drawingContext, Size size)
    {
        var bounds = new Rect(size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        drawingContext.DrawRectangle(Brushes.Transparent, null, bounds);

        var plotBounds = GetPlotBounds(size);
        if (plotBounds.Width <= 0 || plotBounds.Height <= 0)
        {
            return;
        }

        if (_points.Count == 0 || LineBrush is not { } lineBrush)
        {
            return;
        }

        // 線は戦闘の0秒・値0(描く領域の左下の原点)から始め、区切りの中央に置いた点を順につなぐ。
        // 見ている窓の外は描く領域の左右で切る。高さは最大値で割る(全部0なら下端)。
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(ToX(plotBounds, 0d), plotBounds.Bottom), false, false);
            for (var index = 0; index < _points.Count; index++)
            {
                var point = _points[index];
                var ratio = _maxValuePerSecond > 0d
                    ? point.ValuePerSecond / _maxValuePerSecond
                    : 0d;
                context.LineTo(
                    new Point(
                        ToX(plotBounds, (point.StartSeconds + point.EndSeconds) * 0.5d),
                        plotBounds.Bottom - ratio * plotBounds.Height),
                    true,
                    false);
            }
        }

        geometry.Freeze();

        var linePen = CreateLinePen(lineBrush, LineThickness);
        drawingContext.PushClip(new RectangleGeometry(new Rect(plotBounds.Left, bounds.Top, plotBounds.Width, bounds.Height)));
        drawingContext.DrawGeometry(null, linePen, geometry);
        drawingContext.Pop();
    }

    /// <summary>
    /// 格子と軸の線を <paramref name="target"/>(枠の層の <see cref="MetricGraphGridLayer"/>)の座標で描く。
    /// 格子は描く領域の横 <see cref="HorizontalGridCount"/> 等分の横線だけ(横軸の分割線は描かない)。
    /// 軸は左端の縦線と下端の横線(下端の横線は格子の横線の最後の1本と同じ)。
    /// </summary>
    internal void RenderGrid(DrawingContext drawingContext, Visual target, Pen pen)
    {
        var plotBounds = GetPlotBounds(RenderSize);
        if (plotBounds.Width <= 0 || plotBounds.Height <= 0 || FindCommonVisualAncestor(target) is null)
        {
            return;
        }

        var transform = TransformToVisual(target);
        var lines = new List<(Point Start, Point End)>();
        for (var index = 0; index <= HorizontalGridCount; index++)
        {
            var y = plotBounds.Top + plotBounds.Height * index / HorizontalGridCount;
            lines.Add((transform.Transform(new Point(plotBounds.Left, y)), transform.Transform(new Point(plotBounds.Right, y))));
        }

        // 縦軸。下端の横軸は上の横線の最後の1本。
        lines.Add((transform.Transform(plotBounds.TopLeft), transform.Transform(plotBounds.BottomLeft)));

        // 分割線(Border)と同じく画素にそろえる。線の中心を画素の中央へ寄せないと 2px にぼける。
        var halfThickness = pen.Thickness / 2d;
        var guidelines = new GuidelineSet();
        foreach (var (start, end) in lines)
        {
            guidelines.GuidelinesX.Add(start.X + halfThickness);
            guidelines.GuidelinesY.Add(start.Y + halfThickness);
            guidelines.GuidelinesY.Add(end.Y + halfThickness);
        }

        drawingContext.PushGuidelineSet(guidelines);
        foreach (var (start, end) in lines)
        {
            drawingContext.DrawLine(pen, start, end);
        }

        drawingContext.Pop();
    }

    /// <summary>描く領域の左上の、<paramref name="target"/> から見た位置と描く領域の大きさ。格子の部品が位置の変化を見るのに使う。</summary>
    internal (Point Origin, Size PlotSize)? GetPlotPlacement(Visual target)
    {
        if (FindCommonVisualAncestor(target) is null)
        {
            return null;
        }

        var plotBounds = GetPlotBounds(RenderSize);
        return (TransformToVisual(target).Transform(plotBounds.TopLeft), plotBounds.Size);
    }

    private double ToX(Rect plotBounds, double seconds)
    {
        return plotBounds.Left + (seconds - HorizontalOffset) / VisibleSeconds * plotBounds.Width;
    }

    /// <summary>
    /// 描く領域。周りの余白は軸の数字の大きさから決め、数字のいちばん外側がこの要素の端に来るようにする
    /// (窓の枠との隙間はビューの余白が持つ)。左は縦軸の数字のいちばん広い幅と間、上は縦軸の数字の高さの半分
    /// (いちばん上の数字は線の高さを中心に置く)、下は横軸の数字の高さと間に技のアイコンの行(間と一辺)を足したもの、
    /// 右は横軸の数字の幅の半分(右端の数字は時刻を中心に置く)。アイコンの行は技が無くても空けておく(軸が上下に動かないように)。
    /// 行を出さない設定(<see cref="ShowSkillMarkers"/>)のときは空けない。
    /// 数字の大きさは測った後の値(<see cref="MeasureOverride"/> で測る)。
    /// </summary>
    private Rect GetPlotBounds(Size size)
    {
        var valueLabelWidth = 0d;
        var valueLabelHeight = 0d;
        foreach (var label in _valueLabels)
        {
            valueLabelWidth = Math.Max(valueLabelWidth, label.DesiredSize.Width);
            valueLabelHeight = Math.Max(valueLabelHeight, label.DesiredSize.Height);
        }

        var secondLabelWidth = 0d;
        var secondLabelHeight = 0d;
        foreach (var label in _secondLabels)
        {
            secondLabelWidth = Math.Max(secondLabelWidth, label.DesiredSize.Width);
            secondLabelHeight = Math.Max(secondLabelHeight, label.DesiredSize.Height);
        }

        var left = valueLabelWidth + ValueLabelGap;
        var top = valueLabelHeight / 2d;
        var right = secondLabelWidth / 2d;
        var bottom = secondLabelHeight + SecondLabelGap + (ShowSkillMarkers ? SkillMarkerGap + SkillMarkerRowHeight : 0d);
        return new Rect(
            left,
            top,
            Math.Max(size.Width - left - right, 0d),
            Math.Max(size.Height - top - bottom, 0d));
    }

    /// <summary>横軸の数字のいちばん高い高さ(測った後の値)。</summary>
    private double GetSecondLabelHeight()
    {
        var height = 0d;
        foreach (var label in _secondLabels)
        {
            height = Math.Max(height, label.DesiredSize.Height);
        }

        return height;
    }

    private static Pen CreateLinePen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        if (pen.CanFreeze)
        {
            pen.Freeze();
        }

        return pen;
    }

    /// <summary>グラフの線の層。影を軸の数字と別に掛けるために、文字とは別の要素にしてある。</summary>
    private sealed class LineLayer(MetricGraphChart owner) : FrameworkElement
    {
        protected override void OnRender(DrawingContext drawingContext)
        {
            owner.RenderLines(drawingContext, RenderSize);
        }
    }

}

/// <summary>
/// 推移グラフの格子と軸の線。<b>窓の枠の層(<c>WidgetWindow.FrameOverlayHost</c>)へ差し込む</b>ので、
/// 分割線と同じく窓の不透明度で合成され、影は付かない。線の太さは <see cref="LineThickness"/>(XAML で決める)、色は <see cref="Brush"/>(分割線の色を束縛する)。
///
/// <para>
/// 位置は <see cref="Chart"/> の描く領域から求める。格子は描く領域の位置と大きさだけで決まる(点にも横のずらしにも依らない)ので、
/// 配置の後に描く領域の位置か大きさが変わったと気付いたとき(縦軸の数字の幅が変わった、ヘッダーを隠したなど)だけ描き直す。
/// </para>
/// </summary>
public sealed class MetricGraphGridLayer : FrameworkElement
{
    public static readonly DependencyProperty BrushProperty = DependencyProperty.Register(
        nameof(Brush),
        typeof(Brush),
        typeof(MetricGraphGridLayer),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineThicknessProperty = DependencyProperty.Register(
        nameof(LineThickness),
        typeof(double),
        typeof(MetricGraphGridLayer),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    private (Point Origin, Size PlotSize)? _renderedPlacement;

    public MetricGraphGridLayer()
    {
        IsHitTestVisible = false;
        LayoutUpdated += GridLayer_LayoutUpdated;
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

    /// <summary>格子を描く相手のグラフ。置くのはビュー。</summary>
    public MetricGraphChart? Chart { get; set; }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        _renderedPlacement = Chart?.GetPlotPlacement(this);
        if (Chart is null || Brush is null)
        {
            return;
        }

        var pen = new Pen(Brush, LineThickness);
        if (pen.CanFreeze)
        {
            pen.Freeze();
        }

        Chart.RenderGrid(drawingContext, this, pen);
    }

    private void GridLayer_LayoutUpdated(object? sender, EventArgs e)
    {
        if (Chart is null || Chart.GetPlotPlacement(this) == _renderedPlacement)
        {
            return;
        }

        InvalidateVisual();
    }
}
