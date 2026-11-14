using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using StarResonanceDps.Plugins.KeybindTool.ViewModels;

namespace StarResonanceDps.Plugins.KeybindTool.Views;

public partial class KeybindToolView : UserControl
{
    private bool _isSyncingExternalScrollBar;

    internal KeybindToolView(KeybindToolViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
        Loaded += KeybindToolView_Loaded;
    }

    private void KeybindToolView_Loaded(object sender, RoutedEventArgs e)
    {
        QueueUpdateExternalScrollBar();

        if (DataContext is KeybindToolViewModel viewModel)
        {
            Dispatcher.BeginInvoke(
                viewModel.ShowPendingInitialPresetUnavailableMessage,
                DispatcherPriority.ContextIdle);
            Dispatcher.BeginInvoke(
                viewModel.ShowPendingLayoutLoadFailedMessage,
                DispatcherPriority.ContextIdle);
        }
    }

    private void ContentScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateExternalScrollBar();
    }

    private void ContentScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
    }

    private void ContentStackPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
    }

    private void ContentExternalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSyncingExternalScrollBar || !IsLoaded)
        {
            return;
        }

        ContentScrollViewer.ScrollToVerticalOffset(e.NewValue);
    }

    private void QueueUpdateExternalScrollBar()
    {
        Dispatcher.BeginInvoke(
            UpdateExternalScrollBar,
            DispatcherPriority.Loaded);
    }

    private void UpdateExternalScrollBar()
    {
        _isSyncingExternalScrollBar = true;

        try
        {
            var maximum = Math.Max(ContentScrollViewer.ScrollableHeight, 0);
            ContentExternalScrollBar.Maximum = maximum;
            ContentExternalScrollBar.ViewportSize = Math.Max(ContentScrollViewer.ViewportHeight, 0);
            ContentExternalScrollBar.LargeChange = Math.Max(ContentScrollViewer.ViewportHeight * 0.9, 1);
            ContentExternalScrollBar.SmallChange = 48;
            ContentExternalScrollBar.Value = Math.Min(ContentScrollViewer.VerticalOffset, maximum);

            var isScrollBarVisible = maximum > 0;
            ContentExternalScrollBar.Visibility = isScrollBarVisible
                ? Visibility.Visible
                : Visibility.Collapsed;
            ContentExternalScrollBarColumn.Width = isScrollBarVisible
                ? new GridLength(16)
                : new GridLength(8);
        }
        finally
        {
            _isSyncingExternalScrollBar = false;
        }
    }
}
