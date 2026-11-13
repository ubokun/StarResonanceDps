// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Google.Protobuf;
using Serilog;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>チャットのメッセージ1件(<see cref="ChatMessageStore"/>)。</summary>
/// <param name="MessageId">メッセージの番号(<c>msgId</c>)。チャンネル(ワールドはその中のチャンネル)とプライベートトークの相手ごとの通し番号で、通知と記録で同じ。</param>
/// <param name="SenderId">発言者のキャラID。</param>
/// <param name="SenderLevel">発言者のレベル(<c>sendCharInfo.level</c>)。届いていなければ 0。</param>
/// <param name="TargetId">メッセージの相手のキャラID(<c>msgInfo.targetId</c>)。プライベートトークで自分が送った分は、これが相手。</param>
/// <param name="SentAt">送られた時刻。通信の時刻が 0 なら null。</param>
/// <param name="Text">本文(<c>msgText</c>)。</param>
/// <param name="VoiceText">ボイスの文字起こし。ボイスでなければ空。</param>
/// <param name="PictureEmojiConfigId">スタンプの番号(<c>pictureEmoji.configId</c>、顔文字・スタンプの表の Id)。スタンプでなければ 0。</param>
/// <param name="Hyperlink">リンクの発言の中身(<c>chatHypertext</c>)。リンクの発言でないか、中身が届いていなければ null。</param>
public sealed record ChatMessageRecord(
    ChitChatChannelType Channel,
    long MessageId,
    long SenderId,
    string SenderName,
    int SenderLevel,
    long TargetId,
    DateTimeOffset? SentAt,
    ChitChatMsgType MessageType,
    string Text,
    string VoiceText,
    uint PictureEmojiConfigId,
    ChatHyperlinkRecord? Hyperlink);

/// <summary>
/// リンクの発言の中身。本文は空で届き、ゲームは表 <c>ChatHyperlink</c> の文の型をこの値で埋めて出す。
/// </summary>
/// <param name="ConfigId">表 <c>ChatHyperlink</c> の番号。</param>
/// <param name="Values">差し込みの値。届いた順。</param>
public sealed record ChatHyperlinkRecord(uint ConfigId, IReadOnlyList<ChatHyperlinkValue> Values);

/// <summary>リンクの発言の差し込みの値1つ。</summary>
/// <param name="Value">種類ごとに解いた値(<c>PlaceHolderStr</c> など)。アプリが文に使わない種類と、解けなかったときは null。</param>
public sealed record ChatHyperlinkValue(PlaceHolderType Type, IMessage? Value);

/// <summary>チャットの窓が読む相手。プライベートトークは相手のキャラID、ほかのチャンネルは 0。</summary>
public readonly record struct ChatTarget(ChitChatChannelType Channel, long PartnerId);

/// <summary>チャットの発言者の名前とレベル(その人の一番新しいメッセージの発言者の情報)。</summary>
public sealed record ChatSenderIdentity(string Name, int Level);

/// <summary>プライベートトークの相手1人。</summary>
/// <param name="Name">相手が書いたメッセージの発言者名、無ければ名刺照会の名前。どちらも無ければ空。</param>
public sealed record ChatPrivatePartner(long CharacterId, string Name);

/// <summary>プライベートトークのメッセージ1件の相手(キャラID、決まらなければ 0)と、自分の発言か(<see cref="ChatMessageStore.ResolvePrivateMessage"/>)。</summary>
public readonly record struct ChatPrivateMessageSide(long PartnerId, bool IsFromSelf);

/// <summary>窓が前回の続きから読んだ分。</summary>
/// <param name="Revision">一覧の版の番号。一覧を空にしたときと、末尾より前に足したときに変わる。窓の知っている番号と違えば、<paramref name="Messages"/> は最初からの分。</param>
public sealed record ChatMessageSlice(int Revision, IReadOnlyList<ChatMessageRecord> Messages, int NextIndex);

/// <summary>
/// チャットのメッセージの控え。ワールド・Ch・パーティ・ギルド・初心者はチャンネルごと、プライベートトークは相手ごとに、送られた時刻の順に持つ。
/// ほかのチャンネルは持たない。
///
/// <para>
/// 入口は2つ。新しいメッセージの通知 <c>ChitChatNtf.NotifyNewestChitChatMsgs</c>(自分の発言も届く)と、過去の記録の応答 <c>GetChipChatRecords</c>。
/// 同じメッセージは通知と記録で同じ番号なので、一覧の中で同じ番号は1件にする。
/// </para>
///
/// <para>
/// <b>一覧はゲームの一覧と同じ中身にそろえる。</b>ゲームは一覧を空にしてから最新の記録を読み直す
/// (ログインの後・ワールドと初心者のチャンネルの切り替え・プライベートトークを初めて開いたとき。要求の起点が 0)。
/// その要求で <see cref="BeginRecordLoad"/> が一覧を空にし、応答の記録を <see cref="AddRecords"/> が足す。
/// プライベートトークの相手の一覧も、ゲームと同じく応答の中身に置き換える(<see cref="ReplacePrivatePartners"/>)。
/// </para>
///
/// <para>
/// プライベートトークの通知は届いた時点で相手に振り分ける(<see cref="ResolvePrivateMessage"/>)。記録は要求の相手に入れる。
/// </para>
///
/// <para>
/// 件数の上限は持たない(ゲームも受け取った分を一覧に足すときに上限を渡していない)。
/// 消すのはログアウトだけで、キャプチャの停止とマップ移動では消さない(記録は古くならず、止めていた間の分が抜けるだけ)。
/// </para>
/// </summary>
public static class ChatMessageStore
{
    private static readonly object StateLock = new();
    private static readonly Dictionary<ChitChatChannelType, MessageList> ChannelMessages = [];
    private static readonly Dictionary<long, PrivateSession> PrivateSessions = [];
    private static readonly HashSet<long> OwnPrivateSendTargets = [];
    private static readonly Dictionary<long, (ChatSenderIdentity Identity, DateTimeOffset? SentAt)> SenderIdentities = [];
    private static int _lastRevision;
    private static bool _hasLoginRecords;

    /// <summary>発言者の名前かレベルが変わった(<see cref="TryGetSenderIdentity"/>)。引数はキャラID。受信のスレッドから上がる。</summary>
    public static event Action<long>? SenderIdentityChanged;

    /// <summary>
    /// その人の発言者の名前とレベル。控えているメッセージのうち、送られた時刻が一番新しいものの値(後から届いた古い記録では置き換えない)。
    /// メッセージが無ければ偽。
    /// </summary>
    public static bool TryGetSenderIdentity(long characterId, out ChatSenderIdentity identity)
    {
        lock (StateLock)
        {
            if (SenderIdentities.TryGetValue(characterId, out var entry))
            {
                identity = entry.Identity;
                return true;
            }
        }

        identity = null!;
        return false;
    }

    /// <summary>控えるチャンネルか(ワールド・Ch・パーティ・ギルド・初心者・プライベートトーク)。</summary>
    public static bool IsKeptChannel(ChitChatChannelType channel)
    {
        return channel is ChitChatChannelType.ChannelWorld
            or ChitChatChannelType.ChannelScene
            or ChitChatChannelType.ChannelTeam
            or ChitChatChannelType.ChannelUnion
            or ChitChatChannelType.ChannelNewbie
            or ChitChatChannelType.ChannelPrivate;
    }

    /// <summary>
    /// その窓の過去のチャットを受け取ったか。ログインの後の過去のチャット(プライベートトークの相手の一覧の応答)を受けたか、
    /// その窓の一覧を最新の分から読み直した応答を受けたとき(ワールドと初心者のチャンネルの切り替え・プライベートトークを開いたとき)に真。
    /// どちらもまだ届いていなければ偽(起動の直後とログインの直後も)。欠けかどうかは、接続を途中から読み始めたかと合わせて呼ぶ側が決める。
    /// </summary>
    public static bool HasPastChat(ChatTarget target)
    {
        lock (StateLock)
        {
            return _hasLoginRecords || FindListLocked(target)?.HasLatestRecords == true;
        }
    }

    /// <summary>自分がプライベートトークを送った(上りの送信の要求)。相手を控える(<see cref="ResolvePrivateMessage"/>)。受信のスレッドから呼ぶ。</summary>
    public static void AddOwnPrivateSend(long targetId)
    {
        if (targetId == 0)
        {
            return;
        }

        lock (StateLock)
        {
            OwnPrivateSendTargets.Add(targetId);
        }
    }

    /// <summary>
    /// プライベートトークのメッセージの相手と、自分の発言か。
    /// 自分のキャラIDが分かっていればゲームのクライアントと同じ決め方(発言者が自分なら <c>msgInfo.targetId</c>、それ以外は発言者)。
    /// 分からないうちは、targetId が自分の送った相手(<see cref="AddOwnPrivateSend"/>)にあれば自分の発言(相手は targetId)、無ければほかの人の発言(相手は発言者)。
    /// ほかの人から届いたメッセージの targetId はいつも自分で、自分の送った相手に自分は入らない。自分の発言の通知は、必ずその送信の要求の後に届く。
    /// </summary>
    public static ChatPrivateMessageSide ResolvePrivateMessage(long senderId, long targetId)
    {
        lock (StateLock)
        {
            return ResolvePrivateMessageLocked(senderId, targetId);
        }
    }

    /// <summary>通知のメッセージを控える。受信のスレッドから呼ぶ。</summary>
    public static void Add(ChitChatChannelType channel, ChitChatMsg message)
    {
        if (!IsKeptChannel(channel))
        {
            return;
        }

        var record = ToRecord(channel, message);
        var identityChanged = false;
        lock (StateLock)
        {
            if (channel == ChitChatChannelType.ChannelPrivate)
            {
                var side = ResolvePrivateMessageLocked(record.SenderId, record.TargetId);
                if (side.PartnerId == 0)
                {
                    Log.Warning(
                        "Private chat message has no partner. Sender: {SenderId}, Target: {TargetId}",
                        record.SenderId,
                        record.TargetId);
                    return;
                }

                GetOrCreateSessionLocked(side.PartnerId).Insert(record);
            }
            else
            {
                GetOrCreateChannelListLocked(channel).Insert(record);
            }

            identityChanged = RememberSenderLocked(record);
        }

        if (identityChanged)
        {
            SenderIdentityChanged?.Invoke(record.SenderId);
        }
    }

    /// <summary>
    /// 起点 0 の記録の要求(ゲームが一覧を空にしてから最新の記録を読むとき)。こちらの一覧も空にする。受信のスレッドから呼ぶ。
    /// </summary>
    public static void BeginRecordLoad(ChatTarget target)
    {
        if (!IsKeptChannel(target.Channel))
        {
            return;
        }

        lock (StateLock)
        {
            if (target.Channel == ChitChatChannelType.ChannelPrivate)
            {
                GetOrCreateSessionLocked(target.PartnerId).Messages.Clear();
                return;
            }

            GetOrCreateChannelListLocked(target.Channel).Clear();
        }
    }

    /// <summary>記録の応答のメッセージを足す。同じ番号がもうあれば足さない。受信のスレッドから呼ぶ。</summary>
    /// <param name="isLatestLoad">最新の分から読み直した要求(起点 0)の応答。その窓の過去のチャットを受け取ったことにする(<see cref="HasPastChat"/>)。</param>
    public static void AddRecords(ChatTarget target, IEnumerable<ChitChatMsg> messages, bool isLatestLoad)
    {
        if (!IsKeptChannel(target.Channel))
        {
            return;
        }

        var changedSenderIds = new HashSet<long>();
        lock (StateLock)
        {
            MessageList list;
            if (target.Channel == ChitChatChannelType.ChannelPrivate)
            {
                var session = GetOrCreateSessionLocked(target.PartnerId);
                foreach (var message in messages)
                {
                    var record = ToRecord(target.Channel, message);
                    session.Insert(record);
                    if (RememberSenderLocked(record))
                    {
                        changedSenderIds.Add(record.SenderId);
                    }
                }

                list = session.Messages;
            }
            else
            {
                list = GetOrCreateChannelListLocked(target.Channel);
                foreach (var message in messages)
                {
                    var record = ToRecord(target.Channel, message);
                    list.Insert(record);
                    if (RememberSenderLocked(record))
                    {
                        changedSenderIds.Add(record.SenderId);
                    }
                }
            }

            if (isLatestLoad)
            {
                list.HasLatestRecords = true;
            }
        }

        RaiseSenderIdentityChanged(changedSenderIds);
    }

    /// <summary>
    /// プライベートトークの相手の一覧の応答。ゲームと同じく、相手をこの一覧に置き換え、中身は相手ごとの最後の1件だけにする
    /// (会話の全文は、その相手を開いたときの記録で届く)。受信のスレッドから呼ぶ。
    /// </summary>
    public static void ReplacePrivatePartners(IEnumerable<PrivateChatTargetInfo> targets)
    {
        var changedSenderIds = new HashSet<long>();
        lock (StateLock)
        {
            PrivateSessions.Clear();
            foreach (var target in targets)
            {
                if (target.CharId == 0)
                {
                    Log.Warning("Private chat target list has an entry without a character ID");
                    continue;
                }

                var session = GetOrCreateSessionLocked(target.CharId);
                if (target.LatestMsg is { } latest)
                {
                    var record = ToRecord(ChitChatChannelType.ChannelPrivate, latest);
                    session.Insert(record);
                    if (RememberSenderLocked(record))
                    {
                        changedSenderIds.Add(record.SenderId);
                    }
                }
            }

            _hasLoginRecords = true;
        }

        RaiseSenderIdentityChanged(changedSenderIds);
    }

    /// <summary>
    /// 相手のメッセージのうち、<paramref name="startIndex"/> 番目から後。<paramref name="knownRevision"/> が今の番号と違えば
    /// (読んだ後に一覧を空にしたか、前に足した)最初から返す。
    /// </summary>
    public static ChatMessageSlice GetMessages(ChatTarget target, int knownRevision, int startIndex)
    {
        lock (StateLock)
        {
            var list = FindListLocked(target);
            if (list is null)
            {
                return new ChatMessageSlice(0, [], 0);
            }

            var start = knownRevision == list.Revision ? startIndex : 0;
            if (start >= list.Items.Count)
            {
                return new ChatMessageSlice(list.Revision, [], start);
            }

            return new ChatMessageSlice(list.Revision, list.Items.GetRange(start, list.Items.Count - start), list.Items.Count);
        }
    }

    /// <summary>プライベートトークの相手。最後のメッセージの時刻が新しい順。</summary>
    public static IReadOnlyList<ChatPrivatePartner> GetPrivatePartners()
    {
        lock (StateLock)
        {
            return [.. PrivateSessions
                .OrderByDescending(pair => pair.Value.LastMessageAt ?? DateTimeOffset.MinValue)
                .ThenBy(pair => pair.Key)
                .Select(pair => new ChatPrivatePartner(pair.Key, ResolvePartnerName(pair.Key, pair.Value)))];
        }
    }

    /// <summary>プライベートトークの相手の名前。相手の発言の名前、無ければ名刺照会の名前。どちらも無ければ空。</summary>
    public static string GetPrivatePartnerName(long characterId)
    {
        lock (StateLock)
        {
            return ResolvePartnerName(characterId, PrivateSessions.GetValueOrDefault(characterId));
        }
    }

    /// <summary>起動時の値に戻す(ログアウト)。</summary>
    public static void ResetToStartup()
    {
        lock (StateLock)
        {
            ChannelMessages.Clear();
            PrivateSessions.Clear();
            OwnPrivateSendTargets.Clear();
            SenderIdentities.Clear();
            _hasLoginRecords = false;
        }
    }

    /// <summary>メッセージの発言者の名前とレベルを控える。前に控えた値より古いメッセージでは置き換えない。値が変わったら真。</summary>
    private static bool RememberSenderLocked(ChatMessageRecord record)
    {
        if (record.SenderId <= 0)
        {
            return false;
        }

        var identity = new ChatSenderIdentity(record.SenderName, record.SenderLevel);
        if (SenderIdentities.TryGetValue(record.SenderId, out var existing))
        {
            if (record.SentAt < existing.SentAt || existing.Identity == identity)
            {
                return false;
            }
        }

        SenderIdentities[record.SenderId] = (identity, record.SentAt);
        return true;
    }

    private static void RaiseSenderIdentityChanged(IEnumerable<long> senderIds)
    {
        foreach (var senderId in senderIds)
        {
            SenderIdentityChanged?.Invoke(senderId);
        }
    }

    private static ChatMessageRecord ToRecord(ChitChatChannelType channel, ChitChatMsg message)
    {
        var info = message.MsgInfo;
        return new ChatMessageRecord(
            channel,
            message.MsgId,
            message.SendCharInfo?.CharID ?? 0,
            message.SendCharInfo?.Name ?? string.Empty,
            message.SendCharInfo?.Level ?? 0,
            info?.TargetId ?? 0,
            message.Timestamp > 0 ? DateTimeOffset.FromUnixTimeSeconds(message.Timestamp) : null,
            info?.MsgType ?? ChitChatMsgType.ChatMsgTextMessage,
            info?.MsgText ?? string.Empty,
            info?.Voice?.Text ?? string.Empty,
            info?.PictureEmoji?.ConfigId ?? 0,
            info is { MsgType: ChitChatMsgType.ChatMsgHypertext, ChatHypertext: { } hypertext } ? ToHyperlinkRecord(hypertext) : null);
    }

    private static ChatHyperlinkRecord ToHyperlinkRecord(ChatHypertext hypertext)
    {
        var values = new List<ChatHyperlinkValue>(hypertext.HypertextContents.Count);
        foreach (var content in hypertext.HypertextContents)
        {
            values.Add(new ChatHyperlinkValue(content.Type, ParseHyperlinkValue(hypertext.ConfigId, content)));
        }

        return new ChatHyperlinkRecord(hypertext.ConfigId, values);
    }

    /// <summary>
    /// 差し込みの値を種類ごとに解く。アプリが文に使わない種類(ギルドの集まり・家・設計図など)は null。
    /// 解けなかった値も null にしてログに残す(その発言はリンクの文字で出る)。
    /// </summary>
    private static IMessage? ParseHyperlinkValue(uint configId, PlaceHolder content)
    {
        var bytes = content.BytesContent;
        try
        {
            return content.Type switch
            {
                PlaceHolderType.Val => PlaceHolderVal.Parser.ParseFrom(bytes),
                PlaceHolderType.Player => PlaceHolderPlayer.Parser.ParseFrom(bytes),
                PlaceHolderType.Item => PlaceHolderItem.Parser.ParseFrom(bytes),
                PlaceHolderType.Union => PlaceHolderUnion.Parser.ParseFrom(bytes),
                PlaceHolderType.Buff => PlaceHolderBuff.Parser.ParseFrom(bytes),
                PlaceHolderType.Timestamp => PlaceHolderTimestamp.Parser.ParseFrom(bytes),
                PlaceHolderType.String => PlaceHolderStr.Parser.ParseFrom(bytes),
                PlaceHolderType.FishPersonalTotal => PlaceHolderFishPersonalTotal.Parser.ParseFrom(bytes),
                PlaceHolderType.FishItem => PlaceHolderFishItem.Parser.ParseFrom(bytes),
                PlaceHolderType.FishRank => PlaceHolderFishRank.Parser.ParseFrom(bytes),
                PlaceHolderType.MasterMode => PlaceHolderMasterMode.Parser.ParseFrom(bytes),
                PlaceHolderType.ScenePosition => PlaceHolderScenePosition.Parser.ParseFrom(bytes),
                _ => null
            };
        }
        catch (InvalidProtocolBufferException ex)
        {
            Log.Warning(ex, "Chat hyperlink {ConfigId} has an unreadable {Type} value", configId, content.Type);
            return null;
        }
    }

    private static string ResolvePartnerName(long characterId, PrivateSession? session)
    {
        return session is not null && session.PartnerName.Length > 0
            ? session.PartnerName
            : SocialDataStore.GetName(characterId);
    }

    /// <summary>
    /// 一覧の版の番号を新しくする。番号は起動から増え続けるだけなので、作り直した一覧も前の番号と重ならない。
    /// 0 は「一覧が無い」の番号(<see cref="GetMessages"/>)。
    /// </summary>
    private static int NextRevision() => ++_lastRevision;

    private static MessageList? FindListLocked(ChatTarget target)
    {
        return target.Channel == ChitChatChannelType.ChannelPrivate
            ? PrivateSessions.GetValueOrDefault(target.PartnerId)?.Messages
            : ChannelMessages.GetValueOrDefault(target.Channel);
    }

    private static MessageList GetOrCreateChannelListLocked(ChitChatChannelType channel)
    {
        if (!ChannelMessages.TryGetValue(channel, out var list))
        {
            list = new MessageList();
            ChannelMessages.Add(channel, list);
        }

        return list;
    }

    private static PrivateSession GetOrCreateSessionLocked(long partnerId)
    {
        if (!PrivateSessions.TryGetValue(partnerId, out var session))
        {
            session = new PrivateSession(partnerId);
            PrivateSessions.Add(partnerId, session);
        }

        return session;
    }

    private static ChatPrivateMessageSide ResolvePrivateMessageLocked(long senderId, long targetId)
    {
        var selfId = AppState.PlayerUID;
        var isFromSelf = selfId != 0
            ? senderId == selfId
            : OwnPrivateSendTargets.Contains(targetId);
        return new ChatPrivateMessageSide(isFromSelf ? targetId : senderId, isFromSelf);
    }

    /// <summary>1つの窓の一覧。送られた時刻(同じなら番号)の順に持ち、同じ番号は1件にする。</summary>
    private sealed class MessageList
    {
        private readonly HashSet<long> _messageIds = [];

        public List<ChatMessageRecord> Items { get; } = [];

        public int Revision { get; private set; } = NextRevision();

        /// <summary>最新の分から読み直した記録の応答を受けた。ゲームがこの一覧を空にしたら(<see cref="Clear"/>)、次の応答まで偽に戻す。</summary>
        public bool HasLatestRecords { get; set; }

        /// <summary>順の位置に入れる。同じ番号がもうあれば入れずに偽。末尾より前に入れたら版を変える。</summary>
        public bool Insert(ChatMessageRecord record)
        {
            if (record.MessageId != 0 && !_messageIds.Add(record.MessageId))
            {
                return false;
            }

            var index = Items.Count;
            while (index > 0 && Compare(Items[index - 1], record) > 0)
            {
                index--;
            }

            Items.Insert(index, record);
            if (index < Items.Count - 1)
            {
                Revision = NextRevision();
            }

            return true;
        }

        public void Clear()
        {
            Items.Clear();
            _messageIds.Clear();
            HasLatestRecords = false;
            Revision = NextRevision();
        }

        private static int Compare(ChatMessageRecord left, ChatMessageRecord right)
        {
            var byTime = Nullable.Compare(left.SentAt, right.SentAt);
            return byTime != 0 ? byTime : left.MessageId.CompareTo(right.MessageId);
        }
    }

    private sealed class PrivateSession(long partnerId)
    {
        private DateTimeOffset? _partnerNameAt;

        public MessageList Messages { get; } = new();

        /// <summary>相手が書いたメッセージの発言者名(一番新しいもの)。</summary>
        public string PartnerName { get; private set; } = string.Empty;

        /// <summary>一番新しいメッセージの時刻。一覧を空にしても残す(相手の並びに使う)。</summary>
        public DateTimeOffset? LastMessageAt { get; private set; }

        public void Insert(ChatMessageRecord record)
        {
            if (!Messages.Insert(record))
            {
                return;
            }

            if (Nullable.Compare(record.SentAt, LastMessageAt) > 0)
            {
                LastMessageAt = record.SentAt;
            }

            if (record.SenderId == partnerId
                && record.SenderName.Length > 0
                && (PartnerName.Length == 0 || Nullable.Compare(record.SentAt, _partnerNameAt) >= 0))
            {
                PartnerName = record.SenderName;
                _partnerNameAt = record.SentAt;
            }
        }
    }
}
