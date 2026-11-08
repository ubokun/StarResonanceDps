using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

public partial class EntityListWidgetView : UserControl, IWidgetVerticalScrollContent
{
    private const string EntitySelectionContextMenuStyleKey = "Menu.WidgetWindowEntitySelectionContextMenu";
    private const string EntitySelectionContextMenuFirstItemStyleKey = "Menu.WidgetWindowEntitySelectionContextMenuItem.First";
    private const string EntitySelectionContextMenuMiddleItemStyleKey = "Menu.WidgetWindowEntitySelectionContextMenuItem.Middle";
    private const string EntitySelectionContextMenuLastItemStyleKey = "Menu.WidgetWindowEntitySelectionContextMenuItem.Last";

    private readonly DispatcherTimer _castRefreshTimer;

    /// <summary>一覧のテンプレートの中の ScrollViewer(仮想化のため一覧の中に置く)。読み込むまでは null。</summary>
    private ScrollViewer? _scrollViewer;

    private ContextMenu? _openEntitySelectionMenu;
    private WidgetListItemViewModel? _openEntitySelectionWidget;

    public event EventHandler? VerticalScrollMetricsChanged;

    public EntityListWidgetView()
    {
        InitializeComponent();
        _castRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _castRefreshTimer.Tick += CastRefreshTimer_Tick;
        Loaded += EntityListWidgetView_Loaded;
        Unloaded += EntityListWidgetView_Unloaded;
        SizeChanged += EntityListWidgetView_SizeChanged;
    }

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
            50);
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

    private void EntityListWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_scrollViewer is null)
        {
            EntityListItemsControl.ApplyTemplate();
            _scrollViewer = (ScrollViewer)EntityListItemsControl.Template.FindName("EntityListScrollViewer", EntityListItemsControl);
            _scrollViewer.ScrollChanged += EntityListScrollViewer_ScrollChanged;
        }

        if (DataContext is WidgetListItemViewModel viewModel)
        {
            viewModel.ReapplyEntityListCasts();
        }

        _castRefreshTimer.Start();
        NotifyVerticalScrollMetricsChanged();
    }

    private void EntityListWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        _castRefreshTimer.Stop();
        CloseEntitySelectionMenu();
    }

    private void CastRefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (DataContext is WidgetListItemViewModel viewModel)
        {
            viewModel.RefreshEntityListCasts();
        }
    }

    private void EntityListWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void EntityListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // スクロールしたら開いているメニューを閉じる(押した行とメニューの位置が離れる)。
        if (e.VerticalChange != 0)
        {
            CloseEntitySelectionMenu();
        }

        NotifyVerticalScrollMetricsChanged();
    }

    /// <summary>行の左クリックはエンティティ情報を開く(右クリックのメニューの1行目と同じ)。</summary>
    private void EntityListItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: EntityListEntry entry }
            && DataContext is WidgetListItemViewModel viewModel)
        {
            viewModel.RequestEntityInfoCommand.Execute(entry.Target);
        }
    }

    private void EntityListItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button)
        {
            OpenEntitySelectionMenu(button);
            e.Handled = true;
        }
    }

    private void OpenEntitySelectionMenu(Button button)
    {
        if (button.ContextMenu is not ContextMenu menu
            || button.DataContext is not EntityListEntry entry
            || DataContext is not WidgetListItemViewModel widget)
        {
            return;
        }

        if (!ReferenceEquals(_openEntitySelectionMenu, menu))
        {
            CloseEntitySelectionMenu();
            _openEntitySelectionMenu = menu;
            _openEntitySelectionWidget = widget;
            widget.EntityMenuTargetLeft += Widget_EntityMenuTargetLeft;
        }

        // 行は位置に結びつき別の敵に替わるので、メニューは開いた時の相手に固定する。
        // 項目の引数は Tag を読み、コマンドは押した行の部品に頼らない(開いている間に行が替わっても開いた時の相手を開く)。
        menu.DataContext = widget;
        menu.Tag = entry.Target;
        foreach (var item in menu.Items.OfType<MenuItem>().Where(x=>Equals(x.Tag,"HateMeter")))
            item.Visibility=entry.EntityType==Zproto.EEntityType.EntMonster ? Visibility.Visible : Visibility.Collapsed;
        ApplyEntitySelectionMenuStyles(menu);
        menu.PlacementTarget = button;
        widget.SetEntityMenuTarget(entry);
        menu.IsOpen = true;
    }

    private void OpenHateMeter_Click(object sender, RoutedEventArgs e)
    {
        if(sender is MenuItem { CommandParameter: EntityTargetInfo target } && target.EntityType==Zproto.EEntityType.EntMonster)
            StarResonanceDps.App.Services.ExtensionWidgetHost.Open(WidgetKind.HatePs, target.EntityUuid);
    }

    private void EntitySelectionContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            ClearEntitySelectionMenu(menu);
        }
    }

    private void Widget_EntityMenuTargetLeft(object? sender, EventArgs e)
    {
        CloseEntitySelectionMenu();
    }

    private void CloseEntitySelectionMenu()
    {
        var menu = _openEntitySelectionMenu;
        ClearEntitySelectionMenu(menu);

        if (menu?.IsOpen == true)
        {
            menu.IsOpen = false;
        }
    }

    private void ClearEntitySelectionMenu(ContextMenu? expectedMenu)
    {
        if (expectedMenu is not null
            && !ReferenceEquals(_openEntitySelectionMenu, expectedMenu))
        {
            return;
        }

        var widget = _openEntitySelectionWidget;
        _openEntitySelectionMenu = null;
        _openEntitySelectionWidget = null;

        if (widget is null)
        {
            return;
        }

        widget.EntityMenuTargetLeft -= Widget_EntityMenuTargetLeft;
        widget.SetEntityMenuTarget(null);
    }

    private void ApplyEntitySelectionMenuStyles(ContextMenu menu)
    {
        if (TryFindResource(EntitySelectionContextMenuStyleKey) is Style contextMenuStyle)
        {
            menu.Style = contextMenuStyle;
        }

        var menuItems = menu.Items.OfType<MenuItem>().Where(x=>x.Visibility==Visibility.Visible).ToArray();
        for (var index = 0; index < menuItems.Length; index++)
        {
            var styleKey = index switch
            {
                0 => EntitySelectionContextMenuFirstItemStyleKey,
                var lastIndex when lastIndex == menuItems.Length - 1 => EntitySelectionContextMenuLastItemStyleKey,
                _ => EntitySelectionContextMenuMiddleItemStyleKey
            };

            if (TryFindResource(styleKey) is Style menuItemStyle)
            {
                menuItems[index].Style = menuItemStyle;
            }
        }
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
