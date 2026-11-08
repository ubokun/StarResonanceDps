using System;
using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.Behaviors;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// タイムライン。縦スクロールはウィンドウ枠側の細いスクロールバーに繋ぐ(他のウィジェットと同じ)。
///
/// <para>
/// <b>末尾に居るときだけ追記に追従する。</b> 途中を読んでいる間に行が増えても位置は動かさない。
/// 追従するかは、利用者のスクロール(内容の高さが変わっていない回)で決める。
/// </para>
///
/// <para>
/// 文字は行をまたいで選べ、選んだ文字は右クリックの「コピー」(窓がアクティブなら Ctrl+C も)でクリップボードへ入れる。
/// 選ぶ・コピーするのは <see cref="TextRowsSelectionBehavior"/>、文字にするのは <see cref="TimelineWidgetViewModel.CreateCopyText"/>。
/// </para>
/// </summary>
public partial class TimelineWidgetView : UserControl, IWidgetVerticalScrollContent
{
    private ScrollViewer? _scrollViewer;
    private bool _followsTail = true;
    private readonly CombatTimelineWindow _extensionController;
    private bool _extensionStarted;
    private bool _extensionStopped;

    public TimelineWidgetView()
    {
        InitializeComponent();
        _extensionController = new CombatTimelineWindow();
        Content = null;
        Resources.MergedDictionaries.Add(_extensionController.Resources);
        System.Windows.Data.BindingOperations.SetBinding(this, ThemePaletteScope.PaletteProperty,
            new System.Windows.Data.Binding("Widget.ThemePalette"));
        SetResourceReference(ForegroundProperty, "Brush.TextPrimary");
        Content = _extensionController.IntegrateExistingLog(LogItemsControl);
        Unloaded += (_, _) => StopExtension();
    }

    private void StopExtension()
    {
        if (_extensionStopped) return;
        _extensionStopped = true;
        _extensionController.StopWidget(); _extensionController.Close();
    }

    public event EventHandler? VerticalScrollMetricsChanged;

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        if (_scrollViewer is null)
        {
            return new WidgetVerticalScrollMetrics(0, 0, 0, 1, 1);
        }

        var maximum = Math.Max(_scrollViewer.ScrollableHeight, 0);
        var viewport = Math.Max(_scrollViewer.ViewportHeight, 0);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(_scrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9, 1),
            18);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        if (_scrollViewer is null)
        {
            return;
        }

        var maximum = Math.Max(_scrollViewer.ScrollableHeight, 0);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0, maximum)
            : 0;

        _scrollViewer.ScrollToVerticalOffset(offset);
    }

    private void TimelineWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_extensionStarted)
        {
            _extensionStarted = true;
            if (Window.GetWindow(this) is { } owner) owner.Closed += (_, _) => StopExtension();
            _extensionController.StartWidget();
        }
        if (_scrollViewer is null)
        {
            LogItemsControl.ApplyTemplate();
            _scrollViewer = (ScrollViewer)LogItemsControl.Template.FindName("LogScrollViewer", LogItemsControl);
            _scrollViewer.ScrollChanged += LogScrollViewer_ScrollChanged;
        }

        NotifyVerticalScrollMetricsChanged();
    }

    private void TimelineWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void LogScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        var scrollViewer = (ScrollViewer)sender;

        if (e.ExtentHeightChange == 0)
        {
            _followsTail = scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 1;
        }
        else if (_followsTail)
        {
            scrollViewer.ScrollToEnd();
        }

        NotifyVerticalScrollMetricsChanged();
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
