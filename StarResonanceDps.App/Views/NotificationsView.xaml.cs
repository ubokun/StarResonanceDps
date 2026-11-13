// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using StarResonanceDps.App.Behaviors;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels.Notifications;
using StarResonanceDps.App.ViewModels.Plugins;
using StarResonanceDps.App.Views.WidgetSettings;

namespace StarResonanceDps.App.Views;

public partial class NotificationsView : UserControl
{
    /// <summary>右上と右下のあいだ(行の高さ)。</summary>
    private const double PanelGap = 6;

    /// <summary>
    /// 右上が伸びても右下に残す高さ。右下の枠 1 ＋ 1、スクロールの外の上下の余白 16 ＋ 16、設定の行1行 44。
    /// この高さのとき、右下には最初の設定の行だけがちょうど真ん中に出る(先頭が注意書きの項目では、注意書きと次の行の一部)。
    /// 右上を下端まで伸ばせると、長い文のとき右下が高さ 0 になり、設定もバーも使えなくなるため。
    /// </summary>
    private const double SettingsPanelMinHeight = 78;

    /// <summary>空だった VOICEVOX のリストを、選択肢ができたので開き直している(開き直しでは問い合わせない)。</summary>
    private bool _isReopeningVoicevoxDropDown;

    private bool _isDraggingSlider;

    private NotificationsViewModel? _subscribedViewModel;

    public NotificationsView()
    {
        InitializeComponent();
        DataContextChanged += NotificationsView_DataContextChanged;

        // パネルの中のスクロールと、パネルの外に置いたバー。バーが出たら、パネルの右端をバーのぶん縮める
        // (中央は右のパネルとの間 6 を 16 に、右下は窓の枠との間 8 を 16 にする)。
        // つなぎはスクロールとバーの合図が持つので、ここで控えない。
        _ = new PanelScrollBarLink(ItemsScrollViewer, ItemsScrollBar, ItemsPanel, shownRightMargin: 10);
        _ = new PanelScrollBarLink(SettingsScrollViewer, SettingsScrollBar, SettingsPanel, shownRightMargin: 8);
    }

    private NotificationsViewModel? ViewModel => DataContext as NotificationsViewModel;

    /// <summary>
    /// 中身を測る前に、右上の高さの上限を決め直す。右上は中身に合わせて伸びるが、この領域の下端を越えず、右下に <see cref="SettingsPanelMinHeight"/> を残す
    /// (上限が無いと右上が下にはみ出し、同じ縦の枠を使う中央のパネルも窓に合わせて縮まない)。
    /// 上限は与えられた高さから決める(はみ出した中身の大きさから決めると、はみ出しが上限に跳ね返るため)。
    /// </summary>
    protected override Size MeasureOverride(Size constraint)
    {
        if (!double.IsInfinity(constraint.Height))
        {
            var maxHeight = Math.Max(
                constraint.Height - RootGrid.Margin.Top - RootGrid.Margin.Bottom - PanelGap - SettingsPanelMinHeight,
                0);
            if (TextPanel.MaxHeight != maxHeight)
            {
                TextPanel.MaxHeight = maxHeight;
            }
        }

        return base.MeasureOverride(constraint);
    }

    private void NotificationsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.SpeechVoiceCheckRequested -= ViewModel_SpeechVoiceCheckRequested;
        }

        _subscribedViewModel = ViewModel;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.SpeechVoiceCheckRequested += ViewModel_SpeechVoiceCheckRequested;
        }
    }

    /// <summary>
    /// タブを開いた・離れた。離れたら待っている文章を保存する。
    /// 開いたとき VOICEVOX の項目があれば話者の一覧を取る(スタイルの行を出すかは、エンジンの一覧で話者のスタイルの数を見ないと決まらない)。
    /// つながらなくても知らせない(知らせるのは読み上げで選んだときと起動時。失敗は VoicevoxClient がログに書く)。
    /// </summary>
    private async void NotificationsView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        if (!IsVisible)
        {
            viewModel.FlushPendingSave();
            return;
        }

        if (viewModel.Items.Any(item => item.IsVoicevoxSelected))
        {
            await viewModel.RefreshVoicevoxSpeakersAsync();
        }
    }

    /// <summary>
    /// 読み上げの項目をオンにした・出し方を読み上げにした・読み上げ方法を変えた。声を確かめて、使えなければ知らせる。
    /// VOICEVOX のときは、確かめるのを兼ねて話者の一覧を取る。
    /// </summary>
    private async void ViewModel_SpeechVoiceCheckRequested(object? sender, NotificationItemViewModel item)
    {
        if (ViewModel is not { } viewModel || Window.GetWindow(this) is not { } owner)
        {
            return;
        }

        if (item.IsVoicevoxSelected)
        {
            NotificationCheckMessage.ShowVoicevoxFailure(owner, await viewModel.RefreshVoicevoxSpeakersAsync());
            return;
        }

        if (item.IsSpeechSelected)
        {
            NotificationCheckMessage.CheckWindowsVoice(owner);
        }
    }

    /// <summary>
    /// 話者かスタイルのリストを開いた。エンジンに一覧を問い合わせて作り直す。
    /// つながらなくても知らせない(読み上げで VOICEVOX を選んだときと起動時に知らせている。失敗は VoicevoxClient がログに書く)。
    /// 選択肢が1つも無いときは選択リストのスタイルが閉じるので、問い合わせで選択肢ができたら開き直す。
    /// </summary>
    private async void VoicevoxComboBox_DropDownOpened(object? sender, EventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var comboBox = (ComboBox)sender!;
        if (_isReopeningVoicevoxDropDown)
        {
            _isReopeningVoicevoxDropDown = false;
            return;
        }

        var wasEmpty = comboBox.Items.Count == 0;
        await viewModel.RefreshVoicevoxSpeakersAsync();

        if (wasEmpty && comboBox.Items.Count > 0 && comboBox.IsVisible)
        {
            _isReopeningVoicevoxDropDown = true;
            comboBox.IsDropDownOpen = true;
        }
    }

    private void VoicevoxDownloadLink_Click(object sender, RoutedEventArgs e)
    {
        ExternalLinkOpener.Open(VoicevoxClient.DownloadPageUrl);
    }

    /// <summary>利用規約のリンクを押した。選んでいる項目の話者の規約の本文をエンジンから取って出す。</summary>
    private async void VoicevoxPolicyLink_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedItem is not { HasVoicevoxSpeaker: true } item || Window.GetWindow(this) is not { } owner)
        {
            return;
        }

        await NotificationCheckMessage.ShowVoicevoxPolicyAsync(owner, item.VoicevoxSpeakerUuid, item.VoicevoxSpeakerName);
    }

    private void AddFormatField_Click(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.Insert(
            NotificationTextBox,
            ViewModel?.SelectedItem?.Text.SelectedFormatField?.Placeholder);
    }

    private void NotificationTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        FormatFieldInsertion.MoveCaretToEndWhenUnfocused((TextBox)sender);
    }

    private void NotificationTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FormatFieldInsertion.MoveCaretToEndWhenUnfocused((TextBox)sender);
    }

    // --- 通知音量とプラグインの設定のスライドバー ---
    // 値を項目へ渡す(保存と通知に効く)のは、つまみを手放したときと、
    // ドラッグ以外の操作(クリック・キー・ホイール)で値が変わったときだけ。ドラッグ中は打つ前の値のまま。
    // 作っている途中(範囲と値を入れているあいだ)の値の変化は渡さない。

    private void ReleaseCommitSlider_DragStarted(object sender, DragStartedEventArgs e)
    {
        _isDraggingSlider = true;
    }

    private void ReleaseCommitSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isDraggingSlider = false;
        CommitSlider((Slider)sender);
    }

    private void ReleaseCommitSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isDraggingSlider && ((Slider)sender).IsLoaded)
        {
            CommitSlider((Slider)sender);
        }
    }

    private static void CommitSlider(Slider slider)
    {
        slider.GetBindingExpression(RangeBase.ValueProperty)?.UpdateSource();
    }

    // --- プラグインの設定の数の欄 ---
    // 文字はバインドせず、出すときと確定のたびに今の値で書き直す。空なら打つ前の値に戻して警告音(全体設定の計測時間の欄と同じ)。

    private void PluginNumberTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        var textBox = (TextBox)sender;
        textBox.Text = ((PluginNumberSettingViewModel)textBox.DataContext).DisplayText;
    }

    private void PluginNumberTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitPluginNumber((TextBox)sender);
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void PluginNumberTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        CommitPluginNumber((TextBox)sender);
    }

    private static void CommitPluginNumber(TextBox textBox)
    {
        if (textBox.DataContext is not PluginNumberSettingViewModel setting)
        {
            return;
        }

        if (!setting.TryCommitText(textBox.Text))
        {
            DigitsOnlyInputBehavior.PlayInvalidInputSound();
        }

        textBox.Text = setting.DisplayText;
    }

    /// <summary>
    /// パネルの中のスクロール(バーは隠してある)と、パネルの外に置いたバーをつなぐ。動きはメイン窓の外のバー(ManagerView)と同じで、
    /// 溢れたときだけバーを出す。バーを出したら、そのパネルの右端を縮めてすき間を作る(列の幅は変えないので、ほかのパネルの幅は変わらない)。
    /// </summary>
    private sealed class PanelScrollBarLink
    {
        private readonly ScrollViewer _scrollViewer;
        private readonly ScrollBar _scrollBar;
        private readonly FrameworkElement _panel;
        private readonly double _shownRightMargin;
        private bool _isSyncing;

        public PanelScrollBarLink(ScrollViewer scrollViewer, ScrollBar scrollBar, FrameworkElement panel, double shownRightMargin)
        {
            _scrollViewer = scrollViewer;
            _scrollBar = scrollBar;
            _panel = panel;
            _shownRightMargin = shownRightMargin;
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

                var isScrollBarVisible = maximum > 0;
                _scrollBar.Visibility = isScrollBarVisible ? Visibility.Visible : Visibility.Collapsed;
                _panel.Margin = new Thickness(0, 0, isScrollBarVisible ? _shownRightMargin : 0, 0);
            }
            finally
            {
                _isSyncing = false;
            }
        }
    }
}
