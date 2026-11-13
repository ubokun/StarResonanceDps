// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using PacketDotNet;
using PacketDotNet.Tcp;
using Serilog;
using Serilog.Events;

namespace StarResonanceDps.Core.CombatRuntime.Protocols;

internal enum TcpCloseReason
{
    BothFin,
    Reset,
    ReplacedBySyn,
    CaptureStopped
}

/// <summary>
/// 1つの TCP 接続(2つの向き)。閉じた後も中身を捨てて残り、閉じた後に届いたパケットを数える。
/// </summary>
internal sealed class TcpConnection
{
    /// <summary>Window Scale の上限(RFC 7323)。これより大きい値は 14 として扱う。</summary>
    private const int MaxWindowScale = 14;

    private static readonly ILogger Log = Serilog.Log.ForContext<TcpConnection>();

    private readonly TcpStreamDirection _fromLow;
    private readonly TcpStreamDirection _fromHigh;
    private bool _hasWarnedLatePayload;

    public TcpConnection(
        TcpConnectionKey key,
        DateTime openedAtUtc,
        Action<byte[], int, DateTime> onMessage,
        Func<string> describeCaptureStatistics)
    {
        Key = key;
        OpenedAtUtc = openedAtUtc;
        _fromLow = new TcpStreamDirection(key.Low, key.High, onMessage, describeCaptureStatistics);
        _fromHigh = new TcpStreamDirection(key.High, key.Low, onMessage, describeCaptureStatistics);
    }

    public TcpConnectionKey Key { get; }

    public DateTime OpenedAtUtc { get; }

    public bool IsClosed { get; private set; }

    /// <summary>閉じた後に届いた中身の無いパケット(最後の ACK など)。</summary>
    public long LatePureAfterClose { get; private set; }

    /// <summary>閉じた後に届いた中身のあるパケット。</summary>
    public long LatePayloadAfterClose { get; private set; }

    /// <summary>その向きの始まりが、届いた SYN の ISN と違うか(同じ4つ組の別の接続)。</summary>
    public bool IsDifferentStreamStart(bool fromLow, uint isn)
    {
        return Direction(fromLow).StartedDifferentlyFrom(isn);
    }

    public bool IsBeyondFin(bool fromLow, uint seq)
    {
        return Direction(fromLow).IsBeyondFin(seq);
    }

    public void NoteLatePureAfterClose()
    {
        LatePureAfterClose++;
    }

    public void NoteLatePayloadAfterClose(bool fromLow, int bytes)
    {
        LatePayloadAfterClose++;
        if (_hasWarnedLatePayload)
        {
            return;
        }

        _hasWarnedLatePayload = true;
        Log.Warning(
            "TCP {Stream} received {Bytes} bytes after the connection {Connection} closed; ignoring data for this closed connection",
            Direction(fromLow).Name,
            bytes,
            Key);
    }

    public void Process(bool fromLow, TcpPacket tcp, ReadOnlySpan<byte> payload, DateTime arrivalUtc)
    {
        var direction = Direction(fromLow);
        var peer = Direction(!fromLow);

        if (tcp.Synchronize)
        {
            direction.OnSyn(tcp.SequenceNumber, GetWindowScale(tcp));
            if (tcp.Acknowledgment)
            {
                peer.StartFromPeerSynAck(tcp.AcknowledgmentNumber);
            }

            UpdateReceiverWindows();
        }

        if (tcp.Acknowledgment)
        {
            peer.OnPeerAck(tcp.AcknowledgmentNumber, arrivalUtc);
        }

        if (tcp.Reset)
        {
            Close(TcpCloseReason.Reset, arrivalUtc);
            return;
        }

        // SYN は seq を1つ使うので、SYN と一緒の中身は ISN+1 から。
        var dataSeq = tcp.Synchronize ? unchecked(tcp.SequenceNumber + 1) : tcp.SequenceNumber;
        if (!payload.IsEmpty)
        {
            direction.OnSegment(dataSeq, payload, arrivalUtc);
        }

        if (tcp.Finished)
        {
            direction.OnFin(dataSeq, payload.Length, arrivalUtc);
        }

        if (_fromLow.IsFinished && _fromHigh.IsFinished)
        {
            Close(TcpCloseReason.BothFin, arrivalUtc);
        }
    }

    public void Close(TcpCloseReason reason, DateTime closedAtUtc)
    {
        if (IsClosed)
        {
            return;
        }

        IsClosed = true;
        _fromLow.Finish();
        _fromHigh.Finish();

        var hasLoss = _fromLow.HasLoss || _fromHigh.HasLoss;
        var hasUnfinished = _fromLow.UnfinishedAtCloseBytes > 0 || _fromHigh.UnfinishedAtCloseBytes > 0;
        var level = hasLoss || (hasUnfinished && reason != TcpCloseReason.CaptureStopped)
            ? LogEventLevel.Warning
            : LogEventLevel.Information;
        Log.Write(
            level,
            "TCP connection closed {Connection} ({Reason}, {LifetimeMs:F0} ms): {LowToHigh}; {HighToLow}",
            Key,
            reason,
            (closedAtUtc - OpenedAtUtc).TotalMilliseconds,
            _fromLow.DescribeSummary(),
            _fromHigh.DescribeSummary());
    }

    private TcpStreamDirection Direction(bool fromLow)
    {
        return fromLow ? _fromLow : _fromHigh;
    }

    /// <summary>
    /// 受け手が窓で表せる最大を決める。両方の SYN を見たときだけ分かる(Window Scale は両方が SYN で知らせたときだけ効く)。
    /// </summary>
    private void UpdateReceiverWindows()
    {
        if (!_fromLow.SawSyn || !_fromHigh.SawSyn)
        {
            _fromLow.ReceiverMaxWindow = TcpStreamDirection.MaxWindow;
            _fromHigh.ReceiverMaxWindow = TcpStreamDirection.MaxWindow;
            return;
        }

        var isScaling = _fromLow.AnnouncedWindowScale.HasValue && _fromHigh.AnnouncedWindowScale.HasValue;
        var lowScale = isScaling ? Math.Min((int)_fromLow.AnnouncedWindowScale!.Value, MaxWindowScale) : 0;
        var highScale = isScaling ? Math.Min((int)_fromHigh.AnnouncedWindowScale!.Value, MaxWindowScale) : 0;

        // Low から出るデータの受け手は High。
        _fromLow.ReceiverMaxWindow = (long)ushort.MaxValue << highScale;
        _fromHigh.ReceiverMaxWindow = (long)ushort.MaxValue << lowScale;
    }

    private static byte? GetWindowScale(TcpPacket tcp)
    {
        if (tcp.OptionsCollection is not { } options)
        {
            return null;
        }

        foreach (var option in options)
        {
            if (option is WindowScaleFactorOption windowScale)
            {
                return windowScale.ScaleFactor;
            }
        }

        return null;
    }
}
