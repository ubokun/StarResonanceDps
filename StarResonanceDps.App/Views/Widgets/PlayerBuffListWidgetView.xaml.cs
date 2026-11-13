// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>
/// バフ/デバフの一覧。プレイヤー用とエンティティ用の2つのViewModel(<see cref="IBuffListWindowViewModel"/>)が同じこのビューを使う。
///
/// <para>
/// 縦スクロールは<b>ウィンドウ枠側の細いスクロールバー</b>に繋ぐ。内側にWPFのスクロールバーを
/// 出すと他のウィジェットと見た目が揃わず、コンテンツ幅も削られる。
/// </para>
///
/// <para>
/// 行の左クリックはカードを開き、右クリックはメニュー(カード・優先表示にする/やめる・非表示・グループに追加・バフ・デバフカードのショートカットに追加)を開く。
/// メニューの開き方はエンティティリストと同じ。優先表示の項目の文言は開いた行の今の状態で決め(<see cref="IBuffListWindowViewModel.MenuEntry"/>)、
/// 「グループに追加」と「バフ・デバフカードのショートカットに追加」は自分の一覧で足せる行だけに出す。
/// </para>
/// </summary>
public partial class PlayerBuffListWidgetView : UserControl, IWidgetVerticalScrollContent
{
    private const string BuffContextMenuStyleKey = "Menu.WidgetWindowBuffListContextMenu";
    private const string BuffContextMenuFirstItemStyleKey = "Menu.WidgetWindowBuffListContextMenuItem.First";
    private const string BuffContextMenuMiddleItemStyleKey = "Menu.WidgetWindowBuffListContextMenuItem.Middle";
    private const string BuffContextMenuLastItemStyleKey = "Menu.WidgetWindowBuffListContextMenuItem.Last";

    /// <summary>メニューの「グループに追加」の項目の名前(画面の作りの <c>Name</c>)。</summary>
    private const string AddToGroupMenuItemName = "AddToGroupMenuItem";

    /// <summary>メニューの「バフ・デバフカードのショートカットに追加」の項目の名前(画面の作りの <c>Name</c>)。</summary>
    private const string AddToShortcutsMenuItemName = "AddToShortcutsMenuItem";

    private ContextMenu? _openBuffMenu;
    private PlayerBuffEntry? _openBuffEntry;

    public PlayerBuffListWidgetView()
    {
        InitializeComponent();
    }

    public event EventHandler? VerticalScrollMetricsChanged;

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(BuffListScrollViewer.ScrollableHeight, 0);
        var viewport = Math.Max(BuffListScrollViewer.ViewportHeight, 0);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(BuffListScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9, 1),
            34);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(BuffListScrollViewer.ScrollableHeight, 0);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0, maximum)
            : 0;

        BuffListScrollViewer.ScrollToVerticalOffset(offset);
    }

    private void PlayerBuffListWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerBuffListWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        CloseBuffMenu();
    }

    private void BuffListItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button)
        {
            OpenBuffMenu(button);
            e.Handled = true;
        }
    }

    private void OpenBuffMenu(Button button)
    {
        if (DataContext is not IBuffListWindowViewModel viewModel
            || button.ContextMenu is not ContextMenu menu
            || button.DataContext is not PlayerBuffEntry entry)
        {
            return;
        }

        viewModel.MenuEntry = entry;
        viewModel.RefreshMenuText();

        // 「グループに追加」と「バフ・デバフカードのショートカットに追加」は足せる行(自分の一覧で、まだオンで入っていない)だけに出す。
        foreach (var menuItem in menu.Items.OfType<MenuItem>())
        {
            if (menuItem.Name == AddToGroupMenuItemName)
            {
                menuItem.Visibility = viewModel.CanAddToGroup(entry) ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (menuItem.Name == AddToShortcutsMenuItemName)
            {
                menuItem.Visibility = viewModel.CanAddToShortcuts(entry) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

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

        // 角丸の型は、出している項目の並びで決める(隠した項目は数えない)。
        var menuItems = menu.Items
            .OfType<MenuItem>()
            .Where(menuItem => menuItem.Visibility == Visibility.Visible)
            .ToArray();
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

    private void PlayerBuffListWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void PlayerBuffListWidgetView_DataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void BuffListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
