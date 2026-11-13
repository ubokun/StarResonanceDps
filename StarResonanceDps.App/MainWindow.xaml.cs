// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.Views;
#if DEBUG
using StarResonanceDps.App.DebugTools;
#endif

namespace StarResonanceDps.App;

public partial class MainWindow : Window
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

    private static readonly bool IsInDesignMode =
        DesignerProperties.GetIsInDesignMode(new DependencyObject());

    /// <summary>
    /// 起動できないときに出す窓(持ち主はこの窓)。null なら起動を続ける。
    /// 見せるだけの窓では Loaded の処理(ウィジェットの復元・ホットキー)を走らせない(Core の初期化が済んでいない)。
    /// </summary>
    private readonly Action<Window>? _showStartupFailure;

    /// <summary>入っている Npcap の版。古ければ窓が描かれたところで知らせる。</summary>
    private readonly Version _npcapVersion;

    /// <summary>起動の処理(<see cref="MainWindow_ContentRendered"/>)を走らせたか。</summary>
    private bool _hasStartedUp;

    /// <summary>起動を続けるメイン窓。</summary>
    public static MainWindow Create(Version npcapVersion)
    {
        return new MainWindow(showStartupFailure: null, npcapVersion);
    }

    /// <summary>
    /// 起動できないときに見せるだけのメイン窓(起動エラーの窓の持ち主。どのアプリの窓かが分かる)。
    /// 描かれたところで <paramref name="showStartupFailure"/> の窓を出し、閉じたら終了コード 1 で終わる。
    /// </summary>
    public static MainWindow CreateForStartupFailure(Action<Window> showStartupFailure)
    {
        return new MainWindow(showStartupFailure, new Version());
    }

    private MainWindow(Action<Window>? showStartupFailure, Version npcapVersion)
    {
        _showStartupFailure = showStartupFailure;
        _npcapVersion = npcapVersion;
        InitializeComponent();

        if (!IsInDesignMode)
        {
            DataContext = new MainViewModel();
        }

        ContentRendered += MainWindow_ContentRendered;
        SourceInitialized += MainWindow_SourceInitialized;
#if DEBUG
        PreviewKeyDown += MainWindow_PreviewKeyDown;
#endif
    }

    /// <summary>
    /// 起動の処理。この窓が表示されて最初の描画が終わった後(WPF の合図)に1回だけ走る。
    ///
    /// <para>
    /// <c>Loaded</c> には置かない。<c>Loaded</c> は <c>Show()</c> の中で HWND を作ったときのレイアウトで呼ばれ、
    /// <c>ShowWindow</c> より前なので、そこで開いたウィジェットや出した窓はこの窓より先に見え、
    /// 処理の中でメッセージが回る(補助の起動のユーザーアカウント制御)と予約した処理も表示前に走る。
    /// <c>ContentRendered</c> の予約は <c>Show()</c> の中でメッセージが回らなければ <c>OnStartup</c> が戻った後に処理される。
    /// </para>
    ///
    /// <para>
    /// 起動できないときはその窓を出して終わる。続けるときは、設定ファイルの知らせ → Windows起動の登録を合わせる →
    /// プラグインの初期化 → ウィジェットの復元 → ホットキー(起動設定でオンなら補助を起動) → VOICEVOX の起動(起動設定でオンなら) →
    /// VOICEVOX の初期化 → 古い Npcap の確認 → 読み上げの声の確認 → 更新が済んだことの知らせ → 更新の確認(起動設定でオンなら)、の順。
    /// 1つの処理に直列なので、途中でメッセージが回ってもこの順は崩れない。
    /// </para>
    /// </summary>
    private async void MainWindow_ContentRendered(object? sender, EventArgs e)
    {
        // ContentRendered は中身を入れ替えると再び届く。起動の処理は1回だけ。
        if (_hasStartedUp)
        {
            return;
        }

        _hasStartedUp = true;

        if (_showStartupFailure is not null)
        {
            _showStartupFailure(this);
            Application.Current.Shutdown(1);
            return;
        }

        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        // 起動時に読めなかった設定ファイルを知らせる。ウィジェットを開く前に出す(開くとウィジェットの設定が保存され、壊れたファイルが上書きされる)。
        SettingsLoadFailureMessage.Show(this);

        // フォルダーを動かしたときに場所を合わせるため、起動のたびに書き直す。
        if (!WindowsStartupRegistration.Apply(ConfigManager.Instance.GetSettingsSnapshot().LaunchAtWindowsStartup))
        {
            WindowsStartupRegistrationFailureMessage.Show(this);
        }

        // プラグインはウィジェットを開く前に初期化する。読めなかったものはここで1回だけ知らせる。
        PluginLoadFailureMessage.Show(this, PluginManager.Instance.InitializeAll());

        viewModel.RestoreRunningWidgetWindows();

        // ホットキーの補助が使えなかったこと・登録できなかったホットキーはここで知らせる
        // (メッセージの親にこの窓が要るので、開いた後に登録する)。
        GlobalHotkeyService.Instance.HelperUnavailable += HotkeyService_HelperUnavailable;
        HotkeyRegistrationFailureMessage.Show(this, viewModel.ApplyHotkeys());

        var hasLaunchedVoicevox = LaunchVoicevoxOnStartup();

        // 読み上げに使う VOICEVOX のスタイルを、最初の通知より前に初期化しておく(返事は待たない)。
        VoicevoxStyleInitializer.Start();

        // 古い Npcap は知らせて続ける(無いときは App が初期化の前に止めている)。読み上げの確認より先。
        if (NpcapVersionProbe.IsOutdated(_npcapVersion))
        {
            OwnerModalWindow.RunWhenNotMovingOrSizing(() => NpcapOutdatedMessage.Show(this, _npcapVersion));
        }

        // 読み上げの声が使えなければ知らせる(オンの読み上げの項目が無ければ何もしない)。
        await NotificationCheckMessage.CheckSpeechVoicesAsync(
            this,
            ConfigManager.Instance.GetNotificationsSnapshot(),
            checksVoicevox: !hasLaunchedVoicevox);

        if (AppUpdateService.WasJustUpdated)
        {
            UpdateCompletedMessage.Show(this);
        }

        if (ConfigManager.Instance.GetSettingsSnapshot().CheckForUpdatesOnStartup)
        {
            await AppUpdateService.CheckOnStartupAsync(this);
        }
    }

    /// <summary>
    /// 起動設定の VOICEVOX のインストール先を見つけた場所に合わせ、オンなら VOICEVOX を起動する(動いていなければ)。
    /// 起動できなければ知らせる。この処理で起動したかを返す。
    /// </summary>
    private bool LaunchVoicevoxOnStartup()
    {
        VoicevoxLauncher.UpdateInstallFolder();
        var settings = ConfigManager.Instance.GetSettingsSnapshot();
        if (!settings.LaunchVoicevoxOnLaunch)
        {
            return false;
        }

        var result = VoicevoxLauncher.LaunchOnStartup(settings.VoicevoxInstallFolder);
        if (result == VoicevoxLaunchResult.Failed)
        {
            VoicevoxLaunchFailureMessage.Show(this);
        }

        return result == VoicevoxLaunchResult.Launched;
    }

    private void HotkeyService_HelperUnavailable(object? sender, EventArgs e)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            this,
            localization.GetString("Hotkey_Error_Title"),
            localization.GetString("Hotkey_HelperUnavailable_Message"),
            localization.GetString("Hotkey_HelperUnavailable_Detail"));
    }

#if DEBUG
    // Ctrl+Shift+D でデバッグ用の確認画面(DebugTools、Debug の構成でだけビルドされる)を開く。
    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.D || Keyboard.Modifiers != (ModifierKeys.Control | ModifierKeys.Shift))
        {
            return;
        }

        e.Handled = true;
        new MessagePreviewWindow { Owner = this }.Show();
    }
#endif

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmNcHitTest || ResizeMode == ResizeMode.NoResize || WindowState == WindowState.Maximized)
        {
            return IntPtr.Zero;
        }

        Point cursor = PointFromScreen(GetScreenPoint(lParam));
        double width = ActualWidth;
        double height = ActualHeight;

        bool left = cursor.X >= 0 && cursor.X < ResizeBorderThickness;
        bool right = cursor.X <= width && cursor.X > width - ResizeBorderThickness;
        bool top = cursor.Y >= 0 && cursor.Y < ResizeBorderThickness;
        bool bottom = cursor.Y <= height && cursor.Y > height - ResizeBorderThickness;

        int hitTest = HtClient;

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

    private static Point GetScreenPoint(IntPtr lParam)
    {
        long value = lParam.ToInt64();
        int x = unchecked((short)(value & 0xFFFF));
        int y = unchecked((short)((value >> 16) & 0xFFFF));
        return new Point(x, y);
    }
}
