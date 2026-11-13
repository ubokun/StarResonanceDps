// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Serilog;
using StarResonanceDps.App.Behaviors;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

public partial class WidgetWindow : Window
{
    private const int WmNcHitTest = 0x0084;
    private const int HtClient = 1;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;

    private const double ResizeBorderThickness = 8.0;

    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExTopmost = 0x00000008;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private const int WmMouseActivate = 0x0021;
    private const uint MonitorDefaultToNearest = 0x00000002;
    private static readonly IntPtr HwndTopmost = new(-1);

    /// <summary>ヘッダー/フッター1つ分の高さ。XAML の行定義と合わせること。</summary>
    private const double ChromeRowHeight = 30d;

    /// <summary>横のスクロールバーの行の高さ。バーがあれば16、無ければ5(縦のバーの列の幅と同じ)。</summary>
    private const double HorizontalScrollBarRowVisibleHeight = 16d;
    private const double HorizontalScrollBarRowHiddenHeight = 5d;
    private const int MaNoActivate = 3;

    public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register(
        nameof(HeaderText),
        typeof(string),
        typeof(WidgetWindow),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DataStatusLevelProperty = DependencyProperty.Register(
        nameof(DataStatusLevel),
        typeof(WidgetDataStatusLevel),
        typeof(WidgetWindow),
        new PropertyMetadata(WidgetDataStatusLevel.Complete));

    public static readonly DependencyProperty DataStatusTextProperty = DependencyProperty.Register(
        nameof(DataStatusText),
        typeof(string),
        typeof(WidgetWindow),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsDataStatusMarkShownProperty = DependencyProperty.Register(
        nameof(IsDataStatusMarkShown),
        typeof(bool),
        typeof(WidgetWindow),
        new PropertyMetadata(true));

    public static readonly DependencyProperty IsDataStatusTipShownProperty = DependencyProperty.Register(
        nameof(IsDataStatusTipShown),
        typeof(bool),
        typeof(WidgetWindow),
        new PropertyMetadata(false));

    private readonly WidgetListItemViewModel _widget;

    /// <summary>タイトルの左の円が映す状態の印の持ち主(窓の中身)。</summary>
    private readonly IWidgetDataStatusSource _dataStatusSource;
    private readonly IWidgetVerticalScrollContent? _verticalScrollContent;
    private readonly IWidgetHorizontalScrollContent? _horizontalScrollContent;
    private readonly IWidgetMouseWheelContent? _mouseWheelContent;
    private readonly DispatcherTimer _saveBoundsTimer;
    private readonly bool _usesWidgetDisplayNameForHeader;
    private bool _isRestoringBounds = true;
    private bool _isSynchronizingContentScrollBar;
    private bool _isSynchronizingContentHorizontalScrollBar;

    /// <summary>フッターに中身があるか。ピン留め中に隠す判定と合わせて可視状態を決める。</summary>
    private bool _hasFooterContent;

    private bool _isPinned;

    /// <summary>中身から高さを合わせる合図が来て、まだ合わせていないか(<see cref="FitHeightToContent"/>)。</summary>
    private bool _isFitHeightPending;

    /// <summary>中身に合わせて高さを変えている最中か。この大きさの変化は保存しない(手で変えた大きさではない)。</summary>
    private bool _isFittingHeight;

    /// <summary>高さを合わせる合図を出した、測る <see cref="ScrollViewer"/>(<see cref="AutoFitHeightBehavior"/>)。</summary>
    private ScrollViewer? _autoFitHeightSource;

    /// <summary>この窓を開くときに渡された位置と大きさ。まだ測れていない間の保存に使う。</summary>
    private readonly WidgetWindowConfig _restoredBounds;

    /// <param name="dataStatusSource">タイトルの左の円に映す状態の印の持ち主(窓の中身)。</param>
    public WidgetWindow(
        WidgetListItemViewModel widget,
        FrameworkElement? widgetContent,
        IWidgetDataStatusSource dataStatusSource,
        WidgetWindowConfig savedBounds,
        Window? owner,
        string? headerText = null,
        FrameworkElement? headerChromeActions = null,
        FrameworkElement? headerActions = null,
        FrameworkElement? footerContent = null)
    {
        _widget = widget;
        _dataStatusSource = dataStatusSource;
        _usesWidgetDisplayNameForHeader = string.IsNullOrWhiteSpace(headerText);

        InitializeComponent();
        DataContext = widget;
        HeaderText = _usesWidgetDisplayNameForHeader
            ? widget.DisplayName
            : headerText!;
        WidgetContentHost.Content = widgetContent;
        WidgetHeaderChromeActionsHost.Content = headerChromeActions;
        WidgetHeaderActionsHost.Content = headerActions;
        SetFooterContent(footerContent);
        _widget.PropertyChanged += Widget_PropertyChanged;
        ConfigManager.Instance.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        _dataStatusSource.DataStatusChanged += DataStatusSource_DataStatusChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        // 中身によっては、作った後に何か届くまで印を計算しない。開いた時点の事実で一度計算させる。
        _dataStatusSource.RefreshDataStatus();
        ApplyDataStatus();

        _verticalScrollContent = widgetContent as IWidgetVerticalScrollContent;
        if (_verticalScrollContent is not null)
        {
            Grid.SetColumnSpan(WidgetContentHost, 1);
            _verticalScrollContent.VerticalScrollMetricsChanged += VerticalScrollContent_VerticalScrollMetricsChanged;
            WidgetContentScrollBar.ValueChanged += WidgetContentScrollBar_ValueChanged;
        }

        _horizontalScrollContent = widgetContent as IWidgetHorizontalScrollContent;
        if (_horizontalScrollContent is not null)
        {
            // 行は両方の層に同じ高さで置く(バーを出すときに広げる、UpdateContentHorizontalScrollBar)。本体の層の行は空なので、クリックは枠の層のバーへ通る。
            SetHorizontalScrollBarRowHeight(HorizontalScrollBarRowHiddenHeight);

            // 縦のバーが無い中身では、縦のバーの列まで伸ばす(中身も右端まで使っている)。
            if (_verticalScrollContent is null)
            {
                Grid.SetColumnSpan(WidgetContentHorizontalScrollBar, 2);
            }
            _horizontalScrollContent.HorizontalScrollMetricsChanged += HorizontalScrollContent_HorizontalScrollMetricsChanged;
            WidgetContentHorizontalScrollBar.ValueChanged += WidgetContentHorizontalScrollBar_ValueChanged;
        }

        // 窓の上のホイールを受ける中身なら、窓まで上がってきたホイールを全部渡す(ヘッダー・余白・枠の層のバーの上も)。
        _mouseWheelContent = widgetContent as IWidgetMouseWheelContent;
        if (_mouseWheelContent is not null)
        {
            MouseWheel += WidgetWindow_MouseWheel;
        }

        // 行の数が変わったら高さを中身に合わせる(中身の ScrollViewer に付けた AutoFitHeightBehavior から合図が上がる。FitHeightToContent)。
        AddHandler(AutoFitHeightBehavior.FitHeightRequestedEvent, new RoutedEventHandler(AutoFitHeight_FitHeightRequested));

        // まだ測れていないときに返す値。窓を出す前に保存を要求されることがある
        // (対象ごとに開く窓は Show() の前に SaveOpenTargets が走る)ので、
        // そこで 0 を書かせないために復元に使った値を控えておく。
        _restoredBounds = savedBounds.Clone();

        ApplySavedBounds(savedBounds, owner, widget.OriginalIndex);
        ApplyPinState(widget.IsPinned);

        _saveBoundsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _saveBoundsTimer.Tick += SaveBoundsTimer_Tick;

        Loaded += WidgetWindow_Loaded;
        LocationChanged += WidgetWindow_BoundsChanged;
        SizeChanged += WidgetWindow_SizeChanged;
        StateChanged += WidgetWindow_StateChanged;
        SourceInitialized += WidgetWindow_SourceInitialized;
    }

    public WidgetListItemViewModel Widget => _widget;

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        private set => SetValue(HeaderTextProperty, value);
    }

    public void SetHeaderText(string headerText)
    {
        HeaderText = string.IsNullOrWhiteSpace(headerText)
            ? _widget.DisplayName
            : headerText;
    }

    /// <summary>タイトルの左の円の色。</summary>
    public WidgetDataStatusLevel DataStatusLevel
    {
        get => (WidgetDataStatusLevel)GetValue(DataStatusLevelProperty);
        private set => SetValue(DataStatusLevelProperty, value);
    }

    /// <summary>円の TIPS の文(優先の高い理由1つ)。緑のときは空で、TIPS を出さない。</summary>
    public string DataStatusText
    {
        get => (string)GetValue(DataStatusTextProperty);
        private set => SetValue(DataStatusTextProperty, value);
    }

    /// <summary>タイトルの左に円を出すか。全体設定「ウィジェット状態」と今の色で決まる(<see cref="ApplyDataStatus"/>)。</summary>
    public bool IsDataStatusMarkShown
    {
        get => (bool)GetValue(IsDataStatusMarkShownProperty);
        private set => SetValue(IsDataStatusMarkShownProperty, value);
    }

    /// <summary>円とタイトルの上で TIPS を出すか。円を出していて、緑でないときだけ。</summary>
    public bool IsDataStatusTipShown
    {
        get => (bool)GetValue(IsDataStatusTipShownProperty);
        private set => SetValue(IsDataStatusTipShownProperty, value);
    }

    /// <summary>今の状態の印を、窓が束ねている中身から読む。</summary>
    public WidgetDataStatus DataStatus => _dataStatusSource.DataStatus;

    /// <summary>窓が束ねている中身に、状態の印を今の値で計算し直させる(受信の状態が変わったとき)。</summary>
    public void RefreshDataStatus()
    {
        _dataStatusSource.RefreshDataStatus();
    }

    /// <summary>状態の印が変わった(<see cref="Services.WidgetWindowManager"/> がカードの円をまとめ直すのに使う)。</summary>
    public event EventHandler? DataStatusChanged;

    private void DataStatusSource_DataStatusChanged(object? sender, EventArgs e)
    {
        ApplyDataStatus();
        DataStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        ApplyDataStatus();
    }

    /// <summary>
    /// 円の色と TIPS の文、円と TIPS を出すかを当て直す。出すかは全体設定「ウィジェット状態」で、
    /// 表示なら常に、非表示なら出さず、正常状態以外のみなら緑でないときだけ円を出す。
    /// </summary>
    private void ApplyDataStatus()
    {
        var status = _dataStatusSource.DataStatus;
        var isNormal = status.Level == WidgetDataStatusLevel.Complete;
        var isMarkShown = ConfigManager.Instance.CurrentSettings.WidgetDataStatusDisplayModeIndex switch
        {
            AppConfigDefaults.WidgetDataStatusDisplayHideIndex => false,
            AppConfigDefaults.WidgetDataStatusDisplayNonNormalOnlyIndex => !isNormal,
            _ => true
        };

        DataStatusLevel = status.Level;
        DataStatusText = WidgetDataStatusText.Format(status);
        IsDataStatusMarkShown = isMarkShown;
        IsDataStatusTipShown = isMarkShown && !isNormal;
    }

    /// <summary>
    /// ピン留めは「配置を終えて、以後は動かさずに使う」状態。
    /// 位置と大きさを固定し(ヘッダーのドラッグと縁での大きさの変更を止める)、クリックの操作は残す。
    /// フォーカスを奪わなくなり、ヘッダー/フッターを隠す設定もここでだけ効く。
    /// 最前面も、全体設定が「ピン留め時のみ最前面」ならピン留めに従う(<see cref="ApplyTopmost"/>)。
    /// 開いた直後から掛かる(置き直すときはピン留めを外す)。
    /// </summary>
    public void ApplyPinState(bool isPinned)
    {
        _isPinned = isPinned;
        ApplyTopmost();
        ApplyNoActivateState();
        ApplyInactiveChromeVisibility();
        QueueContentScrollBarUpdate();
    }

    /// <summary>
    /// 最前面にするか。全体設定「ウィジェットウィンドウ」が常に最前面なら常に、ピン留め時のみ最前面ならピン留め中だけ。
    /// <b><c>Topmost</c> を書く場所はここ1つだけ。</b>
    /// </summary>
    private void ApplyTopmost()
    {
        Topmost = _isPinned
            || ConfigManager.Instance.CurrentSettings.WidgetWindowTopmostModeIndex
                == AppConfigDefaults.AlwaysWidgetWindowTopmostModeIndex;

        if (Topmost)
        {
            BringToTopmostIfRefused();
        }
    }

    /// <summary>
    /// 最前面の指定が実際に付いたかを確かめ、付いていなければ属する窓を動かさずに上げ直す。
    /// WPF の <c>Topmost</c> は属する窓も一緒に動かす。この窓が IME の窓(Default IME。最後にアクティブにした窓が持ち主になる)の持ち主のとき、
    /// IME の窓は最前面にならず、この窓もその後ろに留まる。
    /// それでも付かなければ警告を出す。
    /// </summary>
    private void BringToTopmostIfRefused()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || HasTopmostStyle(handle))
        {
            return;
        }

        if (!SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate | SwpNoOwnerZOrder))
        {
            Log.Warning("Could not raise widget window {Title} to the top: Win32 error {Error}", Title, Marshal.GetLastWin32Error());
        }
        else if (!HasTopmostStyle(handle))
        {
            Log.Warning("Widget window {Title} is still not topmost after raising it without moving owned windows", Title);
        }
    }

    /// <summary>
    /// 全体設定(プレビューか保存)が変わった。最前面の条件と、状態の円と TIPS を出すかを当て直す。
    /// ピン留め時のみ最前面に切り替わって最前面から外れた窓は、前面の窓のすぐ後ろへ置く
    /// (普通の窓がほかの窓をアクティブにされたときと同じ位置。アクティブな窓は OS が前に置くので動かさない)。
    /// </summary>
    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        ApplyDataStatus();

        var wasTopmost = Topmost;
        ApplyTopmost();
        if (wasTopmost && !Topmost && !IsActive)
        {
            PlaceBehindForeground();
        }
    }

    /// <summary>
    /// 前面の窓のすぐ後ろへ置く(アクティブにはしない)。前面の窓が無いか、この窓自身なら動かさない。
    /// 前面がゲーム(管理者権限)だと本体(通常権限)はその窓を基準に置けないので、断られたら補助(管理者権限)に置いてもらう。
    /// </summary>
    private void PlaceBehindForeground()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var foreground = GetForegroundWindow();
        if (handle == IntPtr.Zero || foreground == IntPtr.Zero || foreground == handle)
        {
            return;
        }

        if (SetWindowPos(handle, foreground, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate))
        {
            return;
        }

        var error = GlobalHotkeyService.Instance.TryPlaceBehindForegroundThroughHelper(handle);
        if (error is null)
        {
            Log.Warning("Could not place widget window {Title} behind the foreground window, and the hotkey helper is not running", Title);
        }
        else if (error != 0)
        {
            Log.Warning("The hotkey helper could not place widget window {Title} behind the foreground window: Win32 error {Error}", Title, error);
        }
    }

    private static bool HasTopmostStyle(IntPtr handle)
    {
        return (GetWindowLong(handle, GwlExStyle) & WsExTopmost) != 0;
    }

    /// <summary>
    /// ピン留め中にフォーカスを奪わないようにする(<c>WS_EX_NOACTIVATE</c>)。
    /// マウス入力自体は届くので操作はできるが、アクティブにはならない。
    /// そのぶんアクティブ時の不透明度の底上げも起きない。
    /// </summary>
    private void ApplyNoActivateState()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            // まだ HWND が無い。SourceInitialized で貼り直す。
            return;
        }

        var noActivate = _isPinned;
        var exStyle = GetWindowLong(handle, GwlExStyle);
        var updated = noActivate
            ? exStyle | WsExNoActivate
            : exStyle & ~WsExNoActivate;

        if (updated != exStyle)
        {
            SetWindowLong(handle, GwlExStyle, updated);
        }
    }

    /// <summary>
    /// クリック透過(ウィジェットごとのスイッチ、ピン留めとは別)を当てる。
    /// 透過中は窓へのマウス入力を受けず、裏の窓へそのまま通す(<c>WS_EX_TRANSPARENT</c>)。
    /// WPF 側の当たり判定も合わせて止める。
    /// </summary>
    private void ApplyClickThroughState()
    {
        var clickThrough = _widget.IsClickThrough;
        IsHitTestVisible = !clickThrough;

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            // まだ HWND が無い。SourceInitialized で貼り直す。
            return;
        }

        var exStyle = GetWindowLong(handle, GwlExStyle);
        var updated = clickThrough
            ? exStyle | WsExTransparent
            : exStyle & ~WsExTransparent;

        if (updated != exStyle)
        {
            SetWindowLong(handle, GwlExStyle, updated);
        }
    }

    /// <summary>
    /// ピン留め中にヘッダー/フッターを隠す設定を反映する。
    ///
    /// <para>
    /// <b>窓の矩形は動かさない。</b> 行を畳んで窓を縮める形にすると、
    /// 「本体が窓の中で30px上へ」と「窓が画面上で30px下へ」の2つが必要になり、
    /// どちらか片方だけ反映された瞬間が1フレームでも出ると本体が跳ねて見える
    /// (順序を入れ替えても跳ねる向きが変わるだけ)。
    /// 代わりに行の高さは残したまま中身だけ隠し、枠の背景をその分だけ内側へ寄せる。
    /// <b>本体は1pxも動かないので跳ねようがない。</b>
    /// </para>
    ///
    /// <para>
    /// 隠した側は完全に透明になる。階層化ウィンドウではアルファ0のピクセルは
    /// クリックが素通りするので、窓の矩形が残っていても操作の邪魔にならない。
    /// </para>
    /// </summary>
    private void ApplyInactiveChromeVisibility()
    {
        var pinned = _isPinned;
        var hideHeader = _widget.HideHeaderWhenInactive && pinned;
        var hideFooter = _hasFooterContent && _widget.HideFooterWhenInactive && pinned;

        FrameHeaderChrome.Visibility = hideHeader ? Visibility.Collapsed : Visibility.Visible;
        WidgetHeader.Visibility = hideHeader ? Visibility.Collapsed : Visibility.Visible;

        // 枠の層の行も詰める。枠を30px内側へ寄せたうえで行が30px残っていると、
        // 枠の中身(スクロールバー)が本体の層より30px下にずれる。
        // 本体の層の行は30のまま残す(そこを詰めると本体が動いてしまう)。
        FrameHeaderRow.Height = hideHeader ? new GridLength(0) : new GridLength(ChromeRowHeight);

        UpdateFooterVisibility();

        // 枠(背景・角丸)を隠した分だけ内側へ寄せる。ここを寄せないと、
        // 中身を消しても背景の帯だけが残ってしまう。
        WidgetWindowFrame.Margin = new Thickness(
            0,
            hideHeader ? ChromeRowHeight : 0d,
            0,
            hideFooter ? ChromeRowHeight : 0d);

        QueueContentScrollBarUpdate();
    }

    /// <summary>
    /// フッターの可視状態。<b>枠の層と本体の層で Visibility が違う。</b>
    ///
    /// <para>
    /// 枠側は <c>Collapsed</c> にして行を詰める(枠が30px上で終わるようにする)。
    /// 本体側は <c>Hidden</c> で場所を残す。ここを <c>Collapsed</c> にすると行が0になり、
    /// 本体の表示領域が下へ30px広がってスクロール範囲が枠とずれる。
    /// </para>
    /// </summary>
    private void UpdateFooterVisibility()
    {
        if (!_hasFooterContent)
        {
            WidgetFooterFrame.Visibility = Visibility.Collapsed;
            WidgetFooterHost.Visibility = Visibility.Collapsed;
            return;
        }

        var hidden = _widget.HideFooterWhenInactive && _isPinned;
        WidgetFooterFrame.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
        WidgetFooterHost.Visibility = hidden ? Visibility.Hidden : Visibility.Visible;
    }

    /// <summary>ヘッダーの文字ボタンの2層を後から入れる(プラグインの空の窓に中身が入ったとき)。</summary>
    public void SetHeaderActions(FrameworkElement headerChromeActions, FrameworkElement headerActions)
    {
        WidgetHeaderChromeActionsHost.Content = headerChromeActions;
        WidgetHeaderActionsHost.Content = headerActions;
    }

    private void SetFooterContent(FrameworkElement? footerContent)
    {
        WidgetFooterHost.Content = footerContent;
        _hasFooterContent = footerContent is not null;
        UpdateFooterVisibility();
    }

    /// <summary>
    /// UIオートメーションに見せるのは窓そのもの(名前と位置)だけで、中の要素は見せない。
    /// ほかのプログラムが変化の通知を登録すると、WPF はレイアウトのたびに peer の子をたどり直し、行の数に比例して割り当てるため。
    /// </summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new WidgetWindowAutomationPeer(this);

    private sealed class WidgetWindowAutomationPeer(Window owner) : WindowAutomationPeer(owner)
    {
        protected override List<AutomationPeer>? GetChildrenCore() => null;

        // 既定は読むたびに窓の文字を取り直して割り当てる。レイアウトのたびに読まれるので、同じ文字の Title を返す。
        protected override string GetNameCore() => ((Window)Owner).Title;
    }

    protected override void OnClosed(EventArgs e)
    {
        // 動かしてから保存待ちの間に閉じた窓は、その移動も種別ごとの位置へ書く。
        SaveBounds(isMoveOrResize: _saveBoundsTimer.IsEnabled);
        _saveBoundsTimer.Stop();
        _saveBoundsTimer.Tick -= SaveBoundsTimer_Tick;
        _widget.PropertyChanged -= Widget_PropertyChanged;
        ConfigManager.Instance.SettingsPreviewChanged -= ConfigManager_SettingsPreviewChanged;
        _dataStatusSource.DataStatusChanged -= DataStatusSource_DataStatusChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        if (_verticalScrollContent is not null)
        {
            _verticalScrollContent.VerticalScrollMetricsChanged -= VerticalScrollContent_VerticalScrollMetricsChanged;
            WidgetContentScrollBar.ValueChanged -= WidgetContentScrollBar_ValueChanged;
        }

        if (_horizontalScrollContent is not null)
        {
            _horizontalScrollContent.HorizontalScrollMetricsChanged -= HorizontalScrollContent_HorizontalScrollMetricsChanged;
            WidgetContentHorizontalScrollBar.ValueChanged -= WidgetContentHorizontalScrollBar_ValueChanged;
        }

        if (_mouseWheelContent is not null)
        {
            MouseWheel -= WidgetWindow_MouseWheel;
        }

        RemoveHandler(AutoFitHeightBehavior.FitHeightRequestedEvent, new RoutedEventHandler(AutoFitHeight_FitHeightRequested));
        _autoFitHeightSource = null;

        if (WidgetContentHost.Content is FrameworkElement { DataContext: IDisposable disposable })
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }

    private void WidgetWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _isRestoringBounds = false;
        UpdateWindowRootClip();
        QueueContentScrollBarUpdate();
        QueueFitHeightIfPending();
    }

    private void WidgetWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            // ウィジェットの窓はソフトウェアで描く。GPU で描くと、文字とアイコンの影の効果ごとに
            // テクスチャを作り GPU の完了を待つので、窓が多いと描画のスレッドが詰まる。
            source.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
            source.AddHook(WndProc);
        }

        ApplyNoActivateState();
        ApplyClickThroughState();
        ApplyInactiveChromeVisibility();
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // WS_EX_NOACTIVATE だけでは足りない。クリックすると WM_MOUSEACTIVATE が来て、
        // 既定では MA_ACTIVATE が返るのでフォーカスを奪ってしまう。
        // ここで MA_NOACTIVATE を返して、入力だけ受け取り活性化はしない状態にする。
        if (msg == WmMouseActivate && _isPinned)
        {
            handled = true;
            return new IntPtr(MaNoActivate);
        }

        // ピン留め中は大きさを固定するので、縁を返さない(全体がクライアント領域になる)。
        if (msg != WmNcHitTest || WindowState == WindowState.Maximized || _isPinned)
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

        var hitTest = HtClient;

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

        if (hitTest == HtClient)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(hitTest);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // ピン留め中は位置を固定するので動かさない。
        // DragMove() は OS の移動ループの中で必ずアクティブ化されるので、ピン留め中に使うとフォーカスも奪う。
        if (e.LeftButton != MouseButtonState.Pressed || _isPinned)
        {
            return;
        }

        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Widget_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_usesWidgetDisplayNameForHeader
            && e.PropertyName == nameof(WidgetListItemViewModel.DisplayName))
        {
            HeaderText = _widget.DisplayName;
        }

        if (e.PropertyName is nameof(WidgetListItemViewModel.HideHeaderWhenInactive)
            or nameof(WidgetListItemViewModel.HideFooterWhenInactive))
        {
            ApplyInactiveChromeVisibility();
        }

        if (e.PropertyName == nameof(WidgetListItemViewModel.IsClickThrough))
        {
            ApplyClickThroughState();
        }
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        ApplyInactiveChromeVisibility();
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        ApplyInactiveChromeVisibility();
    }

    private void WidgetWindow_BoundsChanged(object? sender, EventArgs e)
    {
        ScheduleBoundsSave();
    }

    private void WidgetWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateWindowRootClip();
        QueueContentScrollBarUpdate();
        ScheduleBoundsSave();
    }

    private void WidgetWindow_StateChanged(object? sender, EventArgs e)
    {
        UpdateWindowRootClip();
        QueueFitHeightIfPending();
    }

    private void AutoFitHeight_FitHeightRequested(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not ScrollViewer source)
        {
            return;
        }

        _autoFitHeightSource = source;
        _isFitHeightPending = true;
        QueueFitHeightIfPending();
    }

    /// <summary>合わせる合図が残っていれば、描画の後に合わせる(窓を出す前・最小化の間に来た合図は、出した後・戻した後に合わせる)。</summary>
    private void QueueFitHeightIfPending()
    {
        if (_isFitHeightPending)
        {
            Dispatcher.BeginInvoke(FitHeightToContent, DispatcherPriority.Loaded);
        }
    }

    /// <summary>
    /// 高さを中身の全部が入る高さにする(<see cref="AutoFitHeightBehavior"/>)。上端はそのままで下端を動かす。
    ///
    /// <para>
    /// 伸ばすのは、窓が載っている画面の作業領域(タスクバーを除いた領域)の下端まで。越える分はスクロールバーで見る
    /// (上限が無いと、はみ出した行を見る方法が無くなる)。上限は伸ばすときだけで、今の高さがすでに下端を越えていればそれより縮めない
    /// (端からはみ出して置く使い方を崩さない)。ピン留め中も合わせる(ピン留めが止めるのは手での移動と大きさの変更)。
    /// </para>
    /// </summary>
    private void FitHeightToContent()
    {
        if (!_isFitHeightPending
            || _autoFitHeightSource is null
            || !IsLoaded
            || WindowState != WindowState.Normal)
        {
            return;
        }

        _isFitHeightPending = false;

        var shortfall = AutoFitHeightBehavior.GetHeightShortfall(_autoFitHeightSource);
        if (!double.IsFinite(shortfall) || Math.Abs(shortfall) < 0.5d)
        {
            return;
        }

        var currentHeight = ActualHeight;
        var targetHeight = Math.Max(currentHeight + shortfall, MinHeight);
        if (targetHeight > currentHeight)
        {
            if (TryGetWorkAreaBottom(out var workAreaBottom))
            {
                targetHeight = Math.Min(targetHeight, Math.Max(currentHeight, workAreaBottom - Top));
            }
            else
            {
                Log.Warning("Widget window: could not get the monitor work area; fitting the height without the screen limit.");
            }
        }

        if (Math.Abs(targetHeight - currentHeight) < 0.5d)
        {
            return;
        }

        _isFittingHeight = true;
        Height = targetHeight;
        _isFittingHeight = false;
    }

    /// <summary>窓が載っている(一番近い)画面の作業領域の下端。窓の座標(論理ピクセル)で返す。</summary>
    private bool TryGetWorkAreaBottom(out double workAreaBottom)
    {
        workAreaBottom = 0d;
        if (PresentationSource.FromVisual(this) is not HwndSource { CompositionTarget: { } compositionTarget } source)
        {
            return false;
        }

        var monitor = MonitorFromWindow(source.Handle, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        workAreaBottom = compositionTarget.TransformFromDevice.Transform(new Point(0d, monitorInfo.WorkArea.Bottom)).Y;
        return double.IsFinite(workAreaBottom);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr windowHandle, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitorHandle, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public MonitorRect MonitorArea;
        public MonitorRect WorkArea;
        public uint Flags;
    }

    private void UpdateWindowRootClip()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowRoot.Clip = null;
            return;
        }

        if (WindowRoot.ActualWidth <= 0 || WindowRoot.ActualHeight <= 0)
        {
            return;
        }

        var cornerRadius = TryFindResource("Radius.Default") is CornerRadius configuredRadius
            ? configuredRadius.TopLeft
            : 6d;

        WindowRoot.Clip = new RectangleGeometry(
            new Rect(0, 0, WindowRoot.ActualWidth, WindowRoot.ActualHeight),
            cornerRadius,
            cornerRadius);
    }

    private void VerticalScrollContent_VerticalScrollMetricsChanged(object? sender, EventArgs e)
    {
        UpdateContentScrollBar();
    }

    private void QueueContentScrollBarUpdate()
    {
        if (_horizontalScrollContent is not null)
        {
            Dispatcher.BeginInvoke(
                UpdateContentHorizontalScrollBar,
                DispatcherPriority.Loaded);
        }

        if (_verticalScrollContent is null)
        {
            WidgetContentScrollBar.Visibility = Visibility.Collapsed;
            WidgetContentScrollBarColumn.Width = new GridLength(5);
            return;
        }

        Dispatcher.BeginInvoke(
            UpdateContentScrollBar,
            DispatcherPriority.Loaded);
    }

    private void WidgetContentScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSynchronizingContentScrollBar || _verticalScrollContent is null)
        {
            return;
        }

        _verticalScrollContent.SetVerticalScrollOffset(e.NewValue);
    }

    private void UpdateContentScrollBar()
    {
        if (_verticalScrollContent is null || !IsLoaded)
        {
            WidgetContentScrollBar.Visibility = Visibility.Collapsed;
            WidgetContentScrollBarColumn.Width = new GridLength(5);
            return;
        }

        var metrics = _verticalScrollContent.GetVerticalScrollMetrics();
        var maximum = Math.Max(metrics.Maximum, 0);
        var viewport = Math.Max(metrics.ViewportSize, 0);

        _isSynchronizingContentScrollBar = true;
        try
        {
            WidgetContentScrollBar.Minimum = 0;
            WidgetContentScrollBar.Maximum = maximum;
            WidgetContentScrollBar.ViewportSize = viewport;
            WidgetContentScrollBar.LargeChange = Math.Max(metrics.LargeChange, 1);
            WidgetContentScrollBar.SmallChange = Math.Max(metrics.SmallChange, 1);
            WidgetContentScrollBar.Value = Math.Clamp(metrics.Value, 0, maximum);

            var isScrollBarVisible = maximum > 0;

            WidgetContentScrollBar.Visibility = isScrollBarVisible
                ? Visibility.Visible
                : Visibility.Collapsed;

            WidgetContentScrollBarColumn.Width = isScrollBarVisible
                ? new GridLength(16)
                : new GridLength(5);
        }
        finally
        {
            _isSynchronizingContentScrollBar = false;
        }
    }

    private void WidgetWindow_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!e.Handled && _mouseWheelContent!.HandleWidgetMouseWheel(e.Delta))
        {
            e.Handled = true;
        }
    }

    private void HorizontalScrollContent_HorizontalScrollMetricsChanged(object? sender, EventArgs e)
    {
        UpdateContentHorizontalScrollBar();
    }

    private void WidgetContentHorizontalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSynchronizingContentHorizontalScrollBar || _horizontalScrollContent is null)
        {
            return;
        }

        _horizontalScrollContent.SetHorizontalScrollOffset(e.NewValue);
    }

    /// <summary>
    /// 横のバーを中身の値へ合わせる。ずらせるときだけバーを出し、行をバーの分(16)に広げる。出さないときの行は5
    /// (縦のバーの列と同じ。中身は下の余白を持たず、この行が下の隙間になる)。
    /// </summary>
    private void UpdateContentHorizontalScrollBar()
    {
        if (_horizontalScrollContent is null || !IsLoaded)
        {
            WidgetContentHorizontalScrollBar.Visibility = Visibility.Collapsed;
            if (_horizontalScrollContent is not null)
            {
                SetHorizontalScrollBarRowHeight(HorizontalScrollBarRowHiddenHeight);
            }

            return;
        }

        var metrics = _horizontalScrollContent.GetHorizontalScrollMetrics();
        var maximum = Math.Max(metrics.Maximum, 0);
        var viewport = Math.Max(metrics.ViewportSize, 0);

        _isSynchronizingContentHorizontalScrollBar = true;
        try
        {
            WidgetContentHorizontalScrollBar.Minimum = 0;
            WidgetContentHorizontalScrollBar.Maximum = maximum;
            WidgetContentHorizontalScrollBar.ViewportSize = viewport;
            WidgetContentHorizontalScrollBar.LargeChange = Math.Max(metrics.LargeChange, 1);
            WidgetContentHorizontalScrollBar.SmallChange = Math.Max(metrics.SmallChange, 1);
            WidgetContentHorizontalScrollBar.Value = Math.Clamp(metrics.Value, 0, maximum);

            var isScrollBarVisible = maximum > 0;
            WidgetContentHorizontalScrollBar.Visibility = isScrollBarVisible
                ? Visibility.Visible
                : Visibility.Collapsed;
            SetHorizontalScrollBarRowHeight(isScrollBarVisible
                ? HorizontalScrollBarRowVisibleHeight
                : HorizontalScrollBarRowHiddenHeight);
        }
        finally
        {
            _isSynchronizingContentHorizontalScrollBar = false;
        }
    }

    /// <summary>横のバーの行の高さを枠の層と本体の層で同じにそろえる(ずれるとクリックがバーへ届かない)。</summary>
    private void SetHorizontalScrollBarRowHeight(double height)
    {
        FrameHorizontalScrollBarRow.Height = new GridLength(height);
        WidgetContentHorizontalScrollBarRow.Height = new GridLength(height);
    }

    private void ScheduleBoundsSave()
    {
        if (_isRestoringBounds || _isFittingHeight || !IsLoaded || WindowState == WindowState.Minimized)
        {
            return;
        }

        _saveBoundsTimer.Stop();
        _saveBoundsTimer.Start();
    }

    private void SaveBoundsTimer_Tick(object? sender, EventArgs e)
    {
        _saveBoundsTimer.Stop();
        SaveBounds(isMoveOrResize: true);
    }

    /// <summary>
    /// 枠のレイヤーに重ねる飾りの置き場。<b>中身のレイヤーに置くと背景の上にベタで乗る</b>ので、
    /// 分割線や閉じる印と同じ不透明度で合成したいものはここへ入れる。
    /// </summary>
    public ContentControl FrameOverlayHost => WidgetFrameOverlayHost;

    /// <summary>
    /// この窓ぶんの位置を保存する経路。<c>null</c> なら種別ごとの位置へ保存する。
    ///
    /// <para>
    /// 同じウィジェットを複数開くと、種別ごとの位置では最後に動かした窓が全部を上書きし、
    /// 次の起動で全員が同じ場所に出る。1枚ずつ持つ窓はこちらへ流す。
    /// </para>
    ///
    /// <para>
    /// こちらへ流す窓も、動かしたときと大きさを変えたときは種別ごとの位置にも書く(次に新しく開く窓の位置と大きさになる)。
    /// <b>閉じたときは書かない。</b>閉じる順に上書きされ、最後に動かした窓の位置ではなくなる。
    /// </para>
    /// </summary>
    public Action? SaveWindowBoundsOverride { get; set; }

    /// <param name="isMoveOrResize">
    /// 動かした・大きさを変えたことによる保存か。閉じたときの保存は、その保存待ちが残っているときだけ真。
    /// </param>
    private void SaveBounds(bool isMoveOrResize)
    {
        if (_isRestoringBounds
            || !IsLoaded
            || WindowState == WindowState.Minimized
            || !IsFinitePositive(ActualWidth)
            || !IsFinitePositive(ActualHeight)
            || !double.IsFinite(Left)
            || !double.IsFinite(Top))
        {
            return;
        }

        if (SaveWindowBoundsOverride is not null)
        {
            SaveWindowBoundsOverride();
            if (!isMoveOrResize)
            {
                return;
            }
        }

        // 大きさを保存しない辺(WidgetConfigDefaults.GetFixedWindowSize)は書かない。
        var fixedSize = WidgetConfigDefaults.GetFixedWindowSize(_widget.Kind);
        WidgetStateManager.Instance.SaveWidgetWindowBounds(
            _widget.Key,
            Left,
            Top,
            fixedSize.Width is null ? ActualWidth : null,
            fixedSize.Height is null ? ActualHeight : null);
    }

    /// <summary>
    /// いまの位置と大きさ。保存用。
    ///
    /// <para>
    /// <b>まだ測れていない項目は、復元に使った値をそのまま返す。</b>
    /// この窓を <c>Show()</c> する前に <c>SaveOpenTargets</c> が走る経路があり
    /// (窓を作った直後・対象名が判明したとき・別の窓が閉じたとき)、そこでは
    /// <see cref="FrameworkElement.ActualWidth"/> がまだ 0 になっている。
    /// </para>
    ///
    /// <para>
    /// 素通しで 0 を書くと、復元側 <c>ApplySavedBounds</c> が正の有限値しか採らないため
    /// <b>大きさだけが既定に戻る</b>。位置は <c>Left</c>/<c>Top</c> がコンストラクタで
    /// 入るので残り、「位置は覚えているのにリサイズだけ忘れる」という形で出る。
    /// </para>
    ///
    /// <para>
    /// 大きさを保存しない辺(<see cref="WidgetConfigDefaults.GetFixedWindowSize"/>)は null。
    /// </para>
    /// </summary>
    public WidgetWindowConfig GetCurrentBounds()
    {
        var hasMeasuredSize = IsFinitePositive(ActualWidth) && IsFinitePositive(ActualHeight);
        var hasPosition = double.IsFinite(Left) && double.IsFinite(Top);
        var fixedSize = WidgetConfigDefaults.GetFixedWindowSize(_widget.Kind);

        return new WidgetWindowConfig
        {
            X = hasPosition ? Left : _restoredBounds.X,
            Y = hasPosition ? Top : _restoredBounds.Y,
            Width = fixedSize.Width is not null
                ? null
                : hasMeasuredSize ? ActualWidth : _restoredBounds.Width,
            Height = fixedSize.Height is not null
                ? null
                : hasMeasuredSize ? ActualHeight : _restoredBounds.Height
        };
    }

    /// <summary>掴める最小の領域(px)。これだけ仮想画面内に残っていれば動かさない。</summary>
    private const double ReachableWidth = 60d;
    private const double ReachableHeight = 30d;

    /// <summary>
    /// 保存された位置が画面に届かないときだけ、仮想画面の中へ寄せる。
    ///
    /// <para>
    /// 表示していたモニタが無くなると、保存座標がどのモニタにも属さなくなって
    /// ウィンドウごと画面外に残る。メインウィンドウは
    /// <c>WindowPlacementBehavior</c> で同じ対策をしているが、ウィジェットには無かった。
    /// </para>
    ///
    /// <para>
    /// <b>少しはみ出している程度では動かさない。</b> オーバーレイなので端から
    /// わざとはみ出して置く使い方があり、そこを引き戻すと邪魔になる。
    /// ヘッダーを掴める分が残っているかだけを見る。
    /// </para>
    /// </summary>
    private void MoveIntoVirtualScreenIfUnreachable()
    {
        if (!double.IsFinite(Left) || !double.IsFinite(Top)
            || !IsFinitePositive(Width) || !IsFinitePositive(Height))
        {
            return;
        }

        var minX = SystemParameters.VirtualScreenLeft;
        var minY = SystemParameters.VirtualScreenTop;
        var maxX = minX + SystemParameters.VirtualScreenWidth;
        var maxY = minY + SystemParameters.VirtualScreenHeight;

        var visibleWidth = Math.Min(Left + Width, maxX) - Math.Max(Left, minX);
        var visibleHeight = Math.Min(Top + Height, maxY) - Math.Max(Top, minY);
        if (visibleWidth >= ReachableWidth && visibleHeight >= ReachableHeight)
        {
            return;
        }

        var width = Math.Min(Width, SystemParameters.VirtualScreenWidth);
        var height = Math.Min(Height, SystemParameters.VirtualScreenHeight);

        Width = width;
        Height = height;
        Left = Math.Clamp(Left, minX, Math.Max(minX, maxX - width));
        Top = Math.Clamp(Top, minY, Math.Max(minY, maxY - height));
    }

    private void ApplySavedBounds(WidgetWindowConfig savedBounds, Window? owner, int originalIndex)
    {
        // 大きさを保存しない辺は、保存先に値が残っていてもいつも既定の大きさで開く。
        var fixedSize = WidgetConfigDefaults.GetFixedWindowSize(_widget.Kind);
        var width = fixedSize.Width ?? savedBounds.Width;
        var height = fixedSize.Height ?? savedBounds.Height;
        if (IsFinitePositive(width) && IsFinitePositive(height))
        {
            Width = Math.Max(width!.Value, MinWidth);
            Height = Math.Max(height!.Value, MinHeight);
        }

        if (double.IsFinite(savedBounds.X ?? double.NaN) && double.IsFinite(savedBounds.Y ?? double.NaN))
        {
            Left = savedBounds.X!.Value;
            Top = savedBounds.Y!.Value;
            MoveIntoVirtualScreenIfUnreachable();
            return;
        }

        if (owner is null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        if (_widget.Kind is WidgetKind.UserBuildLibrary or WidgetKind.CombatExtensionTimeline or WidgetKind.HatePs or WidgetKind.PartyDps or WidgetKind.EnemyCatalog)
        {
            var area = WindowWorkingAreaBehavior.GetWorkingAreaForWindow(owner);
            Width = Math.Min(Width, Math.Max(MinWidth, area.Width - 24));
            Height = Math.Min(Height, Math.Max(MinHeight, area.Height - 24));
            Left = area.Left + Math.Max(0, (area.Width - Width) / 2);
            Top = area.Top + Math.Max(0, (area.Height - Height) / 2);
            return;
        }

        const double initialOffset = 44;
        const double cascadeOffset = 20;
        Left = owner.Left + initialOffset + (cascadeOffset * originalIndex);
        Top = owner.Top + initialOffset + (cascadeOffset * originalIndex);
    }

    private static bool IsFinitePositive(double? value)
    {
        return value.HasValue && double.IsFinite(value.Value) && value.Value > 0;
    }

    private static Point GetScreenPoint(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        var x = unchecked((short)(value & 0xFFFF));
        var y = unchecked((short)((value >> 16) & 0xFFFF));
        return new Point(x, y);
    }
}
