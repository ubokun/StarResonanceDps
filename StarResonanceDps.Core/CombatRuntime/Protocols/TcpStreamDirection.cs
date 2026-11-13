// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Text;
using Serilog;

namespace StarResonanceDps.Core.CombatRuntime.Protocols;

/// <summary>
/// TCP 接続の1つの向きの組み立て。seq はストリーム上の64ビットの位置(<see cref="NextPos"/> を基準にした符号つきの差)に直して比べる。
///
/// <para>
/// 届いたセグメントは「済んだ範囲(捨てて数える)・next をまたぐ(済んだ分を切る)・next ちょうど・先(待ち)」に分ける。
/// next が進むたびに待ちの前の方を見直す。
/// </para>
///
/// <para>
/// <b>欠けは時間では諦めない。</b>欠けが見えている(先のデータが待ちにある、または FIN が先にある)ときに、
/// 相手の累積 ACK が欠けを越えたら、相手は受け取ったのにキャプチャが見ていないので再送は来ない。そこで飛ぶ。
/// ACK が越えない欠けは再送で埋まる。ACK を見失ったときは、受け手の窓で表せる最大を超えて待ちが溜まったことで飛ぶ
/// (受け手が受け取っていなければ、送り手はその先を送れない)。
/// </para>
/// </summary>
internal sealed class TcpStreamDirection
{
    /// <summary>位置の窓。TCP の窓の最大(RFC 7323)。これより遠い seq・ACK はこの流れのものとみなさない。</summary>
    public const long MaxWindow = 1L << 30;

    private static readonly ILogger Log = Serilog.Log.ForContext<TcpStreamDirection>();

    private readonly SortedList<long, QueuedSegment> _queue = new();
    private readonly List<(long Start, long End)> _skippedRanges = new();
    private readonly MessageFramer _framer;
    private readonly Func<string> _describeCaptureStatistics;
    private uint _nextSeq;
    private long? _peerAckPos;
    private DateTime? _ackAheadSinceUtc;
    private long _queuedMaxEnd;

    public TcpStreamDirection(
        TcpEndpoint source,
        TcpEndpoint destination,
        Action<byte[], int, DateTime> onMessage,
        Func<string> describeCaptureStatistics)
    {
        Source = source;
        Destination = destination;
        Name = $"{source} -> {destination}";
        _framer = new MessageFramer(Name, onMessage);
        _describeCaptureStatistics = describeCaptureStatistics;
    }

    public TcpEndpoint Source { get; }

    public TcpEndpoint Destination { get; }

    public string Name { get; }

    public bool HasBase { get; private set; }

    /// <summary>ストリームの先頭(SYN の次)から読んでいるか。<see cref="Isn"/> はそのときだけ意味を持つ。</summary>
    public bool StartedAtStreamStart { get; private set; }

    public uint Isn { get; private set; }

    /// <summary>この向きの送り手の SYN を見たか。</summary>
    public bool SawSyn { get; private set; }

    /// <summary>この向きの送り手が SYN で知らせた Window Scale(無ければ null)。</summary>
    public byte? AnnouncedWindowScale { get; private set; }

    /// <summary>この向きのデータの受け手が窓で表せる最大のバイト数。分からなければ <see cref="MaxWindow"/>。</summary>
    public long ReceiverMaxWindow { get; set; } = MaxWindow;

    public long NextPos { get; private set; }

    public long? FinPos { get; private set; }

    public bool IsFinished => FinPos is { } fin && NextPos >= fin;

    public long DeliveredBytes { get; private set; }

    public long RetransmittedSegments { get; private set; }

    public long RetransmittedBytes { get; private set; }

    public long OverlapTrimmedSegments { get; private set; }

    public long OverlapTrimmedBytes { get; private set; }

    public long OutOfOrderSegments { get; private set; }

    public long DuplicateQueuedSegments { get; private set; }

    public int PeakQueuedSegments { get; private set; }

    public long PeakQueuedBytes { get; private set; }

    public long QueuedBytes { get; private set; }

    public long GapCount { get; private set; }

    public long LostBytes { get; private set; }

    public long LateAfterSkipBytes { get; private set; }

    public long AckBeforeDataCount { get; private set; }

    public double AckBeforeDataMaxMs { get; private set; }

    public long OutOfWindowRestarts { get; private set; }

    public long IgnoredAcks { get; private set; }

    public long UnfinishedAtCloseBytes { get; private set; }

    /// <summary>失ったものがあるか(閉じたときの要約を Warning にするか)。閉じたときの途中のバイトは含めない。</summary>
    public bool HasLoss =>
        GapCount > 0
        || LateAfterSkipBytes > 0
        || OutOfWindowRestarts > 0
        || _framer.UnsyncedDiscardedBytes > 0
        || _framer.InvalidLengthCount > 0
        || _framer.MessagesBrokenByGap > 0;

    public void OnSyn(uint isn, byte? windowScale)
    {
        SawSyn = true;
        AnnouncedWindowScale = windowScale;
        if (!HasBase)
        {
            StartAtStreamStart(isn);
        }
    }

    /// <summary>相手の SYN-ACK の ACK から、この向きのストリームの先頭を知る(この向きの SYN は見ていない)。</summary>
    public void StartFromPeerSynAck(uint acknowledgedSeq)
    {
        if (!HasBase)
        {
            StartAtStreamStart(unchecked(acknowledgedSeq - 1));
        }
    }

    /// <summary>この向きの始まりが、届いた SYN の ISN と違うか(同じ4つ組の別の接続)。</summary>
    public bool StartedDifferentlyFrom(uint isn)
    {
        return HasBase && !(StartedAtStreamStart && Isn == isn);
    }

    /// <summary>閉じた後に届いた seq が、この向きの FIN より先(新しいデータ)か。</summary>
    public bool IsBeyondFin(uint seq)
    {
        return HasBase && FinPos is { } fin && ToPos(seq) > fin;
    }

    public void OnSegment(uint seq, ReadOnlySpan<byte> payload, DateTime arrivalUtc)
    {
        if (!HasBase)
        {
            StartMidStream(seq);
        }

        var pos = ToPos(seq);
        if (Math.Abs(pos - NextPos) > MaxWindow)
        {
            RestartAt(seq, pos, arrivalUtc);
            pos = NextPos;
        }

        var end = pos + payload.Length;
        if (end <= NextPos)
        {
            RetransmittedSegments++;
            RetransmittedBytes += payload.Length;
            NoteLateAfterSkip(pos, end);
            return;
        }

        if (pos < NextPos)
        {
            var trim = (int)(NextPos - pos);
            OverlapTrimmedSegments++;
            OverlapTrimmedBytes += trim;
            NoteLateAfterSkip(pos, NextPos);
            Deliver(payload[trim..], isWholeSegment: false, arrivalUtc);
            Drain(arrivalUtc);
            EvaluateGap(arrivalUtc);
            return;
        }

        if (pos == NextPos)
        {
            NoteAckBeforeData(arrivalUtc);
            Deliver(payload, isWholeSegment: true, arrivalUtc);
            Drain(arrivalUtc);
            EvaluateGap(arrivalUtc);
            return;
        }

        Enqueue(pos, payload, arrivalUtc);
        EvaluateGap(arrivalUtc);
    }

    /// <summary>相手から届いた累積 ACK(この向きのデータへの受信確認)。</summary>
    public void OnPeerAck(uint acknowledgedSeq, DateTime arrivalUtc)
    {
        if (!HasBase)
        {
            return;
        }

        var ackPos = ToPos(acknowledgedSeq);
        if (Math.Abs(ackPos - NextPos) > MaxWindow)
        {
            IgnoredAcks++;
            return;
        }

        if (_peerAckPos is null || ackPos > _peerAckPos)
        {
            _peerAckPos = ackPos;
        }

        if (_peerAckPos > NextPos)
        {
            _ackAheadSinceUtc ??= arrivalUtc;
        }

        EvaluateGap(arrivalUtc);
    }

    /// <summary>この向きの FIN。<paramref name="seq"/> は中身の先頭の seq。基点が無ければ、そこを基点にして終わった扱いにする。</summary>
    public void OnFin(uint seq, int payloadLength, DateTime arrivalUtc)
    {
        if (!HasBase)
        {
            StartMidStream(seq);
        }

        FinPos ??= ToPos(seq) + payloadLength;
        EvaluateGap(arrivalUtc);
    }

    /// <summary>閉じるときに呼ぶ。待ちと途中のメッセージを捨て、そのバイト数を控える。</summary>
    public void Finish()
    {
        UnfinishedAtCloseBytes = QueuedBytes + _framer.Finish();
        _queue.Clear();
        QueuedBytes = 0;
        _queuedMaxEnd = 0;
    }

    /// <summary>閉じたときの要約の1方向分(英語)。値が0の項目は書かない。</summary>
    public string DescribeSummary()
    {
        var builder = new StringBuilder();
        builder.Append(Name)
            .Append(' ')
            .Append(DeliveredBytes)
            .Append(" B / ")
            .Append(_framer.Messages)
            .Append(" messages");
        AppendIf(builder, RetransmittedSegments > 0, $"retransmitted {RetransmittedSegments} ({RetransmittedBytes} B)");
        AppendIf(builder, OverlapTrimmedSegments > 0, $"overlap trimmed {OverlapTrimmedSegments} ({OverlapTrimmedBytes} B)");
        AppendIf(builder, OutOfOrderSegments > 0, $"out of order {OutOfOrderSegments}, peak queue {PeakQueuedSegments} segments / {PeakQueuedBytes} B");
        AppendIf(builder, DuplicateQueuedSegments > 0, $"duplicates in queue {DuplicateQueuedSegments}");
        AppendIf(builder, GapCount > 0, $"skipped {GapCount} gaps / {LostBytes} B");
        AppendIf(builder, LateAfterSkipBytes > 0, $"late after skip {LateAfterSkipBytes} B");
        AppendIf(builder, AckBeforeDataCount > 0, $"ack before data {AckBeforeDataCount} (max {AckBeforeDataMaxMs:F0} ms)");
        AppendIf(builder, _framer.UnsyncedDiscardedSegments > 0, $"discarded while unsynced {_framer.UnsyncedDiscardedSegments} segments / {_framer.UnsyncedDiscardedBytes} B");
        AppendIf(builder, _framer.ResyncCount > 0, $"resynced {_framer.ResyncCount}");
        AppendIf(builder, _framer.InvalidLengthCount > 0, $"invalid lengths {_framer.InvalidLengthCount} ({_framer.InvalidLengthDroppedBytes} B dropped)");
        AppendIf(builder, _framer.MessagesBrokenByGap > 0, $"messages broken by gaps {_framer.MessagesBrokenByGap}");
        AppendIf(builder, OutOfWindowRestarts > 0, $"out-of-window restarts {OutOfWindowRestarts}");
        AppendIf(builder, IgnoredAcks > 0, $"ignored acks {IgnoredAcks}");
        AppendIf(builder, UnfinishedAtCloseBytes > 0, $"unfinished at close {UnfinishedAtCloseBytes} B");
        return builder.ToString();
    }

    private static void AppendIf(StringBuilder builder, bool condition, string text)
    {
        if (condition)
        {
            builder.Append(", ").Append(text);
        }
    }

    private long ToPos(uint seq)
    {
        return NextPos + unchecked((int)(seq - _nextSeq));
    }

    private void StartAtStreamStart(uint isn)
    {
        HasBase = true;
        StartedAtStreamStart = true;
        Isn = isn;
        _nextSeq = unchecked(isn + 1);
        NextPos = 0;
        _framer.StartAtStreamStart();
    }

    private void StartMidStream(uint seq)
    {
        HasBase = true;
        StartedAtStreamStart = false;
        _nextSeq = seq;
        NextPos = 0;
    }

    /// <summary>
    /// 窓の外の seq が来た。この流れの続きではないので、待ち・ACK・飛ばした範囲を捨て、そのセグメントから途中として読み直す。
    /// </summary>
    private void RestartAt(uint seq, long pos, DateTime arrivalUtc)
    {
        OutOfWindowRestarts++;
        Log.Warning(
            "TCP {Stream} segment seq {Seq} is {Distance} bytes from the expected position, outside the maximum TCP window; restarting the stream here ({QueuedSegments} segments / {QueuedBytes} B queued dropped)",
            Name,
            seq,
            pos - NextPos,
            _queue.Count,
            QueuedBytes);
        UnfinishedAtCloseBytes += QueuedBytes;
        _queue.Clear();
        QueuedBytes = 0;
        _queuedMaxEnd = 0;
        _skippedRanges.Clear();
        _peerAckPos = null;
        _ackAheadSinceUtc = null;
        _framer.Restart(arrivalUtc);
        _nextSeq = seq;
    }

    private void Deliver(ReadOnlySpan<byte> data, bool isWholeSegment, DateTime arrivalUtc)
    {
        NextPos += data.Length;
        _nextSeq = unchecked(_nextSeq + (uint)data.Length);
        DeliveredBytes += data.Length;
        if (_peerAckPos is not { } ack || ack <= NextPos)
        {
            _ackAheadSinceUtc = null;
        }

        _framer.OnData(data, isWholeSegment, arrivalUtc);
    }

    private void Enqueue(long pos, ReadOnlySpan<byte> payload, DateTime arrivalUtc)
    {
        OutOfOrderSegments++;
        if (_queue.TryGetValue(pos, out var existing))
        {
            DuplicateQueuedSegments++;
            if (existing.Data.Length >= payload.Length)
            {
                return;
            }

            _queue.Remove(pos);
            QueuedBytes -= existing.Data.Length;
        }

        _queue.Add(pos, new QueuedSegment(payload.ToArray(), arrivalUtc));
        QueuedBytes += payload.Length;
        _queuedMaxEnd = Math.Max(_queuedMaxEnd, pos + payload.Length);
        PeakQueuedSegments = Math.Max(PeakQueuedSegments, _queue.Count);
        PeakQueuedBytes = Math.Max(PeakQueuedBytes, QueuedBytes);
    }

    /// <summary>next に届いた待ちを前から渡す。</summary>
    private void Drain(DateTime arrivalUtc)
    {
        var removed = false;
        while (_queue.Count > 0)
        {
            var start = _queue.Keys[0];
            if (start > NextPos)
            {
                break;
            }

            var segment = _queue.Values[0];
            _queue.RemoveAt(0);
            QueuedBytes -= segment.Data.Length;
            removed = true;

            var end = start + segment.Data.Length;
            if (end <= NextPos)
            {
                RetransmittedSegments++;
                RetransmittedBytes += segment.Data.Length;
                continue;
            }

            var trim = (int)(NextPos - start);
            if (trim > 0)
            {
                OverlapTrimmedSegments++;
                OverlapTrimmedBytes += trim;
            }

            Deliver(segment.Data.AsSpan(trim), isWholeSegment: trim == 0, arrivalUtc);
        }

        if (removed)
        {
            _queuedMaxEnd = 0;
            foreach (var (start, segment) in _queue)
            {
                _queuedMaxEnd = Math.Max(_queuedMaxEnd, start + segment.Data.Length);
            }
        }
    }

    /// <summary>欠けを諦めてよいかを見る。データが来たときにも、相手の ACK が来たときにも呼ぶ。</summary>
    private void EvaluateGap(DateTime arrivalUtc)
    {
        while (HasBase)
        {
            long gapEnd;
            if (_queue.Count > 0)
            {
                gapEnd = _queue.Keys[0];
            }
            else if (FinPos is { } fin && fin > NextPos)
            {
                gapEnd = fin;
            }
            else
            {
                return;
            }

            long target;
            string reason;
            if (_peerAckPos is { } ack && ack > NextPos)
            {
                target = Math.Min(ack, gapEnd);
                reason = $"peer acknowledged up to offset {ack}";
            }
            else if (_queue.Count > 0 && _queuedMaxEnd - NextPos > ReceiverMaxWindow)
            {
                target = Math.Min(gapEnd, _queuedMaxEnd - ReceiverMaxWindow);
                reason = $"queued data reaches offset {_queuedMaxEnd}, beyond the receive window of {ReceiverMaxWindow} B";
            }
            else
            {
                return;
            }

            Skip(target, reason, arrivalUtc);
            Drain(arrivalUtc);
        }
    }

    private void Skip(long target, string reason, DateTime arrivalUtc)
    {
        var offset = NextPos;
        var lost = target - offset;
        var waitedMs = 0d;
        if (_queue.Count > 0)
        {
            var oldest = _queue.Values.Min(static segment => segment.ArrivalUtc);
            waitedMs = (arrivalUtc - oldest).TotalMilliseconds;
        }

        var framing = _framer.OnGap(lost, arrivalUtc);
        _skippedRanges.Add((offset, target));
        GapCount++;
        LostBytes += lost;
        NextPos = target;
        _nextSeq = unchecked(_nextSeq + (uint)lost);
        if (_peerAckPos is not { } ack || ack <= NextPos)
        {
            _ackAheadSinceUtc = null;
        }

        Log.Warning(
            "TCP {Stream} skipped {Lost} bytes not seen by the capture at offset {Offset} ({Reason}); waited {WaitedMs:F0} ms with {QueuedSegments} segments / {QueuedBytes} B queued; framing {Framing}; {Capture}",
            Name,
            lost,
            offset,
            reason,
            waitedMs,
            _queue.Count,
            QueuedBytes,
            framing,
            _describeCaptureStatistics());
    }

    private void NoteLateAfterSkip(long start, long end)
    {
        long overlap = 0;
        long firstOffset = -1;
        foreach (var (skippedStart, skippedEnd) in _skippedRanges)
        {
            var from = Math.Max(start, skippedStart);
            var to = Math.Min(end, skippedEnd);
            if (to > from)
            {
                overlap += to - from;
                if (firstOffset < 0)
                {
                    firstOffset = from;
                }
            }
        }

        if (overlap == 0)
        {
            return;
        }

        LateAfterSkipBytes += overlap;
        Log.Warning(
            "TCP {Stream} received {Bytes} bytes inside a range already skipped at offset {Offset}",
            Name,
            overlap,
            firstOffset);
    }

    private void NoteAckBeforeData(DateTime arrivalUtc)
    {
        if (_peerAckPos is { } ack && ack > NextPos && _ackAheadSinceUtc is { } since)
        {
            AckBeforeDataCount++;
            AckBeforeDataMaxMs = Math.Max(AckBeforeDataMaxMs, (arrivalUtc - since).TotalMilliseconds);
        }
    }

    private readonly record struct QueuedSegment(byte[] Data, DateTime ArrivalUtc);
}
