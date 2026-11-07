using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

public partial class ManagerView : UserControl
{
    private const string HelpUrl = "https://github.com/kokoukohiro/StarResonanceDps";

    private bool _isSyncingExternalScrollBar;

    public ManagerView()
    {
        InitializeComponent();
        LogsContent.ContentScrollViewer.ScrollChanged += ContentScrollViewer_ScrollChanged;
        LogsContent.ContentScrollViewer.SizeChanged += ContentScrollViewer_SizeChanged;
        AggregationContent.ContentScrollViewer.ScrollChanged += ContentScrollViewer_ScrollChanged;
        AggregationContent.ContentScrollViewer.SizeChanged += ContentScrollViewer_SizeChanged;
        PluginsContent.ContentScrollViewer.ScrollChanged += ContentScrollViewer_ScrollChanged;
        PluginsContent.ContentScrollViewer.SizeChanged += ContentScrollViewer_SizeChanged;
        Loaded += ManagerView_Loaded;
    }

    private void ManagerView_Loaded(object sender, RoutedEventArgs e)
    {
        WidgetsNavigationButton.IsChecked = true;
    }

    private void WidgetsNavigationButton_Checked(object sender, RoutedEventArgs e)
    {
        ShowNavigationContent(ManagerContent.Widgets);
    }

    private void AggregationNavigationButton_Checked(object sender, RoutedEventArgs e)
    {
        ShowNavigationContent(ManagerContent.Aggregation);
    }

    private void NotificationsNavigationButton_Checked(object sender, RoutedEventArgs e)
    {
        ShowNavigationContent(ManagerContent.Notifications);
    }

    private void PluginsNavigationButton_Checked(object sender, RoutedEventArgs e)
    {
        ShowNavigationContent(ManagerContent.Plugins);
    }

    private void LogsNavigationButton_Checked(object sender, RoutedEventArgs e)
    {
        ShowNavigationContent(ManagerContent.Logs);
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        ExternalLinkOpener.Open(HelpUrl);
    }

    private void ShowNavigationContent(ManagerContent content)
    {
        // 通知タブは大きなパネルを使わず、自分のパネルを並べる。
        ManagerContentPanel.Visibility = content == ManagerContent.Notifications
            ? Visibility.Collapsed
            : Visibility.Visible;
        NotificationsContent.Visibility = content == ManagerContent.Notifications
            ? Visibility.Visible
            : Visibility.Collapsed;
        WidgetContent.Visibility = content == ManagerContent.Widgets
            ? Visibility.Visible
            : Visibility.Collapsed;
        AggregationContent.Visibility = content == ManagerContent.Aggregation
            ? Visibility.Visible
            : Visibility.Collapsed;
        PluginsContent.Visibility = content == ManagerContent.Plugins
            ? Visibility.Visible
            : Visibility.Collapsed;
        LogsContent.Visibility = content == ManagerContent.Logs
            ? Visibility.Visible
            : Visibility.Collapsed;

        QueueUpdateExternalScrollBar();
    }

    private void UserBuildButton_Click(object sender, RoutedEventArgs e)
    {
        ShowManagerDialog(new UserBuildWindow());
    }

    private void CombatTimelineButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new CombatTimelineWindow { Owner = Window.GetWindow(this) };
        window.Show();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowManagerDialog(new SettingsWindow());
    }

    private void WidgetActionComboBox_DropDownClosed(object sender, EventArgs e)
    {
        if (sender is not ComboBox comboBox)
        {
            return;
        }

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.WidgetActionIndex = -1;
        }

        comboBox.SetCurrentValue(Selector.SelectedIndexProperty, -1);
        comboBox.Items.MoveCurrentToPosition(-1);
    }

    private void ShowManagerDialog(Window dialog)
    {
        var managerWindow = Window.GetWindow(this);

        if (managerWindow is not null)
        {
            const double leftOffset = 24;
            const double topOffset = 72;

            dialog.Owner = managerWindow;
            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
            dialog.Left = managerWindow.Left + leftOffset;
            dialog.Top = managerWindow.Top + topOffset;
        }

        OwnerModalWindow.Show(dialog, managerWindow);
    }

    private void WidgetListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateExternalScrollBar();
    }

    private void WidgetListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
    }

    private void WidgetItemsControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
    }

    private void ContentScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateExternalScrollBar();
    }

    private void ContentScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
    }

    private void ManagerContentExternalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSyncingExternalScrollBar || !IsLoaded)
        {
            return;
        }

        GetActiveScrollViewer()?.ScrollToVerticalOffset(e.NewValue);
    }

    private void QueueUpdateExternalScrollBar()
    {
        Dispatcher.BeginInvoke(
            UpdateExternalScrollBar,
            DispatcherPriority.Loaded);
    }

    private void UpdateExternalScrollBar()
    {
        var scrollViewer = GetActiveScrollViewer();

        if (scrollViewer is null)
        {
            ManagerContentExternalScrollBar.Visibility = Visibility.Collapsed;
            ManagerContentExternalScrollBarColumn.Width = new GridLength(8);
            return;
        }

        _isSyncingExternalScrollBar = true;

        try
        {
            var maximum = Math.Max(scrollViewer.ScrollableHeight, 0);
            ManagerContentExternalScrollBar.Maximum = maximum;
            ManagerContentExternalScrollBar.ViewportSize = Math.Max(scrollViewer.ViewportHeight, 0);
            ManagerContentExternalScrollBar.LargeChange = Math.Max(scrollViewer.ViewportHeight * 0.9, 1);
            ManagerContentExternalScrollBar.SmallChange = 48;
            ManagerContentExternalScrollBar.Value = Math.Min(scrollViewer.VerticalOffset, maximum);

            var isScrollBarVisible = maximum > 0;
            ManagerContentExternalScrollBar.Visibility = isScrollBarVisible
                ? Visibility.Visible
                : Visibility.Collapsed;
            ManagerContentExternalScrollBarColumn.Width = isScrollBarVisible
                ? new GridLength(16)
                : new GridLength(8);
        }
        finally
        {
            _isSyncingExternalScrollBar = false;
        }
    }

    /// <summary>外のバーが追いかけるスクロール。通知タブでは無い(通知タブはパネルごとに自分のバーを持つ)。</summary>
    private ScrollViewer? GetActiveScrollViewer()
    {
        if (WidgetContent.Visibility == Visibility.Visible)
        {
            return WidgetListScrollViewer;
        }

        if (AggregationContent.Visibility == Visibility.Visible)
        {
            return AggregationContent.ContentScrollViewer;
        }

        if (PluginsContent.Visibility == Visibility.Visible)
        {
            return PluginsContent.ContentScrollViewer;
        }

        return LogsContent.Visibility == Visibility.Visible
            ? LogsContent.ContentScrollViewer
            : null;
    }

    private enum ManagerContent
    {
        Widgets,
        Aggregation,
        Notifications,
        Plugins,
        Logs
    }
}
