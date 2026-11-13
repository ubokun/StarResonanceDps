// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StarResonanceDps.HotkeyHost;

/// <summary>
/// 低レベルのキーボードフック(<c>WH_KEYBOARD_LL</c>)でホットキーを受ける。
///
/// <para>
/// 割り当てたキーが押されたら前面の窓のプロセスを見て、ゲームかこのアプリ(<c>ownerProcessId</c>)のときだけ番号を知らせ、
/// キーを前面のアプリに渡さない。それ以外のアプリが前面なら何もせず、キーはそのアプリに届く。
/// 押しっぱなしの繰り返しは知らせない(渡さないのは同じ)。修飾キーは Ctrl・Alt・Shift の組が一致したときだけで、Win キーを押していれば一致しない。
/// </para>
///
/// <para>
/// 作ったスレッドでだけ呼ぶこと。フックはそのスレッドのメッセージの回り方で呼ばれる。
/// 押されたことの知らせ(<c>pressed</c>)はフックの中から呼ぶので、待つ処理をしない(遅いとフックごと外される)。
/// </para>
/// </summary>
internal sealed class KeyboardHotkeyHook : IDisposable
{
    private const int WhKeyboardLowLevel = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLeftWindows = 0x5B;
    private const int VkRightWindows = 0x5C;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModifierMask = ModAlt | ModControl | ModShift;

    private readonly int _ownerProcessId;
    private readonly Action<int> _pressed;

    // フックに渡したデリゲートが回収されないよう持っておく。
    private readonly LowLevelKeyboardProc _proc;

    private readonly Dictionary<int, (uint Modifiers, uint VirtualKey)> _bindings = [];
    private readonly HashSet<uint> _heldKeys = [];
    private string[] _gameProcessNames = [];
    private int _lastForegroundProcessId;
    private bool _isLastForegroundAllowed;
    private IntPtr _hook;

    public KeyboardHotkeyHook(int ownerProcessId, Action<int> pressed)
    {
        _ownerProcessId = ownerProcessId;
        _pressed = pressed;
        _proc = HookProc;
    }

    /// <summary>
    /// 割り当てを足す。足せたら 0、足せなければ Windows のエラー番号を返す。
    /// ほかのアプリが同じキーを登録していたら足さない(<c>RegisterHotKey</c> で一度登録してすぐ外して確かめる)。
    /// フックは最初に足すときに掛ける。
    /// </summary>
    public int Register(int id, uint modifiers, uint virtualKey)
    {
        if (_hook == IntPtr.Zero)
        {
            _hook = SetWindowsHookEx(WhKeyboardLowLevel, _proc, GetModuleHandle(null), 0);
            if (_hook == IntPtr.Zero)
            {
                return Marshal.GetLastWin32Error();
            }
        }

        if (!RegisterHotKey(IntPtr.Zero, id, modifiers, virtualKey))
        {
            return Marshal.GetLastWin32Error();
        }

        UnregisterHotKey(IntPtr.Zero, id);
        _bindings[id] = (modifiers & ModifierMask, virtualKey);
        return 0;
    }

    public void Unregister(int id)
    {
        _bindings.Remove(id);
    }

    /// <summary>ゲームとして扱うプロセス名(拡張子なし)。比べ方はキャプチャと同じ。</summary>
    public void SetGameProcessNames(IEnumerable<string> names)
    {
        _gameProcessNames = names.ToArray();
        _lastForegroundProcessId = 0;
    }

    public void Dispose()
    {
        _bindings.Clear();
        if (_hook == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr HookProc(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0)
        {
            var messageId = message.ToInt32();
            // KBDLLHOOKSTRUCT の先頭が仮想キー。
            var virtualKey = (uint)Marshal.ReadInt32(data);

            if (messageId is WmKeyDown or WmSysKeyDown)
            {
                var isRepeat = !_heldKeys.Add(virtualKey);
                if (TryFindBinding(virtualKey, out var id) && IsForegroundAllowed())
                {
                    if (!isRepeat)
                    {
                        _pressed(id);
                    }

                    return new IntPtr(1);
                }
            }
            else if (messageId is WmKeyUp or WmSysKeyUp)
            {
                _heldKeys.Remove(virtualKey);
            }
        }

        return CallNextHookEx(_hook, code, message, data);
    }

    private bool TryFindBinding(uint virtualKey, out int id)
    {
        uint? modifiers = null;
        foreach (var (bindingId, binding) in _bindings)
        {
            if (binding.VirtualKey != virtualKey)
            {
                continue;
            }

            modifiers ??= CurrentModifiers();
            if (binding.Modifiers == modifiers)
            {
                id = bindingId;
                return true;
            }
        }

        id = 0;
        return false;
    }

    /// <summary>今押している修飾キー。Win キーを押していれば、どの割り当てにも一致しない値を返す。</summary>
    private static uint CurrentModifiers()
    {
        if (IsKeyDown(VkLeftWindows) || IsKeyDown(VkRightWindows))
        {
            return uint.MaxValue;
        }

        var modifiers = 0u;
        if (IsKeyDown(VkMenu))
        {
            modifiers |= ModAlt;
        }

        if (IsKeyDown(VkControl))
        {
            modifiers |= ModControl;
        }

        if (IsKeyDown(VkShift))
        {
            modifiers |= ModShift;
        }

        return modifiers;
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    /// <summary>前面の窓がゲームかこのアプリのものか。プロセス名は前面のプロセスが変わったときだけ引く。</summary>
    private bool IsForegroundAllowed()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(window, out var processId);
        if (processId == 0)
        {
            return false;
        }

        if ((int)processId == _lastForegroundProcessId)
        {
            return _isLastForegroundAllowed;
        }

        _lastForegroundProcessId = (int)processId;
        _isLastForegroundAllowed = (int)processId == _ownerProcessId || IsGameProcess((int)processId);
        return _isLastForegroundAllowed;
    }

    private bool IsGameProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return _gameProcessNames.Contains(process.ProcessName);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            // 名前を引く前に前面のプロセスが終わった。ゲームとしては扱わない。
            return false;
        }
    }

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr message, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, LowLevelKeyboardProc proc, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
