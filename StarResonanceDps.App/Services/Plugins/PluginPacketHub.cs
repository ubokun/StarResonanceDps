// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.Frozen;
using System.Threading;
using System.Threading.Channels;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.CombatRuntime.Protocols;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 本体が受けたゲームのメッセージを、プラグインの受け手(<see cref="IPluginPacketService.ReadAsync"/> の繰り返し)へ配る。全部で1つ。
///
/// <para>
/// パケット処理のスレッドで、どれかの受け手が受ける種類のメッセージだけを1件につき1回写し、受け手ごとの順番待ちに入れる。
/// 順番待ちに上限は無く、捨てない。まだ取られていない中身がプラグインごとに <see cref="MaxPendingBytesPerPlugin"/> を越えたら、
/// そのプラグインを止める(<see cref="Account"/> の越えたときの処理)。
/// </para>
/// </summary>
internal sealed class PluginPacketHub
{
    public const long MaxPendingBytesPerPlugin = 64L * 1024 * 1024;

    private readonly object _sync = new();
    private readonly List<Reader> _readers = [];
    private FrozenDictionary<PluginPacketType, Reader[]> _readersByType = FrozenDictionary<PluginPacketType, Reader[]>.Empty;
    private long _sequenceNumber;

    private PluginPacketHub()
    {
        MessageManager.MessageObserved += MessageManager_MessageObserved;
    }

    public static PluginPacketHub Instance { get; } = new();

    /// <summary>プラグイン1つの受け取りの勘定。プラグインを読み込むときに作る。</summary>
    /// <param name="onOverflow">取り残しが上限を越えたときに1回呼ぶ(パケット処理のスレッド)。</param>
    public Account CreateAccount(Action onOverflow)
    {
        return new Account(this, onOverflow);
    }

    private void MessageManager_MessageObserved(ObservedMessage message)
    {
        var readersByType = Volatile.Read(ref _readersByType);
        if (readersByType.Count == 0)
        {
            return;
        }

        var kind = message.Kind switch
        {
            ObservedMessageKind.Notify => PluginPacketKind.Notify,
            ObservedMessageKind.Call => PluginPacketKind.Call,
            ObservedMessageKind.Return => PluginPacketKind.Return,
            _ => throw new ArgumentOutOfRangeException(nameof(message), message.Kind, "Unknown message kind."),
        };
        if (!readersByType.TryGetValue(new PluginPacketType(kind, message.ServiceId, message.MethodId), out var readers))
        {
            return;
        }

        var packet = new PluginPacketMessage(
            Interlocked.Increment(ref _sequenceNumber),
            kind,
            message.ServiceId,
            message.MethodId,
            message.CallId,
            message.Payload.ToArray(),
            message.ArrivalTimeUtc);
        foreach (var reader in readers)
        {
            reader.Enqueue(packet);
        }
    }

    private Reader? Register(Account account, IReadOnlyCollection<PluginPacketType> types)
    {
        lock (_sync)
        {
            if (account.IsClosed)
            {
                return null;
            }

            var reader = new Reader(account, types.ToFrozenSet());
            _readers.Add(reader);
            RebuildLookup();
            return reader;
        }
    }

    private void Unregister(Reader reader)
    {
        lock (_sync)
        {
            if (_readers.Remove(reader))
            {
                RebuildLookup();
            }
        }

        reader.Complete();
    }

    private void UnregisterAll(Account account)
    {
        List<Reader> removed;
        lock (_sync)
        {
            removed = _readers.Where(reader => reader.Account == account).ToList();
            if (removed.Count == 0)
            {
                return;
            }

            _readers.RemoveAll(reader => reader.Account == account);
            RebuildLookup();
        }

        foreach (var reader in removed)
        {
            reader.Complete();
        }
    }

    private void RebuildLookup()
    {
        var lookup = _readers
            .SelectMany(reader => reader.Types.Select(type => (Type: type, Reader: reader)))
            .GroupBy(pair => pair.Type)
            .ToFrozenDictionary(group => group.Key, group => group.Select(pair => pair.Reader).ToArray());
        Volatile.Write(ref _readersByType, lookup);
    }

    /// <summary>プラグイン1つの受け取りの勘定(まだ取られていない中身の大きさ)と、そのプラグインの受け手の口。</summary>
    public sealed class Account : IPluginPacketService
    {
        private readonly PluginPacketHub _hub;
        private readonly Action _onOverflow;
        private bool _isClosed;
        private long _pendingBytes;
        private int _hasOverflowed;

        public Account(PluginPacketHub hub, Action onOverflow)
        {
            _hub = hub;
            _onOverflow = onOverflow;
        }

        /// <summary>閉じたか。<see cref="PluginPacketHub._sync"/> の中で読み書きする。</summary>
        public bool IsClosed => _isClosed;

        /// <summary>プラグインを止めた・終えたときに、受け手を全部外して溜まった分を捨てる。繰り返しは終わる。</summary>
        public void Close()
        {
            lock (_hub._sync)
            {
                if (_isClosed)
                {
                    return;
                }

                _isClosed = true;
            }

            _hub.UnregisterAll(this);
        }

        public async IAsyncEnumerable<PluginPacketMessage> ReadAsync(
            IReadOnlyCollection<PluginPacketType> types,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(types);
            if (types.Count == 0)
            {
                throw new ArgumentException("At least one message type is required.", nameof(types));
            }

            if (_hub.Register(this, types) is not { } reader)
            {
                yield break;
            }

            try
            {
                // 本体がプラグインを止めると受け手が外され(Close)、待ちは false で終わる。
                while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var message))
                    {
                        yield return message;
                    }
                }
            }
            finally
            {
                _hub.Unregister(reader);
            }
        }

        public void AddPending(int bytes)
        {
            if (Interlocked.Add(ref _pendingBytes, bytes) > MaxPendingBytesPerPlugin
                && Interlocked.Exchange(ref _hasOverflowed, 1) == 0)
            {
                _onOverflow();
            }
        }

        public void RemovePending(int bytes)
        {
            Interlocked.Add(ref _pendingBytes, -bytes);
        }
    }

    private sealed class Reader
    {
        // 外すとき(Complete)は、プラグインの繰り返しとは別のスレッドからも読んで捨てる。
        private readonly Channel<PluginPacketMessage> _channel = Channel.CreateUnbounded<PluginPacketMessage>();

        public Reader(Account account, FrozenSet<PluginPacketType> types)
        {
            Account = account;
            Types = types;
        }

        public Account Account { get; }

        public FrozenSet<PluginPacketType> Types { get; }

        public void Enqueue(PluginPacketMessage packet)
        {
            if (_channel.Writer.TryWrite(packet))
            {
                Account.AddPending(packet.Payload.Length);
            }
        }

        public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
        {
            return _channel.Reader.WaitToReadAsync(cancellationToken);
        }

        public bool TryRead(out PluginPacketMessage packet)
        {
            if (!_channel.Reader.TryRead(out packet!))
            {
                return false;
            }

            Account.RemovePending(packet.Payload.Length);
            return true;
        }

        /// <summary>受け手を外したときに、繰り返しを終わらせ、溜まった分を捨てる。</summary>
        public void Complete()
        {
            _channel.Writer.TryComplete();
            while (_channel.Reader.TryRead(out var packet))
            {
                Account.RemovePending(packet.Payload.Length);
            }
        }
    }
}
