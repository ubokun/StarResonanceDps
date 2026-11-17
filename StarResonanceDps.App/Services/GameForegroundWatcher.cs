// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;
using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 全体設定「ゲーム非アクティブ時にウィジェットを隠す」。前面の窓がゲームでもこのアプリでもない間、ウィジェットの窓を隠す合図を出す
/// (<see cref="HidesWidgets"/>。隠し方は <c>WidgetWindow</c> が持つ)。
///
/// <para>
/// 前面が替わったことは Windows の知らせ(<c>EVENT_SYSTEM_FOREGROUND</c>、プロセスの外で受ける形)で受け、見回りはしない。
/// 管理者権限で動くゲームが前面になっても、通常権限のこのアプリに知らせが届き、プロセス名も引ける。
/// 判定は知らせが運ぶ窓で行い、その時点の前面を調べ直さない(切り替えの途中で前面が一瞬「無し」になる)。
/// 「このアプリ」は自分のプロセスの窓(メイン画面・設定・ウィジェット・プラグインの窓・メッセージの窓)。
/// ゲームの見分けは、ホットキーの補助と同じ全体設定「ゲーム実行ファイル」の一覧(<see cref="GlobalHotkeyService.GameProcessNames"/>)。
/// </para>
///
/// <para>
/// 知らせを受けるのは設定がオンの間だけ。登録できなかったらエラーをログに書き、ウィジェットは隠さない。呼ぶのは UI スレッドだけ
/// (知らせもプロセスの外の形なので、登録した UI スレッドで届く)。
/// </para>
/// </summary>
public sealed class GameForegroundWatcher
{
    private const uint EventSystemForeground = 0x0003;
    private const uint WinEventOutOfContext = 0x0000;

    private static readonly Lazy<GameForegroundWatcher> LazyInstance = new(() => new GameForegroundWatcher());

    private readonly uint _ownProcessId = (uint)Environment.ProcessId;

    /// <summary>登録した知らせの受け手。登録している間は手放さない(GC に集められると知らせで落ちる)。</summary>
    private WinEventProc? _callback;
    private IntPtr _hook;
    private bool _hasStarted;
    private bool _isGameOrAppForeground = true;

    // 同じプロセスが続く間は名前を引き直さない(補助の IsForegroundAllowed と同じ形)。ゲームの一覧が替わったら引き直す。
    private uint _lastProcessId;
    private bool _isLastProcessAllowed;

    private GameForegroundWatcher()
    {
    }

    public static GameForegroundWatcher Instance => LazyInstance.Value;

    /// <summary>ウィジェットを隠すか。設定がオンで、前面がゲームでもこのアプリでもない間だけ true。</summary>
    public bool HidesWidgets { get; private set; }

    /// <summary><see cref="HidesWidgets"/> が変わった。UI スレッドで上がる。</summary>
    public event EventHandler? HidesWidgetsChanged;

    /// <summary>アプリの起動時に1回、ウィジェットを開く前に呼ぶ。設定(プレビューを含む)とゲームの一覧の変化も受け始める。</summary>
    public void Start()
    {
        if (_hasStarted)
        {
            return;
        }

        _hasStarted = true;
        ConfigManager.Instance.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        GlobalHotkeyService.Instance.GameProcessNamesChanged += GlobalHotkeyService_GameProcessNamesChanged;
        ApplySetting();
    }

    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        ApplySetting();
    }

    /// <summary>設定がオンなら知らせを受け始め(今の前面で一度判定する)、オフなら受けるのをやめる。</summary>
    private void ApplySetting()
    {
        var isEnabled = ConfigManager.Instance.CurrentSettings.HidesWidgetsWhenGameInactive;
        if (isEnabled && _hook == IntPtr.Zero)
        {
            _callback = OnWinEvent;
            _hook = SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero, _callback, 0, 0, WinEventOutOfContext);
            if (_hook == IntPtr.Zero)
            {
                Log.Error("Could not watch the foreground window (Win32 error {Error}); widgets stay visible", Marshal.GetLastWin32Error());
                _callback = null;
            }
            else
            {
                _lastProcessId = 0;
                Evaluate(GetForegroundWindow());
            }
        }
        else if (!isEnabled && _hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
            _callback = null;
            _isGameOrAppForeground = true;
        }

        UpdateHidesWidgets();
    }

    private void GlobalHotkeyService_GameProcessNamesChanged(object? sender, EventArgs e)
    {
        if (_hook == IntPtr.Zero)
        {
            return;
        }

        _lastProcessId = 0;
        Evaluate(GetForegroundWindow());
        UpdateHidesWidgets();
    }

    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint threadId, uint eventTime)
    {
        Evaluate(window);
        UpdateHidesWidgets();
    }

    /// <summary>前面になった窓がゲームかこのアプリのものか。窓かプロセスが分からないとき(切り替えの途中)は前の判定のまま。</summary>
    private void Evaluate(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return;
        }

        GetWindowThreadProcessId(window, out var processId);
        if (processId == 0)
        {
            return;
        }

        if (processId != _lastProcessId)
        {
            _lastProcessId = processId;
            _isLastProcessAllowed = processId == _ownProcessId || IsGameProcess(processId);
        }

        _isGameOrAppForeground = _isLastProcessAllowed;
    }

    private void UpdateHidesWidgets()
    {
        var hidesWidgets = _hook != IntPtr.Zero && !_isGameOrAppForeground;
        if (hidesWidgets == HidesWidgets)
        {
            return;
        }

        HidesWidgets = hidesWidgets;
        HidesWidgetsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsGameProcess(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return GlobalHotkeyService.Instance.GameProcessNames.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            // 名前を引く前に前面のプロセスが終わった。ゲームとしては扱わない。
            return false;
        }
    }

    private delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint threadId, uint eventTime);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc callback, uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
