// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using PacketDotNet;
using Serilog;

namespace StarResonanceDps.Core.CombatRuntime.Protocols;

/// <summary>
/// キャプチャのパケットを TCP 接続へ振り分け、揃ったメッセージを受け手へ渡す。
///
/// <para>
/// <b>時間で決める値は持たない。</b>接続を閉じるのは、両方向の FIN・RST・同じ4つ組の別の SYN・キャプチャの停止だけ。
/// 閉じた接続は中身を捨てて表に残し(同じ4つ組の SYN か停止まで)、後から来るパケットで接続を作らない。
/// 時計はキャプチャの時刻(UTC)だけを使う。
/// </para>
///
/// <para>
/// キャプチャのスレッドと停止の呼び手(UI スレッド)の両方が触るので、表は <see cref="_sync"/> で守る。
/// 受け手(<see cref="_onMessage"/>)はこの lock の中で呼ばれる。
/// </para>
/// </summary>
public sealed class TcpReassembler
{
    private static readonly ILogger Log = Serilog.Log.ForContext<TcpReassembler>();

    private readonly object _sync = new();
    private readonly Dictionary<TcpConnectionKey, TcpConnection> _connections = new();
    private readonly Action<byte[], int, DateTime> _onMessage;
    private readonly Func<string> _describeCaptureStatistics;
    private readonly Action _onMidStreamConnectionOpened;
    private bool _isStopped;
    private bool _hasLoggedPacketAfterStop;
    private long _ignoredWithoutConnection;
    private long _replacedLatePureAfterClose;
    private long _replacedLatePayloadAfterClose;

    /// <param name="onMessage">揃ったメッセージ(<see cref="System.Buffers.ArrayPool{T}.Shared"/> から借りたバッファ、長さ、揃ったパケットのキャプチャ時刻)。バッファを返すのは受け手。</param>
    /// <param name="describeCaptureStatistics">ログに添えるキャプチャの統計(英語)。</param>
    /// <param name="onMidStreamConnectionOpened">接続を始まり(SYN)を見ずに途中から開いたとき。lock の中で呼ばれる。</param>
    public TcpReassembler(
        Action<byte[], int, DateTime> onMessage,
        Func<string> describeCaptureStatistics,
        Action onMidStreamConnectionOpened)
    {
        _onMessage = onMessage;
        _describeCaptureStatistics = describeCaptureStatistics;
        _onMidStreamConnectionOpened = onMidStreamConnectionOpened;
    }

    public void AddPacket(IPv4Packet ip, TcpPacket tcp, DateTime captureTimeUtc)
    {
        var source = TcpEndpoint.From(ip.SourceAddress, tcp.SourcePort);
        var destination = TcpEndpoint.From(ip.DestinationAddress, tcp.DestinationPort);
        var key = TcpConnectionKey.From(source, destination);
        var fromLow = source == key.Low;
        ReadOnlySpan<byte> payload = tcp.PayloadData ?? [];

        lock (_sync)
        {
            if (_isStopped)
            {
                if (!_hasLoggedPacketAfterStop)
                {
                    _hasLoggedPacketAfterStop = true;
                    Log.Information("Ignoring TCP packets that arrive after the capture stopped");
                }

                return;
            }

            _connections.TryGetValue(key, out var connection);

            if (tcp.Synchronize && !tcp.Reset)
            {
                if (connection is { IsClosed: false } && connection.IsDifferentStreamStart(fromLow, tcp.SequenceNumber))
                {
                    connection.Close(TcpCloseReason.ReplacedBySyn, captureTimeUtc);
                }

                if (connection is null || connection.IsClosed)
                {
                    connection = Open(key, tcp.Acknowledgment ? "SYN-ACK" : "SYN", captureTimeUtc);
                }
            }
            else if (connection is null)
            {
                // 中身の無いパケット(閉じた後の最後の ACK など)と、FIN・RST の付いた途中のパケットからは接続を作らない。
                if (payload.IsEmpty || tcp.Reset || tcp.Finished)
                {
                    _ignoredWithoutConnection++;
                    return;
                }

                connection = Open(key, "mid-stream data", captureTimeUtc);
                _onMidStreamConnectionOpened();
            }
            else if (connection.IsClosed)
            {
                if (payload.IsEmpty)
                {
                    connection.NoteLatePureAfterClose();
                    return;
                }

                // SYN を取りこぼしたまま同じ4つ組が使い回されたときは、FIN より先のデータが来る。
                if (!connection.IsBeyondFin(fromLow, tcp.SequenceNumber))
                {
                    connection.NoteLatePayloadAfterClose(fromLow, payload.Length);
                    return;
                }

                Log.Warning(
                    "TCP {Connection} received data beyond the FIN of a closed connection; treating it as a new connection found mid-stream",
                    key);
                connection = Open(key, "mid-stream data after close", captureTimeUtc);
                _onMidStreamConnectionOpened();
            }

            connection.Process(fromLow, tcp, payload, captureTimeUtc);
        }
    }

    /// <summary>
    /// 停止する。開いている接続を全部閉じて要約を書き、以後のパケットは無視する。
    /// 戻った後に受け手が呼ばれることは無い。
    /// </summary>
    public TcpReassemblerStopSummary Stop(DateTime stoppedAtUtc)
    {
        lock (_sync)
        {
            _isStopped = true;
            var closed = 0;
            var latePure = _replacedLatePureAfterClose;
            var latePayload = _replacedLatePayloadAfterClose;
            foreach (var connection in _connections.Values)
            {
                if (!connection.IsClosed)
                {
                    connection.Close(TcpCloseReason.CaptureStopped, stoppedAtUtc);
                    closed++;
                }

                latePure += connection.LatePureAfterClose;
                latePayload += connection.LatePayloadAfterClose;
            }

            return new TcpReassemblerStopSummary(closed, latePure, latePayload, _ignoredWithoutConnection);
        }
    }

    private TcpConnection Open(TcpConnectionKey key, string openedBy, DateTime captureTimeUtc)
    {
        if (_connections.TryGetValue(key, out var replaced))
        {
            _replacedLatePureAfterClose += replaced.LatePureAfterClose;
            _replacedLatePayloadAfterClose += replaced.LatePayloadAfterClose;
        }

        var connection = new TcpConnection(key, captureTimeUtc, _onMessage, _describeCaptureStatistics);
        _connections[key] = connection;
        Log.Information("TCP connection opened {Connection} by {OpenedBy}", key, openedBy);
        return connection;
    }
}

/// <param name="ClosedConnections">停止で閉じた接続の数。</param>
/// <param name="LatePureAfterClose">閉じた接続へ届いた中身の無いパケットの数。</param>
/// <param name="LatePayloadAfterClose">閉じた接続へ届いた中身のあるパケットの数。</param>
/// <param name="IgnoredWithoutConnection">接続が無く、接続を作らなかったパケットの数。</param>
public sealed record TcpReassemblerStopSummary(
    int ClosedConnections,
    long LatePureAfterClose,
    long LatePayloadAfterClose,
    long IgnoredWithoutConnection);
