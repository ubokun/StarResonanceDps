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
/// バフ・デバフグループ。行の作りはバフ・デバフ一覧と同じ。
/// 縦スクロールはバフ・デバフ一覧と同じく<b>ウィンドウ枠側の細いスクロールバー</b>に繋ぐ。
/// 行の数が変わったら窓の高さを全部の行が入る高さに合わせる(一覧の ScrollViewer に付けた <c>Behaviors.AutoFitHeightBehavior</c>)。
///
/// <para>
/// 行の左クリックはカードを開き、右クリックはメニュー(カード・グループから削除・通知対象にする/しない)を開く。メニューの開き方はバフ・デバフ一覧と同じ。
/// メニューの文言(バフかデバフか)は開いた行で決め(<see cref="BuffDebuffGroupWidgetViewModel.MenuEntry"/>)、
/// カードの項目は開ける行だけに出す。行の並べ替えは一覧に付けた <c>Behaviors.RowReorderBehavior</c> が受け持つ。
/// </para>
/// </summary>
public partial class BuffDebuffGroupWidgetView : UserControl, IWidgetVerticalScrollContent
{
    private const string EntryContextMenuStyleKey = "Menu.WidgetWindowBuffDebuffGroupContextMenu";
    private const string EntryContextMenuFirstItemStyleKey = "Menu.WidgetWindowBuffDebuffGroupContextMenuItem.First";
    private const string EntryContextMenuMiddleItemStyleKey = "Menu.WidgetWindowBuffDebuffGroupContextMenuItem.Middle";
    private const string EntryContextMenuLastItemStyleKey = "Menu.WidgetWindowBuffDebuffGroupContextMenuItem.Last";
    private const string EntryContextMenuSingleItemStyleKey = "Menu.WidgetWindowBuffDebuffGroupContextMenuItem.Single";

    /// <summary>メニューのカードの項目の名前(画面の作りの <c>Name</c>)。</summary>
    private const string OpenCardMenuItemName = "OpenCardMenuItem";

    private ContextMenu? _openEntryMenu;
    private PlayerBuffEntry? _openEntry;

    public BuffDebuffGroupWidgetView()
    {
        InitializeComponent();
    }

    public event EventHandler? VerticalScrollMetricsChanged;

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(EntryScrollViewer.ScrollableHeight, 0);
        var viewport = Math.Max(EntryScrollViewer.ViewportHeight, 0);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(EntryScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9, 1),
            34);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(EntryScrollViewer.ScrollableHeight, 0);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0, maximum)
            : 0;

        EntryScrollViewer.ScrollToVerticalOffset(offset);
    }

    private void BuffDebuffGroupWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void BuffDebuffGroupWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        CloseEntryMenu();
    }

    private void EntryItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button)
        {
            OpenEntryMenu(button);
            e.Handled = true;
        }
    }

    private void OpenEntryMenu(Button button)
    {
        if (DataContext is not BuffDebuffGroupWidgetViewModel viewModel
            || button.ContextMenu is not ContextMenu menu
            || button.DataContext is not PlayerBuffEntry entry)
        {
            return;
        }

        viewModel.MenuEntry = entry;
        viewModel.RefreshMenuText();

        // カードの項目は、カードを開ける行(自分が分かっていて、バフかデバフに振り分けられる行)だけに出す。
        foreach (var menuItem in menu.Items.OfType<MenuItem>())
        {
            if (menuItem.Name == OpenCardMenuItemName)
            {
                menuItem.Visibility = viewModel.CanOpenCard(entry) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        ApplyEntryMenuStyles(menu);

        if (!ReferenceEquals(_openEntryMenu, menu))
        {
            CloseEntryMenu();
        }

        menu.PlacementTarget = button;
        SetOpenEntryMenu(menu, entry);
        menu.IsOpen = true;
    }

    private void EntryContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu
            && menu.PlacementTarget is Button { DataContext: PlayerBuffEntry entry })
        {
            SetOpenEntryMenu(menu, entry);
        }
    }

    private void EntryContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            ClearEntryMenu(menu);
        }
    }

    private void SetOpenEntryMenu(ContextMenu menu, PlayerBuffEntry entry)
    {
        if (ReferenceEquals(_openEntryMenu, menu)
            && ReferenceEquals(_openEntry, entry))
        {
            entry.IsMenuOpen = true;
            return;
        }

        CloseEntryMenu();

        _openEntryMenu = menu;
        _openEntry = entry;
        entry.PropertyChanged += OpenEntry_PropertyChanged;
        entry.IsMenuOpen = true;
    }

    private void CloseEntryMenu()
    {
        var menu = _openEntryMenu;
        ClearEntryMenu(menu);

        if (menu?.IsOpen == true)
        {
            menu.IsOpen = false;
        }
    }

    private void ClearEntryMenu(ContextMenu? expectedMenu)
    {
        if (expectedMenu is not null
            && !ReferenceEquals(_openEntryMenu, expectedMenu))
        {
            return;
        }

        var entry = _openEntry;
        _openEntryMenu = null;
        _openEntry = null;

        if (entry is null)
        {
            return;
        }

        entry.PropertyChanged -= OpenEntry_PropertyChanged;
        entry.IsMenuOpen = false;
    }

    private void OpenEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerBuffEntry.IsMenuOpen)
            && sender is PlayerBuffEntry { IsMenuOpen: false } entry
            && ReferenceEquals(entry, _openEntry))
        {
            CloseEntryMenu();
        }
    }

    /// <summary>角丸の型は、出している項目の並びで決める(隠した項目は数えない。1つだけなら四隅とも角丸)。</summary>
    private void ApplyEntryMenuStyles(ContextMenu menu)
    {
        if (TryFindResource(EntryContextMenuStyleKey) is Style contextMenuStyle)
        {
            menu.Style = contextMenuStyle;
        }

        var menuItems = menu.Items
            .OfType<MenuItem>()
            .Where(menuItem => menuItem.Visibility == Visibility.Visible)
            .ToArray();
        for (var index = 0; index < menuItems.Length; index++)
        {
            var styleKey = menuItems.Length == 1
                ? EntryContextMenuSingleItemStyleKey
                : index switch
                {
                    0 => EntryContextMenuFirstItemStyleKey,
                    var lastIndex when lastIndex == menuItems.Length - 1 => EntryContextMenuLastItemStyleKey,
                    _ => EntryContextMenuMiddleItemStyleKey
                };

            if (TryFindResource(styleKey) is Style menuItemStyle)
            {
                menuItems[index].Style = menuItemStyle;
            }
        }
    }

    private void BuffDebuffGroupWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void BuffDebuffGroupWidgetView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void EntryScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void NotifyVerticalScrollMetricsChanged()
    {
        VerticalScrollMetricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
