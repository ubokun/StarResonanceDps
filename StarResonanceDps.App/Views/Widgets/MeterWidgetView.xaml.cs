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

public partial class MeterWidgetView : UserControl, IWidgetVerticalScrollContent
{
    private const string PlayerSelectionContextMenuStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenu";
    private const string PlayerSelectionContextMenuFirstItemStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenuItem.First";
    private const string PlayerSelectionContextMenuMiddleItemStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenuItem.Middle";
    private const string PlayerSelectionContextMenuLastItemStyleKey = "Menu.WidgetWindowPlayerSelectionContextMenuItem.Last";

    private ContextMenu? _openPlayerSelectionMenu;
    private MeterPlayerEntry? _openPlayerSelectionEntry;

    public event EventHandler? VerticalScrollMetricsChanged;

    public MeterWidgetView()
    {
        InitializeComponent();
        Loaded += MeterWidgetView_Loaded;
        Unloaded += MeterWidgetView_Unloaded;
        SizeChanged += MeterWidgetView_SizeChanged;
    }

    public WidgetVerticalScrollMetrics GetVerticalScrollMetrics()
    {
        var maximum = Math.Max(MeterScrollViewer.ScrollableHeight, 0d);
        var viewport = Math.Max(MeterScrollViewer.ViewportHeight, 0d);

        return new WidgetVerticalScrollMetrics(
            maximum,
            viewport,
            Math.Min(MeterScrollViewer.VerticalOffset, maximum),
            Math.Max(viewport * 0.9d, 1d),
            31d);
    }

    public void SetVerticalScrollOffset(double verticalOffset)
    {
        var maximum = Math.Max(MeterScrollViewer.ScrollableHeight, 0d);
        var offset = double.IsFinite(verticalOffset)
            ? Math.Clamp(verticalOffset, 0d, maximum)
            : 0d;

        MeterScrollViewer.ScrollToVerticalOffset(offset);
    }

    private void MeterWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void MeterWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        ClosePlayerSelectionMenu();
    }

    private void MeterWidgetView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    private void MeterScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        NotifyVerticalScrollMetricsChanged();
    }

    /// <summary>
    /// 左クリックはメニューを開かず、その行のスキル詳細(与ダメ/ヒール)を直接開く。
    /// メニューは右クリック側が担う。
    /// </summary>
    private void MeterItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MeterWidgetViewModel viewModel, DataContext: MeterPlayerEntry entry })
        {
            viewModel.RequestSkillDetailCommand.Execute(entry);
        }
    }

    private void MeterItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button)
        {
            OpenPlayerSelectionMenu(button);
            e.Handled = true;
        }
    }

    private void OpenPlayerSelectionMenu(Button button)
    {
        if (button.ContextMenu is not ContextMenu menu || button.DataContext is not MeterPlayerEntry entry)
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
            && menu.PlacementTarget is Button { DataContext: MeterPlayerEntry entry })
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

    private void SetOpenPlayerSelectionMenu(ContextMenu menu, MeterPlayerEntry entry)
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

    private void OpenPlayerSelectionEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MeterPlayerEntry.IsPlayerSelectionMenuOpen)
            && sender is MeterPlayerEntry { IsPlayerSelectionMenuOpen: false } entry
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

        var menuItems = menu.Items.OfType<MenuItem>().ToArray();
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
