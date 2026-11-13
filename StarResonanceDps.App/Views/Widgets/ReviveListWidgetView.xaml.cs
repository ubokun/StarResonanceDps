// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// 蘇生リストの中身。縦のスクロールバーは窓の枠の層が出す(プレイヤーリストと同じ)。
/// 行の左クリックと右クリックのメニューの作りはプレイヤーリストと同じ。
/// </summary>
public partial class ReviveListWidgetView : UserControl, IWidgetVerticalScrollContent
{
    private const string PlayerSelectionContextMenuStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenu";
    private const string PlayerSelectionContextMenuFirstItemStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenuItem.First";
    private const string PlayerSelectionContextMenuMiddleItemStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenuItem.Middle";
    private const string PlayerSelectionContextMenuLastItemStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenuItem.Last";

    /// <summary>スクロールバーの矢印1回で動く量。行の高さと同じ。</summary>
    private const double SmallScrollChange = 31d;

    private ContextMenu? _openPlayerSelectionMenu;
    private ReviveListEntry? _openPlayerSelectionEntry;

    public event EventHandler? VerticalScrollMetricsChanged;

    public ReviveListWidgetView()
    {
        InitializeComponent();
        Loaded += ReviveListWidgetView_Loaded;
        Unloaded += ReviveListWidgetView_Unloaded;
        SizeChanged += ReviveListWidgetView_SizeChanged;
    }

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(ReviveListScrollViewer.ScrollableHeight, 0);
        var viewport = Math.Max(ReviveListScrollViewer.ViewportHeight, 0);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(ReviveListScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9, 1),
            SmallScrollChange);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(ReviveListScrollViewer.ScrollableHeight, 0);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0, maximum)
            : 0;

        ReviveListScrollViewer.ScrollToVerticalOffset(offset);
    }

    private void ReviveListWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void ReviveListWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        ClosePlayerSelectionMenu();
    }

    private void ReviveListWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void ReviveListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    /// <summary>左クリックはメニューを開かず、その行の人のプレイヤー情報を直接開く。メニューは右クリック側が担う。</summary>
    private void ReviveListItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ReviveListWidgetViewModel viewModel, DataContext: ReviveListEntry entry })
        {
            viewModel.RequestPlayerInfoCommand.Execute(entry);
        }
    }

    private void ReviveListItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button)
        {
            OpenPlayerSelectionMenu(button);
            e.Handled = true;
        }
    }

    private void OpenPlayerSelectionMenu(Button button)
    {
        if (button.ContextMenu is not ContextMenu menu || button.DataContext is not ReviveListEntry entry)
        {
            return;
        }

        ApplyPlayerSelectionMenuStyles(menu);

        if (!ReferenceEquals(_openPlayerSelectionMenu, menu))
        {
            ClosePlayerSelectionMenu();
        }

        menu.PlacementTarget = button;
        SetOpenPlayerSelectionMenu(menu, entry);
        menu.IsOpen = true;
    }

    private void PlayerSelectionContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu
            && menu.PlacementTarget is Button { DataContext: ReviveListEntry entry })
        {
            SetOpenPlayerSelectionMenu(menu, entry);
        }
    }

    private void PlayerSelectionContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            ClearPlayerSelectionMenu(menu);
        }
    }

    private void SetOpenPlayerSelectionMenu(ContextMenu menu, ReviveListEntry entry)
    {
        if (ReferenceEquals(_openPlayerSelectionMenu, menu)
            && ReferenceEquals(_openPlayerSelectionEntry, entry))
        {
            entry.IsPlayerSelectionMenuOpen = true;
            return;
        }

        ClosePlayerSelectionMenu();

        _openPlayerSelectionMenu = menu;
        _openPlayerSelectionEntry = entry;
        entry.PropertyChanged += OpenPlayerSelectionEntry_PropertyChanged;
        entry.IsPlayerSelectionMenuOpen = true;
    }

    private void ClosePlayerSelectionMenu()
    {
        var menu = _openPlayerSelectionMenu;
        ClearPlayerSelectionMenu(menu);

        if (menu?.IsOpen == true)
        {
            menu.IsOpen = false;
        }
    }

    private void ClearPlayerSelectionMenu(ContextMenu? expectedMenu)
    {
        if (expectedMenu is not null
            && !ReferenceEquals(_openPlayerSelectionMenu, expectedMenu))
        {
            return;
        }

        var entry = _openPlayerSelectionEntry;
        _openPlayerSelectionMenu = null;
        _openPlayerSelectionEntry = null;

        if (entry is null)
        {
            return;
        }

        entry.PropertyChanged -= OpenPlayerSelectionEntry_PropertyChanged;
        entry.IsPlayerSelectionMenuOpen = false;
    }

    /// <summary>行の側でメニューの印が下ろされた(その人が一覧から消えた)。メニューを閉じる。</summary>
    private void OpenPlayerSelectionEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReviveListEntry.IsPlayerSelectionMenuOpen)
            && sender is ReviveListEntry { IsPlayerSelectionMenuOpen: false } entry
            && ReferenceEquals(entry, _openPlayerSelectionEntry))
        {
            ClosePlayerSelectionMenu();
        }
    }

    private void ApplyPlayerSelectionMenuStyles(ContextMenu menu)
    {
        if (TryFindResource(PlayerSelectionContextMenuStyleKey) is Style contextMenuStyle)
        {
            menu.Style = contextMenuStyle;
        }

        // 隠れている項目は数に入れない。自分の行だけに出る「ステータス詳細」を
        // Collapsed のまま数えると、見えていない項目に先頭/末尾の角丸が当たる。
        var menuItems = menu.Items.OfType<MenuItem>()
            .Where(item => item.Visibility == Visibility.Visible)
            .ToArray();
        for (var index = 0; index < menuItems.Length; index++)
        {
            var styleKey = index switch
            {
                0 => PlayerSelectionContextMenuFirstItemStyleKey,
                var lastIndex when lastIndex == menuItems.Length - 1 => PlayerSelectionContextMenuLastItemStyleKey,
                _ => PlayerSelectionContextMenuMiddleItemStyleKey
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
