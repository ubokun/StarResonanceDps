// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Serilog;

namespace StarResonanceDps.App.Services;

/// <summary>
/// オーナーだけを止めるモーダル表示。
///
/// <para>
/// <see cref="Window.ShowDialog"/> は<b>アプリケーション モーダル</b>で、アプリ内の全ウィンドウの
/// 入力を止めてしまう。設定ウィンドウを開いている間もウィジェットは操作したいので、
/// 止める範囲をオーナー1枚に限定する。
/// </para>
///
/// <para>
/// 無効化に <c>IsEnabled</c> を使わないのは、WPF の無効状態が視覚ツリー全体に伝わって
/// <b>オーナーの中身が灰色に沈む</b>ため(このプロジェクトのスタイルは <c>IsEnabled=False</c> で
/// 不透明度を落とす)。<c>EnableWindow</c> は入力だけを止めるので見た目が変わらない。
/// モーダルダイアログが内部でやっているのと同じこと。
/// </para>
///
/// <para>
/// <b>窓の移動・大きさの変更の最中に、閉じるまで待つ窓を出さない。</b>ヘッダーのドラッグ(<c>DragMove</c>)の間、
/// 画面のスレッドは Windows の移動のループの中にいて、その中でも溜まった処理は流れる。そこで <see cref="ShowAndWait"/> の
/// 待ちを始めると、待ちが移動のループの内側に入れ子になり、閉じるまで移動が終われず、ほかの窓も動かせなくなる
/// (動かしていた窓がマウスの捕捉も持ったまま)。後から流れる処理(非同期の続き・通知)から待つ窓を出すときは
/// <see cref="RunWhenNotMovingOrSizing"/> を通す。
/// </para>
/// </summary>
public static class OwnerModalWindow
{
    private const int WmEnterSizeMove = 0x0231;
    private const int WmExitSizeMove = 0x0232;
    private const uint GuiInMoveSize = 0x0002;

    /// <summary>移動・大きさの変更が終わるのを待っている処理。来た順に走らせる。画面のスレッドだけが触る。</summary>
    private static readonly Queue<Action> PendingActions = new();

    /// <summary>終わりを待っている、動かしている最中の窓。</summary>
    private static HwndSource? _watchedSource;

    /// <summary>
    /// <see cref="_watchedSource"/> が動いている最中か。<c>WM_ENTERSIZEMOVE</c> で立ち、<c>WM_EXITSIZEMOVE</c> で落ちる。
    /// 終わりを受けた窓は、次に動かし始めるまで動いていないとみなす(終わりはこの通知で決める)。
    /// </summary>
    private static bool _isWatchedWindowMoving;

    private static bool _isRunPosted;
    private static bool _isRunningPendingActions;

    /// <param name="onClosed">
    /// 閉じた後に走らせる処理。<c>ShowDialog</c> と違って呼び出しは待たないので、
    /// 結果を使う続きはここに渡す。
    /// </param>
    public static void Show(Window window, Window? owner, Action? onClosed = null)
    {
        ArgumentNullException.ThrowIfNull(window);

        var ownerHandle = owner is null
            ? IntPtr.Zero
            : new WindowInteropHelper(owner).Handle;

        if (ownerHandle != IntPtr.Zero)
        {
            // <b>閉じると決まった瞬間(窓が消える前)にオーナーを戻す。</b>ShowDialog が閉じるときと同じ順番。
            // 消えた後に戻すと、消える瞬間はオーナーが止まったままで選ばれず、Windows が別のアプリを前面にする。
            // 前面を移すのは閉じる窓が前面だったときだけ(別のアプリへ切り替えてから閉じたときは奪わない)。
            // Closing が届くのは閉じると決まったときだけという前提(各窓は取り消すとき base.OnClosing を呼ばずに戻る)。
            window.Closing += (_, e) =>
            {
                if (e.Cancel)
                {
                    return;
                }

                var wasActive = window.IsActive;
                EnableWindow(ownerHandle, true);
                if (wasActive)
                {
                    owner!.Activate();
                }
            };

            // 閉じ始めを通らずに閉じたとき(オーナーが閉じる・アプリの終了で、取り消しを無視して閉じられた)の戻し。
            // 止めたオーナーは、実際に閉じるたびに必ず戻す。
            window.Closed += (_, _) => EnableWindow(ownerHandle, true);
        }

        if (onClosed is not null)
        {
            window.Closed += (_, _) => onClosed();
        }

        window.Show();

        // <b>止めるのは表示の後。</b>アクティブなまま無効化すると、無効なウィンドウは
        // アクティブでいられないので、Windows がアクティブ化を次のウィンドウ(下にある
        // 別アプリ)へ渡してしまい、オーナーがその後ろへ回る。
        if (ownerHandle != IntPtr.Zero)
        {
            EnableWindow(ownerHandle, false);
        }
    }

    /// <summary>
    /// <see cref="Show"/> と同じくオーナーだけを止めて表示し、閉じるまで呼び出しを待つ。
    ///
    /// <para>
    /// 結果をその場で使う呼び出し(<c>Window.OnClosing</c> の中で閉じるかを決める確認など)用。
    /// 待つ間は <see cref="Dispatcher.PushFrame"/> でメッセージを回すので、<see cref="Window.ShowDialog"/> と違って
    /// オーナー以外のウィンドウ(ウィジェット)は動き続ける。
    /// </para>
    ///
    /// <para>
    /// 窓の移動・大きさの変更の最中に呼ばれたら、そのまま出してログに警告を残す(移動は閉じるまで終われない)。
    /// 後から流れる処理から呼ぶときは <see cref="RunWhenNotMovingOrSizing"/> を通す。
    /// </para>
    /// </summary>
    public static void ShowAndWait(Window window, Window? owner)
    {
        var movingWindow = GetMovingWindow();
        if (movingWindow != IntPtr.Zero)
        {
            Log.Warning(
                "{Window} is shown and waited for while window 0x{MovingWindow:X} is being moved or resized; the move cannot end until it closes",
                window.GetType().Name,
                movingWindow.ToInt64());
        }

        var frame = new DispatcherFrame();
        Show(window, owner, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    /// <summary>
    /// 画面のスレッドが窓の移動・大きさの変更の中になければすぐ、中なら終わってから <paramref name="action"/> を走らせる。
    ///
    /// <para>
    /// 終わりは、動かしている窓の <c>WM_EXITSIZEMOVE</c> で知る。待っている処理は来た順に1つずつ走らせ、次は前の処理が
    /// 戻ってから(待つ窓なら閉じてから)。走らせる直前にもう一度確かめ、まだ動かしていれば次の終わりを待つ。
    /// 待っている処理がある間に来た処理は、動かしていなくても後ろに並べる(順番を守る)。
    /// 動かしている窓がこのアプリの窓でなく終わりを受けられないときは、すぐ走らせる(待つ窓なら <see cref="ShowAndWait"/> が警告を残す)。
    /// </para>
    ///
    /// <para>後から走らせたときは、呼び出し元はもう戻っている。結果をその場で使う呼び出しには使えない。</para>
    /// </summary>
    public static void RunWhenNotMovingOrSizing(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!_isRunningPendingActions && PendingActions.Count == 0)
        {
            var movingWindow = GetMovingWindow();
            if (movingWindow == IntPtr.Zero || !TryWatch(movingWindow))
            {
                action();
                return;
            }
        }

        PendingActions.Enqueue(action);
    }

    private static void RunPendingActions()
    {
        _isRunPosted = false;
        _isRunningPendingActions = true;
        try
        {
            while (PendingActions.Count > 0)
            {
                var movingWindow = GetMovingWindow();
                if (movingWindow != IntPtr.Zero && TryWatch(movingWindow))
                {
                    return;
                }

                Unwatch();
                PendingActions.Dequeue()();
            }
        }
        finally
        {
            _isRunningPendingActions = false;

            // 処理が例外で抜けたときも、残りの処理を止めない。
            if (PendingActions.Count > 0 && _watchedSource is null)
            {
                PostRunPendingActions();
            }
        }
    }

    private static void PostRunPendingActions()
    {
        if (_isRunPosted)
        {
            return;
        }

        _isRunPosted = true;
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(RunPendingActions));
    }

    /// <summary>
    /// 動かしている窓の終わりを待ち始める。待っている窓と同じなら何もしない。
    /// このアプリの窓でなければ(<see cref="HwndSource"/> が無い)待てないので false。
    /// </summary>
    private static bool TryWatch(IntPtr movingWindow)
    {
        if (_watchedSource is { } watched && watched.Handle == movingWindow)
        {
            return true;
        }

        Unwatch();
        if (HwndSource.FromHwnd(movingWindow) is not { } source)
        {
            return false;
        }

        source.AddHook(WatchedWindowProc);
        source.Disposed += WatchedSource_Disposed;
        _watchedSource = source;
        _isWatchedWindowMoving = true;
        return true;
    }

    private static void Unwatch()
    {
        if (_watchedSource is not { } source)
        {
            return;
        }

        _watchedSource = null;
        source.RemoveHook(WatchedWindowProc);
        source.Disposed -= WatchedSource_Disposed;
    }

    private static IntPtr WatchedWindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmEnterSizeMove)
        {
            _isWatchedWindowMoving = true;
        }
        else if (message == WmExitSizeMove)
        {
            _isWatchedWindowMoving = false;

            // ここはまだ移動のループの終わりの通知の中なので、走らせるのはループを抜けた後。
            PostRunPendingActions();
        }

        return IntPtr.Zero;
    }

    /// <summary>動かしている最中の窓が閉じた。移動もそこで終わる。閉じた窓からはフックを外さない(参照だけ捨てる)。</summary>
    private static void WatchedSource_Disposed(object? sender, EventArgs e)
    {
        if (sender is HwndSource source)
        {
            source.Disposed -= WatchedSource_Disposed;
        }

        _watchedSource = null;
        PostRunPendingActions();
    }

    /// <summary>画面のスレッドが窓の移動・大きさの変更の中なら、動かしている窓。中でなければ <see cref="IntPtr.Zero"/>。</summary>
    private static IntPtr GetMovingWindow()
    {
        var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
        if (!GetGUIThreadInfo(GetCurrentThreadId(), ref info))
        {
            Log.Warning(
                "Could not query whether a window is being moved (Win32 error {Error}); treating it as not moving",
                Marshal.GetLastWin32Error());
            return IntPtr.Zero;
        }

        if ((info.Flags & GuiInMoveSize) == 0 || info.MoveSize == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        if (_watchedSource is { } watched && watched.Handle == info.MoveSize && !_isWatchedWindowMoving)
        {
            return IntPtr.Zero;
        }

        return info.MoveSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size;
        public uint Flags;
        public IntPtr Active;
        public IntPtr Focus;
        public IntPtr Capture;
        public IntPtr MenuOwner;
        public IntPtr MoveSize;
        public IntPtr Caret;
        public int CaretLeft;
        public int CaretTop;
        public int CaretRight;
        public int CaretBottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool bEnable);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
