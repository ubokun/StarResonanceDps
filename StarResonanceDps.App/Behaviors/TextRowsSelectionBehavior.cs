// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Serilog;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.Views;

namespace StarResonanceDps.App.Behaviors;

/// <summary>
/// 選んだ範囲。行は一覧の項目の番号、位置は行の文字(<see cref="TextRowsSelectionBehavior"/>)の中の文字の位置。
/// <see cref="StartRow"/> / <see cref="StartOffset"/> が前で、<see cref="EndOffset"/> の文字は含まない。
/// </summary>
public readonly record struct TextRowsSelection(int StartRow, int StartOffset, int EndRow, int EndOffset)
{
    public bool IsEmpty => StartRow == EndRow && StartOffset == EndOffset;
}

/// <summary>
/// 仮想化した <see cref="ItemsControl"/> の行を、行をまたいで文字単位で選べるようにし、選んだ文字をコピーできるようにする振る舞い。
///
/// <para>
/// 使い方: 一覧に <c>TextRowsSelectionBehavior.PartElementNames</c>(行の文字の部品の <see cref="TextBlock"/> の名前を、読む順にカンマで区切る)と、
/// <c>HighlightElementName</c>(選んだ文字の地を描く <see cref="Path"/> の名前。行の作りの左上に置き、地の形で行の大きさが変わらないよう <see cref="Canvas"/> に入れる)を、行の作りの中の名前で付ける。
/// 地の色は行の作りが決め、この振る舞いは形だけを決める。部品は横に並んでも(時刻と本文)、上下に並んでも(名前の段と本文)、本文が折り返してもよい。
/// </para>
///
/// <para>
/// 行の文字は、部品の文字を名前の順に並べ、空でない部品の後ろに区切りの1文字を置いたもの(最後の部品の後ろには置かない)。
/// 部品の文字は <see cref="Run"/>(リンクの中も)の文字と、文の中の部品(顔文字など)に <c>Text</c> で付けた文字。付けていない部品(アイコンなど)は数えない。
/// 隠れている(<see cref="Visibility.Hidden"/>)部品は、文字は数え、場所は部品の先頭として扱う。
/// 選んだ文字をどう文字にするかは、一覧の <c>DataContext</c> が <see cref="ITextRowsCopySource"/> で決める(行の文字の作り方をこの決まりにそろえる)。
/// </para>
///
/// <para>
/// 押してなぞると選び、Shift+クリックで選択を広げ、ただのクリックで選択を解除する。なぞったまま一覧の上下の外へ出ると流れ、ホイールでも流せる。
/// リンクの上で押したときは選び始めない(リンクを押せるように)。右クリックでは選択を変えず、右クリックのメニューは選んだ文字があるときだけ開く。
/// 押したときに一覧へキーボードの焦点を移す(窓がアクティブなら Ctrl+C が一覧に届く)。
/// コピー(<see cref="ApplicationCommands.Copy"/>。右クリックのメニューの項目もこれを使う)は、選んだ文字をクリップボードへ入れる。
/// 入れられなければ(ほかのアプリがクリップボードを使っているときなど)、メッセージの窓で知らせる。オーナーは一覧の窓。
/// </para>
///
/// <para>
/// 選択は「何行目の何文字目」で持つ。前に行が足されたり外されたりしたら番号をずらし、一覧が作り直された(<see cref="NotifyCollectionChangedAction.Reset"/>)ら消す。
/// 地は見えている行(作られている行の部品)の分だけ動かし、画面の文字の1行ごとに四角を描く(同じ高さに並ぶ部品の選択は1つの四角にまとめる)。
/// DragDrop は使わず、マウスの捕捉だけで閉じた作りにする(<see cref="RowReorderBehavior"/> と同じ理由)。
/// </para>
/// </summary>
public static class TextRowsSelectionBehavior
{
    public static readonly DependencyProperty PartElementNamesProperty = DependencyProperty.RegisterAttached(
        "PartElementNames",
        typeof(string),
        typeof(TextRowsSelectionBehavior),
        new PropertyMetadata(null, OnPartElementNamesChanged));

    public static readonly DependencyProperty HighlightElementNameProperty = DependencyProperty.RegisterAttached(
        "HighlightElementName",
        typeof(string),
        typeof(TextRowsSelectionBehavior),
        new PropertyMetadata(null));

    /// <summary>文の中の部品(顔文字・スタンプの絵など)を、選択とコピーでこの文字として数える。部品の作りの一番外の要素に付ける。</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text",
        typeof(string),
        typeof(TextRowsSelectionBehavior),
        new PropertyMetadata(null));

    private static readonly DependencyProperty ControllerProperty = DependencyProperty.RegisterAttached(
        "Controller",
        typeof(TextRowsSelectionController),
        typeof(TextRowsSelectionBehavior),
        new PropertyMetadata(null));

    public static string? GetPartElementNames(DependencyObject obj)
    {
        return (string?)obj.GetValue(PartElementNamesProperty);
    }

    public static void SetPartElementNames(DependencyObject obj, string? value)
    {
        obj.SetValue(PartElementNamesProperty, value);
    }

    public static string? GetHighlightElementName(DependencyObject obj)
    {
        return (string?)obj.GetValue(HighlightElementNameProperty);
    }

    public static void SetHighlightElementName(DependencyObject obj, string? value)
    {
        obj.SetValue(HighlightElementNameProperty, value);
    }

    public static string? GetText(DependencyObject obj)
    {
        return (string?)obj.GetValue(TextProperty);
    }

    public static void SetText(DependencyObject obj, string? value)
    {
        obj.SetValue(TextProperty, value);
    }

    private static void OnPartElementNamesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl itemsControl)
        {
            throw new InvalidOperationException($"{nameof(TextRowsSelectionBehavior)} can only be attached to an ItemsControl.");
        }

        if (itemsControl.GetValue(ControllerProperty) is TextRowsSelectionController previous)
        {
            previous.Detach();
            itemsControl.ClearValue(ControllerProperty);
        }

        var partNames = (e.NewValue as string)?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        if (partNames.Length > 0)
        {
            var controller = new TextRowsSelectionController(itemsControl, partNames);
            controller.Attach();
            itemsControl.SetValue(ControllerProperty, controller);
        }
    }
}

/// <summary>1つの一覧の文字の選択の状態と処理(<see cref="TextRowsSelectionBehavior"/>)。</summary>
internal sealed class TextRowsSelectionController
{
    /// <summary>なぞったまま一覧の外へ出たとき、1回に流す最大の量。外へ遠いほどこの値に近づく。</summary>
    private const double AutoScrollMaxStep = 12d;

    /// <summary>流す量がいちばん大きくなる、一覧の外への距離。</summary>
    private const double AutoScrollFullSpeedDistance = 40d;

    /// <summary>位置の比べの許し(画面の文字の行の上端は、同じ行でも小数の端で揺れる)。</summary>
    private const double LayoutTolerance = 0.5d;

    private static readonly TimeSpan AutoScrollInterval = TimeSpan.FromMilliseconds(16);

    /// <summary>地の <see cref="Path"/> に描いている四角。同じなら描き直さない(描き直しは配置をやり直させる)。</summary>
    private static readonly DependencyProperty HighlightRectsProperty = DependencyProperty.RegisterAttached(
        "HighlightRects",
        typeof(Rect[]),
        typeof(TextRowsSelectionController),
        new PropertyMetadata(null));

    /// <summary>地の <see cref="Path"/> の四角を計算したときの行と範囲と部品の置き場所。同じなら四角も同じなので計算し直さない。</summary>
    private static readonly DependencyProperty HighlightKeyProperty = DependencyProperty.RegisterAttached(
        "HighlightKey",
        typeof(HighlightKey),
        typeof(TextRowsSelectionController),
        new PropertyMetadata(null));

    private readonly ItemsControl _itemsControl;
    private readonly string[] _partNames;
    private readonly DispatcherTimer _autoScrollTimer;
    private readonly CommandBinding _copyBinding;
    private ScrollViewer? _scrollViewer;
    private Panel? _itemsHost;
    private INotifyCollectionChanged? _items;
    private TextPosition? _anchor;
    private TextPosition? _active;
    private bool _isDragging;

    /// <summary>地を出している行の部品がある。選択が無い間に地を消し終えたら、行の作り直しのたびに見直さない。</summary>
    private bool _hasShownHighlights;

    public TextRowsSelectionController(ItemsControl itemsControl, string[] partNames)
    {
        _itemsControl = itemsControl;
        _partNames = partNames;
        _autoScrollTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = AutoScrollInterval
        };
        _autoScrollTimer.Tick += AutoScrollTimer_Tick;
        _copyBinding = new CommandBinding(ApplicationCommands.Copy, Copy_Executed, Copy_CanExecute);
    }

    /// <summary>今選んでいる範囲(前から後ろの順)。選び始めていなければ null。</summary>
    public TextRowsSelection? Selection
    {
        get
        {
            if (_anchor is not { } anchor || _active is not { } active)
            {
                return null;
            }

            var (start, end) = anchor.CompareTo(active) <= 0 ? (anchor, active) : (active, anchor);
            return new TextRowsSelection(start.Row, start.Offset, end.Row, end.Offset);
        }
    }

    public void Attach()
    {
        _itemsControl.PreviewMouseLeftButtonDown += ItemsControl_PreviewMouseLeftButtonDown;
        _itemsControl.PreviewMouseMove += ItemsControl_PreviewMouseMove;
        _itemsControl.PreviewMouseLeftButtonUp += ItemsControl_PreviewMouseLeftButtonUp;
        _itemsControl.LostMouseCapture += ItemsControl_LostMouseCapture;
        _itemsControl.PreviewMouseWheel += ItemsControl_PreviewMouseWheel;
        _itemsControl.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(ItemsControl_ScrollChanged));
        _itemsControl.LayoutUpdated += ItemsControl_LayoutUpdated;
        _itemsControl.ContextMenuOpening += ItemsControl_ContextMenuOpening;
        _itemsControl.CommandBindings.Add(_copyBinding);
        _items = _itemsControl.Items;
        _items.CollectionChanged += Items_CollectionChanged;
    }

    public void Detach()
    {
        EndDrag();
        _itemsControl.PreviewMouseLeftButtonDown -= ItemsControl_PreviewMouseLeftButtonDown;
        _itemsControl.PreviewMouseMove -= ItemsControl_PreviewMouseMove;
        _itemsControl.PreviewMouseLeftButtonUp -= ItemsControl_PreviewMouseLeftButtonUp;
        _itemsControl.LostMouseCapture -= ItemsControl_LostMouseCapture;
        _itemsControl.PreviewMouseWheel -= ItemsControl_PreviewMouseWheel;
        _itemsControl.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(ItemsControl_ScrollChanged));
        _itemsControl.LayoutUpdated -= ItemsControl_LayoutUpdated;
        _itemsControl.ContextMenuOpening -= ItemsControl_ContextMenuOpening;
        _itemsControl.CommandBindings.Remove(_copyBinding);
        if (_items is not null)
        {
            _items.CollectionChanged -= Items_CollectionChanged;
            _items = null;
        }

        _autoScrollTimer.Tick -= AutoScrollTimer_Tick;
    }

    private void Copy_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = Selection is { IsEmpty: false } && _itemsControl.DataContext is ITextRowsCopySource;
        e.Handled = true;
    }

    /// <summary>右クリックのメニュー(項目は「コピー」)は、選んだ文字があるときだけ開く。</summary>
    private void ItemsControl_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (Selection is not { IsEmpty: false })
        {
            e.Handled = true;
        }
    }

    private void Copy_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        e.Handled = true;
        if (_itemsControl.DataContext is not ITextRowsCopySource source || Selection is not { IsEmpty: false } selection)
        {
            return;
        }

        var text = source.CreateCopyText(selection);
        try
        {
            Clipboard.SetText(text);
        }
        catch (ExternalException ex)
        {
            Log.Warning(ex, "Failed to copy selected rows to clipboard");
            var localization = LocalizationManager.Instance;
            MessageWindow.Show(
                Window.GetWindow(_itemsControl),
                localization.GetString("TextCopyFailed_Title"),
                localization.GetString("TextCopyFailed_Message"),
                localization.GetString("TextCopyFailed_Detail"));
        }
    }

    private void ItemsControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 1 || IsInHyperlink(e.OriginalSource) || !TryHitTest(e.GetPosition(_itemsControl), out var hit))
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 && _anchor is not null)
        {
            _active = hit;
        }
        else
        {
            _anchor = hit;
            _active = hit;
        }

        _isDragging = _itemsControl.CaptureMouse();
        _itemsControl.Focus();
        e.Handled = true;
        RefreshHighlights();
    }

    private void ItemsControl_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var point = e.GetPosition(_itemsControl);
        if (point.Y < 0d || point.Y > _itemsControl.ActualHeight)
        {
            _autoScrollTimer.Start();
        }
        else
        {
            _autoScrollTimer.Stop();
        }

        UpdateActive(point);
    }

    private void ItemsControl_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        EndDrag();
        e.Handled = true;
    }

    private void ItemsControl_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _isDragging = false;
        _autoScrollTimer.Stop();
    }

    /// <summary>
    /// なぞっている間はマウスを一覧が捕まえているので、ホイールの知らせが中のスクロールに届かない。
    /// 同じ回転の量の知らせをスクロールへ渡して、普段と同じ量だけ流す。
    /// </summary>
    private void ItemsControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_isDragging || GetScrollViewer() is not { } scrollViewer)
        {
            return;
        }

        scrollViewer.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent
        });
        e.Handled = true;
    }

    /// <summary>なぞっている間に流れたら(ホイール・一覧の外での自動スクロール)、マウスが指している行と文字まで選択を伸ばす。</summary>
    private void ItemsControl_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isDragging)
        {
            UpdateActive(Mouse.GetPosition(_itemsControl));
        }
    }

    /// <summary>
    /// 行の部品の作り直し(流したときの使い回しなど)や文字の変化に追いかける。選択も地も無ければ何もしない。
    /// 知らせは窓の中のどの配置でも来るので、変わっていない行は <see cref="RefreshHighlights"/> が計算を省く。
    /// </summary>
    private void ItemsControl_LayoutUpdated(object? sender, EventArgs e)
    {
        if (Selection is { IsEmpty: false } || _hasShownHighlights)
        {
            RefreshHighlights();
        }
    }

    /// <summary>
    /// 選んでいる位置は行の番号で持つので、前に行が足されたり外されたりしたら番号をずらし、同じ行の文字を選んだままにする
    /// (古い行を前から外していく一覧がある)。外された行の中の位置は、外された所の次の行の先頭へ寄せる。作り直しなら選択を消す。
    /// </summary>
    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var (anchor, active) = (_anchor, _active);
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is { Count: > 0 } added && e.NewStartingIndex >= 0:
                _anchor = ShiftForInsert(_anchor, e.NewStartingIndex, added.Count);
                _active = ShiftForInsert(_active, e.NewStartingIndex, added.Count);
                break;
            case NotifyCollectionChangedAction.Remove when e.OldItems is { Count: > 0 } removed && e.OldStartingIndex >= 0:
                _anchor = ShiftForRemove(_anchor, e.OldStartingIndex, removed.Count);
                _active = ShiftForRemove(_active, e.OldStartingIndex, removed.Count);
                break;
            case NotifyCollectionChangedAction.Reset:
                EndDrag();
                _anchor = null;
                _active = null;
                break;
            default:
                return;
        }

        if (e.Action == NotifyCollectionChangedAction.Reset || anchor != _anchor || active != _active)
        {
            RefreshHighlights();
        }
    }

    private static TextPosition? ShiftForInsert(TextPosition? position, int index, int count)
    {
        return position is { } current && current.Row >= index ? current with { Row = current.Row + count } : position;
    }

    private static TextPosition? ShiftForRemove(TextPosition? position, int index, int count)
    {
        if (position is not { } current || current.Row < index)
        {
            return position;
        }

        return current.Row >= index + count
            ? current with { Row = current.Row - count }
            : new TextPosition(index, 0);
    }

    private void EndDrag()
    {
        _isDragging = false;
        _autoScrollTimer.Stop();
        if (ReferenceEquals(Mouse.Captured, _itemsControl))
        {
            _itemsControl.ReleaseMouseCapture();
        }
    }

    /// <summary>なぞったまま一覧の上下の外にいる間、外へ遠いほど速く流し、端の行の文字までを選ぶ。</summary>
    private void AutoScrollTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isDragging || GetScrollViewer() is not { } scrollViewer)
        {
            _autoScrollTimer.Stop();
            return;
        }

        var point = Mouse.GetPosition(_itemsControl);
        var distance = point.Y < 0d
            ? point.Y
            : point.Y > _itemsControl.ActualHeight
                ? point.Y - _itemsControl.ActualHeight
                : 0d;
        if (distance == 0d)
        {
            _autoScrollTimer.Stop();
            return;
        }

        var step = Math.Sign(distance) * AutoScrollMaxStep * Math.Min(1d, Math.Abs(distance) / AutoScrollFullSpeedDistance);
        var offset = Math.Clamp(scrollViewer.VerticalOffset + step, 0d, scrollViewer.ScrollableHeight);
        if (offset != scrollViewer.VerticalOffset)
        {
            scrollViewer.ScrollToVerticalOffset(offset);
            scrollViewer.UpdateLayout();
        }

        UpdateActive(point);
    }

    private void UpdateActive(Point point)
    {
        if (TryHitTest(point, out var hit) && hit != _active)
        {
            _active = hit;
            RefreshHighlights();
        }
    }

    /// <summary>押した所がリンク(<see cref="Hyperlink"/>)の中か。</summary>
    private static bool IsInHyperlink(object? source)
    {
        for (var current = source as FrameworkContentElement; current is not null; current = current.Parent as FrameworkContentElement)
        {
            if (current is Hyperlink)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 一覧の座標の点が指す、行と文字の位置。見えている行より上なら最初の行の先頭、下なら最後の行の末尾。
    /// 作られている行が無ければ false。
    /// </summary>
    private bool TryHitTest(Point point, out TextPosition position)
    {
        position = default;
        var rows = GetRealizedRows();
        if (rows.Count == 0)
        {
            return false;
        }

        if (point.Y < rows[0].Top)
        {
            position = new TextPosition(rows[0].Index, 0);
            return true;
        }

        var row = rows.Find(candidate => point.Y < candidate.Bottom);
        if (row is null)
        {
            var last = rows[^1];
            position = new TextPosition(last.Index, GetRowLayout(last.Container).Length);
            return true;
        }

        position = new TextPosition(row.Index, GetRowOffsetAt(row.Container, point));
        return true;
    }

    /// <summary>
    /// 行の中の点が指す文字の位置。点と同じ高さの部品の中ならその部品の文字の位置(隠れた部品は先頭)、
    /// 同じ高さの部品の左やあいだなら次の部品の先頭、右なら最後の部品の末尾。同じ高さに部品が無ければ、下にある最初の部品の先頭か行の末尾。
    /// </summary>
    private int GetRowOffsetAt(FrameworkElement container, Point point)
    {
        var layout = GetRowLayout(container);
        RowPart? lastOnLine = null;
        foreach (var part in layout.Parts)
        {
            var bounds = GetPartBounds(part.Block, _itemsControl);
            if (point.Y < bounds.Top - LayoutTolerance)
            {
                return lastOnLine is null ? part.Start : lastOnLine.Start + lastOnLine.Length;
            }

            if (point.Y >= bounds.Bottom)
            {
                continue;
            }

            if (point.X < bounds.Left)
            {
                return part.Start;
            }

            if (point.X <= bounds.Right)
            {
                return part.Block.Visibility == Visibility.Visible
                    ? part.Start + GetTextOffsetAt(part, _itemsControl.TranslatePoint(point, part.Block))
                    : part.Start;
            }

            lastOnLine = part;
        }

        return lastOnLine is null ? layout.Length : lastOnLine.Start + lastOnLine.Length;
    }

    /// <summary>
    /// 見えている行の地を、選んだ文字の範囲に合わせる。選んでいない行は地を消す。
    /// 行と範囲と部品の置き場所が前に計算したときと同じ行は、計算し直さずに今の地のままにする。
    /// </summary>
    private void RefreshHighlights()
    {
        var selection = Selection;
        var highlightName = TextRowsSelectionBehavior.GetHighlightElementName(_itemsControl);
        var hasShownHighlights = false;
        foreach (var row in GetRealizedRows())
        {
            if (FindPart<Path>(row.Container, highlightName) is not { } highlight)
            {
                continue;
            }

            Rect[] rects = [];
            HighlightKey? key = null;
            if (selection is { IsEmpty: false } range && row.Index >= range.StartRow && row.Index <= range.EndRow)
            {
                var layout = GetRowLayout(row.Container);
                var start = row.Index == range.StartRow ? Math.Clamp(range.StartOffset, 0, layout.Length) : 0;
                var end = row.Index == range.EndRow ? Math.Clamp(range.EndOffset, start, layout.Length) : layout.Length;
                if (start < end)
                {
                    key = new HighlightKey(row.Index, start, end, GetPartPlacements(layout, highlight));
                    if (key.Equals(highlight.GetValue(HighlightKeyProperty)))
                    {
                        hasShownHighlights |= ((Rect[]?)highlight.GetValue(HighlightRectsProperty))?.Length > 0;
                        continue;
                    }

                    rects = GetSelectedRects(layout, start, end, highlight);
                }
            }

            highlight.SetValue(HighlightKeyProperty, key);
            SetHighlightRects(highlight, rects);
            hasShownHighlights |= rects.Length > 0;
        }

        _hasShownHighlights = hasShownHighlights;
    }

    /// <summary>行の部品ごとの、地の座標の置き場所と大きさ・余白・見え方・文字の範囲。地の四角はこれと選んだ範囲だけで決まる。</summary>
    private static PartPlacement[] GetPartPlacements(RowLayout layout, UIElement highlight)
    {
        var placements = new PartPlacement[layout.Parts.Count];
        for (var index = 0; index < placements.Length; index++)
        {
            var part = layout.Parts[index];
            var block = part.Block;
            var origin = block.TranslatePoint(new Point(0d, 0d), highlight);
            placements[index] = new PartPlacement(
                origin.X, origin.Y, block.ActualWidth, block.ActualHeight,
                block.Margin.Top, block.Margin.Bottom, block.Visibility, part.Start, part.Length, block.Inlines.FirstInline);
        }

        return placements;
    }

    /// <summary>
    /// 行の中の選んだ文字の、画面の文字の1行ごとの四角(地の <paramref name="highlight"/> の座標)。
    /// 部品の最初と最後の画面の文字の行は、同じ高さに並ぶ部品の余白まで含めた上下まで広げる(横に並ぶ部品の地の高さがそろう)。
    /// 隠れた部品の中から選んでいれば部品の左端から、部品の後ろの区切りを選んでいれば部品の末尾から次の部品の先頭まで地を伸ばす。
    /// </summary>
    private static Rect[] GetSelectedRects(RowLayout layout, int start, int end, UIElement highlight)
    {
        var parts = layout.Parts.Where(part => part.Block.Visibility != Visibility.Collapsed).ToList();
        var bounds = parts.Select(part => GetPartBounds(part.Block, highlight)).ToList();
        var rects = new List<Rect>();
        for (var index = 0; index < parts.Count; index++)
        {
            var part = parts[index];
            var partEnd = part.Start + part.Length;
            if (part.Block.Visibility == Visibility.Hidden)
            {
                if (start < partEnd && end > part.Start)
                {
                    AddRect(rects, part, GetStartCaretRect(part), GetBand(bounds, index), highlight);
                }

                continue;
            }

            var from = Math.Clamp(start - part.Start, 0, part.Length);
            var to = Math.Clamp(end - part.Start, 0, part.Length);
            if (from < to)
            {
                foreach (var rect in GetLineRects(part, from, to))
                {
                    AddRect(rects, part, rect, GetBand(bounds, index), highlight);
                }
            }

            if (part.Length > 0 && start <= partEnd && end > partEnd && partEnd < layout.Length)
            {
                AddRect(rects, part, GetEndCaretRect(part), GetBand(bounds, index), highlight);
                if (index + 1 < parts.Count && parts[index + 1].Start == partEnd + 1)
                {
                    AddRect(rects, parts[index + 1], GetStartCaretRect(parts[index + 1]), GetBand(bounds, index + 1), highlight);
                }
            }
        }

        return MergeSameLineRects(rects);
    }

    /// <summary>部品と同じ高さに並ぶ部品(自分も含む)の、余白まで含めた四角を合わせたもの。上下だけを使う。</summary>
    private static Rect GetBand(List<Rect> bounds, int index)
    {
        var band = bounds[index];
        foreach (var other in bounds)
        {
            if (OverlapsVertically(other, bounds[index]))
            {
                band.Union(other);
            }
        }

        return band;
    }

    /// <summary>
    /// 部品の座標の四角を、横は部品の文字の箱の中に収め、地の座標で足す。部品の最初の画面の文字の行は上を、最後の行は下を、
    /// 同じ高さに並ぶ部品の上下(<paramref name="band"/>)まで広げる。
    /// </summary>
    private static void AddRect(List<Rect> rects, RowPart part, Rect rect, Rect band, UIElement highlight)
    {
        var block = part.Block;
        var left = Math.Clamp(rect.Left, 0d, block.ActualWidth);
        var right = Math.Clamp(rect.Right, left, block.ActualWidth);
        var origin = block.TranslatePoint(new Point(left, rect.Top), highlight);
        var top = OverlapsVertically(rect, GetStartCaretRect(part)) ? band.Top : origin.Y;
        var bottom = OverlapsVertically(rect, GetLastLineCaretRect(part)) ? band.Bottom : origin.Y + rect.Height;
        rects.Add(new Rect(origin.X, top, right - left, Math.Max(0d, bottom - top)));
    }

    private static bool OverlapsVertically(Rect first, Rect second)
    {
        return Math.Min(first.Bottom, second.Bottom) - Math.Max(first.Top, second.Top) > LayoutTolerance;
    }

    /// <summary>高さが重なる四角(同じ画面の文字の行)を1つにまとめる。幅の無い四角は落とす。</summary>
    private static Rect[] MergeSameLineRects(List<Rect> rects)
    {
        var merged = new List<Rect>();
        foreach (var rect in rects.OrderBy(rect => rect.Top))
        {
            var index = merged.FindIndex(line =>
                Math.Min(line.Bottom, rect.Bottom) - Math.Max(line.Top, rect.Top) > LayoutTolerance);
            if (index < 0)
            {
                merged.Add(rect);
            }
            else
            {
                merged[index] = Rect.Union(merged[index], rect);
            }
        }

        return [.. merged.Where(rect => rect.Width > 0d)];
    }

    /// <summary>部品の文字の範囲の、画面の文字の1行ごとの四角(部品の座標)。</summary>
    private static List<Rect> GetLineRects(RowPart part, int from, int to)
    {
        var lines = new List<Rect>();
        foreach (var unit in part.Units)
        {
            var unitFrom = Math.Max(from, unit.Start);
            var unitTo = Math.Min(to, unit.Start + unit.Length);
            if (unitFrom >= unitTo)
            {
                continue;
            }

            if (unit.Run is { } run)
            {
                AddRunLineRects(lines, run, unitFrom - unit.Start, unitTo - unit.Start);
            }
            else if (unit.Container?.Child is { } element)
            {
                AddToLine(lines, GetElementRect(element, part.Block));
            }
        }

        return lines;
    }

    /// <summary>
    /// <see cref="Run"/> の文字の範囲(<paramref name="from"/> から <paramref name="to"/> の手前まで)を、画面の文字の行ごとの四角にして足す。
    /// 測るのは各行の最初の文字の前と最後の文字の後ろだけ(文字の位置を測るのは重い)。範囲の両端の文字の行が違えば、
    /// 行が替わる文字を二分探索で探して行を分ける(文字の行の上端は文字の並びの順に下がっていく)。
    /// </summary>
    private static void AddRunLineRects(List<Rect> lines, Run run, int from, int to)
    {
        var start = from;
        while (start < to)
        {
            var leading = GetLeadingRect(run, start);
            var lineEnd = to;
            if (!IsSameLine(GetLeadingRect(run, to - 1), leading))
            {
                var low = start + 1;
                var high = to - 1;
                while (low < high)
                {
                    var middle = low + (high - low) / 2;
                    if (IsSameLine(GetLeadingRect(run, middle), leading))
                    {
                        low = middle + 1;
                    }
                    else
                    {
                        high = middle;
                    }
                }

                lineEnd = low;
            }

            var trailing = run.ContentStart.GetPositionAtOffset(lineEnd)!.GetCharacterRect(LogicalDirection.Backward);
            AddToLine(lines, new Rect(leading.Left, leading.Top, Math.Max(0d, trailing.Left - leading.Left), leading.Height));
            start = lineEnd;
        }
    }

    /// <summary><see cref="Run"/> の中の文字の前の位置の四角(部品の座標)。</summary>
    private static Rect GetLeadingRect(Run run, int offset)
    {
        return run.ContentStart.GetPositionAtOffset(offset)!.GetCharacterRect(LogicalDirection.Forward);
    }

    private static bool IsSameLine(Rect first, Rect second)
    {
        return Math.Abs(first.Top - second.Top) <= LayoutTolerance;
    }

    /// <summary>同じ画面の文字の行(上端がほぼ同じ)の四角へ広げる。無ければ新しい行にする。</summary>
    private static void AddToLine(List<Rect> lines, Rect rect)
    {
        var index = lines.FindIndex(line => Math.Abs(line.Top - rect.Top) <= LayoutTolerance);
        if (index < 0)
        {
            lines.Add(rect);
        }
        else
        {
            lines[index] = Rect.Union(lines[index], rect);
        }
    }

    /// <summary>部品の文字の先頭の位置の、幅の無い四角(部品の座標)。隠れているか文字が無ければ、部品の左端の高さいっぱい。</summary>
    private static Rect GetStartCaretRect(RowPart part)
    {
        if (part.Block.Visibility != Visibility.Visible || part.Units.Count == 0)
        {
            return new Rect(0d, 0d, 0d, part.Block.ActualHeight);
        }

        var first = part.Units[0];
        if (first.Run is { } run)
        {
            var caret = run.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            return new Rect(caret.Left, caret.Top, 0d, caret.Height);
        }

        var element = GetElementRect(first.Container!.Child, part.Block);
        return new Rect(element.Left, element.Top, 0d, element.Height);
    }

    /// <summary>部品の最後の画面の文字の行にある、幅の無い四角(部品の座標)。隠れているか文字が無ければ、部品の左端の高さいっぱい。</summary>
    private static Rect GetLastLineCaretRect(RowPart part)
    {
        return part.Block.Visibility != Visibility.Visible || part.Units.Count == 0
            ? new Rect(0d, 0d, 0d, part.Block.ActualHeight)
            : GetEndCaretRect(part);
    }

    /// <summary>部品の文字の末尾の位置の、幅の無い四角(部品の座標)。</summary>
    private static Rect GetEndCaretRect(RowPart part)
    {
        var last = part.Units[^1];
        if (last.Run is { } run)
        {
            var caret = run.ContentEnd.GetCharacterRect(LogicalDirection.Backward);
            return new Rect(caret.Left, caret.Top, 0d, caret.Height);
        }

        var element = GetElementRect(last.Container!.Child, part.Block);
        return new Rect(element.Right, element.Top, 0d, element.Height);
    }

    private static Rect GetElementRect(UIElement element, TextBlock block)
    {
        return new Rect(element.TranslatePoint(new Point(0d, 0d), block), element.RenderSize);
    }

    /// <summary>描いている四角と違えば、地の形を描き直す。</summary>
    private static void SetHighlightRects(Path highlight, Rect[] rects)
    {
        var current = (Rect[]?)highlight.GetValue(HighlightRectsProperty) ?? [];
        if (current.SequenceEqual(rects))
        {
            return;
        }

        highlight.SetValue(HighlightRectsProperty, rects);
        if (rects.Length == 0)
        {
            highlight.Data = null;
            return;
        }

        var geometry = new GeometryGroup { FillRule = FillRule.Nonzero };
        foreach (var rect in rects)
        {
            geometry.Children.Add(new RectangleGeometry(rect));
        }

        geometry.Freeze();
        highlight.Data = geometry;
    }

    /// <summary>部品の中の点に一番近い文字の位置。文の中の部品(顔文字など)の上なら、左半分は前・右半分は後ろ。</summary>
    private static int GetTextOffsetAt(RowPart part, Point point)
    {
        foreach (var unit in part.Units)
        {
            if (unit.Container?.Child is { } element)
            {
                var rect = GetElementRect(element, part.Block);
                if (rect.Contains(point))
                {
                    return point.X < rect.Left + rect.Width / 2d ? unit.Start : unit.Start + unit.Length;
                }
            }
        }

        var block = part.Block;
        var clamped = new Point(
            Math.Max(0d, point.X),
            Math.Clamp(point.Y, 0d, Math.Max(0d, block.ActualHeight - 1d)));
        if (block.GetPositionFromPoint(clamped, snapToText: true) is not { } pointer)
        {
            return clamped.X <= 0d ? 0 : part.Length;
        }

        foreach (var unit in part.Units)
        {
            if (unit.Run is { } run)
            {
                if (pointer.CompareTo(run.ContentStart) <= 0)
                {
                    return unit.Start;
                }

                if (pointer.CompareTo(run.ContentEnd) <= 0)
                {
                    return unit.Start + run.ContentStart.GetOffsetToPosition(pointer);
                }
            }
            else if (unit.Container is { } container)
            {
                if (pointer.CompareTo(container.ElementStart) <= 0)
                {
                    return unit.Start;
                }

                if (pointer.CompareTo(container.ElementEnd) <= 0)
                {
                    return unit.Start + unit.Length;
                }
            }
        }

        return part.Length;
    }

    /// <summary>部品の、横は文字の箱・縦は余白まで広げた四角(<paramref name="reference"/> の座標)。</summary>
    private static Rect GetPartBounds(TextBlock block, UIElement reference)
    {
        var origin = block.TranslatePoint(new Point(0d, 0d), reference);
        return new Rect(
            origin.X,
            origin.Y - block.Margin.Top,
            block.ActualWidth,
            block.ActualHeight + block.Margin.Top + block.Margin.Bottom);
    }

    /// <summary>行の文字の部品(名前の順、見つからない部品は飛ばす)と、行の文字の長さ。</summary>
    private RowLayout GetRowLayout(FrameworkElement container)
    {
        var parts = new List<RowPart>();
        var start = 0;
        for (var index = 0; index < _partNames.Length; index++)
        {
            if (FindPart<TextBlock>(container, _partNames[index]) is not { } block)
            {
                continue;
            }

            var units = GetTextUnits(block);
            var length = units.Count == 0 ? 0 : units[^1].Start + units[^1].Length;
            parts.Add(new RowPart(block, units, start, length));
            start += length;
            if (length > 0 && index < _partNames.Length - 1)
            {
                start++;
            }
        }

        return new RowLayout(parts, start);
    }

    /// <summary>部品の文字の並び。<see cref="Run"/>(リンクなどの中も)と、<c>Text</c> を付けた文の中の部品。</summary>
    private static List<TextUnit> GetTextUnits(TextBlock block)
    {
        var units = new List<TextUnit>();
        var offset = 0;
        AddTextUnits(block.Inlines, units, ref offset);
        return units;
    }

    private static void AddTextUnits(InlineCollection inlines, List<TextUnit> units, ref int offset)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    units.Add(new TextUnit(offset, run.Text.Length, run, null));
                    offset += run.Text.Length;
                    break;
                case Span span:
                    AddTextUnits(span.Inlines, units, ref offset);
                    break;
                case InlineUIContainer container when GetContainerText(container) is { Length: > 0 } text:
                    units.Add(new TextUnit(offset, text.Length, null, container));
                    offset += text.Length;
                    break;
            }
        }
    }

    /// <summary>文の中の部品に付けた文字。部品そのものか、部品が中身を出す作りの一番外の要素に付いたもの。</summary>
    private static string? GetContainerText(InlineUIContainer container)
    {
        if (container.Child is not { } child)
        {
            return null;
        }

        return TextRowsSelectionBehavior.GetText(child)
            ?? (VisualTreeHelper.GetChildrenCount(child) > 0
                ? TextRowsSelectionBehavior.GetText(VisualTreeHelper.GetChild(child, 0))
                : null);
    }

    /// <summary>作られていて見えている行の部品(一覧の座標の上端と下端つき)。行の番号の順。</summary>
    private List<RealizedRow> GetRealizedRows()
    {
        var rows = new List<RealizedRow>();
        if (GetItemsHost() is not { } itemsHost)
        {
            return rows;
        }

        foreach (UIElement child in itemsHost.Children)
        {
            if (child is not FrameworkElement { IsVisible: true } container)
            {
                continue;
            }

            var index = _itemsControl.ItemContainerGenerator.IndexFromContainer(container);
            if (index < 0)
            {
                continue;
            }

            var top = container.TranslatePoint(new Point(0d, 0d), _itemsControl).Y;
            rows.Add(new RealizedRow(index, container, top, top + container.ActualHeight));
        }

        rows.Sort((left, right) => left.Index.CompareTo(right.Index));
        return rows;
    }

    private Panel? GetItemsHost()
    {
        return _itemsHost ??= FindDescendant<Panel>(_itemsControl, panel => panel.IsItemsHost);
    }

    private ScrollViewer? GetScrollViewer()
    {
        return _scrollViewer ??= FindDescendant<ScrollViewer>(_itemsControl, _ => true);
    }

    /// <summary>行の部品の中の、その名前の要素。名前が空か見つからなければ null。</summary>
    private static T? FindPart<T>(DependencyObject container, string? name)
        where T : FrameworkElement
    {
        return string.IsNullOrEmpty(name)
            ? null
            : FindDescendant<T>(container, element => element.Name == name);
    }

    private static T? FindDescendant<T>(DependencyObject start, Func<T, bool> predicate)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(start);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(start, index);
            if (child is T found && predicate(found))
            {
                return found;
            }

            if (FindDescendant(child, predicate) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>行と、行の文字の中の位置。</summary>
    private readonly record struct TextPosition(int Row, int Offset) : IComparable<TextPosition>
    {
        public int CompareTo(TextPosition other)
        {
            return Row != other.Row ? Row.CompareTo(other.Row) : Offset.CompareTo(other.Offset);
        }
    }

    private sealed record RealizedRow(int Index, FrameworkElement Container, double Top, double Bottom);

    /// <summary>地の四角を決める入力(<see cref="HighlightKeyProperty"/>)。部品は並びごと値で比べる。</summary>
    private sealed record HighlightKey(int Row, int Start, int End, PartPlacement[] Parts)
    {
        public bool Equals(HighlightKey? other)
        {
            return other is not null
                && Row == other.Row
                && Start == other.Start
                && End == other.End
                && Parts.AsSpan().SequenceEqual(other.Parts);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Row, Start, End, Parts.Length);
        }
    }

    /// <summary>
    /// 部品の置き場所(地の座標)・大きさ・余白・見え方・文字の範囲と、最初の文の要素(中身が替わると別の要素になる)。
    /// </summary>
    private readonly record struct PartPlacement(
        double X,
        double Y,
        double Width,
        double Height,
        double MarginTop,
        double MarginBottom,
        Visibility Visibility,
        int Start,
        int Length,
        Inline? FirstInline);

    /// <summary>部品の文字の1つの並び。<see cref="Run"/> か、文字を付けた文の中の部品(<see cref="Container"/>)。</summary>
    private sealed record TextUnit(int Start, int Length, Run? Run, InlineUIContainer? Container);

    /// <summary>行の文字の部品1つ。<see cref="Start"/> は行の文字の中の部品の先頭。</summary>
    private sealed record RowPart(TextBlock Block, List<TextUnit> Units, int Start, int Length);

    private sealed record RowLayout(List<RowPart> Parts, int Length);
}
