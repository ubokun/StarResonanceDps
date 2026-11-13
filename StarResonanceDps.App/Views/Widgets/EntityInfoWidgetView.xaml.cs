// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// エンティティ情報。縦スクロールはバフ・デバフグループと同じく<b>ウィンドウ枠側の細いスクロールバー</b>に繋ぎ、
/// 窓の高さは中身に合わせる(ScrollViewer に付けた <c>Behaviors.AutoFitHeightBehavior</c>)。
///
/// <para>
/// 詠唱の文字とゲージは画面が出ている間だけ 0.1 秒ごとに当て直す(エンティティリストと同じ)。
/// バフ・デバフの枠の右クリックはメニュー(バフリスト・カード・優先表示にする/やめる・非表示)を開く。メニューの開き方はバフ・デバフグループと同じで、
/// 文言(バフかデバフか、優先表示の項目は今の状態も)は開いた枠で決める(<see cref="EntityInfoWidgetViewModel.MenuEntry"/>)。
/// </para>
/// </summary>
public partial class EntityInfoWidgetView : UserControl, IWidgetVerticalScrollContent
{
    private const string BuffContextMenuStyleKey = "Menu.WidgetWindowEntityInfoContextMenu";
    private const string BuffContextMenuFirstItemStyleKey = "Menu.WidgetWindowEntityInfoContextMenuItem.First";
    private const string BuffContextMenuMiddleItemStyleKey = "Menu.WidgetWindowEntityInfoContextMenuItem.Middle";
    private const string BuffContextMenuLastItemStyleKey = "Menu.WidgetWindowEntityInfoContextMenuItem.Last";

    private readonly DispatcherTimer _castRefreshTimer;

    private ContextMenu? _openBuffMenu;
    private PlayerBuffEntry? _openBuffEntry;

    public EntityInfoWidgetView()
    {
        InitializeComponent();
        _castRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _castRefreshTimer.Tick += CastRefreshTimer_Tick;
    }

    public event EventHandler? VerticalScrollMetricsChanged;

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(EntityInfoScrollViewer.ScrollableHeight, 0);
        var viewport = Math.Max(EntityInfoScrollViewer.ViewportHeight, 0);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(EntityInfoScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9, 1),
            49);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(EntityInfoScrollViewer.ScrollableHeight, 0);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0, maximum)
            : 0;

        EntityInfoScrollViewer.ScrollToVerticalOffset(offset);
    }

    private void EntityInfoWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is EntityInfoWidgetViewModel viewModel)
        {
            viewModel.ReapplyCast();
        }

        _castRefreshTimer.Start();
        NotifyVerticalScrollMetricsChanged();
    }

    private void EntityInfoWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        _castRefreshTimer.Stop();
        CloseBuffMenu();
    }

    private void CastRefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (DataContext is EntityInfoWidgetViewModel viewModel)
        {
            viewModel.RefreshCast();
        }
    }

    private void BuffSlot_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button)
        {
            OpenBuffMenu(button);
            e.Handled = true;
        }
    }

    private void OpenBuffMenu(Button button)
    {
        if (DataContext is not EntityInfoWidgetViewModel viewModel
            || button.ContextMenu is not ContextMenu menu
            || button.DataContext is not PlayerBuffEntry entry)
        {
            return;
        }

        viewModel.MenuEntry = entry;
        viewModel.RefreshMenuText();
        ApplyBuffMenuStyles(menu);

        if (!ReferenceEquals(_openBuffMenu, menu))
        {
            CloseBuffMenu();
        }

        menu.PlacementTarget = button;
        SetOpenBuffMenu(menu, entry);
        menu.IsOpen = true;
    }

    private void BuffContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu
            && menu.PlacementTarget is Button { DataContext: PlayerBuffEntry entry })
        {
            SetOpenBuffMenu(menu, entry);
        }
    }

    private void BuffContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            ClearBuffMenu(menu);
        }
    }

    private void SetOpenBuffMenu(ContextMenu menu, PlayerBuffEntry entry)
    {
        if (ReferenceEquals(_openBuffMenu, menu)
            && ReferenceEquals(_openBuffEntry, entry))
        {
            entry.IsMenuOpen = true;
            return;
        }

        CloseBuffMenu();

        _openBuffMenu = menu;
        _openBuffEntry = entry;
        entry.PropertyChanged += OpenBuffEntry_PropertyChanged;
        entry.IsMenuOpen = true;
    }

    private void CloseBuffMenu()
    {
        var menu = _openBuffMenu;
        ClearBuffMenu(menu);

        if (menu?.IsOpen == true)
        {
            menu.IsOpen = false;
        }
    }

    private void ClearBuffMenu(ContextMenu? expectedMenu)
    {
        if (expectedMenu is not null
            && !ReferenceEquals(_openBuffMenu, expectedMenu))
        {
            return;
        }

        var entry = _openBuffEntry;
        _openBuffMenu = null;
        _openBuffEntry = null;

        if (entry is null)
        {
            return;
        }

        entry.PropertyChanged -= OpenBuffEntry_PropertyChanged;
        entry.IsMenuOpen = false;
    }

    /// <summary>枠が消える(<see cref="PlayerBuffEntry.IsMenuOpen"/> を偽にする)ときに、開いているメニューを閉じる。</summary>
    private void OpenBuffEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerBuffEntry.IsMenuOpen)
            && sender is PlayerBuffEntry { IsMenuOpen: false } entry
            && ReferenceEquals(entry, _openBuffEntry))
        {
            CloseBuffMenu();
        }
    }

    private void ApplyBuffMenuStyles(ContextMenu menu)
    {
        if (TryFindResource(BuffContextMenuStyleKey) is Style contextMenuStyle)
        {
            menu.Style = contextMenuStyle;
        }

        var menuItems = menu.Items.OfType<MenuItem>().ToArray();
        for (var index = 0; index < menuItems.Length; index++)
        {
            var styleKey = index switch
            {
                0 => BuffContextMenuFirstItemStyleKey,
                var lastIndex when lastIndex == menuItems.Length - 1 => BuffContextMenuLastItemStyleKey,
                _ => BuffContextMenuMiddleItemStyleKey
            };

            if (TryFindResource(styleKey) is Style menuItemStyle)
            {
                menuItems[index].Style = menuItemStyle;
            }
        }
    }

    private void EntityInfoWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void EntityInfoWidgetView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void EntityInfoScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
