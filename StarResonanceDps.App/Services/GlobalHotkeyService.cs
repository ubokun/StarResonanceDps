// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Input;
using System.Windows.Threading;
using Serilog;
using StarResonanceDps.App.Config;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

/// <summary>登録できなかったホットキー。<see cref="ErrorCode"/> は Windows のエラー番号。</summary>
public sealed record HotkeyRegistrationFailure(HotkeyAction Action, HotkeyBindingConfig Binding, int ErrorCode)
{
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    /// <summary>ほかのアプリ(または Windows)が同じキーを登録している。</summary>
    public bool IsInUse => ErrorCode == ErrorHotkeyAlreadyRegistered;
}

/// <summary>
/// アプリ全体のホットキー。割り当てを決め、キーを受けるのは補助(<see cref="HelperHotkeyRegistrar"/>)に任せる。
///
/// <para>
/// 割り当てたキーは、ゲームかこのアプリが前面のときだけ受け取り、ゲームには渡さない。
/// それ以外のアプリが前面なら何もせず、キーはそのアプリに届く(受け方は補助の <c>KeyboardHotkeyHook</c>)。
/// ゲームとして扱うのは、今の「ゲームの種類」の設定から決まるプロセス名(キャプチャと同じ)。
/// ほかのアプリが同じキーを登録していれば割り当てない(失敗として返す)。押しっぱなしの繰り返しは数えない。
/// </para>
///
/// <para>
/// アプリは通常権限で動き、管理者権限で動くのはキーを受ける補助だけ。
/// ゲームが管理者権限で動くと、通常権限ではゲームが前面の間キーを受けられないため。
/// 補助はアプリの起動時に1回だけ起動する(<see cref="Start"/>、起動設定でオフなら起動しない)。
/// <b>補助が使えない間(オフ・起動できない・途中で止まった)は、ホットキーを登録しない(無効)。</b>
/// 起動できない・途中で止まったときは <see cref="HelperUnavailable"/> で知らせる。その回の実行中は補助に戻らない。
/// </para>
///
/// <para>
/// 全体設定でキーを受け付けている間は、全部を一時的に外す(<see cref="Suspend"/> / <see cref="Resume"/>)。
/// 登録したままだと、押したキーがホットキーとして動いて欄に届かない。
/// </para>
///
/// <para>
/// 呼ぶのは UI スレッドだけ。押されたことの通知(<see cref="Pressed"/>)も UI スレッドで上がる。
/// </para>
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private static readonly Lazy<GlobalHotkeyService> LazyInstance = new(() => new GlobalHotkeyService());

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;

    /// <summary>試しの登録に使う番号。アプリが使える番号の上限(0xBFFF)。操作の番号(1〜)とぶつからない。</summary>
    private const int TestHotkeyId = 0xBFFF;

    private readonly HashSet<HotkeyAction> _registeredActions = [];
    private HotkeySettingsConfig _bindings = new();
    private IReadOnlyList<string> _gameProcessNames = [];

    // 全体設定でゲームの種類をプレビューしている間のプロセス名。null なら保存してある設定から取る。
    private IReadOnlyList<string>? _previewGameProcessNames;
    private HelperHotkeyRegistrar? _helper;
    private bool _hasStarted;
    private bool _isSuspended;

    private GlobalHotkeyService()
    {
    }

    public static GlobalHotkeyService Instance => LazyInstance.Value;

    /// <summary>登録したキーが押された。</summary>
    public event Action<HotkeyAction>? Pressed;

    /// <summary>登録の状態が変わった(登録・解除・一時停止・再開・補助が使えなくなった)。</summary>
    public event EventHandler? RegistrationChanged;

    /// <summary>
    /// 補助を起動できなかったか途中で止まり、ホットキーが無効になった。UI スレッドで後から上がる。
    /// 起動設定でオフにして起動しなかったときは上がらない。
    /// </summary>
    public event EventHandler? HelperUnavailable;

    /// <summary>補助が動いていて、ホットキーを登録できるか。false の間はホットキーは無効(全体設定のホットキーの節も隠す)。</summary>
    public bool IsAvailable => _helper is not null;

    public bool IsRegistered(HotkeyAction action)
    {
        return _registeredActions.Contains(action);
    }

    public HotkeyBindingConfig GetBinding(HotkeyAction action)
    {
        return _bindings.Get(action);
    }

    /// <summary>
    /// アプリの起動時に1回だけ呼ぶ。<paramref name="startHelper"/> なら補助を起動する(管理者のアカウントでは UAC の確認が出て、答えるまで戻らない)。
    /// 起動できなければ <see cref="HelperUnavailable"/> で知らせる。割り当てはこの後の <see cref="Apply"/> で登録する。
    /// </summary>
    public void Start(bool startHelper)
    {
        if (_hasStarted)
        {
            return;
        }

        _hasStarted = true;
        if (!startHelper)
        {
            Log.Information("The hotkey helper is turned off in the startup settings; hotkeys are disabled");
            return;
        }

        HelperHotkeyRegistrar helper;
        try
        {
            helper = HelperHotkeyRegistrar.Start();
        }
        catch (HotkeyHelperException exception)
        {
            if (exception.WasDeclined)
            {
                Log.Warning(exception, "The hotkey helper was not started (elevation was declined); hotkeys are disabled");
            }
            else
            {
                Log.Error(exception, "Could not start the hotkey helper; hotkeys are disabled");
            }

            NotifyHelperUnavailable();
            return;
        }

        helper.Pressed += Helper_Pressed;
        helper.Lost += Helper_Lost;
        _helper = helper;
    }

    /// <summary>
    /// 割り当てを入れ替えて登録し直す。一時停止中なら控えるだけで、再開したときに登録する。補助が使えない間は控えるだけ。
    /// ゲームのプロセス名もここで今の設定から取り直す(全体設定の保存では、ゲームの種類がこれより先に反映される。
    /// 全体設定でゲームの種類をプレビューしている間はプレビューの値)。
    /// </summary>
    public IReadOnlyList<HotkeyRegistrationFailure> Apply(HotkeySettingsConfig bindings)
    {
        _bindings = bindings.Clone();
        _gameProcessNames = ResolveGameProcessNames();
        var failures = RegisterAgain();
        RegistrationChanged?.Invoke(this, EventArgs.Empty);
        return failures;
    }

    /// <summary>
    /// 全体設定でゲームの種類をプレビューしている間、ゲームが前面かの判定に使うプロセス名を差し替える(キーの登録はそのまま)。
    /// null で保存してある設定へ戻す。
    /// </summary>
    public void PreviewGameProcessNames(IReadOnlyList<string>? names)
    {
        _previewGameProcessNames = names;
        _gameProcessNames = ResolveGameProcessNames();
        if (_helper is null)
        {
            return;
        }

        try
        {
            _helper.SetGameProcessNames(_gameProcessNames);
        }
        catch (HotkeyHelperException exception)
        {
            DisableHelper(exception);
        }
    }

    private IReadOnlyList<string> ResolveGameProcessNames()
    {
        return _previewGameProcessNames ?? Utils.GameCapturePreferenceToExeNames(CombatRuntimeSettings.GameCapturePreference);
    }

    public void Suspend()
    {
        if (_isSuspended)
        {
            return;
        }

        _isSuspended = true;
        try
        {
            UnregisterAll();
        }
        catch (HotkeyHelperException exception)
        {
            DisableHelper(exception);
            return;
        }

        RegistrationChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<HotkeyRegistrationFailure> Resume()
    {
        if (!_isSuspended)
        {
            return [];
        }

        _isSuspended = false;
        var failures = RegisterAgain();
        RegistrationChanged?.Invoke(this, EventArgs.Empty);
        return failures;
    }

    /// <summary>
    /// そのキーを割り当てられるか(ほかのアプリが同じキーを登録していないか)試す。できたらすぐ外す。
    /// できなければ Windows のエラー番号を返す。補助が使えない間は試さずに null(登録もしないので、割り当ては控えるだけになる)。
    /// </summary>
    public int? TestRegistration(HotkeyBindingConfig binding)
    {
        if (_helper is null)
        {
            return null;
        }

        try
        {
            var error = _helper.Register(TestHotkeyId, ToNativeModifiers(binding.Modifiers), ToVirtualKey(binding.Key));
            if (error != 0)
            {
                Log.Warning("Hotkey {Hotkey} cannot be registered: Win32 error {Error}", HotkeyText.Format(binding), error);
                return error;
            }

            _helper.Unregister(TestHotkeyId);
            return null;
        }
        catch (HotkeyHelperException exception)
        {
            DisableHelper(exception);
            return null;
        }
    }

    /// <summary>
    /// このプロセスの窓を補助(管理者権限)に前面の窓のすぐ後ろへ置いてもらう。Windows のエラー番号(0 は成功)を返す。補助を使っていなければ null。
    /// 前面がゲーム(管理者権限)のとき、本体(通常権限)はその窓を基準に置けない。
    /// </summary>
    public int? TryPlaceBehindForegroundThroughHelper(IntPtr window)
    {
        if (_helper is null)
        {
            return null;
        }

        try
        {
            return _helper.PlaceBehindForeground(window);
        }
        catch (HotkeyHelperException exception)
        {
            DisableHelper(exception);
            return null;
        }
    }

    /// <summary>登録を全部外す。補助は接続を閉じると終わり、補助が登録したキーもそれで外れる。</summary>
    public void Dispose()
    {
        _registeredActions.Clear();
        if (_helper is null)
        {
            return;
        }

        _helper.Pressed -= Helper_Pressed;
        _helper.Lost -= Helper_Lost;
        _helper.Dispose();
        _helper = null;
    }

    // 全部外して、止めていなければ全部登録し直す。補助が途中で使えなくなったら、ホットキーを無効にする。
    private IReadOnlyList<HotkeyRegistrationFailure> RegisterAgain()
    {
        if (_helper is null)
        {
            _registeredActions.Clear();
            return [];
        }

        try
        {
            UnregisterAll();
            _helper.SetGameProcessNames(_gameProcessNames);
            if (_isSuspended)
            {
                return [];
            }

            return RegisterAll(_helper);
        }
        catch (HotkeyHelperException exception)
        {
            DisableHelper(exception);
            return [];
        }
    }

    private List<HotkeyRegistrationFailure> RegisterAll(HelperHotkeyRegistrar helper)
    {
        var failures = new List<HotkeyRegistrationFailure>();

        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            var binding = _bindings.Get(action);
            if (!binding.IsAssigned)
            {
                continue;
            }

            var error = helper.Register(
                ToHotkeyId(action),
                ToNativeModifiers(binding.Modifiers),
                ToVirtualKey(binding.Key));
            if (error == 0)
            {
                _registeredActions.Add(action);
                continue;
            }

            Log.Warning(
                "Failed to register hotkey {Action} ({Hotkey}): Win32 error {Error}",
                action,
                HotkeyText.Format(binding),
                error);
            failures.Add(new HotkeyRegistrationFailure(action, binding.Clone(), error));
        }

        return failures;
    }

    private void UnregisterAll()
    {
        if (_helper is not null)
        {
            foreach (var action in _registeredActions)
            {
                _helper.Unregister(ToHotkeyId(action));
            }
        }

        _registeredActions.Clear();
    }

    // 補助が使えなくなった。補助を閉じてホットキーを全部無効にし、知らせる。
    private void DisableHelper(HotkeyHelperException exception)
    {
        int? exitCode = null;
        if (_helper is { } helper)
        {
            helper.Pressed -= Helper_Pressed;
            helper.Lost -= Helper_Lost;
            exitCode = helper.DisposeAndGetExitCode();
            _helper = null;
        }

        Log.Error(exception, "Lost the hotkey helper (exit code {ExitCode}); hotkeys are disabled", exitCode);
        _registeredActions.Clear();
        RegistrationChanged?.Invoke(this, EventArgs.Empty);
        NotifyHelperUnavailable();
    }

    // 命令の合間に補助が切れた(補助が落ちたなど)。
    private void Helper_Lost(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _helper) || sender is not HelperHotkeyRegistrar helper)
        {
            return;
        }

        DisableHelper(new HotkeyHelperException("The hotkey helper closed the connection", helper.ReadFailure));
    }

    private void Helper_Pressed(int id)
    {
        foreach (var action in _registeredActions)
        {
            if (ToHotkeyId(action) != id)
            {
                continue;
            }

            Pressed?.Invoke(action);
            break;
        }
    }

    // 登録の途中で知らせの窓を出さないよう、後から上げる。
    private void NotifyHelperUnavailable()
    {
        Dispatcher.CurrentDispatcher.InvokeAsync(() => HelperUnavailable?.Invoke(this, EventArgs.Empty));
    }

    private static int ToHotkeyId(HotkeyAction action)
    {
        return (int)action + 1;
    }

    private static uint ToVirtualKey(Key key)
    {
        return (uint)KeyInterop.VirtualKeyFromKey(key);
    }

    private static uint ToNativeModifiers(ModifierKeys modifiers)
    {
        var native = ModNoRepeat;

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            native |= ModAlt;
        }

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            native |= ModControl;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            native |= ModShift;
        }

        return native;
    }
}
