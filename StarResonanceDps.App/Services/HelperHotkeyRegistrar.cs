// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using Microsoft.Win32.SafeHandles;
using StarResonanceDps.App.Config;
using StarResonanceDps.HotkeyHost;

namespace StarResonanceDps.App.Services;

/// <summary>補助を起動できなかった、または補助が使えなくなった。</summary>
internal sealed class HotkeyHelperException(string message, Exception? innerException = null, bool wasDeclined = false)
    : Exception(message, innerException)
{
    /// <summary>管理者権限の確認(UAC)で断られた。</summary>
    public bool WasDeclined { get; } = wasDeclined;
}

/// <summary>
/// 補助(StarResonanceDps.HotkeyHost.exe)にキーを受けるのを任せる。補助は管理者権限で動くので、
/// ゲームが管理者権限で前面にあってもキーを受ける。受けるのはゲームかこのアプリが前面のときだけ(補助の <c>KeyboardHotkeyHook</c>)。
/// このアプリが名前付きパイプのサーバーになり、補助を起動してつながせる(取り決めは <see cref="HotkeyHostProtocol"/>)。
/// 番号・修飾(<c>MOD_*</c>)・仮想キーは <c>RegisterHotKey</c> と同じ値。
///
/// <para>
/// 呼ぶのは UI スレッドだけ。返事は同期で待つ。<see cref="Pressed"/> と <see cref="Lost"/> は UI スレッドで後から上がる。
/// 起動できない・返事が来ない・切れたときは <see cref="HotkeyHelperException"/> を投げる
/// (命令の合間に切れたときは <see cref="Lost"/>)。一度使えなくなったら戻らない。
/// </para>
/// </summary>
internal sealed class HelperHotkeyRegistrar : IDisposable
{
    private const int ErrorCancelled = 1223;
    private const int ExitWaitMilliseconds = 1000;
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(2);

    private readonly NamedPipeServerStream _pipe;
    private readonly Process _process;
    private readonly StreamWriter _writer;
    private readonly StreamReader _reader;
    private readonly BlockingCollection<int> _replies = new();
    private readonly Dispatcher _dispatcher;
    private volatile bool _isDisposed;
    private volatile bool _isLost;

    private HelperHotkeyRegistrar(NamedPipeServerStream pipe, Process process)
    {
        _pipe = pipe;
        _process = process;
        _dispatcher = Dispatcher.CurrentDispatcher;

        var encoding = new UTF8Encoding(false);
        _writer = new StreamWriter(pipe, encoding, leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n"
        };
        _reader = new StreamReader(pipe, encoding, false, 256, leaveOpen: true);

        new Thread(ReadMessages)
        {
            IsBackground = true,
            Name = "HotkeyHelper.Reader"
        }.Start();
    }

    /// <summary>キーが押された。番号は割り当てたときのもの。UI スレッドで上がる。</summary>
    public event Action<int>? Pressed;

    /// <summary>命令の合間に補助との接続が切れた。</summary>
    public event EventHandler? Lost;

    /// <summary>読み取りが止まった理由。補助が普通に閉じたときは null。</summary>
    public Exception? ReadFailure { get; private set; }

    /// <summary>
    /// 補助を起動してつながるまで待つ。管理者のアカウントでは UAC の確認が出て、答えるまで戻らない。
    /// </summary>
    public static HelperHotkeyRegistrar Start()
    {
        var pipeName = $"StarResonanceDps.Hotkeys.{Guid.NewGuid():N}";
        NamedPipeServerStream? pipe = null;
        Process? process = null;

        try
        {
            pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            process = Process.Start(new ProcessStartInfo(
                AppDataPaths.HotkeyHostPath,
                $"{pipeName} {Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}")
            {
                UseShellExecute = true
            }) ?? throw new HotkeyHelperException("The hotkey helper process was not created");

            using var timeout = new CancellationTokenSource(ConnectTimeout);
            var connecting = pipe.WaitForConnectionAsync(timeout.Token);
            var exiting = process.WaitForExitAsync(timeout.Token);
            Task.WaitAny(connecting, exiting);

            if (!connecting.IsCompletedSuccessfully)
            {
                throw process.HasExited
                    ? new HotkeyHelperException(
                        $"The hotkey helper exited with code {process.ExitCode.ToString(CultureInfo.InvariantCulture)} before connecting")
                    : new HotkeyHelperException("The hotkey helper did not connect in time");
            }

            // 同じ名前のパイプにほかのプロセスがつないでいないか。
            if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var clientProcessId)
                || clientProcessId != (uint)process.Id)
            {
                throw new HotkeyHelperException("An unexpected process connected to the hotkey helper pipe");
            }

            return new HelperHotkeyRegistrar(pipe, process);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == ErrorCancelled)
        {
            Release(pipe, process);
            throw new HotkeyHelperException("Elevation of the hotkey helper was declined", exception, wasDeclined: true);
        }
        catch (Exception exception) when (exception is Win32Exception
                                              or IOException
                                              or UnauthorizedAccessException
                                              or InvalidOperationException)
        {
            Release(pipe, process);
            throw new HotkeyHelperException("Could not start the hotkey helper", exception);
        }
        catch (HotkeyHelperException)
        {
            Release(pipe, process);
            throw;
        }
    }

    /// <summary>割り当てを足す。足せたら 0、足せなければ Windows のエラー番号(ほかのアプリが同じキーを登録しているときなど)。</summary>
    public int Register(int id, uint modifiers, uint virtualKey)
    {
        return Request(string.Join(
            ' ',
            HotkeyHostProtocol.Register,
            id.ToString(CultureInfo.InvariantCulture),
            modifiers.ToString(CultureInfo.InvariantCulture),
            virtualKey.ToString(CultureInfo.InvariantCulture)));
    }

    public void Unregister(int id)
    {
        Request($"{HotkeyHostProtocol.Unregister} {id.ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>ゲームとして扱うプロセス名(拡張子なし)。</summary>
    public void SetGameProcessNames(IReadOnlyList<string> names)
    {
        Request(HotkeyHostProtocol.ClearGameProcessNames);
        foreach (var name in names)
        {
            Request($"{HotkeyHostProtocol.AddGameProcessName} {name}");
        }
    }

    /// <summary>このプロセスの窓を補助に前面の窓のすぐ後ろへ置いてもらう。Windows のエラー番号(0 は成功)を返す。</summary>
    public int PlaceBehindForeground(IntPtr window)
    {
        return Request($"{HotkeyHostProtocol.PlaceBehindForeground} {window.ToInt64().ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>補助との接続を閉じ、補助が終わるのを少し待って終了コードを返す。終わらなければ null。</summary>
    public int? DisposeAndGetExitCode()
    {
        ClosePipe();
        int? exitCode = _process.WaitForExit(ExitWaitMilliseconds) ? _process.ExitCode : null;
        _process.Dispose();
        return exitCode;
    }

    /// <summary>補助との接続を閉じる。補助はそれを見て終わり、登録したキーも外れる。</summary>
    public void Dispose()
    {
        ClosePipe();
        _process.Dispose();
    }

    private int Request(string command)
    {
        if (_isLost)
        {
            throw new HotkeyHelperException("The hotkey helper is no longer available");
        }

        try
        {
            _writer.WriteLine(command);
        }
        catch (IOException exception)
        {
            _isLost = true;
            throw new HotkeyHelperException("Could not send a command to the hotkey helper", exception);
        }

        if (_replies.TryTake(out var error, ReplyTimeout))
        {
            return error;
        }

        _isLost = true;
        throw _replies.IsAddingCompleted
            ? new HotkeyHelperException("The hotkey helper closed the connection", ReadFailure)
            : new HotkeyHelperException("The hotkey helper did not reply in time");
    }

    private void ReadMessages()
    {
        try
        {
            string? line;
            while ((line = _reader.ReadLine()) is not null)
            {
                if (!Handle(line))
                {
                    ReadFailure = new InvalidDataException($"Unexpected message from the hotkey helper: {line}");
                    break;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            ReadFailure = exception;
        }

        _replies.CompleteAdding();
        if (!_isDisposed)
        {
            _dispatcher.InvokeAsync(() => Lost?.Invoke(this, EventArgs.Empty));
        }
    }

    private bool Handle(string line)
    {
        var parts = line.Split(' ');
        if (parts.Length != 2
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        switch (parts[0])
        {
            case HotkeyHostProtocol.Reply:
                _replies.Add(value);
                return true;
            case HotkeyHostProtocol.Pressed:
                _dispatcher.InvokeAsync(() =>
                {
                    if (!_isDisposed)
                    {
                        Pressed?.Invoke(value);
                    }
                });
                return true;
            default:
                return false;
        }
    }

    private void ClosePipe()
    {
        _isDisposed = true;
        _pipe.Dispose();
    }

    private static void Release(NamedPipeServerStream? pipe, Process? process)
    {
        pipe?.Dispose();
        process?.Dispose();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);
}
