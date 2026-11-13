// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using StarResonanceDps.Plugins.ModuleTool.ViewModels;

namespace StarResonanceDps.Plugins.ModuleTool.Views;

public partial class ModuleToolView : UserControl, IDisposable
{
    private readonly ModuleToolViewModel _viewModel;

    internal ModuleToolView(ModuleToolViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        _ = new ScrollBarLink(ConditionsScrollViewer, ConditionsScrollBar);
        _ = new ScrollBarLink(ResultsScrollViewer, ResultsScrollBar);
        Loaded += ModuleToolView_Loaded;
    }

    /// <summary>本体が窓を閉じた後に呼ぶ。</summary>
    public void Dispose()
    {
        _viewModel.Dispose();
    }

    private void ModuleToolView_Loaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(_viewModel.ShowPendingSettingsLoadFailedMessage, DispatcherPriority.ContextIdle);
    }

    // 選んだ効果を足したら選択を外し、次も同じ欄から選べるようにする。
    private void AddEffectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AddEffectComboBox.SelectedItem is not ModuleEffectOption option)
        {
            return;
        }

        AddEffectComboBox.SelectedItem = null;
        _viewModel.AddPriorityEffect(option.EffectId);
    }

    /// <summary>
    /// 隠したスクロールと、横に置いたバーをつなぐ。溢れたときだけバーを出す(バーの列は Auto なので、隠すと詰まる)。
    /// </summary>
    private sealed class ScrollBarLink
    {
        private readonly ScrollViewer _scrollViewer;
        private readonly ScrollBar _scrollBar;
        private bool _isSyncing;

        public ScrollBarLink(ScrollViewer scrollViewer, ScrollBar scrollBar)
        {
            _scrollViewer = scrollViewer;
            _scrollBar = scrollBar;
            _scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            _scrollBar.ValueChanged += ScrollBar_ValueChanged;
        }

        // 中身の高さ・見えている高さが変わったときも ScrollChanged が届く。
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            Update();
        }

        private void ScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isSyncing)
            {
                _scrollViewer.ScrollToVerticalOffset(e.NewValue);
            }
        }

        private void Update()
        {
            _isSyncing = true;
            try
            {
                var maximum = Math.Max(_scrollViewer.ScrollableHeight, 0);
                _scrollBar.Maximum = maximum;
                _scrollBar.ViewportSize = Math.Max(_scrollViewer.ViewportHeight, 0);
                _scrollBar.LargeChange = Math.Max(_scrollViewer.ViewportHeight * 0.9, 1);
                _scrollBar.SmallChange = 48;
                _scrollBar.Value = Math.Min(_scrollViewer.VerticalOffset, maximum);
                _scrollBar.Visibility = maximum > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            finally
            {
                _isSyncing = false;
            }
        }
    }
}
