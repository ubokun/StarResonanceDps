// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Buffers;
using System.Buffers.Binary;
using Serilog;

namespace StarResonanceDps.Core.CombatRuntime.Protocols;

/// <summary>
/// 1つの向きのバイト列から、ゲームのメッセージ(先頭4バイトが見出しを含む全長、続く2バイトが種類)を切り出す。
///
/// <para>
/// 同期中は長さだけを検査する(種類は見ない。列挙に無い種類9が届いた前例がある)。
/// 長さが範囲外なら同期を失ったとみなし、途中のバイトを捨てて、1つのセグメントがちょうど1つのメッセージになるものが
/// 来るまで読み直す(notes/131 の判定)。
/// </para>
///
/// <para>
/// 揃ったメッセージのバッファは <see cref="ArrayPool{T}.Shared"/> から借りたもので、受け手へそのまま渡す(返すのは受け手)。
/// </para>
/// </summary>
internal sealed class MessageFramer
{
    public const int HeaderLength = 6;

    /// <summary>
    /// 切り出しの長さの上限。同期を失ったことに気付き、壊れた長さで巨大な領域を借りないための値。
    /// 展開後の大きさの上限(NetCap の展開のバッファ)とは別のもの。
    /// </summary>
    public const int MaxMessageLength = 0x0FFFFF;

    /// <summary>未同期から読み始めてよい種類の上限。</summary>
    private const int MaxSyncMessageType = 9;

    private static readonly ILogger Log = Serilog.Log.ForContext<MessageFramer>();

    private readonly string _streamName;
    private readonly Action<byte[], int, DateTime> _onMessage;
    private readonly byte[] _header = new byte[HeaderLength];
    private int _headerFilled;
    private byte[]? _message;
    private int _messageLength;
    private int _messageFilled;
    private long _discardRemaining;
    private bool _hasSyncedOnce;
    private DateTime? _unsyncedSinceUtc;
    private long _discardedSegmentsWhileUnsynced;
    private long _discardedBytesWhileUnsynced;

    public MessageFramer(string streamName, Action<byte[], int, DateTime> onMessage)
    {
        _streamName = streamName;
        _onMessage = onMessage;
    }

    public bool IsSynced { get; private set; }

    public long Messages { get; private set; }

    /// <summary>読み始める前・読み直すまでに捨てたセグメントとバイト。</summary>
    public long UnsyncedDiscardedSegments { get; private set; }

    public long UnsyncedDiscardedBytes { get; private set; }

    public long InvalidLengthCount { get; private set; }

    public long InvalidLengthDroppedBytes { get; private set; }

    /// <summary>欠けで中身が欠けたため捨てたメッセージの数。</summary>
    public long MessagesBrokenByGap { get; private set; }

    public long ResyncCount { get; private set; }

    /// <summary>SYN の次のバイトから読む。ストリームの先頭はメッセージの先頭。</summary>
    public void StartAtStreamStart()
    {
        IsSynced = true;
        _hasSyncedOnce = true;
    }

    public void OnData(ReadOnlySpan<byte> data, bool isWholeSegment, DateTime arrivalUtc)
    {
        if (!IsSynced)
        {
            if (!CanStartAt(data, isWholeSegment))
            {
                UnsyncedDiscardedSegments++;
                UnsyncedDiscardedBytes += data.Length;
                _discardedSegmentsWhileUnsynced++;
                _discardedBytesWhileUnsynced += data.Length;
                _unsyncedSinceUtc ??= arrivalUtc;
                return;
            }

            SyncHere(arrivalUtc);
        }

        while (!data.IsEmpty)
        {
            if (_discardRemaining > 0)
            {
                var drop = (int)Math.Min(_discardRemaining, data.Length);
                _discardRemaining -= drop;
                data = data[drop..];
                continue;
            }

            if (_message is null)
            {
                var headerTake = Math.Min(HeaderLength - _headerFilled, data.Length);
                data[..headerTake].CopyTo(_header.AsSpan(_headerFilled));
                _headerFilled += headerTake;
                data = data[headerTake..];
                if (_headerFilled < HeaderLength)
                {
                    break;
                }

                var length = BinaryPrimitives.ReadUInt32BigEndian(_header);
                if (length < HeaderLength || length > MaxMessageLength)
                {
                    var dropped = _headerFilled + data.Length;
                    InvalidLengthCount++;
                    InvalidLengthDroppedBytes += dropped;
                    Log.Error(
                        "TCP {Stream} invalid message length {Length} (header {Header}); dropped {Bytes} buffered bytes, resyncing",
                        _streamName,
                        length,
                        Convert.ToHexString(_header),
                        dropped);
                    Desync(arrivalUtc);
                    return;
                }

                _messageLength = (int)length;
                _message = ArrayPool<byte>.Shared.Rent(_messageLength);
                _header.CopyTo(_message, 0);
                _messageFilled = HeaderLength;
                _headerFilled = 0;
            }

            var bodyTake = Math.Min(_messageLength - _messageFilled, data.Length);
            data[..bodyTake].CopyTo(_message.AsSpan(_messageFilled));
            _messageFilled += bodyTake;
            data = data[bodyTake..];
            if (_messageFilled == _messageLength)
            {
                var message = _message;
                _message = null;
                Messages++;
                _onMessage(message, _messageLength, arrivalUtc);
            }
        }
    }

    /// <summary>
    /// 欠け(キャプチャが見ていない <paramref name="lostBytes"/> バイト)を受ける。
    /// 欠けが読んでいるメッセージの本体の中に収まるなら、そのメッセージだけ捨てて同期を保つ。
    /// 境目の位置が分からなくなるなら未同期にする。返す文字列はログに添える説明(英語)。
    /// </summary>
    public string OnGap(long lostBytes, DateTime arrivalUtc)
    {
        if (!IsSynced)
        {
            return "not synced yet";
        }

        if (_discardRemaining > 0)
        {
            if (lostBytes <= _discardRemaining)
            {
                _discardRemaining -= lostBytes;
                return "kept (inside a message already being dropped)";
            }

            Desync(arrivalUtc);
            return "lost, resyncing";
        }

        if (_message is not null)
        {
            var remaining = _messageLength - _messageFilled;
            ReturnMessage();
            MessagesBrokenByGap++;
            if (lostBytes <= remaining)
            {
                _discardRemaining = remaining - lostBytes;
                return "kept (dropped the message in progress)";
            }

            Desync(arrivalUtc);
            return "lost, resyncing";
        }

        // 境目か見出しの途中で欠けた。欠けの中に境目がいくつあったか分からない。
        Desync(arrivalUtc);
        return "lost, resyncing";
    }

    /// <summary>読み直す(未同期にする)。途中のメッセージは捨てる。</summary>
    public void Restart(DateTime arrivalUtc)
    {
        if (_message is not null)
        {
            MessagesBrokenByGap++;
        }

        Desync(arrivalUtc);
    }

    /// <summary>接続を閉じるときに呼ぶ。途中のバイト数を返し、バッファを返す。</summary>
    public long Finish()
    {
        var partial = _headerFilled + (_message is null ? 0 : _messageFilled);
        ReturnMessage();
        _headerFilled = 0;
        _discardRemaining = 0;
        return partial;
    }

    private static bool CanStartAt(ReadOnlySpan<byte> data, bool isWholeSegment)
    {
        return isWholeSegment
            && data.Length >= HeaderLength
            && BinaryPrimitives.ReadInt32BigEndian(data) == data.Length
            && (BinaryPrimitives.ReadInt16BigEndian(data[4..]) & 0x7FFF) <= MaxSyncMessageType;
    }

    private void SyncHere(DateTime arrivalUtc)
    {
        IsSynced = true;
        if (!_hasSyncedOnce)
        {
            Log.Information(
                "TCP {Stream} synced mid-stream after discarding {Segments} segments / {Bytes} bytes",
                _streamName,
                _discardedSegmentsWhileUnsynced,
                _discardedBytesWhileUnsynced);
        }
        else
        {
            ResyncCount++;
            var waited = _unsyncedSinceUtc is { } since ? (arrivalUtc - since).TotalMilliseconds : 0d;
            Log.Warning(
                "TCP {Stream} resynced after discarding {Segments} segments / {Bytes} bytes ({WaitedMs:F0} ms)",
                _streamName,
                _discardedSegmentsWhileUnsynced,
                _discardedBytesWhileUnsynced,
                waited);
        }

        _hasSyncedOnce = true;
        _unsyncedSinceUtc = null;
        _discardedSegmentsWhileUnsynced = 0;
        _discardedBytesWhileUnsynced = 0;
    }

    private void Desync(DateTime arrivalUtc)
    {
        ReturnMessage();
        _headerFilled = 0;
        _discardRemaining = 0;
        IsSynced = false;
        _unsyncedSinceUtc = arrivalUtc;
        _discardedSegmentsWhileUnsynced = 0;
        _discardedBytesWhileUnsynced = 0;
    }

    private void ReturnMessage()
    {
        if (_message is null)
        {
            return;
        }

        ArrayPool<byte>.Shared.Return(_message);
        _message = null;
        _messageLength = 0;
        _messageFilled = 0;
    }
}
