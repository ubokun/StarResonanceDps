// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Serilog;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Behaviors;

/// <summary>
/// <see cref="ItemsControl"/> の行を掴んで並べ替える振る舞い。ウィジェットとウィジェット設定の一覧で共通に使う。
///
/// <para>
/// 使い方: 一覧に <c>RowReorderBehavior.ContentElementName="RowContent"</c> を付け、行の作りの中身(写して薄くする部分)に
/// その名前を付ける。一覧の <c>DataContext</c> は <see cref="IRowReorderTarget"/> を実装する(並びの持ち方と確定はそちら)。
/// 中身の不透明度は掴んでいる間この振る舞いがローカル値で決めるので、行の作りで中身の <c>Opacity</c> をローカル値で決めない。
/// </para>
///
/// <para>
/// 行のどこを押しても掴めるが、中身の中のボタン(スイッチなど)の上は除く。閾値を超えたら掴む。
/// 掴んだ単位(鍵が同じ行。組なら組の行全部)の中身を掴んだ瞬間の見た目で写し、窓全体の飾りの層に描いてマウスへ付ける。
/// 元の行は中身を薄くして一覧の中で予定の位置へ動かし(<see cref="IRowReorderTarget.ShowRowReorderAt"/>)、離したときに確定する。
/// マウスが一覧の入った窓の外にいる間は、カーソルを禁止の印にして写しを隠し、行を元の位置へ戻す(そこで離すと取り消し)。
/// Esc でも取り消す。一覧の一番近い親の <see cref="ScrollViewer"/> は、表示域の上下の端で自動で送る。
/// </para>
///
/// <para>
/// DragDrop.DoDragDrop は使わず、マウスの捕捉だけで閉じた作りにする(ウィジェットは階層化ウィンドウで、OLE のドラッグがその上でどう振る舞うか確かめられないため)。
/// </para>
/// </summary>
public static class RowReorderBehavior
{
    public static readonly DependencyProperty ContentElementNameProperty = DependencyProperty.RegisterAttached(
        "ContentElementName",
        typeof(string),
        typeof(RowReorderBehavior),
        new PropertyMetadata(null, OnContentElementNameChanged));

    private static readonly DependencyProperty ControllerProperty = DependencyProperty.RegisterAttached(
        "Controller",
        typeof(RowReorderController),
        typeof(RowReorderBehavior),
        new PropertyMetadata(null));

    public static string? GetContentElementName(DependencyObject obj)
    {
        return (string?)obj.GetValue(ContentElementNameProperty);
    }

    public static void SetContentElementName(DependencyObject obj, string? value)
    {
        obj.SetValue(ContentElementNameProperty, value);
    }

    private static void OnContentElementNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl itemsControl)
        {
            return;
        }

        if (itemsControl.GetValue(ControllerProperty) is RowReorderController previous)
        {
            previous.Detach();
            itemsControl.ClearValue(ControllerProperty);
        }

        if (e.NewValue is string { Length: > 0 } contentElementName)
        {
            var controller = new RowReorderController(itemsControl, contentElementName);
            controller.Attach();
            itemsControl.SetValue(ControllerProperty, controller);
        }
    }
}

/// <summary>1つの一覧の並べ替えの状態と処理(<see cref="RowReorderBehavior"/>)。</summary>
internal sealed class RowReorderController
{
    /// <summary>掴んでいる間の、元の行の中身の不透明度。</summary>
    private const double DraggedContentOpacity = 0.4d;

    /// <summary>表示域の上下の端から、この距離に入ったら自動で送る。</summary>
    private const double AutoScrollEdge = 40d;

    /// <summary>自動で送る1回の最大の量。端に近いほどこの値に近づく。</summary>
    private const double AutoScrollMaxStep = 12d;

    private static readonly TimeSpan AutoScrollInterval = TimeSpan.FromMilliseconds(16);

    private readonly ItemsControl _itemsControl;
    private readonly string _contentElementName;
    private readonly DispatcherTimer _autoScrollTimer;
    private readonly HashSet<FrameworkElement> _fadedContents = [];
    private IRowReorderTarget? _pressedTarget;
    private object? _pressedKey;
    private Point _pressOrigin;
    private bool _isDragging;
    private IRowReorderTarget? _target;
    private object? _dragKey;
    private Vector _grabOffset;
    private Window? _window;
    private FrameworkElement? _ghostHost;
    private RowDragGhostAdorner? _adorner;
    private AdornerLayer? _adornerLayer;
    private ScrollViewer? _scrollViewer;
    private bool _isOutsideWindow;

    public RowReorderController(ItemsControl itemsControl, string contentElementName)
    {
        _itemsControl = itemsControl;
        _contentElementName = contentElementName;
        _autoScrollTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = AutoScrollInterval };
        _autoScrollTimer.Tick += AutoScrollTimer_Tick;
    }

    public void Attach()
    {
        _itemsControl.PreviewMouseLeftButtonDown += ItemsControl_PreviewMouseLeftButtonDown;
        _itemsControl.PreviewMouseMove += ItemsControl_PreviewMouseMove;
        _itemsControl.PreviewMouseLeftButtonUp += ItemsControl_PreviewMouseLeftButtonUp;
        _itemsControl.LostMouseCapture += ItemsControl_LostMouseCapture;
        _itemsControl.Unloaded += ItemsControl_Unloaded;
    }

    public void Detach()
    {
        EndDrag(commit: false);
        _itemsControl.PreviewMouseLeftButtonDown -= ItemsControl_PreviewMouseLeftButtonDown;
        _itemsControl.PreviewMouseMove -= ItemsControl_PreviewMouseMove;
        _itemsControl.PreviewMouseLeftButtonUp -= ItemsControl_PreviewMouseLeftButtonUp;
        _itemsControl.LostMouseCapture -= ItemsControl_LostMouseCapture;
        _itemsControl.Unloaded -= ItemsControl_Unloaded;
    }

    private void ItemsControl_Unloaded(object sender, RoutedEventArgs e)
    {
        EndDrag(commit: false);
    }

    /// <summary>行を押した。中身の中のボタンの上なら並べ替えに使わない(ボタンの操作をそのまま通す)。</summary>
    private void ItemsControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressedKey = null;
        _pressedTarget = null;
        if (_isDragging
            || _itemsControl.DataContext is not IRowReorderTarget target
            || e.OriginalSource is not DependencyObject source
            || _itemsControl.ContainerFromElement(source) is not FrameworkElement container)
        {
            return;
        }

        var item = _itemsControl.ItemContainerGenerator.ItemFromContainer(container);
        if (item == DependencyProperty.UnsetValue
            || IsInsideButtonInContent(source, container)
            || target.GetRowReorderKey(item) is not { } key)
        {
            return;
        }

        _pressedTarget = target;
        _pressedKey = key;
        _pressOrigin = e.GetPosition(_itemsControl);
    }

    private void ItemsControl_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressedKey is null)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndDrag(commit: false);
            return;
        }

        if (!_isDragging)
        {
            var position = e.GetPosition(_itemsControl);
            if (Math.Abs(position.X - _pressOrigin.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(position.Y - _pressOrigin.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            if (!BeginDrag())
            {
                _pressedKey = null;
                _pressedTarget = null;
                return;
            }
        }

        UpdateDrag();
    }

    private void ItemsControl_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            _pressedKey = null;
            _pressedTarget = null;
            return;
        }

        UpdateDrag();
        // 窓の外(禁止の印)で離したら取り消し。
        EndDrag(commit: !_isOutsideWindow);
        e.Handled = true;
    }

    private void ItemsControl_LostMouseCapture(object sender, MouseEventArgs e)
    {
        // LostMouseCapture はバブリングで子の分も上がってくる(行のボタンは押下で自分を捕捉し、こちらが捕捉した瞬間に失う)。
        // 一覧が失ったときだけ畳む。
        if (_isDragging && ReferenceEquals(e.OriginalSource, _itemsControl))
        {
            EndDrag(commit: false);
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_isDragging && e.Key == Key.Escape)
        {
            EndDrag(commit: false);
            e.Handled = true;
        }
    }

    /// <summary>掴む。掴んだ単位の行の中身を、掴んだ瞬間の見た目で写してから薄くする。</summary>
    private bool BeginDrag()
    {
        if (_pressedTarget is not { } target
            || _pressedKey is not { } pressedKey
            || !ReferenceEquals(_itemsControl.DataContext, target))
        {
            return false;
        }

        var group = GetUnitGroups(target).Find(candidate => Equals(candidate.Key, pressedKey));
        if (group is null)
        {
            return false;
        }

        var image = RenderContents(group, VisualTreeHelper.GetDpi(_itemsControl));

        _target = target;
        _dragKey = group.Key;
        _grabOffset = _pressOrigin - group.Bounds.TopLeft;
        _window = Window.GetWindow(_itemsControl);
        _ghostHost = _window?.Content as FrameworkElement;
        _adornerLayer = _ghostHost is null ? null : AdornerLayer.GetAdornerLayer(_ghostHost);
        if (_adornerLayer is null || _ghostHost is null)
        {
            Log.Warning("No window adorner layer for the reorderable list; the dragged rows are not drawn");
        }
        else
        {
            _adorner = new RowDragGhostAdorner(_ghostHost, image, group.Bounds.Size);
            _adornerLayer.Add(_adorner);
        }

        target.BeginRowReorder(group.Key);
        _isDragging = true;
        _isOutsideWindow = false;
        _itemsControl.CaptureMouse();

        _scrollViewer = FindAncestor<ScrollViewer>(_itemsControl);
        if (_window is not null)
        {
            _window.PreviewKeyDown += Window_PreviewKeyDown;
        }

        UpdateFade();
        _autoScrollTimer.Start();
        return true;
    }

    /// <summary>
    /// 掴んだ状態を解く。<paramref name="commit"/> なら今の見た目の並びを確定し、そうでなければ元の並びへ戻す。何度呼んでもよい。
    /// </summary>
    private void EndDrag(bool commit)
    {
        _pressedKey = null;
        _pressedTarget = null;
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        _autoScrollTimer.Stop();

        if (_isOutsideWindow)
        {
            Mouse.OverrideCursor = null;
            _isOutsideWindow = false;
        }

        foreach (var content in _fadedContents)
        {
            content.ClearValue(UIElement.OpacityProperty);
        }

        _fadedContents.Clear();

        if (_adorner is not null)
        {
            _adornerLayer?.Remove(_adorner);
            _adorner = null;
        }

        _adornerLayer = null;
        _ghostHost = null;

        if (_window is not null)
        {
            _window.PreviewKeyDown -= Window_PreviewKeyDown;
            _window = null;
        }

        _scrollViewer = null;

        if (_itemsControl.IsMouseCaptured)
        {
            _itemsControl.ReleaseMouseCapture();
        }

        var target = _target;
        _target = null;
        _dragKey = null;
        target?.EndRowReorder(commit);
    }

    /// <summary>
    /// 今のマウスの位置に合わせる。窓の外なら禁止の印にして写しを隠し、行を元の位置へ戻す。
    /// 窓の中なら写しをマウスへ付け(掴んだ位置のずれを保つ)、掴んだ行を予定の位置へ動かす。
    /// </summary>
    private void UpdateDrag()
    {
        if (!_isDragging || _target is not { } target)
        {
            return;
        }

        var isOutsideWindow = IsMouseOutsideWindow();
        if (isOutsideWindow != _isOutsideWindow)
        {
            _isOutsideWindow = isOutsideWindow;
            Mouse.OverrideCursor = isOutsideWindow ? Cursors.No : null;
            if (isOutsideWindow)
            {
                target.ShowRowReorderAtOriginalPosition();
                _itemsControl.UpdateLayout();
                UpdateFade();
            }
        }

        if (isOutsideWindow)
        {
            _adorner?.Update(null);
            return;
        }

        if (_adorner is not null && _ghostHost is not null)
        {
            _adorner.Update(SnapToDevicePixels(Mouse.GetPosition(_ghostHost) - _grabOffset));
        }

        UpdateRowPosition(target, Mouse.GetPosition(_itemsControl).Y);
    }

    /// <summary>
    /// マウスの高さから、掴んだ単位を入れる位置(どの単位の前か、末尾か)を決めて動かす。
    /// マウスの下の単位の真ん中より上ならその前、下ならその後ろ。掴んだ単位のすぐ前・すぐ後ろなら動かさない
    /// (動かした直後にマウスの下が掴んだ単位か、真ん中を越えていない隣になるので、行き来しない)。
    /// </summary>
    private void UpdateRowPosition(IRowReorderTarget target, double y)
    {
        var groups = GetUnitGroups(target);
        var dragIndex = groups.FindIndex(group => Equals(group.Key, _dragKey));
        if (dragIndex < 0 || groups.Count < 2)
        {
            return;
        }

        var hovered = groups.FindIndex(group => y < group.Bounds.Bottom);
        if (hovered < 0)
        {
            hovered = groups.Count - 1;
        }

        if (hovered == dragIndex)
        {
            return;
        }

        var bounds = groups[hovered].Bounds;
        var insertIndex = y < bounds.Top + (bounds.Height / 2d) ? hovered : hovered + 1;
        if (insertIndex == dragIndex || insertIndex == dragIndex + 1)
        {
            return;
        }

        target.ShowRowReorderAt(insertIndex < groups.Count ? groups[insertIndex].Key : null);
        _itemsControl.UpdateLayout();
        UpdateFade();
    }

    /// <summary>掴んだ単位の行の中身だけを薄くする。並びが変わったら付け直す(位置ごとに値を入れ替える一覧では、薄くする行も動く)。</summary>
    private void UpdateFade()
    {
        var dragged = new HashSet<FrameworkElement>();
        if (_isDragging && _target is { } target)
        {
            foreach (var group in GetUnitGroups(target))
            {
                if (Equals(group.Key, _dragKey))
                {
                    dragged.UnionWith(group.Contents);
                }
            }
        }

        foreach (var content in _fadedContents)
        {
            if (!dragged.Contains(content))
            {
                content.ClearValue(UIElement.OpacityProperty);
            }
        }

        foreach (var content in dragged)
        {
            if (!_fadedContents.Contains(content))
            {
                content.Opacity = DraggedContentOpacity;
            }
        }

        _fadedContents.Clear();
        _fadedContents.UnionWith(dragged);
    }

    /// <summary>マウスが一覧の入った窓の外にいるか。</summary>
    private bool IsMouseOutsideWindow()
    {
        if (_window is null)
        {
            return false;
        }

        var position = Mouse.GetPosition(_window);
        return position.X < 0d
            || position.Y < 0d
            || position.X >= _window.ActualWidth
            || position.Y >= _window.ActualHeight;
    }

    /// <summary>掴んだまま表示域の上下の端に近づいたら、近いほど速く送る。窓の外にいる間は送らない。</summary>
    private void AutoScrollTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isDragging || _isOutsideWindow || _scrollViewer is null)
        {
            return;
        }

        var y = Mouse.GetPosition(_scrollViewer).Y;
        var height = _scrollViewer.ViewportHeight;
        var step = 0d;
        if (y < AutoScrollEdge)
        {
            step = -AutoScrollMaxStep * Math.Min(1d, (AutoScrollEdge - y) / AutoScrollEdge);
        }
        else if (y > height - AutoScrollEdge)
        {
            step = AutoScrollMaxStep * Math.Min(1d, (y - (height - AutoScrollEdge)) / AutoScrollEdge);
        }

        if (step == 0d)
        {
            return;
        }

        var offset = Math.Clamp(_scrollViewer.VerticalOffset + step, 0d, _scrollViewer.ScrollableHeight);
        if (offset == _scrollViewer.VerticalOffset)
        {
            return;
        }

        _scrollViewer.ScrollToVerticalOffset(offset);
        _scrollViewer.UpdateLayout();
        UpdateDrag();
    }

    /// <summary>
    /// 今の一覧の行を、並べ替えの単位ごとにまとめる(同じ鍵の行は並んでいる)。座標は一覧の座標。
    /// 部品の無い行(仮想化で作られていない行)と、鍵の無い行は数えない。
    /// </summary>
    private List<UnitGroup> GetUnitGroups(IRowReorderTarget target)
    {
        var groups = new List<UnitGroup>();
        var items = _itemsControl.Items;
        for (var index = 0; index < items.Count; index++)
        {
            if (_itemsControl.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement container
                || target.GetRowReorderKey(items[index]!) is not { } key)
            {
                continue;
            }

            var topLeft = container.TransformToAncestor(_itemsControl).Transform(new Point(0d, 0d));
            var bounds = new Rect(topLeft, new Size(container.ActualWidth, container.ActualHeight));
            var content = FindContent(container);
            if (groups.Count > 0 && Equals(groups[^1].Key, key))
            {
                groups[^1].Add(bounds, content);
                continue;
            }

            groups.Add(new UnitGroup(key, bounds, content));
        }

        return groups;
    }

    /// <summary>行の部品の中の中身(<see cref="RowReorderBehavior.ContentElementNameProperty"/> の名前の要素)。</summary>
    private FrameworkElement? FindContent(DependencyObject container)
    {
        var count = VisualTreeHelper.GetChildrenCount(container);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(container, index);
            if (child is FrameworkElement { Name: var name } element && name == _contentElementName)
            {
                return element;
            }

            if (FindContent(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// 単位の行の中身だけを、今の見た目のまま1枚の絵にする。絵の大きさは単位の行の範囲で、中身の周りは透明。
    /// 中身の無い行は警告のログを出して写しから外す。
    /// </summary>
    private BitmapSource RenderContents(UnitGroup group, DpiScale dpi)
    {
        if (group.Contents.Count < group.RowCount)
        {
            Log.Warning(
                "Row content {ContentElementName} not found in {MissingRowCount} dragged rows; those rows are left out of the dragged rows",
                _contentElementName,
                group.RowCount - group.Contents.Count);
        }

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            foreach (var content in group.Contents)
            {
                var topLeft = content.TransformToAncestor(_itemsControl).Transform(new Point(0d, 0d));
                var size = new Size(content.ActualWidth, content.ActualHeight);
                var brush = new VisualBrush(content)
                {
                    Stretch = Stretch.Fill,
                    ViewboxUnits = BrushMappingMode.Absolute,
                    // VisualBrush は部品を親の中の位置(VisualOffset)にずらして描くので、切り出す範囲をその位置に合わせる。
                    Viewbox = new Rect((Point)VisualTreeHelper.GetOffset(content), size),
                };

                context.DrawRectangle(
                    brush,
                    null,
                    new Rect(new Point(topLeft.X - group.Bounds.X, topLeft.Y - group.Bounds.Y), size));
            }
        }

        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(group.Bounds.Width * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Ceiling(group.Bounds.Height * dpi.DpiScaleY)),
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>写しを画素の境目に置く(絵がにじまないように)。</summary>
    private Point SnapToDevicePixels(Point point)
    {
        var dpi = VisualTreeHelper.GetDpi(_itemsControl);
        return new Point(
            Math.Round(point.X * dpi.DpiScaleX) / dpi.DpiScaleX,
            Math.Round(point.Y * dpi.DpiScaleY) / dpi.DpiScaleY);
    }

    /// <summary>押した所が、行の中身の中のボタン(スイッチなど)の中か。中身の外(行そのもののボタンなど)は数えない。</summary>
    private bool IsInsideButtonInContent(DependencyObject source, DependencyObject container)
    {
        var content = FindContent(container);
        var sawButton = false;
        for (var current = source; current is not null && !ReferenceEquals(current, container); current = ElementTree.GetParent(current))
        {
            if (ReferenceEquals(current, content))
            {
                return sawButton;
            }

            if (current is ButtonBase)
            {
                sawButton = true;
            }
        }

        return false;
    }

    private static T? FindAncestor<T>(DependencyObject start)
        where T : DependencyObject
    {
        for (var current = ElementTree.GetParent(start); current is not null; current = ElementTree.GetParent(current))
        {
            if (current is T found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>並べ替えの単位1つぶんの行。</summary>
    private sealed class UnitGroup(object key, Rect bounds, FrameworkElement? content)
    {
        public object Key { get; } = key;

        public Rect Bounds { get; private set; } = bounds;

        public int RowCount { get; private set; } = 1;

        /// <summary>行の中身。見つからなかった行の分は入らない。</summary>
        public List<FrameworkElement> Contents { get; } = content is null ? [] : [content];

        public void Add(Rect nextBounds, FrameworkElement? nextContent)
        {
            Bounds = Rect.Union(Bounds, nextBounds);
            RowCount++;
            if (nextContent is not null)
            {
                Contents.Add(nextContent);
            }
        }
    }
}
