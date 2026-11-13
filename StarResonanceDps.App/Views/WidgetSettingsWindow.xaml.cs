// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.ViewModels.WidgetSettings;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

public partial class WidgetSettingsWindow : Window
{
    private const int WmNcHitTest = 0x0084;

    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;

    private const double ResizeBorderThickness = 8.0;

    private readonly WidgetListItemViewModel _widget;
    private readonly WidgetSettingsViewModel _viewModel;
    private bool _isSyncingExternalScrollBar;

    private WidgetSettingsViewModel ViewModel => _viewModel;

    public WidgetSettingsWindow(WidgetListItemViewModel widget)
    {
        _widget = widget;

        InitializeComponent();
        _viewModel = new WidgetSettingsViewModel(widget);
        _viewModel.ThemePreviewChanged += _widget.ApplyTheme;
        _viewModel.PluginPreviewChanged += _widget.ApplyPluginSettingsPreview;
        _viewModel.MeterPreviewChanged += _widget.ApplyMeterSettingsPreview;
        _viewModel.MetricGraphPreviewChanged += _widget.ApplyMetricGraphSettingsPreview;
        _viewModel.BuffCardPreviewChanged += _widget.ApplyBuffCardSettingsPreview;
        _viewModel.TimelinePreviewChanged += _widget.ApplyTimelineSettingsPreview;
        _viewModel.PlayerInfoPreviewChanged += _widget.ApplyPlayerInfoSettingsPreview;
        _viewModel.BuffListPreviewChanged += _widget.ApplyBuffListSettingsPreview;
        _viewModel.ElementColorPreviewChanged += _widget.ApplyElementColorSettingsPreview;
        _viewModel.SkillDetailPreviewChanged += _widget.ApplySkillDetailSettingsPreview;
        _viewModel.EquipmentPreviewChanged += _widget.ApplyEquipmentSettingsPreview;
        _viewModel.PlayerStatusPreviewChanged += _widget.ApplyPlayerStatusSettingsPreview;
        _viewModel.PlayerStatusRowOrderPreviewChanged += _widget.ApplyPlayerStatusRowOrderPreview;
        _viewModel.BuffDebuffGroupPreviewChanged += _widget.ApplyBuffDebuffGroupSettingsPreview;
        _viewModel.ReviveListPreviewChanged += _widget.ApplyReviveListSettingsPreview;
        _viewModel.DamageTakenLogPreviewChanged += _widget.ApplyDamageTakenLogSettingsPreview;
        _viewModel.EntityInfoPreviewChanged += _widget.ApplyEntityInfoSettingsPreview;
        _viewModel.ChatPreviewChanged += _widget.ApplyChatSettingsPreview;
        _widget.BuffListHiddenBuffsChanged += Widget_BuffListHiddenBuffsChanged;
        _widget.EntityInfoHiddenBuffsChanged += Widget_EntityInfoHiddenBuffsChanged;
        _widget.BuffListPriorityBuffChangedFromWidget += Widget_BuffListPriorityBuffChangedFromWidget;
        _widget.EntityInfoPriorityBuffChangedFromWidget += Widget_EntityInfoPriorityBuffChangedFromWidget;
        _widget.BuffDebuffGroupEntryChangedFromWidget += Widget_BuffDebuffGroupEntryChangedFromWidget;
        _widget.BuffDebuffGroupEntryOrderChangedFromWidget += Widget_BuffDebuffGroupEntryOrderChangedFromWidget;
        _widget.BuffCardShortcutChangedFromWidget += Widget_BuffCardShortcutChangedFromWidget;
        _widget.BuffCardNotificationTargetChangedFromWidget += Widget_BuffCardNotificationTargetChangedFromWidget;
        _widget.BuffDebuffGroupNotificationTargetChangedFromWidget += Widget_BuffDebuffGroupNotificationTargetChangedFromWidget;
        _widget.MetricGraphVisibleSecondsChangedFromWidget += Widget_MetricGraphVisibleSecondsChangedFromWidget;
        _widget.DamageTakenLogShowsLatestHitOnlyChangedFromWidget += Widget_DamageTakenLogShowsLatestHitOnlyChangedFromWidget;

        // 開いている間は、行の並びを保存前のプレビューとして持つ(ウィジェットでの並べ替えも含めて「保存」で確定、閉じたら戻す)。
        _widget.BeginSettingsPreview();
        _widget.PlayerStatusRowOrderChanged += Widget_PlayerStatusRowOrderChanged;
        DataContext = _viewModel;

        Loaded += WidgetSettingsWindow_Loaded;
        SourceInitialized += WidgetSettingsWindow_SourceInitialized;
    }

    private void WidgetSettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
    }

    private void WidgetSettingsWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.ThemePreviewChanged -= _widget.ApplyTheme;
        _viewModel.PluginPreviewChanged -= _widget.ApplyPluginSettingsPreview;
        _viewModel.MeterPreviewChanged -= _widget.ApplyMeterSettingsPreview;
        _viewModel.MetricGraphPreviewChanged -= _widget.ApplyMetricGraphSettingsPreview;
        _viewModel.BuffCardPreviewChanged -= _widget.ApplyBuffCardSettingsPreview;
        _viewModel.TimelinePreviewChanged -= _widget.ApplyTimelineSettingsPreview;
        _viewModel.PlayerInfoPreviewChanged -= _widget.ApplyPlayerInfoSettingsPreview;
        _viewModel.BuffListPreviewChanged -= _widget.ApplyBuffListSettingsPreview;
        _viewModel.ElementColorPreviewChanged -= _widget.ApplyElementColorSettingsPreview;
        _viewModel.SkillDetailPreviewChanged -= _widget.ApplySkillDetailSettingsPreview;
        _viewModel.EquipmentPreviewChanged -= _widget.ApplyEquipmentSettingsPreview;
        _viewModel.PlayerStatusPreviewChanged -= _widget.ApplyPlayerStatusSettingsPreview;
        _viewModel.PlayerStatusRowOrderPreviewChanged -= _widget.ApplyPlayerStatusRowOrderPreview;
        _viewModel.BuffDebuffGroupPreviewChanged -= _widget.ApplyBuffDebuffGroupSettingsPreview;
        _viewModel.ReviveListPreviewChanged -= _widget.ApplyReviveListSettingsPreview;
        _viewModel.DamageTakenLogPreviewChanged -= _widget.ApplyDamageTakenLogSettingsPreview;
        _viewModel.EntityInfoPreviewChanged -= _widget.ApplyEntityInfoSettingsPreview;
        _viewModel.ChatPreviewChanged -= _widget.ApplyChatSettingsPreview;
        _widget.BuffListHiddenBuffsChanged -= Widget_BuffListHiddenBuffsChanged;
        _widget.EntityInfoHiddenBuffsChanged -= Widget_EntityInfoHiddenBuffsChanged;
        _widget.BuffListPriorityBuffChangedFromWidget -= Widget_BuffListPriorityBuffChangedFromWidget;
        _widget.EntityInfoPriorityBuffChangedFromWidget -= Widget_EntityInfoPriorityBuffChangedFromWidget;
        _widget.BuffDebuffGroupEntryChangedFromWidget -= Widget_BuffDebuffGroupEntryChangedFromWidget;
        _widget.BuffDebuffGroupEntryOrderChangedFromWidget -= Widget_BuffDebuffGroupEntryOrderChangedFromWidget;
        _widget.BuffCardShortcutChangedFromWidget -= Widget_BuffCardShortcutChangedFromWidget;
        _widget.BuffCardNotificationTargetChangedFromWidget -= Widget_BuffCardNotificationTargetChangedFromWidget;
        _widget.BuffDebuffGroupNotificationTargetChangedFromWidget -= Widget_BuffDebuffGroupNotificationTargetChangedFromWidget;
        _widget.MetricGraphVisibleSecondsChangedFromWidget -= Widget_MetricGraphVisibleSecondsChangedFromWidget;
        _widget.DamageTakenLogShowsLatestHitOnlyChangedFromWidget -= Widget_DamageTakenLogShowsLatestHitOnlyChangedFromWidget;
        _widget.PlayerStatusRowOrderChanged -= Widget_PlayerStatusRowOrderChanged;
        _widget.EndSettingsPreview();
        _viewModel.Dispose();
        base.OnClosed(e);
    }

    /// <summary>開いている間に一覧の窓の右クリックでバフが非表示になった。設定の行にも足す。</summary>
    private void Widget_BuffListHiddenBuffsChanged(WidgetKind kind, int baseId)
    {
        _viewModel.BuffListSettings?.AddHiddenBuffFromWidget(baseId);
    }

    /// <summary>開いている間にエンティティ情報の枠の右クリックでバフが非表示になった。設定の行にも足す。</summary>
    private void Widget_EntityInfoHiddenBuffsChanged(WidgetKind kind, int baseId)
    {
        _viewModel.EntityInfoSettings?.AddHiddenBuffFromWidget(baseId);
    }

    /// <summary>開いている間に一覧の窓の右クリックで優先表示リストの行がオン/オフになった。設定の行にも同じ変化を入れる。</summary>
    private void Widget_BuffListPriorityBuffChangedFromWidget(WidgetKind kind, BuffEntryKey key, bool isEnabled)
    {
        _viewModel.BuffListSettings?.SetPriorityBuffEnabledFromWidget(key, isEnabled);
    }

    /// <summary>開いている間にエンティティ情報の枠の右クリックで優先表示リストの行がオン/オフになった。設定の行にも同じ変化を入れる。</summary>
    private void Widget_EntityInfoPriorityBuffChangedFromWidget(WidgetKind kind, BuffEntryKey key, bool isEnabled)
    {
        _viewModel.EntityInfoSettings?.SetPriorityBuffEnabledFromWidget(key, isEnabled);
    }

    /// <summary>
    /// 開いている間に右クリックで、バフ・デバフグループの行がオン/オフになった(一覧・カードの「グループに追加」、グループの窓の「グループから削除」)。
    /// 設定の行にも同じ変化を入れる。
    /// </summary>
    private void Widget_BuffDebuffGroupEntryChangedFromWidget(WidgetKind kind, BuffEntryKey key, bool isEnabled)
    {
        _viewModel.BuffDebuffGroupSettings?.SetEntryEnabledFromWidget(key, isEnabled);
    }

    /// <summary>開いている間にバフ・デバフグループの窓で行が並べ替えられた。「バフ・デバフ」の一覧も同じ順に並べ直す。</summary>
    private void Widget_BuffDebuffGroupEntryOrderChangedFromWidget(IReadOnlyList<BuffEntryKey> order)
    {
        _viewModel.BuffDebuffGroupSettings?.ApplyEntryOrderFromWidget(order);
    }

    /// <summary>
    /// 開いている間に右クリックで、バフ・デバフカードのショートカットの行がオンになった(一覧・カードの「バフ・デバフカードのショートカットに追加」)。
    /// 設定の行にも同じ変化を入れる。
    /// </summary>
    private void Widget_BuffCardShortcutChangedFromWidget(WidgetKind kind, BuffEntryKey key, bool isEnabled)
    {
        _viewModel.BuffCardSettings?.SetShortcutEnabledFromWidget(key, isEnabled);
    }

    /// <summary>開いている間に右クリックで、バフ・デバフカードの通知対象の行がオン/オフになった。設定の行にも同じ変化を入れる。</summary>
    private void Widget_BuffCardNotificationTargetChangedFromWidget(WidgetKind kind, BuffEntryKey key, bool isEnabled)
    {
        _viewModel.BuffCardSettings?.SetNotificationTargetEnabledFromWidget(key, isEnabled);
    }

    /// <summary>開いている間に右クリックで、バフ・デバフグループの通知対象の行がオン/オフになった。設定の行にも同じ変化を入れる。</summary>
    private void Widget_BuffDebuffGroupNotificationTargetChangedFromWidget(WidgetKind kind, BuffEntryKey key, bool isEnabled)
    {
        _viewModel.BuffDebuffGroupSettings?.SetNotificationTargetEnabledFromWidget(key, isEnabled);
    }

    /// <summary>開いている間に推移グラフの窓の上のホイールで横軸の長さが変わった。設定の値にも入れる。</summary>
    private void Widget_MetricGraphVisibleSecondsChangedFromWidget(int visibleSeconds)
    {
        _viewModel.MetricGraphSettings?.SetVisibleSecondsFromWidget(visibleSeconds);
    }

    /// <summary>開いている間に被ダメログのヘッダーのボタンで「最新の被ダメのみ表示」が変わった。設定の値にも入れる。</summary>
    private void Widget_DamageTakenLogShowsLatestHitOnlyChangedFromWidget(bool showsLatestHitOnly)
    {
        _viewModel.DamageTakenLogSettings?.SetShowsLatestHitOnlyFromWidget(showsLatestHitOnly);
    }

    /// <summary>
    /// 開いている間にステータス詳細の行の並びが変わった(ウィジェットでの並べ替え、またはこの窓の並べ替えの戻り)。
    /// 「ステータス」と「テキストカラー」の一覧も同じ順に並べ直す。
    /// </summary>
    private void Widget_PlayerStatusRowOrderChanged(IReadOnlyList<int> rowOrder)
    {
        _viewModel.PlayerStatusSettings?.ApplyRowOrder(rowOrder);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (ViewModel.HasUnsavedChanges)
        {
            var confirmed = ConfirmWindow.Show(
                this,
                "Confirm_DiscardUnsaved_Title",
                "Confirm_DiscardUnsaved_Message",
                "Confirm_DiscardUnsaved_Detail");

            if (!confirmed)
            {
                e.Cancel = true;
                return;
            }

            ViewModel.RestoreSavedPreviews();
        }

        base.OnClosing(e);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmNcHitTest || ResizeMode == ResizeMode.NoResize || WindowState == WindowState.Maximized)
        {
            return IntPtr.Zero;
        }

        var cursor = PointFromScreen(GetScreenPoint(lParam));
        var width = ActualWidth;
        var height = ActualHeight;

        var left = cursor.X >= 0 && cursor.X < ResizeBorderThickness;
        var right = cursor.X <= width && cursor.X > width - ResizeBorderThickness;
        var top = cursor.Y >= 0 && cursor.Y < ResizeBorderThickness;
        var bottom = cursor.Y <= height && cursor.Y > height - ResizeBorderThickness;

        var hitTest = 0;

        if (top && left)
        {
            hitTest = HtTopLeft;
        }
        else if (top && right)
        {
            hitTest = HtTopRight;
        }
        else if (bottom && left)
        {
            hitTest = HtBottomLeft;
        }
        else if (bottom && right)
        {
            hitTest = HtBottomRight;
        }
        else if (left)
        {
            hitTest = HtLeft;
        }
        else if (right)
        {
            hitTest = HtRight;
        }
        else if (top)
        {
            hitTest = HtTop;
        }
        else if (bottom)
        {
            hitTest = HtBottom;
        }

        if (hitTest == 0)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(hitTest);
    }

    private static Point GetScreenPoint(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        var x = unchecked((short)(value & 0xFFFF));
        var y = unchecked((short)((value >> 16) & 0xFFFF));
        return new Point(x, y);
    }

    private void WindowSettingsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(WindowSettingsHost);
    }

    private void PluginSectionNavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PluginWidgetSettingSectionViewModel section }
            && PluginSettingsView.FindSectionElement(section) is { } target)
        {
            ScrollToSection(target);
        }
    }

    private void DisplaySettingsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(DisplaySettingsHost);
    }

    private void PlayerStatusRowsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(PlayerStatusRowsHost);
    }

    private void BuffDebuffGroupEntriesNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(BuffDebuffGroupEntriesHost);
    }

    private void BuffCardShortcutsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(BuffCardShortcutsHost);
    }

    private void BuffDebuffGroupNotificationTargetsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(BuffDebuffGroupNotificationTargetsHost);
    }

    private void BuffCardNotificationTargetsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(BuffCardNotificationTargetsHost);
    }

    private void BuffDebuffGroupBuffColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(BuffDebuffGroupBuffColorsHost);
    }

    private void BuffDebuffGroupDebuffColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(BuffDebuffGroupDebuffColorsHost);
    }

    private void ReviveListGaugeColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(ReviveListGaugeColorsHost);
    }

    private void PlayerStatusTextColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(PlayerStatusTextColorsHost);
    }

    private void EquipmentTextColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(EquipmentTextColorsHost);
    }

    private void OtherRoleSkillsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(OtherRoleSkillsHost);
    }

    private void BuffListPriorityBuffsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(BuffListPriorityBuffsHost);
    }

    private void EntityInfoPriorityBuffsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(EntityInfoPriorityBuffsHost);
    }

    private void BuffListHiddenBuffsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(BuffListHiddenBuffsHost);
    }

    private void EntityInfoHiddenBuffsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(EntityInfoHiddenBuffsHost);
    }

    private void ThemeNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(ThemeSection);
    }

    private void GraphColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(GraphColorsHost);
    }

    private void ClassColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(ClassColorsHost);
    }

    private void TimelineClassColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(TimelineClassColorsHost);
    }

    private void TimelineTextColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(TimelineTextColorsHost);
    }

    private void PlayerInfoClassColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(PlayerInfoClassColorsHost);
    }

    private void ReviveListClassColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(ReviveListClassColorsHost);
    }

    private void ReviveListTextColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(ReviveListTextColorsHost);
    }

    private void EntityInfoClassColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(EntityInfoClassColorsHost);
    }

    private void ChatTextColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(ChatTextColorsHost);
    }

    private void BuffListColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(BuffListColorsHost);
    }

    private void DamageTakenLogClassColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(DamageTakenLogClassColorsHost);
    }

    private void ElementColorsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToSection(ElementColorsHost);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        var confirmed = ConfirmWindow.Show(
            this,
            "Confirm_ResetWidgetSettings_Title",
            "Confirm_ResetWidgetSettings_Message",
            "Confirm_ResetWidgetSettings_Detail");

        if (!confirmed)
        {
            return;
        }

        ViewModel.ResetToDefaults();
    }


    private void ColorOptionRadioButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ColorOptionViewModel option })
        {
            ViewModel.ApplyWindowColor(option.Color);
        }
    }

    private void ColorPickerButton_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var window = new ColorPickerWindow(ViewModel.GetSelectedWindowColor());

        if (owner is not null)
        {
            window.Owner = owner;
        }

        OwnerModalWindow.Show(window, owner, () =>
        {
            if (window.IsConfirmed)
            {
                ViewModel.ApplyWindowColor(window.SelectedColor);
            }
        });
    }

    // 前に使ったフォルダと窓の大きさの記録を、ほかのファイル・フォルダの窓と分ける番号。
    private static readonly Guid BackgroundImageDialogClientGuid = new("3F9B97ED-A2F2-48A1-9B10-BCBBD41D8418");

    private void SelectBackgroundImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            ClientGuid = BackgroundImageDialogClientGuid,
            Filter = $"{LocalizationManager.Instance.GetString("Settings_Theme_BackgroundImage_Filter")}|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp",
            CheckFileExists = true,
            Multiselect = false,
            FileName = ViewModel.BackgroundImagePath ?? string.Empty
        };

        if (UserFolders.Pictures is { } pictures)
        {
            dialog.DefaultDirectory = pictures;
        }

        if (dialog.ShowDialog() == true)
        {
            ViewModel.SetBackgroundImagePath(dialog.FileName);
        }
    }


    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // 確定していない数の欄(プラグインの設定)は、欄を離れたときに確定する。
        Keyboard.ClearFocus();
        var config = ViewModel.SaveSettings();
        _widget.ApplyWidgetConfig(config);
        Close();
    }

    private void ScrollToSection(FrameworkElement target)
    {
        if (ContentScrollViewer.Content is not FrameworkElement content)
        {
            target.BringIntoView();
            QueueUpdateExternalScrollBar();
            return;
        }

        var point = target.TransformToVisual(content).Transform(new Point(0, 0));
        ContentScrollViewer.ScrollToVerticalOffset(Math.Max(point.Y, 0));
        QueueUpdateExternalScrollBar();
    }

    private void ContentScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateExternalScrollBar(ContentScrollViewer, SettingsExternalScrollBar);
    }

    private void ContentScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueUpdateExternalScrollBar();
    }

    private void SettingsExternalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
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
            () => UpdateExternalScrollBar(ContentScrollViewer, SettingsExternalScrollBar),
            DispatcherPriority.Loaded);
    }

    private void UpdateExternalScrollBar(ScrollViewer scrollViewer, ScrollBar scrollBar)
    {
        _isSyncingExternalScrollBar = true;

        try
        {
            var maximum = Math.Max(scrollViewer.ScrollableHeight, 0);
            scrollBar.Maximum = maximum;
            scrollBar.ViewportSize = Math.Max(scrollViewer.ViewportHeight, 0);
            scrollBar.LargeChange = Math.Max(scrollViewer.ViewportHeight * 0.9, 1);
            scrollBar.SmallChange = 48;
            scrollBar.Value = Math.Min(scrollViewer.VerticalOffset, maximum);
            var isScrollBarVisible = maximum > 0;

            scrollBar.Visibility = isScrollBarVisible ? Visibility.Visible : Visibility.Collapsed;
            SettingsExternalScrollBarColumn.Width = isScrollBarVisible
                ? new GridLength(16)
                : new GridLength(8);
        }
        finally
        {
            _isSyncingExternalScrollBar = false;
        }
    }
}
