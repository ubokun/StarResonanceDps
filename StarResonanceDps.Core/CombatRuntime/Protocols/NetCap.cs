// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Microsoft.Extensions.ObjectPool;
using PacketDotNet;
using Serilog;
using SharpPcap;
using SharpPcap.LibPcap;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using ZstdSharp;

namespace StarResonanceDps.Core.CombatRuntime.Protocols;

/// <summary>
/// キャプチャ → TCP の組み立てと切り出し(キャプチャのスレッド)→ 待ち行列 → 処理(1本のスレッド)。
/// メッセージはキャプチャの順に処理し、時刻はそのメッセージが揃ったパケットのキャプチャ時刻(UTC)。
/// 待ち行列には、処理のスレッドで実行する命令(<see cref="TryEnqueueCommand"/>)も同じ順で並ぶ。
/// </summary>
public class NetCap
{
    private NetCapConfig Config = null!;
    public ICaptureDevice CaptureDevice = null!;
    private TcpReassembler _tcpReassembler = null!;

    public ObjectPool<RawPacket> RawPacketPool = ObjectPool.Create(new DefaultPooledObjectPolicy<RawPacket>());
    private readonly BlockingCollection<QueuedWork> _workQueue = new();
    private Task PacketParseTask = null!;

    // 命令を積めるか(_isStarted)と、待ち行列を閉じる順番を守る。閉じた後に積むと例外になる。
    private readonly object _queueGate = new();
    private bool _isStarted;

    /// <summary>待ち行列の1件。メッセージか、処理のスレッドで実行する命令のどちらか。</summary>
    private readonly record struct QueuedWork(RawPacket? Packet, Action? Command);

    /// <summary>展開後の大きさの上限。切り出しの長さの上限(<see cref="MessageFramer.MaxMessageLength"/>)とは別。</summary>
    private const int DecompressionBufferLength = 1024 * 1024;

    private byte[] DecompressionScratchBuffer = new byte[DecompressionBufferLength];
    private Decompressor _decompressor = new();
    private Dictionary<NotifyId, Action<ReadOnlySpan<byte>, ExtraPacketData>> NotifyHandlers = new();
    private Dictionary<ProxyId, Action<ReadOnlySpan<byte>, uint, ExtraPacketData>> ProxyHandlers = new();
    private Dictionary<ProxyId, Action<ReadOnlySpan<byte>, uint, ExtraPacketData>> ProxyReturnHandlers = new();
    private ConcurrentDictionary<uint, ProxyId> ProxyReturnsDictionary = new();
    public ConcurrentDictionary<TcpConnectionKey, bool> ConnectionFilters = new();

    private static readonly TimeSpan FailureLogInterval = TimeSpan.FromMinutes(1);
    private readonly object FailureLogSync = new();
    private readonly Dictionary<string, (DateTime LastLogged, int Suppressed)> FailureLogState = new(StringComparer.Ordinal);

    public void Init(NetCapConfig config)
    {
        Config = config;
    }

    public void Start()
    {
        ProxyReturnsDictionary.Clear();

        CaptureDevice = GetCaptureDevice();
        CaptureDevice.Open(DeviceModes.Promiscuous, 100);
        CaptureDevice.Filter = "tcp and not portrange 0-1000";
        CaptureDevice.OnPacketArrival += DeviceOnOnPacketArrival;

        // デバイスを開けた後に作る。開けずに例外で抜けたとき、処理のスレッドが待ち行列を待ったまま残らないように。
        _tcpReassembler = new TcpReassembler(EnqueueMessage, DescribeCaptureStatistics, () => MidStreamConnectionOpened?.Invoke());
        PacketParseTask = Task.Factory.StartNew(ParsePacketsLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            CaptureDevice.StartCapture();
        }
        catch
        {
            _workQueue.CompleteAdding();
            throw;
        }

        lock (_queueGate)
        {
            _isStarted = true;
        }

        Log.Information("Capture device started");
    }

    /// <summary>
    /// 処理のスレッドで <paramref name="command"/> を実行するよう待ち行列に積む(前に積まれたメッセージの処理の後に走る)。
    /// キャプチャが動いていなければ積まずに false を返す。呼び手は自分のスレッドで実行する。
    /// </summary>
    public bool TryEnqueueCommand(Action command)
    {
        lock (_queueGate)
        {
            if (!_isStarted)
            {
                return false;
            }

            _workQueue.Add(new QueuedWork(null, command));
            return true;
        }
    }

    public void RegisterNotifyHandler(ulong serviceId, uint methodId, Action<ReadOnlySpan<byte>, ExtraPacketData> handler)
    {
        NotifyHandlers.Add(new NotifyId(serviceId, methodId), handler);
    }

    /// <summary>
    /// 通知を処理してよいかの関門。<c>null</c> なら全部通す。
    /// ログイン画面にいる間、ゲームの状態を変える通知を落とすために使う(<c>MessageManager.ShouldDispatchNotify</c>)。
    /// </summary>
    public Func<ulong, uint, bool>? NotifyGate { get; set; }

    /// <summary>要求・応答を処理してよいかの関門。<c>null</c> なら全部通す。</summary>
    public Func<uint, uint, bool>? ProxyGate { get; set; }

    /// <summary>
    /// 届いたメッセージを、関門(<see cref="NotifyGate"/>・<see cref="ProxyGate"/>)と本体の受け口より前にのぞく口(処理のスレッド)。
    /// 通知は解凍の後、呼び出しはそのまま、応答は呼び出しと結んだ後に呼ぶ。プラグインへ渡すのに使う。
    /// </summary>
    public Action<ObservedMessage>? MessageObserved { get; set; }

    /// <summary>
    /// パケットを1つ処理する直前に、その到着時刻で呼ぶ。到着時刻までに起きた出来事を、パケットの中身より先に記録するのに使う
    /// (<c>EncounterManager.RecordEndedAnnouncementBars</c>)。
    /// </summary>
    public Action<DateTime>? BeforeParsePacket { get; set; }

    /// <summary>
    /// パケットを1つ処理した後に呼ぶ。パケットの出来事で変わった状態を、次のパケットより前に片付けるのに使う
    /// (<c>EncounterManager.ReleaseHeldPastEncounter</c>)。
    /// </summary>
    public Action? AfterParsePacket { get; set; }

    /// <summary>
    /// ゲームの接続を始まり(SYN)を見ずに途中から開いたときに呼ぶ(キャプチャのスレッド)。
    /// 始まりを見ていない接続では、全体コンテナと入場を取りこぼしている(<c>GameReceptionStateStore</c> の途中起動)。
    /// </summary>
    public Action? MidStreamConnectionOpened { get; set; }

    /// <summary>
    /// 新しい接続をゲームの接続と分かったときに、その接続を持つ実行ファイルの名前で呼ぶ(キャプチャのスレッド)。
    /// 今のサーバー(<c>GameServerStore</c>)を決めるのに使う。
    /// </summary>
    public Action<string>? GameConnectionIdentified { get; set; }

    public void RegisterProxyHandler(uint serviceId, uint methodId, Action<ReadOnlySpan<byte>, uint, ExtraPacketData> handler)
    {
        ProxyHandlers.Add(new ProxyId(serviceId, methodId), handler);
    }

    /// <summary>
    /// 上りで呼んだメソッドの応答の受け口。渡すのは応答の包み(<c>X_Ret { ret }</c>)そのもので、中身の始まりを推測しない
    /// (推測は応答の長さや中身の先頭のバイト次第で外れ、包みや長さの途中から読ませていた)。
    /// </summary>
    public void RegisterProxyReturnHandler(uint serviceId, uint methodId, Action<ReadOnlySpan<byte>, uint, ExtraPacketData> handler)
    {
        ProxyReturnHandlers.Add(new ProxyId(serviceId, methodId), handler);
    }

    public void RegisterMatchNotifyHandler(ServiceMethods.MatchNtf methodId, Action<ReadOnlySpan<byte>, ExtraPacketData> handler)
    {
        NotifyHandlers.Add(new NotifyId((ulong)EServiceId.MatchNtf, (uint)methodId), handler);
    }

    public void RegisterWorldNotifyHandler(ServiceMethods.WorldNtf methodId, Action<ReadOnlySpan<byte>, ExtraPacketData> handler)
    {
        NotifyHandlers.Add(new NotifyId((ulong)EServiceId.WorldNtf, (uint)methodId), handler);
    }

    private void DeviceOnOnPacketArrival(object sender, PacketCapture e)
    {
        try
        {
            var rawPacket = e.GetPacket();
            var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);

            var ipv4 = packet?.Extract<IPv4Packet>();
            if (ipv4 == null)
                return;

            var tcpPacket = packet?.Extract<TcpPacket>();
            if (tcpPacket == null)
                return;

            if (tcpPacket.DestinationPort <= 1000 || tcpPacket.SourcePort <= 1000)
                return;

            var connectionKey = TcpConnectionKey.From(
                TcpEndpoint.From(ipv4.SourceAddress, tcpPacket.SourcePort),
                TcpEndpoint.From(ipv4.DestinationAddress, tcpPacket.DestinationPort));
            if (!ConnectionFilters.TryGetValue(connectionKey, out var allowed))
            {
                var gameExeName = FindGameExeName(ipv4, tcpPacket);
                allowed = gameExeName is not null;
                if (ConnectionFilters.TryAdd(connectionKey, allowed) && gameExeName is not null)
                {
                    GameConnectionIdentified?.Invoke(gameExeName);
                }
            }

            if (!allowed)
                return;

            _tcpReassembler.AddPacket(ipv4, tcpPacket, rawPacket.Timeval.Date);
        }
        catch (Exception ex)
        {
            // キャプチャのスレッドで投げるとキャプチャごと止まる。その1件だけ捨てて続ける。
            LogFailureAndContinue("capture callback", ex);
        }
    }

    /// <summary>組み立ての受け手。キャプチャのスレッドで、組み立ての lock の中で呼ばれる。</summary>
    private void EnqueueMessage(byte[] rentedBuffer, int length, DateTime arrivalUtc)
    {
        var rawPacket = RawPacketPool.Get();
        rawPacket.Adopt(rentedBuffer, length, arrivalUtc);
        _workQueue.Add(new QueuedWork(rawPacket, null));
    }

    private void ParsePacketsLoop()
    {
        foreach (var work in _workQueue.GetConsumingEnumerable())
        {
            if (work.Command is { } command)
            {
                try
                {
                    command();
                }
                catch (Exception ex)
                {
                    // ここで投げ直すとこのループごと終わり、以後のパケットが一切処理されなくなる。
                    LogFailureAndContinue("queued command", ex);
                }

                continue;
            }

            var rawPacket = work.Packet!;

            // 失敗してもこのパケットの処理は続ける(別の try にする)。
            try
            {
                BeforeParsePacket?.Invoke(rawPacket.ArrivalTime);
            }
            catch (Exception ex)
            {
                LogFailureAndContinue("Before parsing a packet", ex);
            }

            try
            {
                ParsePacket(rawPacket.Data.AsSpan(0, rawPacket.Len), rawPacket.ArrivalTime);
            }
            catch (Exception ex)
            {
                // 最後の砦。処理の中で拾えなかった例外(解析そのものの失敗など)。
                // ここで投げ直すとこのループごと終わり、以後のパケットが一切処理されなくなる。
                LogFailureAndContinue("packet parsing", ex);
            }
            finally
            {
                rawPacket.Return();
                RawPacketPool.Return(rawPacket);
            }

            // 処理が途中で失敗したパケットの後でも呼ぶ(別の try にする)。
            try
            {
                AfterParsePacket?.Invoke();
            }
            catch (Exception ex)
            {
                LogFailureAndContinue("After parsing a packet", ex);
            }
        }
    }

    private void ParsePacket(ReadOnlySpan<byte> data, DateTime lastPacketTime)
    {
        int offset = 0;
        while (offset < data.Length)
        {
            var msgData = data[offset..];
            if (msgData.Length < MessageFramer.HeaderLength)
            {
                Log.Error(
                    "Invalid message bundle: {Remaining} trailing bytes at offset {Offset} of {Total} are shorter than a message header; dropping them",
                    msgData.Length,
                    offset,
                    data.Length);
                return;
            }

            var len = BinaryPrimitives.ReadUInt32BigEndian(msgData);
            if (len < MessageFramer.HeaderLength || len > msgData.Length)
            {
                Log.Error(
                    "Invalid message bundle: message length {Length} at offset {Offset} of {Total} does not fit the remaining {Remaining} bytes; dropping the rest of the bundle",
                    len,
                    offset,
                    data.Length,
                    msgData.Length);
                return;
            }
            var rawMsgType = BinaryPrimitives.ReadInt16BigEndian(msgData[4..]);
            var isCompressed = (rawMsgType & 0x8000) != 0;
            var msgType = (MsgTypeId)(rawMsgType & 0x7FFF);
            var msgPayload = msgData.Slice(6, (int)len - 6);
            offset += (int)len;

            switch (msgType)
            {
                case MsgTypeId.Notify:
                    ParseNotify(msgPayload, isCompressed, lastPacketTime);
                    break;
                case MsgTypeId.FrameDown:
                    ParseFrameDown(msgPayload, isCompressed, lastPacketTime);
                    break;
                case MsgTypeId.Call:
                    ParseCall(msgPayload, isCompressed, lastPacketTime);
                    break;
                case MsgTypeId.Return:
                    ParseReturn(msgPayload, isCompressed, lastPacketTime);
                    break;
                case MsgTypeId.FrameUp:
                    ParseFrameUp(msgPayload, isCompressed, lastPacketTime);
                    break;
                case MsgTypeId.None:
                case MsgTypeId.Echo:
                case MsgTypeId.UNK1:
                case MsgTypeId.UNK2:
                    break;
                default:
                    Log.Information("Got an unknown message type: {msgType}", msgType);
                    break;
            }
        }
    }

    private void ParseFrameDown(ReadOnlySpan<byte> data, bool isCompressed, DateTime lastPacketTime)
    {
        var seqNum = BinaryPrimitives.ReadUInt32BigEndian(data);

        if (isCompressed)
        {
            var decompressed = Decompress(data[4..]);
            if (!decompressed.IsEmpty)
            {
                ParsePacket(decompressed, lastPacketTime);
            }
        }
        else
        {
            ParsePacket(data[4..], lastPacketTime);
        }
    }

    private void ParseNotify(ReadOnlySpan<byte> data, bool isCompressed, DateTime lastPacketTime)
    {
        var serviceUuid = BinaryPrimitives.ReadUInt64BigEndian(data);
        var stubId = BinaryPrimitives.ReadUInt32BigEndian(data[8..]);
        var methodId = BinaryPrimitives.ReadUInt32BigEndian(data[12..]);

        var msgData = data[16..];
        if (isCompressed)
        {
            msgData = Decompress(msgData);

            if (msgData.IsEmpty)
            {
                Log.Logger.Warning("Error decompressing data for {serviceUuid}, {stubId}, {methodId}", serviceUuid, stubId, methodId);
                return;
            }
        }

        if (!Enum.IsDefined(typeof(EServiceId), serviceUuid))
        {
            Log.Logger.Information($"Unknown ServiceId = {serviceUuid} MethodId = {methodId}");
        }

        Observe(new ObservedMessage(ObservedMessageKind.Notify, serviceUuid, methodId, 0, msgData, lastPacketTime));

        var id = new NotifyId(serviceUuid, methodId);
        var hasNotifyHandler = NotifyHandlers.TryGetValue(id, out var handler);
        var isNotifyAllowed = NotifyGate is null || NotifyGate(serviceUuid, methodId);
        if (hasNotifyHandler && isNotifyAllowed)
        {
            var extraData = new ExtraPacketData(lastPacketTime);
            try
            {
                handler!(msgData, extraData);
            }
            catch (Exception ex)
            {
                LogFailureAndContinue($"notify service={serviceUuid} method={methodId}", ex);
            }
        }

    }


    private void ParseCall(ReadOnlySpan<byte> data, bool isCompressed, DateTime lastPacketTime)
    {
        if (data.Length < 20)
        {
            return;
        }

        var proxyServiceId = BinaryPrimitives.ReadUInt64BigEndian(data);
        var returnUid = BinaryPrimitives.ReadUInt32BigEndian(data[12..]);
        var proxyMethodId = BinaryPrimitives.ReadUInt32BigEndian(data[16..]);
        var msgData = data[20..];

        var id = new ProxyId((uint)proxyServiceId, proxyMethodId);
        ProxyReturnsDictionary.AddOrUpdate(returnUid, id, (_, _) => id);
        DispatchProxyCall(id, msgData, returnUid, lastPacketTime);
    }

    private void ParseFrameUp(ReadOnlySpan<byte> data, bool isCompressed, DateTime lastPacketTime)
    {
        if (data.Length < 4)
        {
            return;
        }

        var offset = 4;
        while (offset < data.Length)
        {
            if (data.Length - offset < 18)
            {
                return;
            }

            var length = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
            if (length < 18
                || length > int.MaxValue
                || length > data.Length - offset)
            {
                return;
            }

            var endPos = offset + (int)length;
            if (endPos > data.Length)
            {
                return;
            }

            offset += 4;
            var flags = BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
            offset += 2;
            offset += 4;
            var proxyServiceId = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
            offset += 4;

            uint returnUid;
            uint proxyMethodId;
            if (flags == 2)
            {
                if (endPos - offset < 8)
                {
                    return;
                }

                returnUid = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
                offset += 4;
                proxyMethodId = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
                offset += 4;
            }
            else
            {
                if (endPos - offset < 12)
                {
                    return;
                }

                offset += 4;
                returnUid = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
                offset += 4;
                proxyMethodId = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
                offset += 4;
            }

            var msgData = data[offset..endPos];
            var id = new ProxyId(proxyServiceId, proxyMethodId);
            if (flags != 2)
            {
                ProxyReturnsDictionary.AddOrUpdate(returnUid, id, (_, _) => id);
            }

            DispatchProxyCall(id, msgData, returnUid, lastPacketTime);
            offset = endPos;
        }
    }

    private void ParseReturn(ReadOnlySpan<byte> data, bool isCompressed, DateTime lastPacketTime)
    {
        if (data.Length < 12)
        {
            return;
        }

        var returnUid = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
        var msgData = data[12..];
        if (isCompressed)
        {
            msgData = Decompress(msgData);
            if (msgData.IsEmpty)
            {
                return;
            }
        }

        if (!ProxyReturnsDictionary.TryRemove(returnUid, out var id))
        {
            return;
        }

        Observe(new ObservedMessage(ObservedMessageKind.Return, id.ServiceId, id.MethodId, returnUid, msgData, lastPacketTime));

        var hasReturnHandler = ProxyReturnHandlers.TryGetValue(id, out var handler);
        var isReturnAllowed = ProxyGate is null || ProxyGate(id.ServiceId, id.MethodId);
        var extraData = new ExtraPacketData(lastPacketTime);
        if (hasReturnHandler && isReturnAllowed)
        {
            try
            {
                // 応答は呼んだメソッドの包み(サービスの X_Ret { ret })のまま渡す。受け口が包みとして読む。
                handler!(msgData, returnUid, extraData);
            }
            catch (Exception ex)
            {
                LogFailureAndContinue($"return service={id.ServiceId} method={id.MethodId}", ex);
            }
        }
    }

    private void DispatchProxyCall(
        ProxyId id,
        ReadOnlySpan<byte> data,
        uint returnUid,
        DateTime lastPacketTime)
    {
        Observe(new ObservedMessage(ObservedMessageKind.Call, id.ServiceId, id.MethodId, returnUid, data, lastPacketTime));

        var extraData = new ExtraPacketData(lastPacketTime);
        var hasProxyHandler = ProxyHandlers.TryGetValue(id, out var handler);
        var isProxyAllowed = ProxyGate is null || ProxyGate(id.ServiceId, id.MethodId);
        if (hasProxyHandler && isProxyAllowed)
        {
            try
            {
                handler!(data, returnUid, extraData);
            }
            catch (Exception ex)
            {
                LogFailureAndContinue($"call service={id.ServiceId} method={id.MethodId}", ex);
            }
        }
    }

    private void Observe(ObservedMessage message)
    {
        if (MessageObserved is not { } observer)
        {
            return;
        }

        try
        {
            observer(message);
        }
        catch (Exception ex)
        {
            // のぞく側の失敗で本体の処理を止めない。
            LogFailureAndContinue($"message observer kind={message.Kind} service={message.ServiceId} method={message.MethodId}", ex);
        }
    }

    /// <summary>
    /// 処理の途中で出た例外を、見える形で書く。<b>その1件だけ捨てて、処理は続ける。</b>
    ///
    /// <para>
    /// 投げ直すとパケット処理のタスクがそこで終わり、以後の通知が一切処理されなくなる。
    /// 表示が更新されなくなるのに、ログには何も残らない(実際にそうなっていた)。
    /// </para>
    ///
    /// <para>
    /// 握り潰しにはしない。<c>Log.Error</c> はアプリのログ画面(<c>ManagerLogSink</c>)に出る。
    /// ログが埋まらないよう、同じ場所・同じ種類の例外は最初の1件の後は <see cref="FailureLogInterval"/> に1件だけ書き、
    /// その間の件数を添える。
    /// </para>
    /// </summary>
    private void LogFailureAndContinue(string where, Exception exception)
    {
        var key = where + "|" + exception.GetType().FullName;
        int suppressed;
        lock (FailureLogSync)
        {
            var now = DateTime.Now;
            if (FailureLogState.TryGetValue(key, out var state))
            {
                if (now - state.LastLogged < FailureLogInterval)
                {
                    FailureLogState[key] = (state.LastLogged, state.Suppressed + 1);
                    return;
                }

                suppressed = state.Suppressed;
            }
            else
            {
                suppressed = 0;
            }

            FailureLogState[key] = (now, 0);
        }

        if (suppressed > 0)
        {
            Log.Error(exception, "Exception in {Where}. Dropping this one and continuing ({Suppressed} identical exceptions since the last log)", where, suppressed);
        }
        else
        {
            Log.Error(exception, "Exception in {Where}. Dropping this one and continuing", where);
        }
    }

    private ReadOnlySpan<byte> Decompress(ReadOnlySpan<byte> data)
    {
        try
        {
            var decompressedLen = _decompressor.Unwrap(data, DecompressionScratchBuffer.AsSpan());
            return DecompressionScratchBuffer.AsSpan()[..decompressedLen];
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error decompressing data of Len: {Len}, DecompressionScratchBuffer Size: {ScratchSize}", data.Length, DecompressionScratchBuffer.Length);
            return [];
        }
    }

    /// <summary>ゲームの接続なら、その接続を持つ実行ファイルの名前。ゲームの接続でなければ null。</summary>
    private string? FindGameExeName(IPv4Packet ip, TcpPacket tcp)
    {
        var conns = Utils.GetTCPConnectionsForExe(Config.ExeNames);
        var gameConnection = conns.FirstOrDefault((x =>
            (x.Row.LocalAddress == ip.SourceAddress.ToString() && x.Row.LocalPort == tcp.SourcePort) ||
            (x.Row.RemoteAddress == ip.SourceAddress.ToString() && x.Row.RemotePort == tcp.SourcePort) ||
            (x.Row.LocalAddress == ip.DestinationAddress.ToString() && x.Row.LocalPort == tcp.DestinationPort) ||
            (x.Row.RemoteAddress == ip.DestinationAddress.ToString() && x.Row.RemotePort == tcp.DestinationPort)));
        var exeName = gameConnection.ExeName;

        Log.Logger.Debug($"Checking {ip.SourceAddress}:{tcp.SourcePort} > {ip.DestinationAddress}:{tcp.DestinationPort} is game connection: {exeName is not null}");

        return exeName;
    }

    /// <summary>
    /// 止める。キャプチャを止め、組み立ての接続を全部閉じ、待ち行列の残りを処理し終えてから戻る。
    ///
    /// <para>
    /// 停止は UI スレッドから呼ばれる(アプリの終了・アダプターの替え)。処理の経路で UI スレッドへ同期で戻すと、ここで止まる。
    /// SharpPcap の <c>StopCapture</c> はキャプチャのスレッドの終わりを長く待たないので、戻った後に届くパケットは組み立てが無視する。
    /// 統計は閉じた後には読めないので、閉じる前に控える。
    /// </para>
    /// </summary>
    public void Stop()
    {
        // 先に印を下ろして、これ以後の命令は呼び手のスレッドで実行させる(閉じた待ち行列に積まない)。
        lock (_queueGate)
        {
            if (!_isStarted)
            {
                return;
            }

            _isStarted = false;
        }

        CaptureDevice.StopCapture();
        var statistics = DescribeCaptureStatistics();
        var summary = _tcpReassembler.Stop(DateTime.UtcNow);
        _workQueue.CompleteAdding();
        var pending = _workQueue.Count;
        Log.Information("Capture stopped; processing {Pending} queued messages and commands before closing the device", pending);
        PacketParseTask.Wait();
        Log.Information(
            "Capture stopped: closed {Connections} TCP connections, processed {Pending} queued messages and commands, ignored {LatePure} empty and {LatePayload} data packets for closed connections and {WithoutConnection} packets without a connection; {Capture}",
            summary.ClosedConnections,
            pending,
            summary.LatePureAfterClose,
            summary.LatePayloadAfterClose,
            summary.IgnoredWithoutConnection,
            statistics);

        CaptureDevice.Close();
        ConnectionFilters.Clear();
    }

    private string DescribeCaptureStatistics()
    {
        var statistics = CaptureDevice.Statistics;
        return statistics is null
            ? "capture statistics unavailable"
            : $"capture received {statistics.ReceivedPackets}, dropped {statistics.DroppedPackets}, interface dropped {statistics.InterfaceDroppedPackets}";
    }

    private ICaptureDevice GetCaptureDevice()
    {
        var devices = CaptureDeviceList.Instance;

        try
        {
            foreach (var liveDevice in devices)
            {
                var dev = (LibPcapLiveDevice)liveDevice;
                if (dev.Name == Config.CaptureDeviceName)
                {
                    Log.Information("Matched capture device: {DeviceName}, {FriendlyName}", dev.Name, dev.Interface?.FriendlyName);
                    return dev;
                }
            }

            Log.Information("No matched capture device, trying to find Ethernet");
            var ethernet = devices.FirstOrDefault(x => ((LibPcapLiveDevice)x).Interface?.FriendlyName == "Ethernet");
            if (ethernet != null)
            {
                Log.Information("Found Ethernet named capture device, using it: {DeviceName}, {FriendlyName}", ethernet.Name, ((LibPcapLiveDevice)ethernet).Interface?.FriendlyName);
                return ethernet;
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Error getting capture device");
            throw;
        }

        var device = devices[0];
        Log.Information("No matched capture device, using first found: {DeviceName}, {FriendlyName}", device.Name, ((LibPcapLiveDevice)device).Interface?.FriendlyName);
        return device;
    }
}
