using StarResonanceDps.Core.CombatRuntime.Protocols;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static Zproto.WorldNtfCsharp.Types;
using Zproto;
using Google.Protobuf.Collections;
using System.Numerics;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using StarResonanceDps.Core.Services;
using System.Collections.Concurrent;
using ZLinq;

namespace StarResonanceDps.Core.CombatRuntime
{
    public static class MessageManager
    {
        private const double OriginEnergyRawScale = 100d;
        private const string CurrentStaminaSnapshotAttribute = "CurrentStaminaSnapshot";
        private const string MaxStaminaSnapshotAttribute = "MaxStaminaSnapshot";
        private const uint WorldProxyServiceId = 103198054;
        private const uint GetTeamInfoMethodId = 0x4C01F;
        private const uint GetSocialDataMethodId = 0x47065;
        private const uint UseSlotMethodId = 0x3D002;
        private const uint ChitChatProxyServiceId = 1321197368;
        private const uint SendChitChatMsgMethodId = 1;
        private const uint GetChipChatRecordsMethodId = 2;
        private const uint GetPrivateChatTargetsMethodId = 3;
        private const uint SetPrivateChatHasReadMethodId = 6;

        public static NetCap? netCap = null;
        public static string NetCaptureDeviceName = "";
        public static EGameCapturePreference GameCapturePreference = EGameCapturePreference.Auto;
        public static string GameCaptureCustomExeName = "";

        public static void InitializeCapturing()
        {
            if (NetCaptureDeviceName == null)
            {
                throw new InvalidOperationException();
            }

            GrpcTeamManager.ResetMemberState();

            netCap = new NetCap();
            netCap.Init(new NetCapConfig()
            {
                CaptureDeviceName = NetCaptureDeviceName,
                ExeNames = Utils.GameCapturePreferenceToExeNames(GameCapturePreference, GameCaptureCustomExeName)
            });

            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.EnterScene, ProcessEnterScene);

            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncContainerData, ProcessSyncContainerData);
            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncContainerDirtyData, ProcessSyncContainerDirtyData);
            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncSeason, ProcessSyncSeason);

            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncNearDeltaInfo, ProcessSyncNearDeltaInfo);

            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncServerTime, ProcessSyncServerTime);
            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncToMeDeltaInfo, ProcessSyncToMeDeltaInfo);

            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncNearEntities, ProcessSyncNearEntities);

            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncSceneEvents, ProcessSyncSceneEvents);

            netCap.RegisterNotifyHandler(936649811, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldActivityNtf.SyncHitInfo, ProcessSyncHitInfo);

            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncDungeonData, ProcessSyncDungeonData);

            netCap.RegisterWorldNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncDungeonDirtyData, ProcessSyncDungeonDirtyData);

            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NoticeUpdateTeamInfo, ProcessNoticeUpdateTeamInfo);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NoticeUpdateTeamMemberInfo, ProcessNoticeUpdateTeamMemberInfo);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyJoinTeam, ProcessNotifyJoinTeam);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyLeaveTeam, ProcessNotifyLeaveTeam);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyTeamGroupUpdate, ProcessNotifyTeamGroupUpdate);

            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyBeTransferLeader, ProcessNotifyBeTransferLeader);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NoticeTeamDissolve, ProcessNoticeTeamDissolve);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyTeamActivityState, ProcessNotifyTeamActivityState);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.TeamActivityResult, ProcessTeamActivityResult);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.TeamActivityListResult, ProcessTeamActivityListResult);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.TeamActivityVoteResult, ProcessTeamActivityVoteResult);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyCharMatchResult, ProcessNotifyCharMatchResult);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyTeamMatchResult, ProcessNotifyTeamMatchResult);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyCharAbortMatch, ProcessNotifyCharAbortMatch);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.UpdateTeamMemBeCall, ProcessUpdateTeamMemBeCall);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyTeamMemBeCall, ProcessNotifyTeamMemBeCall);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyTeamMemBeCallResult, ProcessNotifyTeamMemBeCallResult);
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcTeamNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcTeamNtf.NotifyTeamEnterErr, ProcessNotifyTeamEnterErr);

            netCap.RegisterNotifyHandler((ulong)EServiceId.WorldActNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldActNtf.SyncWorldActData, ProcessSyncWorldActData);

            netCap.RegisterNotifyHandler((ulong)EServiceId.SocialNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.SocialNtf.NotifySocialData, ProcessNotifySocialData);

            netCap.RegisterNotifyHandler((ulong)EServiceId.ChitChatNtf, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.ChitChatNtf.NotifyNewestChitChatMsgs, ProcessNotifyNewestChitChatMsgs);

            // ログイン画面へ戻ると、サーバーから GrpcCharactor(通知の service も GrpcCharactor)の ExitGame が通知で届く。
            netCap.RegisterNotifyHandler((ulong)EServiceId.GrpcCharactor, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcCharactorNtf.ExitGame, ProcessExitGame);

            // 入り直し(キャラクター選択)。ログイン画面の門を開ける合図。
            netCap.RegisterProxyHandler((uint)EServiceId.GrpcCharactor, (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcCharactorNtf.SelectChar, ProcessSelectChar);

            // マッチング成立(承諾待ち)。通知の入場確認に使う。
            netCap.RegisterMatchNotifyHandler(StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.MatchNtf.EnterMatchResult, ProcessEnterMatchResult);

            netCap.NotifyGate = ShouldDispatchNotify;
            netCap.ProxyGate = ShouldDispatchProxy;

            // 予告のバーが終わった行を、そのあとに届いたパケットの中身より先にタイムラインへ残す。
            netCap.BeforeParsePacket = CustomCombatFeatures.BeforePacket;

            netCap.AfterParsePacket = ProcessAfterParsePacket;

            netCap.MidStreamConnectionOpened = GameReceptionStateStore.MarkMidStreamConnection;

            netCap.GameConnectionIdentified = GameServerStore.SetFromExeName;

            netCap.MessageObserved = message => MessageObserved?.Invoke(message);

            netCap.RegisterProxyReturnHandler(WorldProxyServiceId, GetTeamInfoMethodId, ProcessGetTeamInfoReturn);
            netCap.RegisterProxyReturnHandler(WorldProxyServiceId, GetSocialDataMethodId, ProcessGetSocialDataReturn);

            // 自分が技を使うときの上りの要求とその応答。自分の推移グラフの技の記録はここから取る。
            netCap.RegisterProxyHandler(WorldProxyServiceId, UseSlotMethodId, ProcessUseSlotCall);
            netCap.RegisterProxyReturnHandler(WorldProxyServiceId, UseSlotMethodId, ProcessUseSlotReturn);

            // 過去のチャットの記録と、プライベートトークの相手の一覧の要求と応答。
            netCap.RegisterProxyHandler(ChitChatProxyServiceId, GetChipChatRecordsMethodId, ProcessGetChipChatRecordsCall);
            netCap.RegisterProxyReturnHandler(ChitChatProxyServiceId, GetChipChatRecordsMethodId, ProcessGetChipChatRecordsReturn);
            netCap.RegisterProxyReturnHandler(ChitChatProxyServiceId, GetPrivateChatTargetsMethodId, ProcessGetPrivateChatTargetsReturn);

            // プライベートトークの既読の要求(通知「プライベートトーク」が、画面を開いている相手の新着を出さないために使う)。
            netCap.RegisterProxyHandler(ChitChatProxyServiceId, SetPrivateChatHasReadMethodId, ProcessSetPrivateChatHasReadCall);

            // チャットの送信の要求(自分のキャラIDが分からないうちに、届いたプライベートトークが自分の発言かを見分けるために使う)。
            netCap.RegisterProxyHandler(ChitChatProxyServiceId, SendChitChatMsgMethodId, ProcessSendChitChatMsgCall);

            netCap.Start();
            System.Diagnostics.Debug.WriteLine("MessageManager.InitializeCapturing : Capturing Started...");
        }

        /// <summary>
        /// エンカウンターを触る操作(計測の開始・停止、リセット)を、パケットを処理するスレッドで実行する
        /// (メッセージと同じ待ち行列に積むので、前に届いたメッセージを処理した後に走る)。
        /// キャプチャが動いていなければ、呼んだスレッドでそのまま実行する。
        /// </summary>
        public static void RunOnPacketThread(Action action)
        {
            if (netCap?.TryEnqueueCommand(action) == true)
            {
                return;
            }

            action();
        }

        /// <summary>
        /// パケットを1つ処理した後に呼ばれる。
        ///
        /// <para>
        /// 自分の能力値の変化を記録に入れ、記録が変わったら名簿の自分の行を作り直す(入場とバフの付け直しはパケットの単位で見分けるので、ここで締める)。
        /// 全滅を見つけたパケットなら、ここで回を区切る(<see cref="SplitEncounterOnPendingWipe"/>)。
        /// 保持していた前の回は、今の回に記録が入ったパケットの後で手放し、プレイヤーリストの名簿をメーターと同じ回へ作り直す。
        /// </para>
        /// </summary>
        private static void ProcessAfterParsePacket()
        {
            CustomCombatFeatures.RecordParty(EncounterManager.Current);
            if (SelfAttributeChangeStore.CommitPacket() && currentUserUuid != 0)
            {
                PlayerRosterProjection.UpsertSelf(currentUserUuid);
            }

            GameReceptionStateStore.Commit();
            SplitEncounterOnPendingWipe();
            EncounterManager.ReleaseHeldPastEncounter();
        }

        /// <summary>全滅で区切る回の番号。全滅を見つけたパケットを処理し終えたところで区切る(<see cref="SplitEncounterOnPendingWipe"/>)。</summary>
        private static ulong? _pendingWipeSplitEncounterId;

        /// <summary>
        /// 全滅を見つけたパケットの最後に、その回を区切る(設定「進行で自動リセット」がオンで、回に記録があるとき)。
        /// パケットの途中で回が作り直されていたら(マップ移動など)区切らない。
        /// </summary>
        private static void SplitEncounterOnPendingWipe()
        {
            if (_pendingWipeSplitEncounterId is not ulong encounterId)
            {
                return;
            }

            _pendingWipeSplitEncounterId = null;
            if (EncounterManager.Current.EncounterId != encounterId
                || !CombatRuntimeSettings.SplitEncountersOnNewPhases
                || !EncounterManager.Current.HasStatsBeenRecorded())
            {
                return;
            }

            EncounterManager.EnterDungeon(keepPastEncounterInMeter: true, force: false, reason: EncounterStartReason.Wipe);
        }

        /// <summary>
        /// キャプチャを止める(再開の前)。受信した控えを消し、周りのプレイヤーを見失った扱いにする
        /// (止めている間の出入りは届かない。周りから外れたときと同じく、特化と潜在心相晶はメーター用の控えへ写してから不明にし、名簿を作り直す)。
        /// アプリの終了はこれを使わず <see cref="StopReceiving"/> で止める。
        /// </summary>
        public static void StopCapturing()
        {
            StopReceiving();
            NearbyEntityStore.Instance.Clear();
            ClearReceivedStateStores();
            PlayerRosterProjection.ResetNearbyPlayers();
        }

        /// <summary>
        /// 受信だけを止める(処理のスレッドが終わるまで待つ)。控えは消さない。
        /// アプリの終了は、これで止めてから最後の回を保存する(保存が写真のリンクなど控えの値を焼き付けるため)。
        /// </summary>
        public static void StopReceiving()
        {
            netCap?.Stop();
        }

        /// <summary>
        /// 受信した通知から作った状態を消す。キャプチャの停止とログアウト(起動直後の状態へ戻す)の両方から呼ぶ。
        ///
        /// <para>
        /// 周囲の実体の一覧(<see cref="NearbyEntityStore"/>)はここに入れない。ログアウトでは
        /// 陣営・マップ名と一緒に <see cref="NearbyEntityProjection.ResetToStartup"/> が消すため。
        /// </para>
        /// </summary>
        private static void ClearReceivedStateStores()
        {
            SkillCooldownStateStore.Reset();
            CooldownModifierStore.Clear();
            GrpcTeamManager.ResetMemberState();
            ActiveBuffStore.Instance.Clear();
            BuffInstanceIndex.Instance.Clear();
            SummonSourceIndex.Instance.Clear();
            SourceLandingResolver.Instance.Clear();
            BuffOriginResolver.Instance.Clear();
            PendingEnemyBuffLogs.Clear();
            NearbyMonsterIndex.Instance.Clear();
            EntityOriginIndex.Instance.Clear();
            BossDbmBarStore.Instance.Clear();
            WarningSkillCastStore.Instance.Clear();
            DungeonTargetStore.Instance.Clear();
            DungeonScoreStore.Instance.Clear();
            PartyMemberCache.Instance.Clear();
            SocialDataStore.ResetToStartup();
            SelfEquipmentStore.ResetSelfToStartup();
            SelfModuleStore.ResetSelfToStartup();
            SelfSeasonCultivateStore.ResetSelfToStartup();
            PlayerSkillLevelStateStore.ResetSelfToStartup();
            SelfAttributeChangeStore.Clear();

            // 入場の全属性の印(上)を消した後で組み直す。
            GameReceptionStateStore.Clear();

            // 死亡と自然回復の刻みの控え。止めている間の復活を取りこぼすと、次の死亡を扱わなくなる。
            PlayerDeathStates = [];
            LastPassiveHealTicks = [];

            // 応答待ちの技の要求とチャットの記録の要求。止めている間の応答は届かない。
            PendingUseSlotSkills = [];
            PendingChatRecordRequests = [];
        }

        /// <summary>
        /// 自動のときにキャプチャするアダプタ。
        ///
        /// <para>
        /// 候補は <b>Windows の状態が Up</b> で、アドレス・ゲートウェイ・MAC を持つもの。
        /// 切れた Wi-Fi にも自動割り当てのアドレスと、前に接続したときのゲートウェイが残って見えるので、
        /// Up かどうかで外さないと候補に残ってしまう。
        /// </para>
        ///
        /// <para>
        /// 候補が複数あるときは、<b>Windows が外向きの通信に使っているアダプタ</b>を選ぶ。
        /// Npcap の並び順は優先度ではない。有線を決め打ちで優先しないのは、通信が実際に Wi-Fi を
        /// 通っているときに有線を掴むと何も取れないため。
        /// <b>経路を引けない・経路の先が候補に無いときは、候補(どれも Up)の先頭で動かし続け、警告を残す。</b>
        /// 何も選ばないとキャプチャが止まり、何も取れなくなる。
        /// </para>
        /// </summary>
        public static SharpPcap.LibPcap.LibPcapLiveDevice? TryFindBestNetworkDevice()
        {
            var upInterfaces = new Dictionary<string, System.Net.NetworkInformation.NetworkInterface>(StringComparer.Ordinal);
            foreach (var networkInterface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                {
                    continue;
                }

                var guid = ExtractAdapterGuid(networkInterface.Id);
                if (guid.Length > 0)
                {
                    upInterfaces.TryAdd(guid, networkInterface);
                }
            }

            var candidates = new List<(SharpPcap.LibPcap.LibPcapLiveDevice Device, System.Net.NetworkInformation.NetworkInterface Interface)>();
            foreach (var device in SharpPcap.LibPcap.LibPcapLiveDeviceList.Instance)
            {
                // pcap の名前(\Device\NPF_{GUID})の GUID で Windows のアダプタと突き合わせる。
                // Up でないものと、Windows 側に対応が無いもの(ループバック)はここで外れる。
                if (!upInterfaces.TryGetValue(ExtractAdapterGuid(device.Name), out var networkInterface))
                {
                    continue;
                }

                if (device.Addresses.Count == 0)
                {
                    continue;
                }

                if (device.Interface is null || device.Interface.GatewayAddresses.Count == 0)
                {
                    continue;
                }

                if (device.MacAddress == null)
                {
                    continue;
                }

                candidates.Add((device, networkInterface));
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            if (candidates.Count == 1)
            {
                return candidates[0].Device;
            }

            if (!TryGetOutboundInterfaceIndex(out var outboundIndex))
            {
                Log.Warning(
                    "Could not resolve the outbound route; using the first of {Count} capture adapter candidates: {Adapter}",
                    candidates.Count,
                    candidates[0].Device.Description);
                return candidates[0].Device;
            }

            foreach (var candidate in candidates)
            {
                if (candidate.Interface.Supports(System.Net.NetworkInformation.NetworkInterfaceComponent.IPv4)
                    && candidate.Interface.GetIPProperties().GetIPv4Properties()?.Index == outboundIndex)
                {
                    return candidate.Device;
                }
            }

            Log.Warning(
                "Outbound interface {Index} is not among the {Count} capture adapter candidates; using the first: {Adapter}",
                outboundIndex,
                candidates.Count,
                candidates[0].Device.Description);
            return candidates[0].Device;
        }

        /// <summary>
        /// Windows のアダプタID(<c>{GUID}</c>)と pcap のデバイス名(<c>\Device\NPF_{GUID}</c>)から、GUID の部分だけを取り出す。
        /// 括弧が無ければ空。
        /// </summary>
        private static string ExtractAdapterGuid(string name)
        {
            var start = name.IndexOf('{');
            if (start < 0)
            {
                return string.Empty;
            }

            var end = name.IndexOf('}', start + 1);
            return end > start
                ? name[(start + 1)..end].ToUpperInvariant()
                : string.Empty;
        }

        /// <summary>
        /// Windows が外向きの通信に使うアダプタの番号(IPv4 のインターフェース番号)。
        /// 宛先は既定の経路に乗る公開アドレスなら何でもよく、パケットは送らない。
        /// </summary>
        private static bool TryGetOutboundInterfaceIndex(out int index)
        {
            var destination = BitConverter.ToUInt32(System.Net.IPAddress.Parse("8.8.8.8").GetAddressBytes(), 0);
            var result = GetBestInterface(destination, out var bestInterfaceIndex);
            index = (int)bestInterfaceIndex;
            return result == 0;
        }

        [System.Runtime.InteropServices.DllImport("iphlpapi.dll")]
        private static extern int GetBestInterface(uint destinationAddress, out uint bestInterfaceIndex);

        /// <summary>
        /// 入場時の自分の全属性に含まれない能力値を 0 にする。
        /// 全属性は値が 0 の属性を含まず、コンテンツを出て消えた能力値も値なしでは届かないので、
        /// 重ねるだけだと前のマップから写した値が残る。HP などの状態の値は能力値の表に入らないので触らない。
        /// </summary>
        private static void ResetSelfStatsMissingFromEnterScene(long uuid, RepeatedField<Attr> attrs)
        {
            if (!EncounterManager.Current.Entities.TryGetValue(uuid, out var entity))
            {
                return;
            }

            var statAttrIds = HelperMethods.DataTables.FightAttrs.StatAttrIds;
            var arrivedAttrIds = new HashSet<int>();
            foreach (var attr in attrs)
            {
                if (attr.Id != 0 && attr.RawData != null)
                {
                    arrivedAttrIds.Add(attr.Id);
                }
            }

            foreach (var (key, value) in entity.Attributes.ToArray())
            {
                if (!System.Enum.TryParse<EAttrType>(key, ignoreCase: false, out var attrType)
                    || !statAttrIds.Contains((int)attrType)
                    || arrivedAttrIds.Contains((int)attrType))
                {
                    continue;
                }

                // 値なしで届いたときと同じ値にする。ProcessAttrs は能力値を int か long で控える。
                if (value is int intValue && intValue != 0)
                {
                    EncounterManager.Current.SetAttrKV(uuid, key, 0);
                }
                else if (value is long longValue && longValue != 0)
                {
                    EncounterManager.Current.SetAttrKV(uuid, key, 0L);
                }
            }
        }

        /// <summary>
        /// 全属性の一覧(入場の自分の全属性・出現の一覧)に戦闘状態(<c>AttrCombatState</c>、104)と切り替わった時刻(<c>AttrCombatStateTime</c>、114)が無ければ、
        /// その実体の控えを値なしで届いたときと同じ 0 にする。
        /// 一覧は値が 0 の属性を含まず、戦闘中にマップを移動しても戦闘を抜けた知らせ(値なし)は届かないので、
        /// 重ねるだけだと前のマップから写した値・見えなくなる前の値が残り、戦闘していなくても戦闘状態のままになる。
        /// </summary>
        private static void ResetCombatStateMissingFromFullAttrs(long uuid, RepeatedField<Attr>? attrs)
        {
            if (!EncounterManager.Current.Entities.TryGetValue(uuid, out var entity))
            {
                return;
            }

            var hasCombatState = false;
            var hasCombatStateTime = false;
            if (attrs != null)
            {
                foreach (var attr in attrs)
                {
                    if (attr.Id == (int)EAttrType.AttrCombatState)
                    {
                        hasCombatState = true;
                    }
                    else if (attr.Id == (int)EAttrType.AttrCombatStateTime)
                    {
                        hasCombatStateTime = true;
                    }
                }
            }

            // ProcessAttrs は 104 を int(既定の枝)、114 を long で控える。
            if (!hasCombatState && entity.GetAttrKV("AttrCombatState") is int combatState && combatState != 0)
            {
                EncounterManager.Current.SetAttrKV(uuid, "AttrCombatState", 0);
            }

            if (!hasCombatStateTime && entity.GetAttrKV("AttrCombatStateTime") is long combatStateTime && combatStateTime != 0)
            {
                EncounterManager.Current.SetAttrKV(uuid, "AttrCombatStateTime", 0L);
            }
        }

        public static void ProcessEnterScene(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = EnterScene.Parser.ParseFrom(payloadBuffer);

            if (vData.EnterSceneInfo != null)
            {
                if (vData.EnterSceneInfo.PlayerEnt != null)
                {
                    GameReceptionStateStore.MarkSelfIdentified();
                    NearbyEntityProjection.SetSelfEntity(vData.EnterSceneInfo.PlayerEnt.Uuid);

                    if (vData.EnterSceneInfo.PlayerEnt.Attrs != null)
                    {
                        ResetCombatStateMissingFromFullAttrs(vData.EnterSceneInfo.PlayerEnt.Uuid, vData.EnterSceneInfo.PlayerEnt.Attrs.Attrs);
                        ResetSelfStatsMissingFromEnterScene(vData.EnterSceneInfo.PlayerEnt.Uuid, vData.EnterSceneInfo.PlayerEnt.Attrs.Attrs);
                        ProcessAttrs(vData.EnterSceneInfo.PlayerEnt.Uuid, vData.EnterSceneInfo.PlayerEnt.Attrs.Attrs, extraData.ArrivalTime);
                        SelfAttributeChangeStore.MarkFullSnapshot();
                    }

                    // 入場の一覧は自分の一時属性の全部(無ければ一時属性が無い)。前のマップの値を置き換える。
                    var enterTempAttrs = vData.EnterSceneInfo.PlayerEnt.TempAttrs?.Attrs ?? (IEnumerable<Zproto.TempAttr>)[];
                    ProcessTempAttrs(vData.EnterSceneInfo.PlayerEnt.Uuid, enterTempAttrs, extraData.ArrivalTime, isFullList: true);

                    PlayerRosterProjection.UpsertSelf(vData.EnterSceneInfo.PlayerEnt.Uuid);
                }

                if (vData.EnterSceneInfo.SceneAttrs != null)
                {
                    Log.Debug("ProcessEnterScene");

                    foreach (var attr in vData.EnterSceneInfo.SceneAttrs.Attrs)
                    {
                        var reader = new Google.Protobuf.CodedInputStream(attr.RawData.ToByteArray());

                        EAttrType attrId = (EAttrType)attr.Id;
                        string attrIdName = attrId.ToString();
                        bool isNoValue = attr.RawData.Length == 0;
                        switch (attrId)
                        {
                            case EAttrType.AttrSceneUuid:
                                Log.Debug($"\t{attrIdName} = {(isNoValue ? 0 : reader.ReadInt64())}");
                                break;
                            case EAttrType.AttrSceneBasicId:
                                Log.Debug($"\t{attrIdName} = {(isNoValue ? 0 : reader.ReadUInt32())}");
                                break;
                            case EAttrType.AttrSceneChannel:
                                Log.Debug($"\t{attrIdName} = {(isNoValue ? 0 : reader.ReadUInt32())}");
                                break;
                            default:
                                var val = isNoValue ? 0 : reader.ReadInt32();
                                Log.Debug($"\t{attrIdName} = {val}");
                                break;
                        }
                    }
                }
            }
        }

        public static void ProcessSyncWorldActData(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = WorldActActivityData.Parser.ParseFrom(payloadBuffer);

            System.Diagnostics.Debug.WriteLine("ProcessSyncWorldActData");
        }

        public static void ProcessSyncSceneEvents(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = SyncSceneEvents.Parser.ParseFrom(payloadBuffer);

            foreach (var evt in vData.Evt.Events)
            {
                EncounterManager.Current.AddSceneEvent(evt, extraData);
            }
        }

        /// <summary>
        /// NotifySocialData に同梱される「自分自身」のソーシャル情報を取り込む。
        ///
        /// <para>
        /// 名前・職業・レベル・戦闘力は本来フルコンテナ(SyncContainerData)で届くが、
        /// それはマップをロードした瞬間にしか流れないため、ロード済みマップの途中で
        /// アプリを起動すると永久に受け取れず、自分の行だけ空のままになる。
        /// 周辺プレイヤーは視界進入のたびに全属性が届くので埋まるため、自分だけ取り残される。
        /// </para>
        ///
        /// <para>
        /// この経路は途中起動でも定期的に届く。他プレイヤーに対して
        /// ApplyTeamMemberSocialData がやっているのと同じ内容を、自分にも適用する。
        /// </para>
        ///
        /// <para>
        /// 値が既にある場合は上書きしない。この経路の内容が古い可能性は否定できないため、
        /// 欠落を埋める用途に限定する。
        /// </para>
        /// </summary>
        private static void ApplySelfSocialData(SocialData? socialData)
        {
            if (socialData is null || socialData.CharId <= 0)
            {
                return;
            }

            if (AppState.PlayerUID != 0 && socialData.CharId != AppState.PlayerUID)
            {
                return;
            }

            SocialDataStore.Apply(socialData.CharId, socialData.AvatarInfo);

            var selfUuid = currentUserUuid != 0
                ? currentUserUuid
                : AppState.PlayerUUID != 0
                    ? AppState.PlayerUUID
                    : Utils.EntityIdToUuid(socialData.CharId, (long)EEntityType.EntChar, false, false);
            if (selfUuid == 0 || EncounterManager.Current is null)
            {
                return;
            }

            var entity = EncounterManager.Current.GetOrCreateEntity(selfUuid);
            var values = SocialDataValues.Read(socialData);
            var changed = false;

            if (string.IsNullOrEmpty(entity.Name) && values.Name.Length > 0)
            {
                entity.SetName(values.Name);
                changed = true;
            }

            if (entity.ProfessionId == 0 && values.ProfessionId > 0 && !IsTransformed(entity.UUID))
            {
                entity.SetProfessionId(values.ProfessionId);
                changed = true;
            }

            if (entity.Level == 0 && values.Level > 0)
            {
                entity.SetLevel(values.Level);
                changed = true;
            }

            if (entity.SeasonLevel == 0 && values.SeasonLevel > 0)
            {
                entity.SetSeasonLevel(values.SeasonLevel);
                changed = true;
            }

            if (entity.AbilityScore == 0 && values.CombatPower > 0)
            {
                entity.SetAbilityScore(values.CombatPower);
                changed = true;
            }

            if (entity.SeasonStrength == 0 && values.SeasonStrength > 0)
            {
                entity.SetSeasonStrength(values.SeasonStrength);
                changed = true;
            }

            if (changed)
            {
                PlayerRosterProjection.UpsertSelf(selfUuid);
            }
        }

        /// <summary>
        /// ログイン画面にいる間は <c>true</c>。<c>ExitGame</c> で立て、入り直し(<c>SelectChar</c>)かフルコンテナで下ろす。
        ///
        /// <para>
        /// <b>この門が要る理由。</b> ログアウトの時点で受信の待ち行列に残っている AOI の差分が、
        /// 起動直後の状態へ戻した直後に処理されて、消したはずのプレイヤーリスト・実体を作り直してしまう
        /// (実測: 戻した4ミリ秒後に <c>SyncNearDeltaInfo</c> がプレイヤー51人・実体92体を作り直した)。
        /// </para>
        /// </summary>
        private static volatile bool _isLoggedOut;

        /// <summary>
        /// 通知を処理してよいか。ログイン画面にいる間は、ゲームの状態を変える通知を通さない。
        /// フルコンテナだけは通し、そこで門を開ける(入り直しの合図を取りこぼしたときの保険)。
        /// </summary>
        internal static bool ShouldDispatchNotify(ulong serviceUuid, uint methodId)
        {
            if (!_isLoggedOut)
            {
                return true;
            }

            if (serviceUuid == (ulong)EServiceId.WorldNtf
                && methodId == (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.WorldNtf.SyncContainerData)
            {
                OpenGateAfterLogin("フルコンテナ");
                return true;
            }

            return false;
        }

        /// <summary>
        /// 要求・応答を処理してよいか。ログイン画面にいる間は、入り直し(<c>SelectChar</c>)だけ通す。
        /// </summary>
        internal static bool ShouldDispatchProxy(uint serviceId, uint methodId)
        {
            return !_isLoggedOut
                || (serviceId == (uint)EServiceId.GrpcCharactor
                    && methodId == (uint)StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods.GrpcCharactorNtf.SelectChar);
        }

        /// <summary>入り直し(キャラクター選択)。ここから先は新しいセッションなので門を開ける。</summary>
        public static void ProcessSelectChar(ReadOnlySpan<byte> payloadBuffer, uint returnUid, ExtraPacketData extraData)
        {
            OpenGateAfterLogin("SelectChar");
        }

        private static void OpenGateAfterLogin(string reason)
        {
            if (!_isLoggedOut)
            {
                return;
            }

            _isLoggedOut = false;
            Log.Information("Login screen gate opened ({Reason})", reason);
        }

        /// <summary>
        /// ログアウトで起動直後の状態へ戻し終えた。パケット処理のスレッドで上がる。
        /// App の実体の窓はこれを受けて、捕まえている個体を放し、起動時と同じく種類と種別IDで捕まえ直す。
        /// プレイヤー情報の窓は、出していた行を捨てて、起動時の表示に戻す(一覧から外れた人の最後の行も <see cref="PlayerDirectory.ResetToStartup"/> で捨てている)。
        /// </summary>
        public static event Action? ResetToStartupCompleted;

        /// <summary>
        /// 届いたメッセージ全部(関門の前。<see cref="NetCap.MessageObserved"/>)。パケット処理のスレッドで上がる。
        /// 中身は受け手の中でだけ有効。App がプラグインへ渡すのに使う。
        /// </summary>
        public static event Action<ObservedMessage>? MessageObserved;

        /// <summary>
        /// シーン情報を反映した(<see cref="ApplySceneData"/> の終わり)。値はマップを移動して一覧を作り直したか
        /// (偽は同じマップのシーン通知の重複か、起動して最初のシーンの確定)。パケット処理のスレッドで上がる。
        /// </summary>
        public static event Action<bool>? SceneApplied;

        /// <summary>
        /// マッチングが成立して承諾待ちになった(<c>MatchNtf.EnterMatchResult</c> の状態が <c>WaitReady</c>。ゲームの「マッチング成功、確認待ち」)。
        /// 値はマッチング先の種類と番号(名前は <see cref="CombatDataCatalog.GetMatchTargetName"/>)。パケット処理のスレッドで上がる。
        /// </summary>
        public static event Action<EMatchType, long>? MatchFound;

        public static void ProcessEnterMatchResult(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            var vData = MatchNtf.Types.EnterMatchResultNtf.Parser.ParseFrom(payloadBuffer);
            var matchInfo = vData.VRequest?.MatchInfo;
            if (matchInfo is null || matchInfo.MatchStatus != EMatchStatus.WaitReady)
            {
                return;
            }

            var key = matchInfo.MatchKeyInfo;
            MatchFound?.Invoke(key?.MatchType ?? EMatchType.Null, key?.MatchTypeUuid ?? 0);
        }

        /// <summary>
        /// ゲームがログイン画面へ戻った(サーバーから <c>GrpcCharactor</c> の <c>ExitGame</c> が通知で届く)。
        /// ログイン画面ではキャラクターを替えうるので、自分の素性も含めて<b>アプリ起動直後の状態へ戻す</b>。
        /// ログイン画面のシーン(シーン表の login)はサーバーから届かないので、この通知を合図にする。
        ///
        /// <para>
        /// 順序:
        /// 1. 計測中なら(待機中でも)終える(計測したエンカウンターは次の作り直しで計測の注記つきで保存される)
        /// 2. ダンジョンの状態とシーンを起動時の値に戻す(開いている記録には押さない)
        /// 3. battle 行を閉じて開き直し、エンカウンターをマップ移動と同じ手順で保存して、持ち越しなし(reason=ExitGame、起動時と同じ)で作り直す。
        ///    <b>プレイヤーリストはまだ残っている</b>ので、保存の直前の表示値の焼き付けが効く
        /// 4. 自分の素性と、接続中に積もった状態を起動時の値に戻す。キャプチャ(接続とデバイス)・設定・データ表・DB・計測は戻さない
        /// 5. 空のプレイヤーリストを作り直し、開いている履歴をライブに戻して、App に知らせる
        /// </para>
        /// </summary>
        public static void ProcessExitGame(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            Log.Information("ExitGame: returned to the login screen. Resetting to the startup state");

            // 戻すより先に門を閉じる。待ち行列に残っている AOI の差分が、戻した直後に一覧を作り直すため。
            _isLoggedOut = true;

            try
            {
                ResetToStartupState();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ExitGame: exception while resetting to the startup state");
                throw;
            }
        }

        private static void ResetToStartupState()
        {
            // 計測は待機中でも終える。下の作り直しが、記録があれば計測の注記つきで保存する。
            EncounterManager.EndBenchmarkBeforeSplit("logout", onlyIfBegun: false);

            BattleStateMachine.ResetDungeonStateToStartup();
            EncounterManager.ResetSceneToStartup();

            EncounterManager.StartNewMap();

            // ログアウトは前の結果を保持せず、すぐ新しい回を出す(キャラ交代があるので持ち越さない)。
            EncounterManager.EnterDungeon(keepPastEncounterInMeter: false, force: true, reason: EncounterStartReason.ExitGame);

            BattleStateMachine.ClearEncounterEndFinalData();

            currentUserUuid = 0;
            AppState.PlayerUUID = 0;
            AppState.PlayerUID = 0;
            AppState.AccountId = null!;
            AppState.PlayerName = null!;
            AppState.ProfessionId = 0;
            AppState.PlayerMeterPlacement = 0;
            AppState.PlayerTotalMeterValue = 0;

            // キャプチャの停止(StopCapturing)と共通のもの。停止はしない。
            ClearReceivedStateStores();

            // マップ移動では残していたもの(自分の陣営・シーズン・プレイヤーリストの行など)。
            SeasonStateStore.ResetToStartup();

            // チャットはキャプチャの停止でも残すので、消すのはログアウトだけ。
            ChatMessageStore.ResetToStartup();

            PlayerRosterProjection.ResetToStartup();
            PlayerDirectory.ResetToStartup();

            NearbyEntityProjection.ResetToStartup();

            PlayerRosterProjection.RebuildRoster();

            EncounterHistoryProvider.ResetSelectionForLogout();

            ResetToStartupCompleted?.Invoke();
        }

        /// <summary>
        /// ほかの人からプライベートトークが届いた(自分の発言では上げない)。値は相手のキャラID・メッセージの番号・発言者名。
        /// パケット処理のスレッドで上がる。
        /// </summary>
        public static event Action<long, long, string>? PrivateChatMessageReceived;

        /// <summary>
        /// ゲームがプライベートトークを既読にした(上りの要求 <c>SetPrivateChatHasRead</c>)。値は相手のキャラIDと、どの番号まで読んだか。
        /// ゲームはその相手とのプライベートトークを画面で開いている間、新着のたびにすぐ送る(開いていなければ送らない)。パケット処理のスレッドで上がる。
        /// </summary>
        public static event Action<long, long>? PrivateChatRead;

        /// <summary>
        /// ワールドチャットにほかの人の文の発言が届いた(スタンプ・画像などは上げない)。値は発言者のキャラID・発言者名・本文。
        /// 発言者のいないメッセージ(システムの通知)と、自分のキャラIDが分かっていれば自分の発言は上げない。パケット処理のスレッドで上がる。
        /// </summary>
        public static event Action<long, string, string>? WorldChatMessageReceived;

        /// <summary>チャットの新しいメッセージ。自分の発言も同じ通知で届く。</summary>
        public static void ProcessNotifyNewestChitChatMsgs(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            var notify = ChitChatNtf.Types.NotifyNewestChitChatMsgs.Parser.ParseFrom(payloadBuffer);
            if (notify.VRequest?.ChatMsg is not { } message)
            {
                return;
            }

            ChatMessageStore.Add(notify.VRequest.ChannelType, message);
            if (notify.VRequest.ChannelType == ChitChatChannelType.ChannelPrivate)
            {
                RaisePrivateChatMessageReceived(message);
            }
            else if (notify.VRequest.ChannelType == ChitChatChannelType.ChannelWorld)
            {
                RaiseWorldChatMessageReceived(message);
            }
        }

        /// <summary>
        /// ワールドチャットの新着が、ほかの人の文の発言なら <see cref="WorldChatMessageReceived"/> を上げる。
        /// 自分のキャラIDが分からない途中起動のうちは、自分の発言も上がる。
        /// </summary>
        private static void RaiseWorldChatMessageReceived(ChitChatMsg message)
        {
            if (message.MsgInfo is not { MsgType: ChitChatMsgType.ChatMsgTextMessage } info)
            {
                return;
            }

            var senderId = message.SendCharInfo?.CharID ?? 0;
            var senderName = message.SendCharInfo?.Name ?? string.Empty;
            if ((senderId <= 0 && string.IsNullOrWhiteSpace(senderName))
                || (AppState.PlayerUID != 0 && senderId == AppState.PlayerUID))
            {
                return;
            }

            WorldChatMessageReceived?.Invoke(senderId, senderName, info.MsgText ?? string.Empty);
        }

        /// <summary>
        /// プライベートトークの新着が、ほかの人の発言なら <see cref="PrivateChatMessageReceived"/> を上げる。
        /// 自分の発言かは控えと同じ決め方(<see cref="ChatMessageStore.ResolvePrivateMessage"/>)で、自分のキャラIDが分からない途中起動のうちも決まる。
        /// 相手が決まらない(0)メッセージは、控えがログに残す。
        /// </summary>
        private static void RaisePrivateChatMessageReceived(ChitChatMsg message)
        {
            var side = ChatMessageStore.ResolvePrivateMessage(message.SendCharInfo?.CharID ?? 0, message.MsgInfo?.TargetId ?? 0);
            if (side.IsFromSelf || side.PartnerId == 0)
            {
                return;
            }

            PrivateChatMessageReceived?.Invoke(side.PartnerId, message.MsgId, message.SendCharInfo?.Name ?? string.Empty);
        }

        public static void ProcessNotifySocialData(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = SocialNtf.Types.NotifySocialData.Parser.ParseFrom(payloadBuffer);

            if (vData != null)
            {
                ApplySelfSocialData(vData.VRequest?.Data);
                GrpcTeamManager.ProcessSocialTeamData(vData.VRequest?.Data);
            }

            if (vData?.VRequest?.Data?.SceneData is { } socialScene)
            {
                // AllowSceneUpdate は「同じシーンの繰り返し通知」を抑えるための関門。
                // NotifySocialData は social のスナップショットであってシーン通知ではなく、
                // 中身が食い違ったまま何度も飛んでくるため、無条件に流すと表示が暴れる。
                //
                // **別のマップ/チャンネルへ移った通知は抑制しない。** 関門を再武装するのは
                // フルコンテナ到着(StartNewMap)だけなので、フルコンテナが来ない切替
                // (ギルドハウス levelMapId=12000 など)では関門が閉じたままになり、
                // 移動通知を全部捨ててしまう。
                //
                // **LevelMapId == 0 は「まだシーン通知を一度も受け取っていない」状態。**
                // 0 → 実マップ は移動ではなく初回確定なので、ここを移動として扱わない。
                // 扱うと StartNewMap → EnterDungeon が走り、起動時のエンカウンターを
                // SceneId=0 / SceneName 空のまま保存してしまう。
                var isSceneChange = EncounterManager.LevelMapId != 0
                    && (socialScene.LevelMapId != EncounterManager.LevelMapId
                        || socialScene.LineId != EncounterManager.ChannelLineId);

                if (isSceneChange || EncounterManager.AllowSceneUpdate)
                {
                    ApplySceneData(
                        socialScene.LevelMapId,
                        socialScene.LineId,
                        socialScene.SceneGuid,
                        "NotifySocialData",
                        resetForSceneChange: isSceneChange);
                    EncounterManager.AllowSceneUpdate = false;
                }
            }
        }

        public static void ProcessNoticeUpdateTeamInfo(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NoticeUpdateTeamInfo.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessNoticeUpdateTeamInfo(vData, extraData);
        }

        public static void ProcessNoticeUpdateTeamMemberInfo(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NoticeUpdateTeamMemberInfo.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessNoticeUpdateTeamMemberInfo(vData, extraData);
        }

        public static void ProcessNotifyJoinTeam(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyJoinTeam.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessNotifyJoinTeam(vData, extraData);
        }

        public static void ProcessNotifyLeaveTeam(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyLeaveTeam.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessNotifyLeaveTeam(vData, extraData);
        }

        public static void ProcessNotifyTeamGroupUpdate(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyTeamGroupUpdate.Parser.ParseFrom(payloadBuffer);
            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessNotifyTeamGroupUpdate(vData, extraData);
        }

        public static void ProcessNotifyBeTransferLeader(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyBeTransferLeader.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessNotifyBeTransferLeader(vData, extraData);
        }

        public static void ProcessNoticeTeamDissolve(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NoticeTeamDissolve.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessNoticeTeamDissolve(vData, extraData);
        }

        public static void ProcessNotifyTeamActivityState(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyTeamActivityState.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessNotifyTeamActivityState(vData, extraData);
        }

        public static void ProcessTeamActivityResult(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.TeamActivityResult.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessTeamActivityResult(vData, extraData);
        }

        public static void ProcessTeamActivityListResult(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.TeamActivityListResult.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessTeamActivityListResult(vData, extraData);
        }

        public static void ProcessTeamActivityVoteResult(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.TeamActivityVoteResult.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            GrpcTeamManager.ProcessTeamActivityVoteResult(vData, extraData);
        }

        public static void ProcessNotifyCharMatchResult(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            System.Diagnostics.Debug.WriteLine("ProcessNotifyCharMatchResult");

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyCharMatchResult.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(vData);
        }

        public static void ProcessNotifyTeamMatchResult(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            System.Diagnostics.Debug.WriteLine("ProcessNotifyTeamMatchResult");

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyTeamMatchResult.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(vData);
        }

        public static void ProcessNotifyCharAbortMatch(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            System.Diagnostics.Debug.WriteLine("ProcessNotifyCharAbortMatch");

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyCharAbortMatch.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(vData);
        }

        public static void ProcessUpdateTeamMemBeCall(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            System.Diagnostics.Debug.WriteLine("ProcessUpdateTeamMemBeCall");

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.UpdateTeamMemBeCall.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

        }

        public static void ProcessNotifyTeamMemBeCall(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            System.Diagnostics.Debug.WriteLine("ProcessNotifyTeamMemBeCall");

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyTeamMemBeCall.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

        }

        public static void ProcessNotifyTeamMemBeCallResult(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            System.Diagnostics.Debug.WriteLine("ProcessNotifyTeamMemBeCallResult");

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyTeamMemBeCallResult.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

        }

        public static void ProcessNotifyTeamEnterErr(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            System.Diagnostics.Debug.WriteLine("ProcessNotifyTeamEnterErr");

            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var vData = GrpcTeamNtf.Types.NotifyTeamEnterErr.Parser.ParseFrom(payloadBuffer);

            if (vData == null)
            {
                return;
            }

        }

        public static void ProcessSyncHitInfo(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            System.Diagnostics.Debug.WriteLine($"ProcessSyncHitInfo");
        }
        private static readonly HashSet<EAttrType> ShieldListChangedAttributes = [EAttrType.AttrShieldList];

        internal static bool IsSelfPlayer(long uuid)
        {
            return uuid == currentUserUuid
                || uuid == AppState.PlayerUUID
                || (AppState.PlayerUID != 0
                    && Utils.UuidToEntityId(uuid) == AppState.PlayerUID);
        }

        /// <summary>
        /// 変身中か。変身中は職業IDの属性(AttrProfessionId)だけが職業を決める。
        /// ソーシャル・コンテナ・ダメージの技の経路は変身前のクラスを運んでくるので、変身中は書かない。
        /// ミーンとゴーレムは変身のバフ、ドロシー・ルーシィ・ナツは変身クラスの職業IDで判断する。
        /// </summary>
        private static bool IsTransformed(long uuid)
        {
            if (Models.PlayerClassSpecResolver.HasMeanTransformBuff(uuid)
                || Models.PlayerClassSpecResolver.HasGolemTransformBuff(uuid))
            {
                return true;
            }

            return EncounterManager.Current.Entities.TryGetValue(uuid, out var entity)
                && Models.PlayerClassSpecResolver.TryResolveTransformation(entity.ProfessionId, out _);
        }

        /// <summary>
        /// 回血のバフ(<c>NameDesign</c> 回血)。薬を飲むと付き、同じ差分で HP だけが増える。回復の通知は来ない。
        /// 回復の薬はどれもこのバフを付けるので、どの薬かは分からない。
        /// </summary>
        private const int PotionHealBuffId = 2033000;

        /// <summary>全滅でゲームがリセットしたとき、戦っていた全員に付くバフ(<c>NameDesign</c> 英雄本通用团灭恢复血量清理CD)。ダンジョン開始・ボス部屋の初回の入場にも付く。</summary>
        private const int WipeResetBuffId = 510072;

        /// <summary>復活の前に付くバフ(<c>ReviveTable.BeforeBuffId</c>)。1人の復活にも付く。</summary>
        private const int ReviveStartBuffId = 500111;

        /// <summary>
        /// 付いた人のスキルのクールダウンをゲームが一括で消すバフと、付与から消えるまでの時間。
        /// 510072(<c>NameDesign</c> 英雄本通用团灭恢复血量清理CD)は付与の約3.5秒後、
        /// 900122(清cd和回满生命)と 999962(肉鸽本-进休息室重置)は付与と同じ差分で消える。
        /// 510072 の 3.6秒は、付与から消えるまでの時間の最大(3.57秒)の直後。
        /// この3種が付く瞬間は、ゲームが戦いを仕切り直した瞬間でもある(ダンジョン開始・ボス部屋の初回の入場・全滅)。
        /// </summary>
        private static readonly Dictionary<int, TimeSpan> CooldownResetBuffDelays = new()
        {
            [510072] = TimeSpan.FromSeconds(3.6),
            [900122] = TimeSpan.Zero,
            [999962] = TimeSpan.Zero,
        };

        /// <summary>
        /// 自然回復のバフ。683104(主城自动回血、シーン入場で付く)と 683105(坐木桩自动回血、切り株に座っている間)。
        /// 乗っている間だけ、戦闘とは関係なく約1秒ごとに最大HPの5%ずつ HP が増える。2つは同時に乗らない。
        /// </summary>
        private static readonly int[] RegenBuffIds = [683104, 683105];

        /// <summary>差分を当てる前のプレイヤーの状態・死亡時刻・HP・最大HP・バリア量。</summary>
        private readonly record struct PlayerVitals(EActorState? State, long DeadTime, long? Hp, long? MaxHp, long? Shield)
        {
            /// <summary>記録に渡す「同期を当てる前の HP」。</summary>
            public TargetHealth Health => new(Hp, MaxHp, Shield);
        }

        /// <summary>プレイヤーごとの、いまの死亡の扱い。復活で消す。</summary>
        private sealed class PlayerDeathState
        {
            /// <summary>死亡の印つきの被弾を受けた。</summary>
            public bool HasLethalHit;

            /// <summary>この死亡を扱い終えた(ダメージの無い死亡なら記録に残した)。</summary>
            public bool IsDeathHandled;
        }

        /// <summary>
        /// パケット処理のスレッドだけが触る。消すときは中身を消さず新しい辞書に差し替える
        /// (キャプチャの停止は別のスレッドから消すため。<see cref="ClearReceivedStateStores"/>)。
        /// </summary>
        private static Dictionary<long, PlayerDeathState> PlayerDeathStates = [];

        /// <summary>料理・自然回復の刻みの種類。<see cref="LastPassiveHealTicks"/> に覚える。</summary>
        private enum PassiveHealTick
        {
            Food,
            Regen,
            Both
        }

        /// <summary>
        /// プレイヤーごとの直前の料理・自然回復の刻み。両方乗っているときの満タンの端数を、どちらの番か決めるのに使う。
        /// 端数を振り分けたときも、振り分けた種類で覚え直す。
        /// 消すときは <see cref="PlayerDeathStates"/> と同じく新しい辞書に差し替える。
        /// </summary>
        private static Dictionary<long, PassiveHealTick> LastPassiveHealTicks = [];

        private static PlayerVitals CapturePlayerVitals(long uuid)
        {
            if (!EncounterManager.Current.Entities.TryGetValue(uuid, out var entity))
            {
                return new PlayerVitals(null, 0, null, null, null);
            }

            return new PlayerVitals(
                entity.GetAttrKV("AttrState") as EActorState?,
                entity.GetAttrKV("AttrDeadTime") is long deadTime ? deadTime : 0,
                entity.GetAttrKV("AttrHp") as long?,
                entity.GetAttrKV("AttrMaxHp") as long?,
                Utils.GetCurrentShield(entity));
        }

        private static PlayerDeathState GetPlayerDeathState(long uuid)
        {
            if (!PlayerDeathStates.TryGetValue(uuid, out var death))
            {
                death = new PlayerDeathState();
                PlayerDeathStates[uuid] = death;
            }

            return death;
        }

        /// <summary>
        /// 差分1つぶんの死亡の扱い。死亡は、状態が「死亡」に変わったか、死亡時刻が新しい値になったことで分かる。
        /// 状態の「死亡」は、死亡の印つきの被弾と同じ差分でいきなり復活(27)になる回に届かず、死亡時刻は遅れて届く。
        ///
        /// <para>
        /// 死亡の印つきの被弾が無いまま死亡したら、ダメージの無い死亡として記録に1回だけ残す。
        /// 印つきの被弾がある死亡は被弾の行で分かるので何も足さない。死亡数は死亡時刻が新しい値になったときに数える。
        /// 状態が「死亡」から外れるか復活になったら、次の死亡に備えて消す。
        /// </para>
        /// </summary>
        private static void TrackPlayerDeath(long uuid, PlayerVitals before, HashSet<EAttrType> changedAttributes, ExtraPacketData extraData)
        {
            var entity = EncounterManager.Current.GetOrCreateEntity(uuid);
            var state = entity.GetAttrKV("AttrState") as EActorState?;
            var deadTime = entity.GetAttrKV("AttrDeadTime") is long value ? value : 0;
            var stateChanged = changedAttributes.Contains(EAttrType.AttrState);

            var becameDead = stateChanged
                && state == EActorState.ActorStateDead
                && before.State != EActorState.ActorStateDead;
            var hasNewDeadTime = changedAttributes.Contains(EAttrType.AttrDeadTime)
                && deadTime != 0
                && deadTime != before.DeadTime;

            if (becameDead || hasNewDeadTime)
            {
                var death = GetPlayerDeathState(uuid);
                if (!death.IsDeathHandled)
                {
                    death.IsDeathHandled = true;
                    if (!death.HasLethalHit)
                    {
                        EncounterManager.Current.AddPlayerDeathWithoutDamage(uuid, before.Health, extraData);
                    }
                }
            }

            if (hasNewDeadTime)
            {
                EncounterManager.Current.RecordPlayerDeath(uuid, extraData.ArrivalTime);
            }

            var revived = stateChanged
                && ((before.State == EActorState.ActorStateDead && state != EActorState.ActorStateDead)
                    || state == EActorState.ActorStateResurrection);
            if (revived)
            {
                PlayerDeathStates.Remove(uuid);
            }
        }

        /// <summary>
        /// 薬の回復を HPS に足す。薬は回復の通知を出さず、回血のバフの付与と同じ差分で HP だけが増える。
        /// 増えた量を、飲んだ本人から本人への回復として回血のバフの鍵で足す。行の名前は見出し表に無いので空欄になる。
        ///
        /// <para>
        /// 同じ差分にダメージ・回復の通知があると、HP の増え方から薬のぶんだけを取り出せないので足さずに警告を出す。
        /// </para>
        /// </summary>
        private static void RecordPotionHeal(long uuid, long? hpBefore, HashSet<EAttrType> changedAttributes, AoiSyncDelta delta, ExtraPacketData extraData)
        {
            // HP が変わっていなければ(満タンで飲んだなど)回復量は 0。
            if (!changedAttributes.Contains(EAttrType.AttrHp))
            {
                return;
            }

            if (hpBefore is null)
            {
                Serilog.Log.Warning("Potion healing: cannot determine the amount because the HP before the delta is unknown uuid={Uuid}", uuid);
                return;
            }

            if (delta.SkillEffects?.Damages.Count > 0)
            {
                Serilog.Log.Warning("Potion healing: cannot isolate the potion amount because the same delta carries {Count} damage/healing notifications uuid={Uuid}",
                    delta.SkillEffects.Damages.Count, uuid);
                return;
            }

            var hpAfter = EncounterManager.Current.GetOrCreateEntity(uuid).GetAttrKV("AttrHp") as long?;
            var healing = (hpAfter ?? 0) - hpBefore.Value;
            if (healing <= 0)
            {
                return;
            }

            AddBuffHealing(uuid, PotionHealBuffId, healing, extraData);
        }

        /// <summary>
        /// 料理と自然回復(<see cref="RegenBuffIds"/>)の回復を HPS に足す。どちらも回復の通知が無く、約1秒ごとに HP だけが増える。
        /// 1回の量は、料理は料理の表(<c>CookCuisineTable</c>)の値、自然回復は最大HPの5%(切り捨て)。
        /// 料理の表に回復量の無い料理は回復の通知で届くので、ここでは扱わない。
        ///
        /// <list type="bullet">
        ///   <item>増えが料理の1回ぶん → 料理</item>
        ///   <item>増えが最大HPの5%(自然回復のバフが乗っているとき) → 自然回復</item>
        ///   <item>増えが5%と料理の1回ぶんの合計 → 両方</item>
        ///   <item>
        ///     満タンになった端数 → 片方だけ乗っていればその片方。両方乗っていれば、刻みは料理 → 自然回復の順に来るので、
        ///     直前の刻み(<see cref="LastPassiveHealTicks"/>)が料理だけなら自然回復、それ以外なら料理
        ///     (料理の1回ぶんを超えた分は自然回復)
        ///   </item>
        ///   <item>最大HPが変わった差分(HP が割合のまま付け直される)・それ以外の増え → 数えない</item>
        /// </list>
        ///
        /// <para>
        /// 同じ差分に回復・ダメージの通知があると、HP の増えから料理・自然回復のぶんを取り出せないので数えない。
        /// 自然回復のバフは1回目の刻みと同じ差分で付くことがあるので、この差分で付いたバフも乗っているものとして見る。
        /// </para>
        /// </summary>
        private static void RecordPassiveHeal(long uuid, PlayerVitals before, HashSet<EAttrType> changedAttributes, AoiSyncDelta delta, IReadOnlyCollection<int> addedBuffIds, ExtraPacketData extraData)
        {
            if (!changedAttributes.Contains(EAttrType.AttrHp)
                || delta.SkillEffects?.Damages.Count > 0
                || before.Hp is not { } hpBefore
                || before.MaxHp is not { } maxHpBefore
                || before.State == EActorState.ActorStateDead)
            {
                return;
            }

            var entity = EncounterManager.Current.GetOrCreateEntity(uuid);
            if (entity.GetAttrKV("AttrHp") is not long hpAfter
                || entity.GetAttrKV("AttrMaxHp") is not long maxHp
                || maxHp != maxHpBefore)
            {
                return;
            }

            var increase = hpAfter - hpBefore;
            if (increase <= 0)
            {
                return;
            }

            var activeBuffIds = ActiveBuffStore.Instance.GetActive(uuid).Select(buff => buff.BaseId).Concat(addedBuffIds).ToHashSet();
            var regenBuffId = RegenBuffIds.FirstOrDefault(activeBuffIds.Contains);
            var regenAmount = regenBuffId != 0 ? maxHp * 5 / 100 : (long?)null;

            var regenAmounts = HelperMethods.DataTables.CookCuisines.RegenAmountsByBuffId;
            var foodBuffIds = activeBuffIds.Where(regenAmounts.ContainsKey).ToArray();
            if (foodBuffIds.Length > 1)
            {
                return;
            }

            var foodBuffId = foodBuffIds.Length == 1 ? foodBuffIds[0] : 0;
            var foodAmount = foodBuffId != 0 ? regenAmounts[foodBuffId] : (long?)null;
            var isFull = hpAfter == maxHp;
            PassiveHealTick? tick = null;

            if (foodAmount is { } food && increase == food)
            {
                AddBuffHealing(uuid, foodBuffId, food, extraData);
                tick = PassiveHealTick.Food;
            }
            else if (regenAmount is { } regen && Math.Abs(increase - regen) <= 1)
            {
                AddBuffHealing(uuid, regenBuffId, increase, extraData);
                tick = PassiveHealTick.Regen;
            }
            else if (regenAmount is { } regenPart && foodAmount is { } foodPart && Math.Abs(increase - (regenPart + foodPart)) <= 1)
            {
                AddBuffHealing(uuid, regenBuffId, increase - foodPart, extraData);
                AddBuffHealing(uuid, foodBuffId, foodPart, extraData);
                tick = PassiveHealTick.Both;
            }
            else if (isFull && regenAmount is { } regenTick && foodAmount is { } foodTick && increase < regenTick + foodTick)
            {
                if (LastPassiveHealTicks.TryGetValue(uuid, out var last) && last == PassiveHealTick.Food)
                {
                    if (increase < regenTick)
                    {
                        AddBuffHealing(uuid, regenBuffId, increase, extraData);
                        tick = PassiveHealTick.Regen;
                    }
                }
                else if (increase <= foodTick)
                {
                    AddBuffHealing(uuid, foodBuffId, increase, extraData);
                    tick = PassiveHealTick.Food;
                }
                else
                {
                    AddBuffHealing(uuid, foodBuffId, foodTick, extraData);
                    AddBuffHealing(uuid, regenBuffId, increase - foodTick, extraData);
                    tick = PassiveHealTick.Both;
                }
            }
            else if (isFull && regenAmount is { } regenOnly && foodAmount is null && increase < regenOnly)
            {
                AddBuffHealing(uuid, regenBuffId, increase, extraData);
                tick = PassiveHealTick.Regen;
            }
            else if (isFull && regenAmount is null && foodAmount is { } foodOnly && increase < foodOnly)
            {
                AddBuffHealing(uuid, foodBuffId, increase, extraData);
                tick = PassiveHealTick.Food;
            }

            if (tick is { } counted)
            {
                LastPassiveHealTicks[uuid] = counted;
            }
        }

        /// <summary>
        /// 回復の通知の無い回復を、飲んだ・食べた・受けた本人から本人への回復として、そのバフの鍵で HPS に足す。行の名前は見出し表に無いので空欄になる。
        /// 実際に HP が増えたなら、記録の前に戦闘の出来事にする(最初なら戦闘の時計の起点になる)。計測の回は出来事にしない(計測は自分の与ダメか回復の通知で始まる)。
        /// </summary>
        private static void AddBuffHealing(long uuid, int buffId, long healing, ExtraPacketData extraData)
        {
            if (healing > 0 && !EncounterManager.Current.IsBenchmark)
            {
                EncounterManager.Current.RecordCombatEvent(extraData.ArrivalTime);
            }

            var source = SkillSourceResolver.Resolve(EDamageSource.Buff, buffId, 0);
            // HP の増えはそのまま実際に増えた値なので、名目にも同じ値を渡す(過剰回復は 0)。
            EncounterManager.Current.AddHealing(uuid, uuid, source.Key, 0, healing, healing, healing, 0, default, EDamageType.Heal, default, false, false, false, false, false, extraData);
            EncounterManager.Current.MarkBuffSourcedSkill(uuid, source.Key);
            PlayerRosterProjection.UpsertPlayer(uuid);
        }

        /// <summary>ダメージの値。通知の値が無ければ幸運の値、負なら HP の減り(無ければ 0)。</summary>
        private static long ResolveDamageValue(SyncDamageInfo damageInfo)
        {
            var damage = damageInfo.Value != 0
                ? damageInfo.Value
                : damageInfo.LuckyValue;

            if (damage < 0)
            {
                damage = damageInfo.HpLessenValue > 0 ? damageInfo.HpLessenValue : 0;
            }

            return damage;
        }

        /// <summary>
        /// 被弾した本人が原因の被弾か。落下(<c>OwnerId</c> は 0、加害者は本人)と、加害者も大元の召喚者も無いバフ等のダメージ
        /// (自分が付けたバフの自傷。付与の <c>FireUuid</c> は本人)。被ダメログには本人を加害者として載せ、メーターには入れない。
        /// </summary>
        private static bool IsSelfCausedDamage(SyncDamageInfo damageInfo)
        {
            return damageInfo.DamageSource == EDamageSource.Fall
                || (damageInfo.OwnerId != 0 && damageInfo.AttackerUuid == 0 && damageInfo.TopSummonerId == 0);
        }

        /// <summary>
        /// タイムラインに残す敵の回復か。回復した側がプレイヤー(とその召喚)でなく、実際に HP が増えた回復(回復された側は敵でもプレイヤーでもよい)。
        /// 自傷扱い(出し手の無いバフ)と発生源が 0 の回復は、ほかの通知と同じく扱わない。回復した側と相手の種類は記録の入口(<c>Encounter.AddEnemyHeal</c>)が見る。
        /// <paramref name="healerUuid"/> は被弾の加害者と同じく寄せた回復した側(<c>Encounter.ResolveCombatEventLogActor</c>)。
        /// </summary>
        private static bool TryResolveEnemyHealer(SyncDamageInfo damageInfo, out long healerUuid)
        {
            healerUuid = 0;
            if (damageInfo.Type != EDamageType.Heal
                || damageInfo.ActualValue <= 0
                || IsSelfCausedDamage(damageInfo)
                || damageInfo.OwnerId == 0)
            {
                return false;
            }

            var attackerUuid = damageInfo.TopSummonerId != 0 ? damageInfo.TopSummonerId : damageInfo.AttackerUuid;
            if (attackerUuid == 0 || Utils.UuidToEntityType(attackerUuid) == (long)EEntityType.EntChar)
            {
                return false;
            }

            healerUuid = EncounterManager.Current.ResolveCombatEventLogActor(damageInfo.AttackerUuid, damageInfo.TopSummonerId);
            return healerUuid != 0;
        }

        /// <summary>
        /// 被ダメログとタイムラインに載せる、その通知の行動した側。回復はタイムラインに残す敵の回復なら回復した側(<see cref="TryResolveEnemyHealer"/>)、
        /// 被弾した本人が原因の被弾は本人、それ以外は加害者を寄せた先(見えて名前がある召喚体ならその召喚体、無ければ大元の召喚者)。決まらなければ 0。
        /// </summary>
        private static long ResolveCombatEventLogActor(SyncDamageInfo damageInfo, long targetUuid)
        {
            if (damageInfo.Type == EDamageType.Heal)
            {
                return TryResolveEnemyHealer(damageInfo, out var healerUuid) ? healerUuid : 0;
            }

            if (IsSelfCausedDamage(damageInfo))
            {
                return targetUuid;
            }

            return damageInfo.OwnerId == 0
                ? 0
                : EncounterManager.Current.ResolveCombatEventLogActor(damageInfo.AttackerUuid, damageInfo.TopSummonerId);
        }

        /// <summary>
        /// 敵の攻撃の発生源の技を、いま生きている実体から控える(表示するころには実体が残っていないので、記録の瞬間にしか決まらない)。
        /// バフ由来ならそのバフを付けた技、仮想体などが出したものならその実体を出した技(出現時に決めてある)。決まらなければ 0。
        /// </summary>
        private static (int BuffSourceSkillId, int SummonSourceSkillId) ResolveEnemyAttackSourceSkills(SyncDamageInfo damageInfo, DateTime arrivalTime)
        {
            var attackerUuid = damageInfo.TopSummonerId != 0 ? damageInfo.TopSummonerId : damageInfo.AttackerUuid;
            var buffSourceSkillId = damageInfo.DamageSource == EDamageSource.Buff
                && BuffInstanceIndex.Instance.TryResolveSourceSkill(
                    damageInfo.OwnerId,
                    damageInfo.AttackerUuid,
                    attackerUuid,
                    arrivalTime,
                    out var resolvedBuffSourceSkillId,
                    out _)
                ? resolvedBuffSourceSkillId
                : 0;
            var summonSourceSkillId = SummonSourceIndex.Instance.TryGet(damageInfo.AttackerUuid, out var summonSource)
                ? summonSource.SkillId
                : 0;
            return (buffSourceSkillId, summonSourceSkillId);
        }

        /// <summary>
        /// 同じ差分で、付けた敵が相手に当てた最初の被弾(回復でないもの)。被弾したときに付くバフ(敵のパッシブが付ける凍傷など)の引き金。
        /// 行動した側の決め方はタイムライン・被ダメログと同じ(<see cref="ResolveCombatEventLogActor(SyncDamageInfo, long)"/>)。無ければ null。
        /// </summary>
        private static SyncDamageInfo? FindTriggeringHit(IEnumerable<SyncDamageInfo>? deltaDamages, long casterUuid, long targetUuid)
        {
            if (deltaDamages is null)
            {
                return null;
            }

            foreach (var damageInfo in deltaDamages)
            {
                if (damageInfo.Type != EDamageType.Heal
                    && damageInfo.OwnerId != 0
                    && ResolveCombatEventLogActor(damageInfo, targetUuid) == casterUuid)
                {
                    return damageInfo;
                }
            }

            return null;
        }

        /// <summary>
        /// 敵の自傷(出し手の無いバフのダメージと落下)をタイムラインの敵から敵へのダメージに残す。加害者は本人。
        /// プレイヤーの自傷は被ダメログ(<see cref="AddSelfCausedTakenDamage"/>)。付与元の技の決め方も同じ。
        /// </summary>
        private static void AddEnemySelfCausedDamageLog(SyncDamageInfo damageInfo, long targetUuid, ExtraPacketData extraData)
        {
            var damage = ResolveDamageValue(damageInfo);
            if (damage == 0)
            {
                return;
            }

            var buffSourceSkillId = damageInfo.DamageSource == EDamageSource.Buff
                && BuffInstanceIndex.Instance.TryResolveSourceSkill(
                    damageInfo.OwnerId,
                    targetUuid,
                    targetUuid,
                    extraData.ArrivalTime,
                    out var resolvedBuffSourceSkillId,
                    out _)
                ? resolvedBuffSourceSkillId
                : 0;

            EncounterManager.Current.AddEnemyDamage(targetUuid, targetUuid, damageInfo.OwnerId, damageInfo.DamageSource, buffSourceSkillId, 0, damage, extraData);
        }

        /// <summary>
        /// 通知1件の差分で受けた敵のバフ。通知の全部の差分を処理し終えたところ(と、回を作り直す前)で <see cref="FlushPendingEnemyBuffLogs"/> がまとめて残す。
        /// 触るのはパケットのスレッドだけ(キャプチャを止めた後の消去を除く)。
        /// </summary>
        private static readonly List<(long TargetUuid, BuffInfo BuffInfo, IEnumerable<SyncDamageInfo>? DeltaDamages, ExtraPacketData ExtraData)> PendingEnemyBuffLogs = [];

        /// <summary>
        /// 控えていた敵のバフをタイムラインに残す(<see cref="AddEnemyBuffLog"/>)。通知1件の処理の最後に呼ぶ。
        /// 回を作り直す <c>EncounterManager.EnterDungeon</c> も、閉じる前に呼ぶ(通知の途中のボス部屋の区切りより前に届いた分は前の回のもの)。
        /// </summary>
        internal static void FlushPendingEnemyBuffLogs()
        {
            if (PendingEnemyBuffLogs.Count == 0)
            {
                return;
            }

            (long TargetUuid, BuffInfo BuffInfo, IEnumerable<SyncDamageInfo>? DeltaDamages, ExtraPacketData ExtraData)[] items = [.. PendingEnemyBuffLogs];
            PendingEnemyBuffLogs.Clear();
            foreach (var (targetUuid, buffInfo, deltaDamages, extraData) in items)
            {
                AddEnemyBuffLog(targetUuid, buffInfo, deltaDamages, extraData);
            }
        }

        /// <summary>
        /// バフの付けた側(<c>FireUuid</c>)を、被弾の加害者と同じく召喚体なら召喚者へ寄せる。
        /// </summary>
        private static long ResolveBuffCaster(long fireUuid)
        {
            var encounter = EncounterManager.Current;
            var topSummonerUuid = encounter.GetAttrKV(fireUuid, "AttrTopSummonerId") is long summonerUuid ? summonerUuid : 0;
            return encounter.ResolveCombatEventLogActor(fireUuid, topSummonerUuid);
        }

        /// <summary>
        /// <paramref name="holderUuid"/> が持つ親のバフ <paramref name="parentBuffId"/> を付けたのが、全部プレイヤー(とその召喚)か。親が見つからなければ false。
        /// </summary>
        private static bool IsParentBuffCastByPlayers(int parentBuffId, long holderUuid, DateTime arrivalTime)
        {
            var parentFireUuids = BuffInstanceIndex.Instance.GetHeldInstanceFireUuids(parentBuffId, holderUuid, arrivalTime);
            if (parentFireUuids.Count == 0)
            {
                return false;
            }

            foreach (var parentFireUuid in parentFireUuids)
            {
                if (Utils.UuidToEntityType(ResolveBuffCaster(parentFireUuid)) != (long)EEntityType.EntChar)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 敵が付けたバフをタイムラインに残す(差分の付与だけ。出現時の一覧は見る前から付いていたものなので残さない)。
        /// 差分の中では控えるだけで(<see cref="PendingEnemyBuffLogs"/>)、通知の全部の差分を処理してから呼ぶ。親のバフが別の実体の差分で後ろに届くことがある。
        /// 付けた側は通知の <c>FireUuid</c> で、召喚体は被弾の加害者と同じく寄せる。付けた側の無いものと、プレイヤー(とその召喚)が付けたものは残さない。
        /// 親のバフの中で付いた子のバフは、親を持つ実体が付けた側として届く。付けた側が持つ親を付けたのが全部プレイヤー(とその召喚)なら、
        /// プレイヤーの効果(光のプリズムの子のバフなど)なので同じく残さない。
        /// 付けた側と相手の種類は記録の入口(<c>Encounter.AddEnemyBuff</c>)が見る。
        /// 付与元(<c>FightSourceInfo</c>)は、技・弾・親のバフを発生源の形(<see cref="EnemyBuffRecord"/>)に置き換え、親のバフはそれを付けた技を控える。
        /// 親のバフを付けた技が分からず、同じ差分に付けた敵の被弾があるときは、その被弾を引き金としてその発生源を残す(被弾したときに付くバフ)。
        /// </summary>
        /// <param name="deltaDamages">同じ差分のダメージ・回復の通知。</param>
        private static void AddEnemyBuffLog(long targetUuid, BuffInfo buffInfo, IEnumerable<SyncDamageInfo>? deltaDamages, ExtraPacketData extraData)
        {
            var fireUuid = buffInfo.FireUuid;
            if (fireUuid == 0)
            {
                return;
            }

            var encounter = EncounterManager.Current;
            var casterUuid = ResolveBuffCaster(fireUuid);
            if (casterUuid == 0 || Utils.UuidToEntityType(casterUuid) == (long)EEntityType.EntChar)
            {
                return;
            }

            var source = buffInfo.FightSourceInfo;
            var sourceConfigId = source?.SourceConfigId ?? 0;
            var (damageSource, ownerId) = source is null || sourceConfigId <= 0
                ? (EDamageSource.Other, 0)
                : (EFightSource)source.FightSourceType switch
                {
                    EFightSource.Skill => (EDamageSource.Skill, sourceConfigId),
                    EFightSource.Bullet => (EDamageSource.Bullet, sourceConfigId),
                    EFightSource.Buff => (EDamageSource.Buff, sourceConfigId),
                    _ => (EDamageSource.Other, 0)
                };
            if (damageSource == EDamageSource.Buff && IsParentBuffCastByPlayers(ownerId, fireUuid, extraData.ArrivalTime))
            {
                return;
            }

            var buffSourceSkillId = damageSource == EDamageSource.Buff
                && BuffInstanceIndex.Instance.TryResolveSourceSkill(
                    ownerId,
                    fireUuid,
                    casterUuid,
                    extraData.ArrivalTime,
                    out var parentSourceSkillId,
                    out _)
                ? parentSourceSkillId
                : 0;
            var summonSourceSkillId = SummonSourceIndex.Instance.TryGet(fireUuid, out var summonSource)
                ? summonSource.SkillId
                : 0;

            // 被弾したときに付くバフ(敵のパッシブが付ける凍傷など)は、付与元が敵に乗った親のバフで、親を付けた技が分からない。
            // 引き金の被弾は同じ差分に一緒に届くので、その被弾の発生源を付与元にする。
            if (damageSource == EDamageSource.Buff
                && buffSourceSkillId == 0
                && FindTriggeringHit(deltaDamages, casterUuid, targetUuid) is { } triggeringHit)
            {
                ownerId = triggeringHit.OwnerId;
                damageSource = triggeringHit.DamageSource;
                (buffSourceSkillId, summonSourceSkillId) = ResolveEnemyAttackSourceSkills(triggeringHit, extraData.ArrivalTime);
            }

            encounter.AddEnemyBuff(
                casterUuid,
                targetUuid,
                buffInfo.BaseId,
                buffInfo.Duration,
                ownerId,
                damageSource,
                buffSourceSkillId,
                summonSourceSkillId,
                extraData);
        }

        /// <summary>
        /// 敵の回復をタイムラインに残す。バフ由来ならそのバフを付けた技を、回復した実体(仮想体など)ならそれを出した技を、
        /// 記録の瞬間に控える(被弾と同じ。表示するころには実体が残っていない)。
        /// </summary>
        /// <param name="attackerUuid">召喚元へ寄せた出し手(通知の最上位の召喚者、無ければ出し手そのもの)。付与元の候補に使う。</param>
        private static void AddEnemyHealLog(
            SyncDamageInfo heal,
            long healerUuid,
            long targetUuid,
            long attackerUuid,
            ExtraPacketData extraData)
        {
            var buffSourceSkillId = heal.DamageSource == EDamageSource.Buff
                && BuffInstanceIndex.Instance.TryResolveSourceSkill(
                    heal.OwnerId,
                    heal.AttackerUuid,
                    attackerUuid,
                    extraData.ArrivalTime,
                    out var resolvedBuffSourceSkillId,
                    out _)
                ? resolvedBuffSourceSkillId
                : 0;
            var summonSourceSkillId = SummonSourceIndex.Instance.TryGet(heal.AttackerUuid, out var summonSource)
                ? summonSource.SkillId
                : 0;

            EncounterManager.Current.AddEnemyHeal(
                healerUuid,
                targetUuid,
                heal.OwnerId,
                heal.DamageSource,
                buffSourceSkillId,
                summonSourceSkillId,
                heal.ActualValue,
                extraData);
        }

        /// <summary>
        /// 戦闘の出来事の通知か。通知の判定はここ1か所(<see cref="ProcessAoiSyncDelta"/> が差分ごとに当てる)。
        /// 最初の出来事が戦闘の時計の起点になり、その後の出来事は自動一時停止の時計を続ける(<c>Encounter.RecordCombatEvent</c>)。
        /// 通知の無い薬・料理・自然回復は <see cref="AddBuffHealing"/> が出来事にする。
        /// タイムラインと被ダメログに載る記録(ここで出来事にしない自傷・落下・フレンドリーファイアの被弾、敵から敵へのダメージ、詠唱・予告・敵が付けたバフなど)は、
        /// 記録の入口が出来事にする(<c>Encounter.RecordCombatEventForLog</c>)。
        /// プレイヤーかどうかは UUID の種類で見る(助っ人の NPC もプレイヤーに入る)。
        ///
        /// <list type="bullet">
        ///   <item>普通の回: プレイヤー → プレイヤー以外の与ダメ(値 0・Immune・Miss も)、プレイヤー以外 → プレイヤーの値が 0 でない被弾、
        ///   実際に HP が増えた回復の通知(出し手・相手は問わない)</item>
        ///   <item>計測の回: 自分 → プレイヤー以外の与ダメ(同上)、自分が出した回復の通知で実際に HP が増えたもの</item>
        /// </list>
        ///
        /// <para>
        /// 過剰回復だけの回復(実際に増えた HP の <c>ActualValue</c> が 0)・自傷・落下・フレンドリーファイアは出来事にしない。
        /// 加害者と <c>OwnerId</c> の扱いはダメージ・回復の振り分けと同じ(<c>OwnerId</c> 0・加害者 0 は捨てる)。
        /// </para>
        /// </summary>
        private static bool IsCombatEvent(SyncDamageInfo damageInfo, long targetUuid, bool isBenchmarkEncounter)
        {
            if (IsSelfCausedDamage(damageInfo) || damageInfo.OwnerId == 0)
            {
                return false;
            }

            var attackerUuid = damageInfo.TopSummonerId != 0 ? damageInfo.TopSummonerId : damageInfo.AttackerUuid;
            if (attackerUuid == 0)
            {
                return false;
            }

            var isAttackerPlayer = Utils.UuidToEntityType(attackerUuid) == (long)EEntityType.EntChar;
            var isTargetPlayer = Utils.UuidToEntityType(targetUuid) == (long)EEntityType.EntChar;
            var isHeal = damageInfo.Type == EDamageType.Heal;
            var isSelfAttacker = attackerUuid == AppState.PlayerUUID;

            if (isHeal)
            {
                return damageInfo.ActualValue > 0 && (!isBenchmarkEncounter || isSelfAttacker);
            }

            if (isBenchmarkEncounter)
            {
                return isSelfAttacker && !isTargetPlayer;
            }

            return (isAttackerPlayer && !isTargetPlayer)
                || (!isAttackerPlayer && isTargetPlayer && ResolveDamageValue(damageInfo) != 0);
        }

        /// <summary>
        /// 本人が原因の被弾(<see cref="IsSelfCausedDamage"/>)を被ダメログにだけ残す。
        /// バフ由来なら、本人が付けたそのバフの付与元の技を控える(技の行の名前になる)。
        /// </summary>
        private static void AddSelfCausedTakenDamage(SyncDamageInfo damageInfo, long targetUuid, ExtraPacketData extraData)
        {
            var skillId = SkillSourceResolver.Resolve(damageInfo.DamageSource, damageInfo.OwnerId, damageInfo.HitEventId).Key;
            var damage = ResolveDamageValue(damageInfo);
            var shieldBreak = damageInfo.Type == EDamageType.Absorbed ? damage : 0;

            var buffSourceSkillId = 0;
            if (damageInfo.DamageSource == EDamageSource.Buff
                && BuffInstanceIndex.Instance.TryResolveSourceSkill(
                    damageInfo.OwnerId,
                    targetUuid,
                    targetUuid,
                    extraData.ArrivalTime,
                    out var resolvedBuffSourceSkillId,
                    out _))
            {
                buffSourceSkillId = resolvedBuffSourceSkillId;
            }

            EncounterManager.Current.AddTakenDamage(
                targetUuid,
                targetUuid,
                skillId,
                damageInfo.OwnerId,
                damageInfo.DamageSource,
                buffSourceSkillId,
                0,
                damageInfo.OwnerLevel,
                damage,
                damageInfo.HpLessenValue,
                shieldBreak,
                damageInfo.Property,
                damageInfo.Type,
                damageInfo.DamageMode,
                (damageInfo.TypeFlag & 1) == 1,
                damageInfo.LuckyValue != 0,
                (damageInfo.TypeFlag & 0B100) == 0B100,
                damageInfo.IsMiss,
                damageInfo.IsDead,
                extraData);
        }

        private static void UpdateProfessionId(long uuid, int professionId)
        {
            EncounterManager.Current.SetAttrKV(uuid, "AttrProfessionId", professionId);

            if (!IsSelfPlayer(uuid))
            {
                return;
            }

            PlayerSkillLevelStateStore.SetSelfCurrentProfessionId(professionId);
            AppState.ProfessionId = professionId;
            RefreshSelfRosterEntry(uuid);
        }

        /// <summary>
        /// 職業・タレント・スキルの更新後に自分の行を作り直す。
        ///
        /// <para>
        /// <b>ここで特化は決めない。</b> <c>ProfessionTalentInfo.TalentStageCfgId</c> は
        /// 「どのタレントツリーを選んでいるか」であって「特化アビリティを装着しているか」ではなく、
        /// 未装着でもツリーの値を返し続ける。特化は自分も他人と同じく特化マーカーバフだけで決める。
        /// </para>
        /// </summary>
        private static void RefreshSelfRosterEntry(long uuid)
        {
            if (uuid == 0)
            {
                return;
            }

            PlayerRosterProjection.UpsertSelf(uuid);
        }

        /// <summary>取ったタレントの木の節の番号(<c>ProfessionTalentInfo.TalentNodeIds</c>)を、昇順・重なりなしにする。</summary>
        private static int[] ToTalentNodeIds(IEnumerable<uint> nodeIds)
        {
            return [.. nodeIds.Select(nodeId => (int)nodeId).Distinct().Order()];
        }

        /// <summary>
        /// パーティ情報の応答(パーティ画面を開くと届く)。応答は <c>GetTeamInfo_Ret { ret }</c> で1段包まれている。
        /// 読めなければ例外のまま NetCap の守りに任せる(ログに出す)。
        /// </summary>
        private static void ProcessGetTeamInfoReturn(
            ReadOnlySpan<byte> payloadBuffer,
            uint returnUid,
            ExtraPacketData extraData)
        {
            var response = Zproto.World.Types.GetTeamInfo_Ret.Parser.ParseFrom(payloadBuffer).Ret;
            if (response is null)
            {
                Log.Warning("Team information response has no reply body (returnUid={ReturnUid}, length={Length})", returnUid, payloadBuffer.Length);
                return;
            }

            GrpcTeamManager.ProcessGetTeamInfo(response, extraData);
        }

        /// <summary>
        /// 自分が技を使うときの上りの要求 <c>UseSlot</c> の技ID。応答の番号(returnUid)ごとに、応答が来るまで控える。
        /// 消すときは <see cref="PlayerDeathStates"/> と同じく新しい辞書に差し替える(<see cref="ClearReceivedStateStores"/>)。
        /// </summary>
        private static Dictionary<uint, int> PendingUseSlotSkills = [];

        /// <summary>
        /// 自分が技を使うときの上りの要求 <c>UseSlot</c>。種類が技で、自動(<c>IsPassive</c>)でない要求の技IDを応答まで控える。
        ///
        /// <para>
        /// <b>自分の推移グラフの技の記録は、差分の技の開始ではなくこの要求と成功の応答から取る</b>(<see cref="ProcessUseSlotReturn"/>)。
        /// 短い技(エクストラスキルなど)はサーバーの1回の同期の中で開始と終わりが畳まれ、差分には <c>AttrSkillId</c> が値なしで届くだけで、
        /// 何の技か分からないことがある。
        /// </para>
        /// </summary>
        private static void ProcessUseSlotCall(
            ReadOnlySpan<byte> payloadBuffer,
            uint returnUid,
            ExtraPacketData extraData)
        {
            var request = Zproto.World.Types.UseSlot.Parser.ParseFrom(payloadBuffer).VRequest;
            if (request is null
                || request.UseType != Zproto.EUseSlotType.UseSlotTypeSkill
                || request.ExtraData.IsEmpty)
            {
                return;
            }

            var skill = Zproto.UseSkillParam.Parser.ParseFrom(request.ExtraData);
            if (skill.IsPassive || skill.Skillid <= 0)
            {
                return;
            }

            PendingUseSlotSkills[returnUid] = skill.Skillid;
        }

        /// <summary>
        /// <c>UseSlot</c> の応答。成功なら、要求の技を自分の推移グラフの技の記録に残す(時刻は応答の到着時刻)。失敗なら捨てる。
        /// </summary>
        private static void ProcessUseSlotReturn(
            ReadOnlySpan<byte> payloadBuffer,
            uint returnUid,
            ExtraPacketData extraData)
        {
            if (!PendingUseSlotSkills.Remove(returnUid, out var skillId))
            {
                return;
            }

            if (Zproto.World.Types.UseSlot_Ret.Parser.ParseFrom(payloadBuffer).Ret != Zproto.EErrorCode.ErrSuccess)
            {
                return;
            }

            var selfUuid = AppState.PlayerUUID;
            if (selfUuid == 0)
            {
                Log.Warning("UseSlot succeeded for skill {SkillId} before the player's own entity was known; not recording it in the graph skill log", skillId);
                return;
            }

            EncounterManager.Current.AddSkillActivation(selfUuid, skillId, extraData.ArrivalTime);
        }

        /// <summary>
        /// 過去のチャットの記録の要求 <c>GetChipChatRecords</c> の相手(チャンネルとプライベートトークの相手)と、最新の分から読み直す要求(起点 0)か。
        /// 応答の番号(returnUid)ごとに、応答が来るまで控える。
        /// 消すときは <see cref="PendingUseSlotSkills"/> と同じく新しい辞書に差し替える(<see cref="ClearReceivedStateStores"/>)。
        /// </summary>
        private static Dictionary<uint, (ChatTarget Target, bool IsLatestLoad)> PendingChatRecordRequests = [];

        /// <summary>
        /// 過去のチャットの記録の要求。起点(<c>descMsgId</c>)が 0 の要求は、ゲームが一覧を空にしてから最新の記録を読むとき
        /// (ログインの後・ワールドと初心者のチャンネルの切り替え・プライベートトークを初めて開いたとき)なので、控えの一覧も空にする。
        /// 起点のある要求は、ゲームの一覧の一番古いメッセージより前の分(画面を上へ引っ張ったとき)。
        /// </summary>
        private static void ProcessGetChipChatRecordsCall(
            ReadOnlySpan<byte> payloadBuffer,
            uint returnUid,
            ExtraPacketData extraData)
        {
            var request = ChitChat.Types.GetChipChatRecords.Parser.ParseFrom(payloadBuffer).VRequest;
            if (request is null || !ChatMessageStore.IsKeptChannel(request.ChannelType))
            {
                return;
            }

            var target = new ChatTarget(
                request.ChannelType,
                request.ChannelType == ChitChatChannelType.ChannelPrivate ? request.TargetId : 0);
            var isLatestLoad = request.DescMsgId == 0;
            if (isLatestLoad)
            {
                ChatMessageStore.BeginRecordLoad(target);
            }

            PendingChatRecordRequests[returnUid] = (target, isLatestLoad);
        }

        /// <summary>
        /// 過去のチャットの記録の応答。新しい順の一覧で届く。「記録なし」(<c>ErrChatRecordListIsEmpty</c>)は古い分が無いという意味で、失敗ではない。
        /// 最新の分から読み直した要求の応答なら、成功でも記録なしでも、その窓の過去のチャットを受け取ったことにする。
        /// </summary>
        private static void ProcessGetChipChatRecordsReturn(
            ReadOnlySpan<byte> payloadBuffer,
            uint returnUid,
            ExtraPacketData extraData)
        {
            if (!PendingChatRecordRequests.Remove(returnUid, out var pending))
            {
                return;
            }

            var (target, isLatestLoad) = pending;

            var reply = ChitChat.Types.GetChipChatRecords_Ret.Parser.ParseFrom(payloadBuffer).Ret;
            if (reply is null)
            {
                Log.Warning("Chat records response has no reply body (returnUid={ReturnUid}, length={Length})", returnUid, payloadBuffer.Length);
                return;
            }

            switch (reply.ErrCode)
            {
                case EErrorCode.ErrSuccess:
                    ChatMessageStore.AddRecords(target, reply.MultiMsgList, isLatestLoad);
                    break;
                case EErrorCode.ErrChatRecordListIsEmpty:
                    ChatMessageStore.AddRecords(target, [], isLatestLoad);
                    break;
                default:
                    Log.Warning(
                        "Chat records request failed with {ErrorCode} (channel={Channel}, partner={PartnerId})",
                        reply.ErrCode,
                        target.Channel,
                        target.PartnerId);
                    break;
            }
        }

        /// <summary>プライベートトークの相手の一覧の応答。ゲームはログインの後の最初の入場と再接続で、過去のチャットより先に要求する。</summary>
        private static void ProcessGetPrivateChatTargetsReturn(
            ReadOnlySpan<byte> payloadBuffer,
            uint returnUid,
            ExtraPacketData extraData)
        {
            var reply = ChitChat.Types.GetPrivateChatTargets_Ret.Parser.ParseFrom(payloadBuffer).Ret;
            if (reply is null)
            {
                Log.Warning("Private chat targets response has no reply body (returnUid={ReturnUid}, length={Length})", returnUid, payloadBuffer.Length);
                return;
            }

            if (reply.ErrCode != EErrorCode.ErrSuccess)
            {
                Log.Warning("Private chat targets request failed with {ErrorCode}", reply.ErrCode);
                return;
            }

            ChatMessageStore.ReplacePrivatePartners(reply.TargetList);
        }

        /// <summary>
        /// チャットの送信の要求(上り)。プライベートトークなら相手を控える(<see cref="ChatMessageStore.AddOwnPrivateSend"/>)。
        /// 自分の発言の通知は必ずこの要求の後に届くので、自分のキャラIDが分からないうちも、届いた通知が自分の発言かを見分けられる。
        /// </summary>
        private static void ProcessSendChitChatMsgCall(
            ReadOnlySpan<byte> payloadBuffer,
            uint returnUid,
            ExtraPacketData extraData)
        {
            var request = ChitChat.Types.SendChitChatMsg.Parser.ParseFrom(payloadBuffer).VRequest;
            if (request is null || request.ChannelType != ChitChatChannelType.ChannelPrivate)
            {
                return;
            }

            ChatMessageStore.AddOwnPrivateSend(request.MsgInfo?.TargetId ?? 0);
        }

        /// <summary>プライベートトークの既読の要求(上り)。<see cref="PrivateChatRead"/> を上げる。</summary>
        private static void ProcessSetPrivateChatHasReadCall(
            ReadOnlySpan<byte> payloadBuffer,
            uint returnUid,
            ExtraPacketData extraData)
        {
            var request = ChitChat.Types.SetPrivateChatHasRead.Parser.ParseFrom(payloadBuffer).VRequest;
            if (request is null)
            {
                return;
            }

            PrivateChatRead?.Invoke(request.TargetId, request.MaxReadMsgId);
        }

        /// <summary>
        /// 名刺照会の応答。入っている値を全部控える(<see cref="SocialDataStore.ApplySocialData"/>)。
        /// 照会はゲームが送ったものだけで、照会の種類によって入っている部分が違う。
        /// 名簿に載っている人の値が変わったら、名簿の行を作り直す(空の項目を名刺の値で埋めるため)。
        /// </summary>
        private static void ProcessGetSocialDataReturn(
            ReadOnlySpan<byte> payloadBuffer,
            uint returnUid,
            ExtraPacketData extraData)
        {
            var data = ParseGetSocialDataReply(payloadBuffer)?.Data;
            if (data is not null
                && SocialDataStore.ApplySocialData(data)
                && PlayerRosterStore.Instance.Snapshot.Any(entry => entry.CharacterId == data.CharId))
            {
                PlayerRosterProjection.RebuildRoster();
            }
        }

        /// <summary>名刺照会の応答は <c>GetSocialData_Ret { ret }</c> で1段包まれている。</summary>
        private static GetSocialDataReply? ParseGetSocialDataReply(ReadOnlySpan<byte> payload)
        {
            return Zproto.World.Types.GetSocialData_Ret.Parser.ParseFrom(payload).Ret;
        }

        /// <summary>
        /// プレイヤー以外の実体に出した元(<c>AttrFightSourceInfo</c>)が届いたら、出した技を <see cref="SummonSourceIndex"/> に控える。
        /// 技ならその技、バフならこの瞬間に生きているバフの付与元の技。どちらでもないか引けなければ控えを消す。
        /// 属性を当てた後に呼ぶ(同じ束で届いた最上位の召喚者を使う)。
        /// </summary>
        private static void RecordSummonSource(long uuid, RepeatedField<Attr> attrs, DateTime arrivalTime)
        {
            if (Utils.UuidToEntityType(uuid) == (long)EEntityType.EntChar)
            {
                return;
            }

            foreach (var attr in attrs)
            {
                if ((EAttrType)attr.Id != EAttrType.AttrFightSourceInfo || attr.RawData == null)
                {
                    continue;
                }

                var sourceSkillId = 0;
                if (attr.RawData.Length > 0)
                {
                    var source = FightSourceInfo.Parser.ParseFrom(attr.RawData);
                    if (source.FightSourceType == (int)EFightSource.Skill)
                    {
                        sourceSkillId = source.SourceConfigId;
                    }
                    else if (source.FightSourceType == (int)EFightSource.Buff)
                    {
                        var topSummonerUuid = EncounterManager.Current.GetAttrKV(uuid, "AttrTopSummonerId") as long? ?? 0;
                        if (BuffInstanceIndex.Instance.TryResolveSourceSkill(source.SourceConfigId, uuid, topSummonerUuid, arrivalTime, out var buffSourceSkillId, out _))
                        {
                            sourceSkillId = buffSourceSkillId;
                        }
                    }
                }

                if (sourceSkillId > 0)
                {
                    var summonerUuid = EncounterManager.Current.GetAttrKV(uuid, "AttrSummonerId") as long? ?? 0;
                    SummonSourceIndex.Instance.Set(uuid, sourceSkillId, summonerUuid);
                }
                else
                {
                    SummonSourceIndex.Instance.Remove(uuid);
                }
            }
        }

        /// <summary>
        /// プレイヤー以外の実体の出どころ(付与元と召喚者)を <see cref="SourceLandingResolver"/> に控える。属性を当てた後に呼ぶ。
        /// 出現(<paramref name="isAppear"/>)で付与元が無ければ、同じ UUID の前の控えを消す。
        /// </summary>
        private static void RecordSourceLanding(long uuid, RepeatedField<Attr>? attrs, DateTime arrivalTime, bool isAppear)
        {
            if (Utils.UuidToEntityType(uuid) == (long)EEntityType.EntChar)
            {
                return;
            }

            FightSourceInfo? source = null;
            if (attrs != null)
            {
                foreach (var attr in attrs)
                {
                    if ((EAttrType)attr.Id == EAttrType.AttrFightSourceInfo && attr.RawData is { Length: > 0 })
                    {
                        source = FightSourceInfo.Parser.ParseFrom(attr.RawData);
                    }
                }
            }

            if (source == null && !isAppear)
            {
                return;
            }

            var summonerUuid = EncounterManager.Current.GetAttrKV(uuid, "AttrSummonerId") as long? ?? 0;
            var topSummonerUuid = EncounterManager.Current.GetAttrKV(uuid, "AttrTopSummonerId") as long? ?? 0;
            var attrId = EncounterManager.Current.GetAttrKV(uuid, "AttrId") is { } attrIdValue ? Convert.ToInt32(attrIdValue) : 0;
            SourceLandingResolver.Instance.RecordEntity(uuid, attrId, source, summonerUuid, topSummonerUuid, arrivalTime, isAppear);
        }

        /// <summary>
        /// モンスターの種別ID(<c>AttrId</c>)か召喚者(<c>AttrSummonerId</c>・<c>AttrTopSummonerId</c>)が届いたら、
        /// 今の値を <see cref="NearbyMonsterIndex"/> に入れる。属性を当てた後に呼ぶ。
        /// </summary>
        private static void RecordNearbyMonster(long uuid, RepeatedField<Attr> attrs)
        {
            if (Utils.UuidToEntityType(uuid) != (long)EEntityType.EntMonster
                || !attrs.Any(attr => ((EAttrType)attr.Id == EAttrType.AttrId && attr.RawData != null)
                    || (EAttrType)attr.Id is EAttrType.AttrSummonerId or EAttrType.AttrTopSummonerId))
            {
                return;
            }

            if (EncounterManager.Current.GetAttrKV(uuid, "AttrId") is int monsterId && monsterId > 0)
            {
                NearbyMonsterIndex.Instance.Set(uuid, new NearbyMonsterIndex.NearbyMonster(
                    monsterId,
                    EncounterManager.Current.GetAttrKV(uuid, "AttrSummonerId") as long? ?? 0,
                    EncounterManager.Current.GetAttrKV(uuid, "AttrTopSummonerId") as long? ?? 0));
            }
            else
            {
                NearbyMonsterIndex.Instance.Remove(uuid);
            }
        }

        /// <param name="arrivalUtc">属性が届いたメッセージの到着時刻。技の発動(CD の推定・発動の数)と死亡数に使う。</param>
        public static void ProcessAttrs(long uuid, RepeatedField<Attr> attrs, DateTime arrivalUtc)
        {
            foreach (var attr in attrs)
            {
                if (attr.Id == 0 || attr.RawData == null)
                {
                    continue;
                }
                var reader = new Google.Protobuf.CodedInputStream(attr.RawData.ToByteArray());

                EAttrType attrId = (EAttrType)attr.Id;
                string attrIdName = attrId.ToString();
                bool isNoValue = attr.RawData.Length == 0;
                switch (attrId)
                {
                    case EAttrType.AttrName:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? "" : reader.ReadString().TrimEnd());
                        break;
                    case EAttrType.AttrHatedName:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? "" : reader.ReadString().TrimEnd());
                        break;
                    case EAttrType.AttrSkillId:
                        {
                            // 詠唱が終わると「値なし」で届くので 0 にする。0 は「撃った」ではなく「詠唱が終わった」の合図で、発動には数えない
                            // (数えると SkillMetrics[0] が作られ、発動の数も水増しされる)。
                            var skillId = isNoValue ? 0 : reader.ReadInt32();
                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, skillId);
                            if (skillId > 0)
                            {
                                EncounterManager.Current.RegisterSkillActivation(uuid, skillId, arrivalUtc);
                            }

                            break;
                        }
                    case EAttrType.AttrIsSilence:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, !isNoValue && reader.ReadBool());
                        break;
                    case EAttrType.AttrCdAcceleratePct:
                        {
                            var accelerationPct = isNoValue ? 0 : reader.ReadInt32();
                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, accelerationPct);
                            if (uuid == currentUserUuid || uuid == AppState.PlayerUUID)
                            {
                                SkillCooldownStateStore.UpdateSelfAcceleration(uuid, accelerationPct);
                            }
                            break;
                        }
                    case EAttrType.AttrProfessionId:
                        {
                            var incomingProfessionId = isNoValue ? 0 : reader.ReadInt32();
                            if (IsSelfPlayer(uuid))
                            {
                            }

                            UpdateProfessionId(uuid, incomingProfessionId);
                            break;
                        }
                    case EAttrType.AttrCamp:
                        {
                            var camp = isNoValue ? 0 : reader.ReadInt32();
                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, camp);
                            NearbyEntityProjection.UpdateCamp(uuid, !isNoValue, camp);
                            break;
                        }
                    case EAttrType.AttrFightPoint:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    case EAttrType.AttrLevel:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    case EAttrType.AttrRankLevel:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    case EAttrType.AttrCri:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    case EAttrType.AttrLuck:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    case EAttrType.AttrHp:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrMaxHp:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrOriginEnergy:
                        {
                            var rawOriginEnergy = isNoValue ? 0 : reader.ReadInt32();
                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, rawOriginEnergy);
                            EncounterManager.Current.SetAttrKV(
                                uuid,
                                CurrentStaminaSnapshotAttribute,
                                Math.Max(rawOriginEnergy / OriginEnergyRawScale, 0d));
                            break;
                        }
                    case EAttrType.AttrMaxOriginEnergy:
                        {
                            var rawMaxOriginEnergy = isNoValue ? 0 : reader.ReadInt32();
                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, rawMaxOriginEnergy);
                            EncounterManager.Current.SetAttrKV(
                                uuid,
                                MaxStaminaSnapshotAttribute,
                                Math.Max(rawMaxOriginEnergy / OriginEnergyRawScale, 0d));
                            break;
                        }
                    case EAttrType.AttrMaxStunned:
                    case EAttrType.AttrStunned:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    // 攻撃力・防御力は物理も魔法も同じ形で届く。魔法側を default(int32)へ落とさず、まとめて int64 で読む。
                    case EAttrType.AttrAttack:
                    case EAttrType.AttrMattack:
                    case EAttrType.AttrDefense:
                    case EAttrType.AttrMdefense:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrPos:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? new Vec3() : Vec3.Parser.ParseFrom(reader));
                        break;
                    case EAttrType.AttrTargetPos:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? new Vec3() : Vec3.Parser.ParseFrom(reader));
                        break;
                    case EAttrType.AttrFinalTargetPos:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? new Vec3() : Vec3.Parser.ParseFrom(reader));
                        break;
                    case EAttrType.AttrDmgTargetPos:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? new Vec3() : Vec3.Parser.ParseFrom(reader));
                        break;
                    case EAttrType.AttrBulletTargetPos:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? new Vec3() : Vec3.Parser.ParseFrom(reader));
                        break;
                    case EAttrType.AttrSummonerPos:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? new Vec3() : Vec3.Parser.ParseFrom(reader));
                        break;
                    case EAttrType.AttrTargetPartPos:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? new Vec3() : Vec3.Parser.ParseFrom(reader));
                        break;
                    case EAttrType.AttrParadeLeaderPos:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? new Vec3() : Vec3.Parser.ParseFrom(reader));
                        break;
                    case EAttrType.AttrState:
                        {
                            var state = isNoValue ? (EActorState)0 : (EActorState)reader.ReadInt32();
                            EncounterManager.Current.SetAttrKV(uuid, "AttrState", state);
                            if (state == EActorState.ActorStateDead)
                            {
                                EncounterManager.Current.RecordNonPlayerDeath(uuid, arrivalUtc);
                            }

                            break;
                        }
                    case EAttrType.AttrShieldList:
                        {
                            if (isNoValue)
                            {
                                EncounterManager.Current.SetAttrKV(uuid, attrIdName, new List<ShieldInfo>());
                                break;
                            }

                            List<ShieldInfo> shieldList = new();
                            while (!reader.IsAtEnd)
                            {
                                int len = reader.ReadLength();

                                ShieldInfo shield = new();

                                reader.ReadMessage(shield);
                                shieldList.Add(shield);
                            }
                            EncounterManager.Current.SetAttrKV(uuid, "AttrShieldList", shieldList);
                            break;
                        }
                    case EAttrType.AttrActionTime:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrActionUpperTime:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrStiffTarget:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrStiffStageTime:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrSkillBeginTime:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrFirstAttack:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrCombatStateTime:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrTargetUuid:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrTargetId:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrSummonerId:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrTopSummonerId:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrHateList:
                        if (isNoValue)
                        {
                            HateObservationRecorder.Record(EncounterManager.Current, uuid, [], arrivalUtc);
                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, new List<HateInfo>());
                            break;
                        }

                        List<HateInfo> hateList = new();
                        while (!reader.IsAtEnd)
                        {
                            int len = reader.ReadLength();

                            HateInfo hate = new();

                            reader.ReadMessage(hate);
                            hateList.Add(hate);
                        }
                        HateObservationRecorder.Record(EncounterManager.Current, uuid, hateList, arrivalUtc);
                        EncounterManager.Current.SetAttrKV(uuid, "AttrHateList", hateList);
                        break;
                    case EAttrType.AttrSlot:
                        {
                            var slotPayload = isNoValue ? [] : attr.RawData.ToByteArray();
                            var isSelfSlot = IsSelfPlayer(uuid);

                            // 自分のアクションバー。枠番号つきで全枠が届く唯一の経路。
                            // 他人には届かないので、自分のときだけ取り込む。
                            if (isSelfSlot && slotPayload.Length > 0)
                            {
                                var actionBar = Zproto.Slot.Parser.ParseFrom(slotPayload);
                                var slotMap = new Dictionary<int, int>(actionBar.Slots.Count);
                                foreach (var slotPair in actionBar.Slots)
                                {
                                    // 空枠は skillId=0 のまま入れる。落とすと枠の位置が失われる。
                                    slotMap[slotPair.Key] = slotPair.Value.SkillId;
                                }

                                PlayerSkillLevelStateStore.ReplaceSelfActionBarSlots(slotMap);
                            }

                            break;
                        }
                    case EAttrType.AttrSeasonLevel:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    case EAttrType.AttrSeasonStrength:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    case EAttrType.AttrSeasonStrengthAdd:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    case EAttrType.AttrSeasonStrengthTotal:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                    case EAttrType.AttrSkillLevelIdList:
                        if (isNoValue)
                        {
                            var emptySkillLevels = new List<DataTypes.Skills.SkillLevelInfo>();
                            if (IsSelfPlayer(uuid))
                            {
                                PlayerSkillLevelStateStore.UpdateSelfRawSkillLevels(
                                    emptySkillLevels);
                            }

                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, emptySkillLevels);
                            break;
                        }

                        List<DataTypes.Skills.SkillLevelInfo> skillLevelInfoList = new();
                        while (!reader.IsAtEnd)
                        {
                            int len = reader.ReadLength();

                            SkillLevelInfo info = new();

                            reader.ReadMessage(info);
                            skillLevelInfoList.Add(new DataTypes.Skills.SkillLevelInfo(info));
                        }
                        if (IsSelfPlayer(uuid))
                        {
                            PlayerSkillLevelStateStore.UpdateSelfRawSkillLevels(
                                skillLevelInfoList);
                        }

                        EncounterManager.Current.SetAttrKV(uuid, "AttrSkillLevelIdList", skillLevelInfoList);

                        // AOI同期でしか届かないので、相手がマップ外へ出ると取得できなくなる。
                        // 自分以外のパーティメンバーの分だけ補完用に保持する(判定はキャッシュ側)。
                        PartyMemberCache.Instance.SetSkillLevels(
                            Utils.UuidToEntityId(uuid),
                            skillLevelInfoList);
                        break;
                    case EAttrType.AttrTeamId:
                        var teamId = isNoValue ? 0L : reader.ReadInt64();
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, teamId);
                        GrpcTeamManager.ProcessEntityTeamId(uuid, teamId);
                        break;
                    case EAttrType.AttrStateTime:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrRideUuid:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrDeadTime:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0L : reader.ReadInt64());
                        break;
                    case EAttrType.AttrEquipData:
                        if (isNoValue)
                        {
                            EncounterManager.Current.SetAttrKV(uuid, attrIdName, new List<Zproto.EquipNine>());
                            break;
                        }

                        List<Zproto.EquipNine> equipNineList = new();
                        while (!reader.IsAtEnd)
                        {
                            int len = reader.ReadLength();

                            Zproto.EquipNine info = new();

                            reader.ReadMessage(info);
                            equipNineList.Add(info);
                        }
                        EncounterManager.Current.SetAttrKV(uuid, "AttrEquipData", equipNineList);
                        break;
                    default:
                        EncounterManager.Current.SetAttrKV(uuid, attrIdName, isNoValue ? 0 : reader.ReadInt32());
                        break;
                }
            }
            EnemyCatalogStore.Current.Observe(EncounterManager.Current, uuid, arrivalUtc);
            if (EncounterManager.Current.Entities.TryGetValue(uuid,out var observedEnemy))
                DefeatedEnemyRecorder.Observe(EncounterManager.Current,observedEnemy,arrivalUtc);
        }

        /// <param name="isFullList">
        /// 入場(自分)・出現(他人)の一覧なら true。その人の CD の短縮の控え(<see cref="CooldownModifierStore"/>)を丸ごと置き換える。
        /// 周りの差分なら false で、控えに重ねる。
        /// </param>
        public static void ProcessTempAttrs(long uuid, IEnumerable<Zproto.TempAttr> tempAttrs, DateTime arrivalUtc, bool isFullList)
        {
            // 自分なら、短縮が変わる前の速さでここまでの CD の進みを確定してから変える。
            SkillCooldownStateStore.SettleSelfProgress(uuid);
            if (Utils.UuidToEntityType(uuid) != (long)EEntityType.EntChar)
            {
                return;
            }

            if (isFullList)
            {
                CooldownModifierStore.Replace(uuid, tempAttrs, arrivalUtc);
            }
            else
            {
                CooldownModifierStore.Merge(uuid, tempAttrs, arrivalUtc);
            }
        }

        public static void ProcessSyncNearEntities(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            var syncNearEntities = SyncNearEntities.Parser.ParseFrom(payloadBuffer);

            foreach (var disappearedEntity in syncNearEntities.Disappear)
            {
                PlayerRosterProjection.RemoveNearbyPlayer(disappearedEntity.Uuid);
                NearbyEntityProjection.RemoveEntity(disappearedEntity.Uuid);
                SummonSourceIndex.Instance.Remove(disappearedEntity.Uuid);
                NearbyMonsterIndex.Instance.Remove(disappearedEntity.Uuid);
                PlayerDeathStates.Remove(disappearedEntity.Uuid);
                LastPassiveHealTicks.Remove(disappearedEntity.Uuid);
                EncounterManager.Current.RemoveDisappearedEntity(disappearedEntity.Uuid);
            }

            foreach (var entity in syncNearEntities.Appear)
            {
                EntityOriginIndex.Instance.RemoveDisappeared(entity.Uuid);
                long uid = Utils.UuidToEntityId(entity.Uuid);

                if (uid == 0)
                {
                    continue;
                }

                EncounterManager.Current.SetEntityType(entity.Uuid, entity.EntType);
                ResetCombatStateMissingFromFullAttrs(entity.Uuid, entity.Attrs?.Attrs);

                // 出現の一覧はその実体の一時属性の全部(無ければ一時属性が無い)。前に見たときの値を置き換える。
                var appearTempAttrs = entity.TempAttrs?.Attrs ?? (IEnumerable<Zproto.TempAttr>)[];
                ProcessTempAttrs(entity.Uuid, appearTempAttrs, extraData.ArrivalTime, isFullList: true);

                var attrCollection = entity.Attrs;
                if (attrCollection?.Attrs != null)
                {
                    ProcessAttrs(entity.Uuid, attrCollection.Attrs, extraData.ArrivalTime);
                    RecordSummonSource(entity.Uuid, attrCollection.Attrs, extraData.ArrivalTime);
                    RecordNearbyMonster(entity.Uuid, attrCollection.Attrs);

                    // 名前・クラスなどの出所を受けた印(名簿へ入れる下の行より前に立てる)。
                    if (entity.EntType == EEntityType.EntChar)
                    {
                        EncounterManager.Current.GetOrCreateEntity(entity.Uuid).MarkAppearAttributesReceived();
                    }
                }

                RecordSourceLanding(entity.Uuid, attrCollection?.Attrs, extraData.ArrivalTime, isAppear: true);


                ApplyAppearBuffSnapshot(entity, extraData);

                PlayerRosterProjection.AddOrUpdateNearbyPlayer(entity.Uuid);
                NearbyEntityProjection.AddOrUpdateAppearedEntity(entity.Uuid);
            }

            // 表に名前の無いバフの由来は、通知の全部の付与を索引に入れてからたどる。
            BuffOriginResolver.Instance.ResolvePending();

            BattleStateMachine.CheckDeferredCalls();
        }

        /// <summary>
        /// AOI出現メッセージが運ぶ全バフスナップショット(<c>Entity.BuffInfos</c>, field 7)を
        /// 特化判定へ渡す。
        ///
        /// <para>
        /// 差分(<c>AoiSyncDelta.BuffEffect</c>)は「見ている間に付いたバフ」しか運ばないので、
        /// 既に付いている特化マーカーバフは差分だけでは永久に届かない。
        /// このスナップショットが唯一「いま何を持っているか」を全部運ぶ経路で、
        /// 「マーカーが1つも無い = アビリティ未装着」を確定できるのもここだけ。
        /// </para>
        ///
        /// <para>
        /// <b>表示へは種類を問わず(他のプレイヤーも敵も)、特化判定へはプレイヤーだけ渡す。</b> 差分は「見ている間に付いたバフ」しか運ばないので、
        /// 流さないと<b>自分が見る前から乗っているバフが一生表示されない</b>(敵ではボスの常駐バフや、他の人が先に付けたデバフ)。
        /// 持続の長い料理(1800秒)・薬剤ほど当たりやすく、マップ切替で
        /// <c>ActiveBuffStore.Clear()</c> したあとも復活しない、という見え方になっていた。
        /// 自分は <c>SyncToMeDeltaInfo</c> がイベントとして全部送ってくるので影響を受けない。
        /// </para>
        ///
        /// <para>
        /// 残り時間の起点は <c>NotifyBuffEvent</c> 側の既存の2段に任せる —
        /// 付与時刻と受信時刻の選別と、同一実体(<c>BuffUuid</c>＋付与時刻＋持続が一致)なら
        /// 観測時刻を据え置く扱い。<b>後者があるので、出現のたびに再送されても
        /// 残り時間は満額へ巻き戻らない。</b>
        /// </para>
        /// </summary>
        private static void ApplyAppearBuffSnapshot(Zproto.Entity entity, ExtraPacketData extraData)
        {
            // 一覧そのものが届いていない出現では、何が付いているか分からないので触らない(受けた印も立てない)。
            if (entity.BuffInfos is not { } appearBuffList)
            {
                return;
            }

            var buffInfos = appearBuffList.BuffInfos;

            // 付与元の索引には全件を先に入れる。見る前から乗っているバフはこの経路でしか届かない。
            foreach (var appearBuffInfo in buffInfos)
            {
                BuffInstanceIndex.Instance.Add(
                    entity.Uuid,
                    appearBuffInfo.BuffUuid,
                    appearBuffInfo.BaseId,
                    appearBuffInfo.FireUuid,
                    appearBuffInfo.FightSourceInfo?.FightSourceType ?? -1,
                    appearBuffInfo.FightSourceInfo?.SourceConfigId ?? 0,
                    appearBuffInfo.Duration,
                    extraData.ArrivalTime);
            }

            // FireUuid(術者)と FightSourceType も運ぶ。どちらも特化判定の採用条件。
            // SourceConfigId は判定が食い違ったときに正体を割り出す唯一の手掛かりなので一緒に持つ。
            var snapshot = new List<(int BaseId, int BuffUuid, long FireUuid, int FightSourceType, int SourceConfigId)>(buffInfos.Count);
            for (var index = 0; index < buffInfos.Count; index++)
            {
                var buffInfo = buffInfos[index];
                if (buffInfo.BaseId <= 0)
                {
                    continue;
                }

                snapshot.Add((
                    buffInfo.BaseId,
                    buffInfo.BuffUuid,
                    buffInfo.FireUuid,
                    buffInfo.FightSourceInfo?.FightSourceType ?? 0,
                    buffInfo.FightSourceInfo?.SourceConfigId ?? 0));

                // 差分の BuffEffectAddBuff と同じ入口・同じ引数へ渡す。
                // CreateTime の変換も向こうと揃える(Unixミリ秒 → UTC)。
                DateTime? creationTime = null;
                if (buffInfo.CreateTime > 0)
                {
                    creationTime = DateTimeOffset.FromUnixTimeMilliseconds(buffInfo.CreateTime).UtcDateTime;
                }

                EncounterManager.Current.NotifyBuffEvent(
                    entity.Uuid,
                    EBuffEventType.BuffEventAddTo,
                    buffInfo.BuffUuid,
                    buffInfo.BaseId,
                    buffInfo.Level,
                    buffInfo.FireUuid,
                    buffInfo.Layer,
                    buffInfo.Duration,
                    buffInfo.FightSourceInfo?.SourceConfigId ?? 0,
                    creationTime,
                    extraData,
                    BuffEventPayload.BuffInfo,
                    buffInfo.FightSourceInfo?.FightSourceType);
            }

            // 出現時の一覧は全件なので、一覧に無い項目は見えない間に消えたもの。付与を流した後で、一覧を受けた印と一緒に当てる(0件でも)。
            ActiveBuffStore.Instance.ApplyAppearList(
                entity.Uuid,
                buffInfos.Select(info => (ulong)info.BuffUuid).ToHashSet());

            // 特化を持つのはプレイヤーだけ。0件の一覧でも「全部を見た」になる(未装着・無効を確定する)。
            if (Utils.UuidToEntityType(entity.Uuid) == (long)EEntityType.EntChar)
            {
                EncounterManager.Current.GetOrCreateEntity(entity.Uuid).ApplyBuffSnapshotForSpec(snapshot);
            }
        }

        public static void ProcessSyncNearDeltaInfo(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            var syncNearDeltaInfo = SyncNearDeltaInfo.Parser.ParseFrom(payloadBuffer);
            if (syncNearDeltaInfo.DeltaInfos == null || syncNearDeltaInfo.DeltaInfos.Count == 0)
            {
                return;
            }

            foreach (var aoiSyncDelta in syncNearDeltaInfo.DeltaInfos)
            {
                ProcessAoiSyncDelta(aoiSyncDelta, extraData);
            }

            // 敵が付けたバフと、表に名前の無いバフの由来は、通知の全部の差分を処理してから決める(親のバフが別の実体の差分で後ろに届くことがある)。
            FlushPendingEnemyBuffLogs();
            BuffOriginResolver.Instance.ResolvePending();

            BattleStateMachine.CheckDeferredCalls();
        }

        /// <summary>
        /// バフの付け直しの差分で、最初に付くバフの番号。サーバーは付け直すとき自分のバフを全部この番号から付け直す。
        /// </summary>
        private const long ReinitializedFirstBuffUuid = 1;

        /// <summary>その差分が、自分のバフの付け直し(番号 <see cref="ReinitializedFirstBuffUuid"/> のバフの付与を含む)か。</summary>
        private static bool IsSelfBuffReinitialization(AoiSyncDelta delta)
        {
            if (delta.BuffEffect is null)
            {
                return false;
            }

            foreach (var buffEffect in delta.BuffEffect.BuffEffects)
            {
                if (buffEffect.BuffUuid == ReinitializedFirstBuffUuid
                    && buffEffect.LogicEffect.Any(logicEffect => logicEffect.EffectType == EBuffEffectLogicPbType.BuffEffectAddBuff))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>差分で届いた自分の能力値の、当てる前の値(無ければ null)。能力値の表に無い番号は見ない。</summary>
        private static List<(string Key, object? Before)> CaptureSelfStats(long uuid, RepeatedField<Attr> attrs)
        {
            var statAttrIds = HelperMethods.DataTables.FightAttrs.StatAttrIds;
            var captured = new List<(string Key, object? Before)>();
            foreach (var attr in attrs)
            {
                if (attr.Id == 0 || attr.RawData == null || !statAttrIds.Contains(attr.Id))
                {
                    continue;
                }

                var key = ((EAttrType)attr.Id).ToString();
                captured.Add((key, EncounterManager.Current.GetAttrKV(uuid, key)));
            }

            return captured;
        }

        /// <summary>
        /// 当てた後の値と比べ、変わった番号を <see cref="SelfAttributeChangeStore"/> へ渡す(記録に入るのはパケットの後)。
        /// 数値でない値は比べない。前の値が無い番号は、入場の全属性を受けた後なら 0 と比べる。
        /// </summary>
        private static void RecordSelfStatChanges(long uuid, List<(string Key, object? Before)> captured, DateTime arrivalUtc)
        {
            foreach (var (key, before) in captured)
            {
                if (!TryGetStatNumber(EncounterManager.Current.GetAttrKV(uuid, key), out var after))
                {
                    continue;
                }

                long previous;
                if (before is null)
                {
                    if (!SelfAttributeChangeStore.HasFullSnapshot)
                    {
                        continue;
                    }

                    previous = 0;
                }
                else if (!TryGetStatNumber(before, out previous))
                {
                    continue;
                }

                var sign = after.CompareTo(previous);
                if (sign != 0)
                {
                    SelfAttributeChangeStore.AddPending(key, sign, arrivalUtc);
                }
            }
        }

        /// <summary>能力値の控えは int か long(<see cref="ProcessAttrs"/>)。</summary>
        private static bool TryGetStatNumber(object? value, out long number)
        {
            switch (value)
            {
                case int intValue:
                    number = intValue;
                    return true;
                case long longValue:
                    number = longValue;
                    return true;
                default:
                    number = 0;
                    return false;
            }
        }

        public static void ProcessAoiSyncDelta(AoiSyncDelta delta, ExtraPacketData extraData)
        {
            if (delta == null)
            {
                return;
            }

            long targetUuid = delta.Uuid;
            if (targetUuid == 0)
            {
                return;
            }

            bool isTargetPlayer = (Utils.UuidToEntityType(targetUuid) == (long)EEntityType.EntChar);
            var isSelfTarget = isTargetPlayer && IsSelfPlayer(targetUuid);
            var attrCollection = delta.Attrs;
            HashSet<EAttrType> changedAttributes = [];

            // シーン切り替えのたびの自分のバフの付け直しは、能力値の変化として記録しない(SelfAttributeChangeStore)。
            // 下の早い return(精算・投票の間)より前で見る。
            if (isSelfTarget && IsSelfBuffReinitialization(delta))
            {
                SelfAttributeChangeStore.MarkBuffReinitialized();
            }

            // 差分の中は 属性 → バフ → ダメージ の順に当てるので、死亡の印つきの被弾は属性より先に控える。
            // 同じ差分で状態が「死亡」になる回を、ダメージの無い死亡と取り違えないため。
            var playerVitalsBefore = default(PlayerVitals);
            if (isTargetPlayer)
            {
                if (delta.SkillEffects?.Damages.Any(damage => damage.IsDead && damage.Type != EDamageType.Heal) == true)
                {
                    GetPlayerDeathState(targetUuid).HasLethalHit = true;
                }

                // 属性を当てる前の値。死亡の判定・料理と自然回復の刻みと、ダメージの無い戦闘不能の記録の「同期を当てる前の HP」に使う。
                playerVitalsBefore = CapturePlayerVitals(targetUuid);
            }

            if (attrCollection?.Attrs != null && attrCollection.Attrs.Any())
            {
                foreach (var attr in attrCollection.Attrs)
                {
                    if (attr.Id != 0 && attr.RawData != null)
                    {
                        changedAttributes.Add((EAttrType)attr.Id);
                    }
                }

                var selfStatsBefore = isSelfTarget ? CaptureSelfStats(targetUuid, attrCollection.Attrs) : null;
                var previousEnemySkill = EncounterManager.Current.GetAttrKV(targetUuid, "AttrSkillId") is int oldSkill ? oldSkill : 0;
                var previousSilence = EncounterManager.Current.GetAttrKV(targetUuid, "AttrIsSilence") is bool oldSilence ? (bool?)oldSilence : null;
                ProcessAttrs(targetUuid, attrCollection.Attrs, extraData.ArrivalTime);
                EnemyControlRecorder.RecordState(EncounterManager.Current, targetUuid, extraData.ArrivalTime,
                    previousEnemySkill, EncounterManager.Current.GetAttrKV(targetUuid, "AttrSkillId") is int newSkill ? newSkill : 0,
                    changedAttributes.Contains(EAttrType.AttrSkillId), previousSilence,
                    EncounterManager.Current.GetAttrKV(targetUuid, "AttrIsSilence") is bool newSilence ? (bool?)newSilence : null,
                    changedAttributes.Contains(EAttrType.AttrIsSilence));
                if (selfStatsBefore is not null)
                {
                    RecordSelfStatChanges(targetUuid, selfStatsBefore, extraData.ArrivalTime);
                }

                if (isTargetPlayer)
                {
                    TrackPlayerDeath(targetUuid, playerVitalsBefore, changedAttributes, extraData);
                }

                RecordSummonSource(targetUuid, attrCollection.Attrs, extraData.ArrivalTime);
                RecordSourceLanding(targetUuid, attrCollection.Attrs, extraData.ArrivalTime, isAppear: false);
                RecordNearbyMonster(targetUuid, attrCollection.Attrs);

                // 技の開始は AttrSkillId に値が入ったデルタで分かる。終了は値なし(0)で届く。
                if (changedAttributes.Contains(EAttrType.AttrSkillId)
                    && EncounterManager.Current.GetAttrKV(targetUuid, "AttrSkillId") is int castSkillId
                    && castSkillId > 0)
                {
                    var castSkillLevel = EncounterManager.Current.GetAttrKV(targetUuid, "AttrSkillLevel") is int level ? level : 0;
                    EncounterManager.Current.AddSkillCast(targetUuid, castSkillId, castSkillLevel, extraData);

                    // 自分の推移グラフの技の記録は上りの UseSlot の成功の応答から取る(ProcessUseSlotReturn)。
                    if (isTargetPlayer && !IsSelfPlayer(targetUuid))
                    {
                        EncounterManager.Current.AddSkillActivation(targetUuid, castSkillId, extraData.ArrivalTime);
                    }
                }
            }

            if (delta.TempAttrs != null && delta.TempAttrs.Attrs.Any())
            {
                ProcessTempAttrs(targetUuid, delta.TempAttrs.Attrs, extraData.ArrivalTime, isFullList: false);
            }

            NearbyEntityProjection.RefreshEntity(targetUuid, changedAttributes, extraData.ArrivalTime);
            PlayerRosterProjection.AddOrUpdateNearbyPlayer(targetUuid);


            var lastDungeonState = BattleStateMachine.DungeonStateHistory.LastOrDefault();
            if (lastDungeonState.Key == EDungeonState.DungeonStateSettlement || lastDungeonState.Key == EDungeonState.DungeonStateVote)
            {

                return;
            }

            var originalArrivalTime = extraData.ArrivalTime;

            long buffBasedShieldBreakValue = 0;
            bool shieldListChangedByBuffRemoval = false;

            var sawBuffAdd = false;
            var sawPotionHealBuff = false;
            var addedBuffIds = new List<int>();
            if (delta.BuffEffect != null)
            {
                for (int buffIdx = 0; buffIdx < delta.BuffEffect.BuffEffects.Count; buffIdx++)
                {

                    var buffEffect = delta.BuffEffect.BuffEffects[buffIdx];

                    // この効果を付与・層の変化として処理したか。
                    var handledByLogicEffect = false;

                    if (buffEffect.LogicEffect != null && buffEffect.LogicEffect.Count > 0)
                    {
                        for (int logicIdx = 0; logicIdx < buffEffect.LogicEffect.Count; logicIdx++)
                        {
                            var logicEffect = buffEffect.LogicEffect[logicIdx];
                            var reader = new Google.Protobuf.CodedInputStream(logicEffect.RawData.ToByteArray());
                            if (logicEffect.EffectType == EBuffEffectLogicPbType.BuffEffectAddBuff)
                            {
                                var buffInfo = BuffInfo.Parser.ParseFrom(reader);
                                DateTime? creationTime = null;
                                if (buffInfo.CreateTime > 0)
                                {
                                    DateTimeOffset dto = DateTimeOffset.FromUnixTimeMilliseconds(buffInfo.CreateTime);

                                    creationTime = dto.UtcDateTime;
                                }
                                sawBuffAdd = true;
                                EnemyControlRecorder.RecordBuff(EncounterManager.Current, targetUuid, buffInfo.BaseId, extraData.ArrivalTime);
                                CustomCombatFeatures.RecordFactor(EncounterManager.Current, targetUuid, buffInfo.BaseId,
                                    buffInfo.FightSourceInfo?.FightSourceType == (int)EFightSource.Buff
                                        ? buffInfo.FightSourceInfo.SourceConfigId : 0, extraData.ArrivalTime);
                                addedBuffIds.Add(buffInfo.BaseId);
                                if (buffInfo.BaseId == PotionHealBuffId)
                                {
                                    sawPotionHealBuff = true;
                                }

                                if (CooldownResetBuffDelays.TryGetValue(buffInfo.BaseId, out var cooldownResetDelay))
                                {
                                    SkillCooldownStateStore.NotifyCooldownsResetForPlayer(targetUuid, extraData.ArrivalTime + cooldownResetDelay);
                                }

                                BuffInstanceIndex.Instance.Add(
                                    targetUuid,
                                    buffEffect.BuffUuid,
                                    buffInfo.BaseId,
                                    buffInfo.FireUuid,
                                    buffInfo.FightSourceInfo?.FightSourceType ?? -1,
                                    buffInfo.FightSourceInfo?.SourceConfigId ?? 0,
                                    buffInfo.Duration,
                                    extraData.ArrivalTime);

                                EncounterManager.Current.NotifyBuffEvent(targetUuid, buffEffect.Type, buffEffect.BuffUuid, buffInfo.BaseId, buffInfo.Level, buffInfo.FireUuid, buffInfo.Layer, buffInfo.Duration, buffInfo.FightSourceInfo?.SourceConfigId ?? 0, creationTime, extraData, BuffEventPayload.BuffInfo, buffInfo.FightSourceInfo?.FightSourceType);
                                PendingEnemyBuffLogs.Add((targetUuid, buffInfo, delta.SkillEffects?.Damages, new ExtraPacketData(extraData.ArrivalTime)));
                                handledByLogicEffect = true;
                            }
                            else if (logicEffect.EffectType == EBuffEffectLogicPbType.BuffEffectBuffChange)
                            {

                                var changeInfo = BuffChange.Parser.ParseFrom(reader);
                                DateTime? creationTime = null;
                                if (changeInfo.CreateTime > 0)
                                {
                                    DateTimeOffset dto = DateTimeOffset.FromUnixTimeMilliseconds(changeInfo.CreateTime);

                                    creationTime = dto.UtcDateTime;
                                }

                                EncounterManager.Current.NotifyBuffEvent(targetUuid, buffEffect.Type, buffEffect.BuffUuid, 0, 0, 0, changeInfo.Layer, (int)changeInfo.Duration, 0, creationTime, extraData, BuffEventPayload.BuffChange, fightSourceType: null);
                                handledByLogicEffect = true;
                            }
                        }
                    }

                    // 付与・層の変化として処理できなかった効果は、中身なしの通知として必ず流す。
                    // ここを LogicEffect の有無で切ると、演出などの LogicEffect が付いた除去が
                    // どこにも届かず、そのバフが消えない。同じデルタに同じ buffUuid が二度出る回も同じ。
                    // baseId 以下はサーバが送ってこなかったぶんの 0 で、バフの状態ではない。
                    // 既知のバフの値を上書きしないことは BuffEventPayload.None が担う。
                    if (!handledByLogicEffect)
                    {
                        EncounterManager.Current.NotifyBuffEvent(targetUuid, buffEffect.Type, buffEffect.BuffUuid, 0, 0, 0, 0, 0, 0, null, extraData, BuffEventPayload.None, fightSourceType: null);
                    }

                    if (buffEffect.Type == EBuffEventType.BuffEventRemove)
                    {
                        BuffInstanceIndex.Instance.Remove(targetUuid, buffEffect.BuffUuid, extraData.ArrivalTime);
                        if (EncounterManager.Current.Entities.TryGetValue(targetUuid, out var targetEntity))
                        {
                            List<ShieldInfo>? attrShieldList = targetEntity.GetAttrKV("AttrShieldList") as List<ShieldInfo>;

                            if (attrShieldList != null)
                            {
                                var matches = attrShieldList.Where(x => x.Uuid == buffEffect.BuffUuid);
                                if (matches.Any())
                                {

                                    var match = matches.First();

                                    buffBasedShieldBreakValue = match.Value;

                                    attrShieldList.Remove(match);

                                    targetEntity.SetAttrKV("AttrShieldList", attrShieldList);
                                    shieldListChangedByBuffRemoval = true;
                                }
                            }
                        }
                    }
                }
            }

            // 自分は SyncNearEntities.Appear に出ないので、他人のような全バフスナップショットが届かない。
            // 代わりにサーバーが、シーン切替のたびに自分のバフを番号1から全部付け直す(まとめの差分)。
            // これが立たないと「マーカーが無い = アビリティ未装着」を自分について確定できない。
            if (sawBuffAdd && IsSelfPlayer(targetUuid))
            {
                EncounterManager.Current.GetOrCreateEntity(targetUuid).MarkSelfBuffStreamReceived();
            }

            if (shieldListChangedByBuffRemoval)
            {
                NearbyEntityProjection.RefreshEntity(targetUuid, ShieldListChangedAttributes, originalArrivalTime);
                PlayerRosterProjection.UpsertPlayer(targetUuid);
            }

            extraData.ArrivalTime = originalArrivalTime;

            // 全滅とボス部屋の入場は、どちらもゲームが仕切り直した合図(CooldownResetBuffDelays の3種)から見分ける。
            // 全滅は、戦っていた全員の差分にリセットのバフと 500111 の付与、状態の復活(27)が一緒に来る。
            // ボス部屋の入場は復活の印を伴わず、進行中(Playing)に来る
            // (ダンジョンや層の開始にも来るが、そこは開始の区切りと同じ瞬間なので記録が無く、何も起きない)。
            //
            // **見るのは自分の付与だけ。** バフは戦っていた全員に付くが、PT では到着が分かれることがあり
            // (実測で 0.47秒差)、間に記録が入ると人数分だけ区切ってしまう。1つの出来事につき自分への付与は1回なので、
            // 自分だけを見れば区切りも1回になる。
            //
            // 全滅なら、区切りの設定に関係なく予告のバーの控えを消す。ゲームは全滅でも予告を消す通知を送らないので、
            // 消さないと全滅の後にバーの終わりの行(タイマーも動かす)と通知が出る。
            //
            // 全滅の区切りは、全滅を見つけたパケットを処理し終えたところで行う(SplitEncounterOnPendingWipe)。
            // 最後に倒れた人の戦闘不能の被弾は全滅と同じ差分で届き、差分の中ではバフより後に当てるため、
            // 見つけた時点で区切るとその被弾だけが次の回に入る。
            if (isTargetPlayer
                && addedBuffIds.Exists(CooldownResetBuffDelays.ContainsKey)
                && IsSelfPlayer(targetUuid))
            {
                var isWipe = addedBuffIds.Contains(WipeResetBuffId)
                    && addedBuffIds.Contains(ReviveStartBuffId)
                    && changedAttributes.Contains(EAttrType.AttrState)
                    && EncounterManager.Current.GetAttrKV(targetUuid, "AttrState") is EActorState.ActorStateResurrection;
                if (isWipe)
                {
                    Log.Information("Wipe detected: {Uuid} received the wipe reset buffs and was resurrected", targetUuid);
                    BossDbmBarStore.Instance.Clear();
                    _pendingWipeSplitEncounterId = EncounterManager.Current.EncounterId;
                }
                else if (CombatRuntimeSettings.SplitEncountersOnNewPhases
                    && EncounterManager.Current.HasStatsBeenRecorded()
                    && !addedBuffIds.Contains(ReviveStartBuffId)
                    && BattleStateMachine.IsDungeonPlaying())
                {
                    Log.Information("Boss room entry detected: {Uuid} received a reset buff without being resurrected", targetUuid);
                    EncounterManager.EnterDungeon(keepPastEncounterInMeter: true, force: false, reason: EncounterStartReason.NewObjective);
                }
            }

            // 戦闘の時計の起点と、戦闘の出来事(自動一時停止の時計を続ける)。全滅・ボス部屋の作り直しの後(今の回に当てる)、
            // 回復・薬・料理・自然回復とダメージの記録より前に当てる(起点より前は数えず、止まっていた区間はその記録より前に閉じておく)。
            // メッセージの途中の作り直しにも追従するよう差分ごとに見る。
            if (delta.SkillEffects?.Damages is { Count: > 0 } clockDamages)
            {
                var isBenchmarkEncounter = EncounterManager.Current.IsBenchmark;
                foreach (var clockDamage in clockDamages)
                {
                    if (IsCombatEvent(clockDamage, targetUuid, isBenchmarkEncounter))
                    {
                        EncounterManager.Current.RecordCombatEvent(extraData.ArrivalTime);
                        break;
                    }
                }
            }

            // 薬を飲んだ差分は薬の回復だけ。料理・自然回復としては見ない。
            if (isTargetPlayer)
            {
                if (sawPotionHealBuff)
                {
                    RecordPotionHeal(targetUuid, playerVitalsBefore.Hp, changedAttributes, delta, extraData);
                }
                else
                {
                    RecordPassiveHeal(targetUuid, playerVitalsBefore, changedAttributes, delta, addedBuffIds, extraData);
                }
            }

            var skillEffect = delta.SkillEffects;

            if (skillEffect?.Damages == null || skillEffect.Damages.Count == 0)
            {
                return;
            }

            // 被ダメログとタイムラインの加害者を先に決める(タイムラインに載る敵の回復は回復した側)。
            var damages = skillEffect.Damages;
            var combatEventLogActors = new long[damages.Count];
            for (var index = 0; index < damages.Count; index++)
            {
                combatEventLogActors[index] = ResolveCombatEventLogActor(damages[index], targetUuid);
            }

            HashSet<long> rosterPlayersToUpsert = [];

            var damageIndex = -1;
            foreach (var syncDamageInfo in skillEffect.Damages)
            {
                damageIndex++;

                if (IsSelfCausedDamage(syncDamageInfo))
                {
                    if (syncDamageInfo.Type != EDamageType.Heal)
                    {
                        AddSelfCausedTakenDamage(syncDamageInfo, targetUuid, extraData);
                        if (isTargetPlayer)
                        {
                            rosterPlayersToUpsert.Add(targetUuid);
                        }
                        else
                        {
                            AddEnemySelfCausedDamageLog(syncDamageInfo, targetUuid, extraData);
                        }
                    }

                    continue;
                }

                if (syncDamageInfo.OwnerId == 0)
                {
                    continue;
                }

                long attackerUuid = (syncDamageInfo.TopSummonerId != 0 ? syncDamageInfo.TopSummonerId : syncDamageInfo.AttackerUuid);
                if (attackerUuid == 0)
                {
                    continue;
                }
                bool isAttackerPlayer = (Utils.UuidToEntityType(attackerUuid) == (long)EEntityType.EntChar);

                // OwnerId の中身は DamageSource で変わる(スキルID / 弾ID / バフID)。
                // 表示に使うキーへ畳む。畳み先を決めるのは行対応表と4言語テーブルだけで、
                // 実行時の情報(バフ実体・召喚体の AttrId など)は見ない。
                //
                // HitEventId が要る。ゲームの行は DamageId の粒度で決まっており、
                // その下2桁がこの値にあたる。OwnerId だけだと別の行が1行に潰れる。
                var foldedSource = SkillSourceResolver.Resolve(
                    syncDamageInfo.DamageSource,
                    syncDamageInfo.OwnerId,
                    syncDamageInfo.HitEventId);
                long skillId = foldedSource.Key;

                // 職業・特化の判定には、畳んだIDではなく「ゲームが実際に発動したスキルID」を使う。
                // 畳み込みは弾・バフ・コンボ段を1つの表示IDへ寄せるので、判定表が想定していない
                // 入力まで当たるようになる。味方に発生させるスキルが畳まれて表に当たると、
                // その人の特化と職業を書き換えてしまう。
                //
                // Skill 由来に限るのは、OwnerId がスキルIDを名乗るのがこのときだけだから。
                // 弾・バフ由来の生IDをそのまま渡すと、SkillTable と BuffTable で重複する
                // 90個のIDに当たる(置換後スキル表の 2301 は BuffTable にも存在する)。
                var identitySkillId = syncDamageInfo.DamageSource == EDamageSource.Skill
                    ? syncDamageInfo.OwnerId
                    : 0;

                if (isAttackerPlayer && attackerUuid != 0)
                {
                    EncounterManager.Current.SetEntityType(attackerUuid, EEntityType.EntChar);
                    // 特化判定と同じ理由で、こちらも畳む前の生のスキルIDで引く。
                    // 畳んだIDだと 2301 → 職業13(ビートパフォーマー)が味方に付く。
                    var professionId = foldedSource.IsBuffSource
                        ? 0
                        : Professions.GetBaseProfessionIdBySkillId(identitySkillId);
                    if (professionId != 0
                        && EncounterManager.Current.GetOrCreateEntity(attackerUuid).ProfessionId <= 0
                        && !IsTransformed(attackerUuid))
                    {
                        EncounterManager.Current.SetProfessionId(attackerUuid, professionId);
                    }
                }

                long damage = ResolveDamageValue(syncDamageInfo);

                bool isCrit = (syncDamageInfo.TypeFlag & 1) == 1;
                bool isHeal = syncDamageInfo.Type == EDamageType.Heal;
                var luckyValue = syncDamageInfo.LuckyValue;
                bool isLucky = luckyValue != 0;
                long hpLessen = syncDamageInfo.HpLessenValue;

                bool isCauseLucky = (syncDamageInfo.TypeFlag & 0B100) == 0B100;

                bool isMiss = syncDamageInfo.IsMiss;

                bool isDead = syncDamageInfo.IsDead;

                string damageElement = syncDamageInfo.Property.ToString();

                EDamageSource damageSource = syncDamageInfo.DamageSource;

                long shieldBreak = 0;
                if (syncDamageInfo.Type == EDamageType.Absorbed)
                {
                    shieldBreak = damage;
                }
                else if (buffBasedShieldBreakValue > 0 && targetUuid != attackerUuid)
                {
                    if (hpLessen >= buffBasedShieldBreakValue)
                    {

                        syncDamageInfo.Type = EDamageType.Absorbed;

                        hpLessen = hpLessen - buffBasedShieldBreakValue;
                        shieldBreak = buffBasedShieldBreakValue;
                    }
                    else
                    {

                        shieldBreak = 0;
                    }

                    buffBasedShieldBreakValue = 0;
                }

                if (isHeal)
                {
                    // 回の回復(メーター)に入れるのは、プレイヤー(とその召喚)の回復だけ。ダメージがプレイヤーの攻撃だけを入れるのと同じ。
                    // 足すのは実際に増えた HP(ActualValue)。ゲーム内メーターと同じ。名目(damage)との差は過剰回復。
                    if (isAttackerPlayer)
                    {
                        EncounterManager.Current.AddHealing(attackerUuid, targetUuid, skillId, syncDamageInfo.OwnerLevel, syncDamageInfo.ActualValue, damage, hpLessen, shieldBreak, syncDamageInfo.Property, syncDamageInfo.Type, syncDamageInfo.DamageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead, extraData);
                    }
                    else if (combatEventLogActors[damageIndex] != 0)
                    {
                        AddEnemyHealLog(syncDamageInfo, combatEventLogActors[damageIndex], targetUuid, attackerUuid, extraData);
                    }
                }
                else
                {
                    // 記録するのはプレイヤーから敵への攻撃だけ。敵の攻撃・自傷・フレンドリーファイアは被ダメログ(AddTakenDamage)にだけ残す。
                    if (isAttackerPlayer && !isTargetPlayer)
                    {
                        EncounterManager.Current.AddDamage(attackerUuid, targetUuid, skillId, identitySkillId, syncDamageInfo.OwnerLevel, damage, hpLessen, shieldBreak, syncDamageInfo.Property, syncDamageInfo.Type, syncDamageInfo.DamageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead, extraData);
                    }

                    // 敵の攻撃(プレイヤーへの被弾と敵どうし)は、発生源の技を記録の瞬間に控える。
                    var buffSourceSkillId = 0;
                    var summonSourceSkillId = 0;
                    if (!isAttackerPlayer)
                    {
                        (buffSourceSkillId, summonSourceSkillId) = ResolveEnemyAttackSourceSkills(syncDamageInfo, extraData.ArrivalTime);
                    }

                    EncounterManager.Current.AddTakenDamage(combatEventLogActors[damageIndex], targetUuid, skillId, syncDamageInfo.OwnerId, syncDamageInfo.DamageSource, buffSourceSkillId, summonSourceSkillId, syncDamageInfo.OwnerLevel, damage, hpLessen, shieldBreak, syncDamageInfo.Property, syncDamageInfo.Type, syncDamageInfo.DamageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead, extraData);

                    // 敵から敵へのダメージはタイムラインに残す(敵からプレイヤーへは上の被ダメログ)。値 0 は HP もバリアも減っていない。
                    if (!isAttackerPlayer && !isTargetPlayer && combatEventLogActors[damageIndex] != 0 && damage != 0)
                    {
                        EncounterManager.Current.AddEnemyDamage(combatEventLogActors[damageIndex], targetUuid, syncDamageInfo.OwnerId, syncDamageInfo.DamageSource, buffSourceSkillId, summonSourceSkillId, damage, extraData);
                    }
                }

                // 畳めずバフIDのまま出す行は、名前を GetBuffName で引く必要がある。
                // SkillTable と BuffTable は90IDが重複するので、種別を持たないと取り違える。
                if (foldedSource.IsBuffSource)
                {
                    // 回復の行を持つのはプレイヤーの回復だけ(敵の回復はメーターに入れない)。
                    if (!isHeal || isAttackerPlayer)
                    {
                        EncounterManager.Current.MarkBuffSourcedSkill(attackerUuid, skillId);
                    }

                    EncounterManager.Current.MarkBuffSourcedSkill(targetUuid, skillId);
                }

                // 見出し表で名前を持たない行の出どころを決める(検知ログも兼ねる)。
                // 印を付けた後に呼ぶので、バフ由来かどうかも正しく残る。
                if (isAttackerPlayer)
                {
                    EncounterManager.Current.ResolveSourceLanding(
                        attackerUuid,
                        skillId,
                        isHeal,
                        syncDamageInfo.DamageSource,
                        syncDamageInfo.OwnerId,
                        syncDamageInfo.AttackerUuid,
                        extraData.ArrivalTime);
                }

                if (isAttackerPlayer)
                {
                    rosterPlayersToUpsert.Add(attackerUuid);
                }

                if (isTargetPlayer)
                {
                    rosterPlayersToUpsert.Add(targetUuid);
                }

                buffBasedShieldBreakValue = 0;
            }

            foreach (var playerUuid in rosterPlayersToUpsert)
            {
                PlayerRosterProjection.UpsertPlayer(playerUuid);
            }

            BattleStateMachine.CheckDeferredCalls();
        }

        public static long currentUserUuid = 0;

        public static void ProcessSyncServerTime(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            if (payloadBuffer.Length == 0)
            {
                return;
            }

            var syncServerTime = SyncServerTime.Parser.ParseFrom(payloadBuffer);
            SkillCooldownStateStore.UpdateServerTime(
                syncServerTime.ClientMilliseconds,
                syncServerTime.ServerMilliseconds);
            ActiveBuffStore.Instance.ResolveUnknownElapsed();
        }

        public static void ProcessSyncToMeDeltaInfo(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            var syncToMeDeltaInfo = SyncToMeDeltaInfo.Parser.ParseFrom(payloadBuffer);
            var aoiSyncToMeDelta = syncToMeDeltaInfo.DeltaInfo;
            long uuid = aoiSyncToMeDelta.Uuid;

            // キャプチャをやり直した後は currentUserUuid が変わらないまま印だけ下りているので、下の判定の外で立てる。
            if (uuid != 0)
            {
                GameReceptionStateStore.MarkSelfIdentified();
            }

            if (uuid != 0 && currentUserUuid != uuid)
            {
                currentUserUuid = uuid;
                AppState.PlayerUUID = uuid;
                AppState.PlayerUID = Utils.UuidToEntityId(uuid);
            }

            NearbyEntityProjection.SetSelfEntity(uuid);
            SkillCooldownStateStore.SetSelfPlayer(uuid);
            SkillCooldownStateStore.UpdateSelfCooldowns(uuid, aoiSyncToMeDelta.SyncSkillCDs);

            var aoiSyncDelta = aoiSyncToMeDelta.BaseDelta;
            if (aoiSyncDelta == null)
            {
                return;
            }
            ProcessAoiSyncDelta(aoiSyncDelta, extraData);

            // 敵が付けたバフと、表に名前の無いバフの由来は、差分を処理し終えてから決める。
            FlushPendingEnemyBuffLogs();
            BuffOriginResolver.Instance.ResolvePending();

            BattleStateMachine.CheckDeferredCalls();
        }

        /// <summary>
        /// シーン情報を反映する。シーンが切り替わっていればプレイヤー/エンティティのリストをリセットする。
        ///
        /// <para>
        /// 判定は (LevelMapId, SceneGuid) の組。LevelMapId だけでは同一マップのチャンネル切替を、
        /// SceneGuid だけでは同一 GUID のままのマップ往復を取りこぼす。
        /// </para>
        ///
        /// <para>
        /// 同じシーンで何度呼ばれてもリセットは一度だけ。フルコンテナと NotifySocialData の
        /// 両方から呼ばれるため、後着側で再度消さないようにするのが目的。
        /// </para>
        /// </summary>
        private static void ApplySceneData(
            uint levelMapId,
            uint lineId,
            string? sceneGuid,
            string source,
            bool resetForSceneChange)
        {
            // 先にシーンを反映してから刷新する。StartNewMap の中の DB.StartBattle が
            // LevelMapId / SceneName を読むため、逆順だと古いマップ名で battle 行が作られる。
            //
            // ここへ来た時点で「反映してよい通知」と呼び出し側が判断済みなので、
            // SetSceneId 側の AllowSceneUpdate 関門は通す。
            //
            // ただし resetForSceneChange のときは、この直後の StartNewMap → EnterDungeon が
            // 「移動前のマップで戦ったエンカウンター」をそのまま保存し、その battle 行も閉じる。
            // 移動先のシーンをそこへ押すと保存直前に書き換えることになるので、
            // 開いている記録は触らせない(新しい Current へは EnterDungeon が押す)。
            EncounterManager.SetSceneId(
                levelMapId,
                force: true,
                updateOpenRecords: !resetForSceneChange);
            EncounterManager.SetChannelLineId(lineId);

            if (resetForSceneChange)
            {
                // メーター(DPS/HPS)・プレイヤーリスト・エンティティリストを揃えて刷新する。
                // メーターだけ ProcessSyncContainerData に置いたままだと、
                // フルコンテナが来ない切替(ギルドハウスなど)でメーターだけ取り残される。
                BattleStateMachine.StartNewMap();

                // StartNewMap の中の EnterDungeon が属性を新しいエンカウンターへ運ぶ。
                // バリアはマップ移動で消える(ゲーム仕様)のに更新が来ないので、運んだ直後に落とす。
                // ここはマップ移動だと確定している唯一の場所。
                EncounterManager.ClearCarriedOverShields();

                PlayerRosterProjection.BeginMap();
                NearbyEntityProjection.BeginMap();
            }

            // 移動でない通知(フルコンテナなど)で届いたチャンネルも今の回に入れる。
            EncounterManager.Current.SetChannelLineNumber(lineId);
            PlayerRosterProjection.UpdateMapName();
            NearbyEntityProjection.UpdateMapName();

            SceneApplied?.Invoke(resetForSceneChange);
        }

        /// <summary>
        /// 今のシーズンの通知。マップ移動のたびにフルコンテナの直後に届く。
        /// 自分のシーズンレベルを今のシーズンで引き直し、プレイヤーの一覧へ今のシーズンを渡す。
        /// </summary>
        public static void ProcessSyncSeason(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            var vData = SyncSeason.Parser.ParseFrom(payloadBuffer);
            SeasonStateStore.SetCurrentSeasonId(vData.VSeason);

            if (AppState.PlayerUID != 0)
            {
                ApplySelfSeasonLevel(Utils.EntityIdToUuid(AppState.PlayerUID, (long)EEntityType.EntChar, false, false));
            }

            PlayerRosterProjection.UpdateSeason();
        }

        /// <summary>今のシーズンの自分のシーズンレベルを入れる。今のシーズンが分からないか、表に無ければ書かない。</summary>
        private static void ApplySelfSeasonLevel(long playerUuid)
        {
            if (SeasonStateStore.TryGetSelfCurrentSeasonLevel(out var level))
            {
                EncounterManager.Current.SetAttrKV(playerUuid, "AttrSeasonLevel", level);
            }
        }

        public static void ProcessSyncContainerData(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            BattleStateMachine.CheckDeferredCalls();

            BattleStateMachine.StartNewMap();

            // メーター(StartNewMap)と同じ契機・同じ無条件リセットに揃える。
            // 3つとも消したあとは継続的に埋め直される: メーターは戦闘イベント、
            // プレイヤーリストとエンティティリストは AOIデルタ
            // (AddOrUpdateNearbyPlayer / RefreshEntity の UpsertAppeared)。
            // Appear の再送を待つ必要はない。
            //
            // シーン識別子の変化を条件にした刷新は、AllowSceneUpdate と TryBeginScene という
            // 2つの取りこぼし経路を作り、マップ切替でリストが刷新されない再発を招いた。
            PlayerRosterProjection.BeginMap();
            NearbyEntityProjection.BeginMap();

            var syncContainerData = SyncContainerData.Parser.ParseFrom(payloadBuffer);
            if (syncContainerData?.VData == null)
            {
                return;
            }

            var vData = syncContainerData.VData;
            if (vData.CharId == 0)
            {
                return;
            }

            long playerUuid = Utils.EntityIdToUuid(vData.CharId, (long)EEntityType.EntChar, false, false);

            GameReceptionStateStore.MarkSelfContainerReceived();
            NearbyEntityProjection.SetSelfEntity(playerUuid);
            SkillCooldownStateStore.SetSelfPlayer(playerUuid);
            AppState.PlayerUID = vData.CharId;
            if (!string.IsNullOrEmpty(vData.CharBase.AccountId))
            {
                AppState.AccountId = vData.CharBase.AccountId;
            }
            long playerUid = vData.CharId;

            if (vData.RoleLevel is { Level: not 0 } roleLevel)
            {
                EncounterManager.Current.SetAttrKV(playerUuid, "AttrLevel", roleLevel.Level);
            }

            if (vData.Attr is { CurHp: not 0 } currentHpAttr)
            {
                EncounterManager.Current.SetAttrKV(playerUuid, "AttrHp", currentHpAttr.CurHp);
            }

            if (vData.Attr is { MaxHp: not 0 } maxHpAttr)
            {
                EncounterManager.Current.SetAttrKV(playerUuid, "AttrMaxHp", maxHpAttr.MaxHp);
            }

            if (vData.Attr != null)
            {
                EncounterManager.Current.SetAttrKV(
                    playerUuid,
                    CurrentStaminaSnapshotAttribute,
                    Math.Max(vData.Attr.OriginEnergy, 0f));
            }

            if (vData.CharBase != null)
            {
                if (!string.IsNullOrEmpty(vData.CharBase.Name))
                {
                    EncounterManager.Current.SetName(playerUuid, vData.CharBase.Name);
                    AppState.PlayerName = vData.CharBase.Name;
                }

                if (vData.CharBase.FightPoint != 0)
                {
                    EncounterManager.Current.SetAbilityScore(playerUuid, vData.CharBase.FightPoint);
                }

            }

            var professionList = vData.ProfessionList;
            if (professionList != null && professionList.CurProfessionId != 0 && !IsTransformed(playerUuid))
            {
                UpdateProfessionId(playerUuid, professionList.CurProfessionId);
            }

            if (professionList != null)
            {
                EncounterManager.Current.ReplaceTalentNodes(
                    playerUuid,
                    professionList.TalentList.ToDictionary(pair => pair.Key, pair => ToTalentNodeIds(pair.Value.TalentNodeIds)));
            }

            PlayerSkillLevelStateStore.ReplaceSelfSkillLevels(
                professionList,
                vData.DutyList);
            RefreshSelfRosterEntry(playerUuid);

            var sceneData = vData.SceneData;
            if (sceneData != null)
            {
                System.Diagnostics.Debug.WriteLine($"ProcessSyncContainerData.SceneData:\n{sceneData}");

                ApplySceneData(
                    sceneData.LevelMapId,
                    sceneData.LineId,
                    sceneData.SceneGuid,
                    "フルコンテナ",
                    resetForSceneChange: false);
            }

            // シーズンごとのレベルの表は並びが届くたびに入れ替わるので、控えて今のシーズンで引く。
            // ログインのときは今のシーズン(SyncSeason)がこの直後に届き、そこで入る。
            var seasonRoleLevelData = vData.SeasonRoleLevelData;
            if (seasonRoleLevelData != null)
            {
                SeasonStateStore.ReplaceSelfSeasonLevels(seasonRoleLevelData.SeasonRoleLevelMap
                    .Where(pair => pair.Value != null)
                    .Select(pair => KeyValuePair.Create(pair.Key, pair.Value.Level)));
                ApplySelfSeasonLevel(playerUuid);
            }

            SelfEquipmentStore.ReplaceSelf(vData.Equip, vData.ItemPackage);
            SelfModuleStore.ReplaceSelf(vData.Mod, vData.ItemPackage);
            SelfSeasonCultivateStore.ReplaceSelf(vData.SeasonCultivateLineData);

            if (vData.Equip != null)
            {
                List<Zproto.EquipNine> playerEquips = new();
                foreach (var equip in vData.Equip.EquipList_)
                {
                    System.Diagnostics.Debug.WriteLine($"{playerUid} :: equip::slot={equip.Value.EquipSlot},refinelvl={equip.Value.EquipSlotRefineLevel}");

                    foreach (var item in vData.ItemPackage.Packages[2].Items)
                    {
                        if ((ulong)item.Key == equip.Value.ItemUuid)
                        {
                            playerEquips.Add(new EquipNine() { EquipID = item.Value.ConfigId, Slot = equip.Value.EquipSlot });
                            break;
                        }
                    }
                }
                EncounterManager.Current.SetAttrKV(playerUuid, "AttrEquipData", playerEquips);
            }

            PlayerRosterProjection.RebuildRoster();
        }

        public static void ProcessSyncContainerDirtyData(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            BattleStateMachine.CheckDeferredCalls();

            try
            {
                if (currentUserUuid == 0)
                {
                    return;
                }
                var dirty = SyncContainerDirtyData.Parser.ParseFrom(payloadBuffer);
                if (dirty?.VData?.Buffer == null || dirty.VData.Buffer.Length == 0)
                {
                    return;
                }

                var buf = dirty.VData.Buffer.ToByteArray();
                bool isStreamSafe = dirty.VData.StreamType == EStreamType.StreamTypeDeltaDirtySafe;
                var ser = new StarResonanceDps.Core.Protocols.Game.Binary.CharSerialize(
                    new StarResonanceDps.Core.Protocols.Game.Binary.BlobReader(buf, isStreamSafe));

                if (ser.EquipList is not null || ser.ItemPackage is not null)
                {
                    SelfEquipmentStore.ApplySelfChanges(ser.EquipList, ser.ItemPackage);
                }

                if (ser.Mod is not null || ser.ItemPackage is not null)
                {
                    SelfModuleStore.ApplySelfChanges(ser.Mod, ser.ItemPackage);
                }

                if (ser.SeasonCultivateLineData is not null)
                {
                    SelfSeasonCultivateStore.ApplySelfChanges(ser.SeasonCultivateLineData);
                }

                if (ser.CharBaseInfo != null)
                {
                    if (!string.IsNullOrEmpty(ser.CharBaseInfo.Name))
                    {
                        EncounterManager.Current.SetName(currentUserUuid, ser.CharBaseInfo.Name);
                        AppState.PlayerName = ser.CharBaseInfo.Name;
                    }
                    if (!string.IsNullOrEmpty(ser.CharBaseInfo.AccountId) && string.IsNullOrEmpty(AppState.AccountId))
                    {
                        AppState.AccountId = ser.CharBaseInfo.AccountId;
                    }
                    if (ser.CharBaseInfo.FightPoint != null)
                    {
                        EncounterManager.Current.SetAbilityScore(currentUserUuid, (int)ser.CharBaseInfo.FightPoint);
                    }

                    if (ser.CharBaseInfo.TeamInfo != null)
                    {
                        System.Diagnostics.Debug.WriteLine("ser.CharBaseInfo.TeamInfo != null)");
                    }
                }

                if (ser.Attr != null)
                {
                    if (ser.Attr.CurHp != null)
                    {
                        EncounterManager.Current.SetAttrKV(currentUserUuid, "AttrHp", ser.Attr.CurHp);
                    }
                    if (ser.Attr.MaxHp != null)
                    {
                        EncounterManager.Current.SetAttrKV(currentUserUuid, "AttrMaxHp", ser.Attr.MaxHp);
                    }
                    if (ser.Attr.OriginEnergy != null)
                    {
                        EncounterManager.Current.SetAttrKV(
                            currentUserUuid,
                            CurrentStaminaSnapshotAttribute,
                            Math.Max(ser.Attr.OriginEnergy.Value, 0f));
                    }
                }

                if (ser.ProfessionList is not null)
                {
                    if (ser.ProfessionList.CurProfessionId is { } professionId && !IsTransformed(currentUserUuid))
                    {
                        UpdateProfessionId(currentUserUuid, professionId);
                    }

                    PlayerSkillLevelStateStore.ApplySelfProfessionListChanges(
                        ser.ProfessionList);

                    if (ser.ProfessionList.TalentInfoChanges is { } talentChanges)
                    {
                        // 差分は変わった職業の一覧を丸ごと運ぶ。新しい職業に一覧が無ければ空、既にある職業に無ければ変わっていない。
                        var changedNodeIds = new Dictionary<int, int[]>();
                        foreach (var (talentProfessionId, talentInfo) in talentChanges.Added)
                        {
                            changedNodeIds[talentProfessionId] = ToTalentNodeIds(talentInfo.TalentNodeIds ?? []);
                        }

                        foreach (var (talentProfessionId, talentInfo) in talentChanges.Updated)
                        {
                            if (talentInfo.TalentNodeIds is { } nodeIds)
                            {
                                changedNodeIds[talentProfessionId] = ToTalentNodeIds(nodeIds);
                            }
                        }

                        EncounterManager.Current.ApplyTalentNodeChanges(
                            currentUserUuid,
                            talentChanges.ReplacesExisting,
                            talentChanges.Removed,
                            changedNodeIds);
                    }
                }

                if (ser.DutyList is not null)
                {
                    PlayerSkillLevelStateStore.ApplySelfDutyListChanges(ser.DutyList);
                }

                RefreshSelfRosterEntry(currentUserUuid);

                if (ser.SceneData != null)
                {
                    Log.Debug($"ser.sceneData = {ser.SceneData}");
                    if (ser.SceneData.LevelMapId != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"ser.SceneData.LevelMapId = {ser.SceneData.LevelMapId})");
                    }
                }

                PlayerRosterProjection.UpsertSelf(currentUserUuid);
            }
            catch (Exception)
            {

                throw;
            }
        }

        public static void ProcessSyncDungeonData(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            BattleStateMachine.CheckDeferredCalls();

            var syncDungeonData = SyncDungeonData.Parser.ParseFrom(payloadBuffer);
            if (syncDungeonData?.VData == null)
            {
                return;
            }

            var vData = syncDungeonData.VData;
            ClearAnnouncementBarsOnTargetProgress(DungeonTargetStore.Instance.ApplyFull(vData));
            DungeonScoreStore.Instance.ApplyFull(vData);

            if (vData.DungeonPlayerList != null && vData.DungeonPlayerList.PlayerInfos.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine("vData.DungeonPlayerList != null");
                System.Diagnostics.Debug.WriteLine(vData.DungeonPlayerList.PlayerInfos);
            }

            for(int listIdx = 0; listIdx < vData.Title.TitleList.Count; listIdx++)
            {
                var title_list = vData.Title.TitleList[listIdx];
                for (int infoIdx = 0; infoIdx < title_list.TitleInfo.Count; infoIdx++)
                {
                    var title_info = title_list.TitleInfo[infoIdx];
                    System.Diagnostics.Debug.WriteLine($"TitleList[{listIdx}].TitleInfo[{infoIdx}]: Uuid={title_info.Uuid},TitleId{title_info.TitleId}");
                }
            }

            if (vData.DungeonSceneInfo != null)
            {
                EncounterManager.ApplyDungeonDifficulty(vData.DungeonSceneInfo.Difficulty);
            }

            EncounterManager.Current.DungeonState = vData.FlowInfo.State;
            BattleStateMachine.DungeonStateHistoryAdd(vData.FlowInfo.State, extraData.ArrivalTime, vData.TimerInfo?.StartTime);

            int dungeonVarDataIdx = 0;
            foreach (var dungeonVarData in vData.DungeonVar.DungeonVarData)
            {
                System.Diagnostics.Debug.WriteLine($"DungeonVar.DungeonVarData[{dungeonVarDataIdx}] = {dungeonVarData}");
                dungeonVarDataIdx++;
            }

            foreach (var targetData in vData.Target.TargetData)
            {
                BattleStateMachine.LogDungeonTarget(targetData.Key, targetData.Value.Complete, targetData.Value.Nums);
                System.Diagnostics.Debug.WriteLine($"Target.TargetData[{targetData.Key}]: TargetId={targetData.Value.TargetId},Nums={targetData.Value.Nums},Complete={targetData.Value.Complete}");
            }

            foreach (var damage in vData.Damage.Damages)
            {
                System.Diagnostics.Debug.WriteLine($"Damage.Damages[{damage.Key}]: {damage.Value}");
            }

            if (vData.TimerInfo != null)
            {
                System.Diagnostics.Debug.WriteLine($"TimerInfo = {vData.TimerInfo.Type}, {vData.TimerInfo.StartTime}, {vData.TimerInfo.DungeonTimes}, {vData.TimerInfo.Direction}, {vData.TimerInfo.Index}, {vData.TimerInfo.ChangeTime}, {vData.TimerInfo.EffectType}, {vData.TimerInfo.PauseTime}, {vData.TimerInfo.PauseTotalTime}, {vData.TimerInfo.OutLookType}");
            }

            System.Diagnostics.Debug.WriteLine($"syncDungeonData.vData State={vData.FlowInfo.State},TotalScore={vData.DungeonScore.TotalScore},CurRatio={vData.DungeonScore.CurRatio}");
        }

        public static void ProcessSyncDungeonDirtyData(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {
            BattleStateMachine.CheckDeferredCalls();

            var dirty = SyncDungeonDirtyData.Parser.ParseFrom(payloadBuffer);
            if (dirty?.VData?.Buffer == null || dirty.VData.Buffer.Length == 0)
            {
                return;
            }

            var buf = dirty.VData.Buffer.ToByteArray();

            bool isStreamSafe = dirty.VData.StreamType == EStreamType.StreamTypeDeltaDirtySafe;
            var dun = new StarResonanceDps.Core.Protocols.Game.Binary.DungeonDirtyData(
                new StarResonanceDps.Core.Protocols.Game.Binary.BlobReader(buf, isStreamSafe));
            ClearAnnouncementBarsOnTargetProgress(DungeonTargetStore.Instance.ApplyDirty(dun));
            DungeonScoreStore.Instance.ApplyDirty(dun);

            if (dun?.PlayerList != null && dun?.PlayerList.PlayerInfos.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine("dun?.PlayerList != null");
                System.Diagnostics.Debug.WriteLine(dun?.PlayerList.PlayerInfos);
            }

            if (dun?.FlowInfo != null)
            {
                if (dun.FlowInfo?.State != null)
                {
                    EDungeonState dungeonState = (EDungeonState)dun.FlowInfo.State;
                    EncounterManager.Current.DungeonState = dungeonState;
                    BattleStateMachine.DungeonStateHistoryAdd(dungeonState, extraData.ArrivalTime, dun.TimerInfo?.StartTime);
                }
            }

            if (dun?.Damage != null)
            {
                foreach (var item in dun.Damage.Damages!)
                {
                    System.Diagnostics.Debug.WriteLine($"dun.Damage.Damages = {item.Key}, {item.Value}");
                }
            }

            if (dun?.DungeonPioneer != null)
            {
                foreach (var item in dun.DungeonPioneer.CompletedTargetThisTime!)
                {
                    var CompletedTargetListIdx = 0;
                    foreach (var completedTargetList in item.Value.CompletedTargetList!)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{item.Key}]CompletedTargetList[{CompletedTargetListIdx}] = {completedTargetList.Key}, {completedTargetList.Value}");
                        CompletedTargetListIdx++;
                    }
                }
            }

            if (dun?.DungeonEvent?.DungeonEventData != null)
            {
                foreach (var dungeonEventData in dun.DungeonEvent.DungeonEventData)
                {
                    System.Diagnostics.Debug.WriteLine($"[{dungeonEventData.Key}]DungeonEventData = {dungeonEventData.Value.EventId}, {dungeonEventData.Value.StartTime}, {dungeonEventData.Value.State}, {dungeonEventData.Value.Result}");
                    if (dungeonEventData.Value.DungeonTarget != null)
                    {
                        foreach (var dungeonTarget in dungeonEventData.Value.DungeonTarget)
                        {
                            System.Diagnostics.Debug.WriteLine($"- {dungeonTarget.Key}: {dungeonTarget.Value.TargetId}, {dungeonTarget.Value.Complete}, {dungeonTarget.Value.Nums}");
                        }
                    }
                }
            }

            if (dun?.TimerInfo != null)
            {
                System.Diagnostics.Debug.WriteLine($"dun.TimerInfo = {dun.TimerInfo.TimerType}, {dun.TimerInfo.StartTime}, {dun.TimerInfo.DungeonTimes}, {dun.TimerInfo.Direction}, {dun.TimerInfo.Index}, {dun.TimerInfo.ChangeTime}, {dun.TimerInfo.EffectType}, {dun.TimerInfo.PauseTime}, {dun.TimerInfo.PauseTotalTime}, {dun.TimerInfo.OutLookType}");
                if (dun.TimerInfo.EffectType == EDungeonTimerEffectType.Sub && dun.TimerInfo.ChangeTime != null)
                {
                    EncounterManager.Current.ExData.DungeonTimeDeathChange += (int)dun.TimerInfo.ChangeTime;
                }
            }

            if (dun?.DungeonVarAll?.DungeonVarAllMap?.Count > 0)
            {
                if (dun?.DungeonVar?.Data.Count > 1)
                {
                    System.Diagnostics.Debug.WriteLine("DungeonVarAll.DungeonVarAllMap.Count > 1!!");
                }

                int dungeonVarAllMapIdx = 0;
                foreach (var dungeonVarAllMap in dun.DungeonVarAll.DungeonVarAllMap!)
                {
                    int dungeonVarDataIdx = 0;
                    foreach (var dungeonVarData in dungeonVarAllMap.Value.Data)
                    {
                        System.Diagnostics.Debug.WriteLine($"dun.DungeonVarAll.DungeonVarAllMap.[{dungeonVarAllMapIdx}][{dungeonVarAllMap.Key}][{dungeonVarDataIdx}] = {dungeonVarData.Name}, {dungeonVarData.Value}");
                        dungeonVarDataIdx++;
                    }

                    dungeonVarAllMapIdx++;
                }
            }

            if (dun?.Target?.TargetChanges is { } targetChanges)
            {
                foreach (var (targetId, targetData) in targetChanges.Added.Concat(targetChanges.Updated))
                {
                    BattleStateMachine.LogDungeonTarget(targetId, targetData.Complete, targetData.Nums);
                }

                foreach (var targetId in targetChanges.Removed)
                {
                    BattleStateMachine.LogDungeonTargetRemoved(targetId);
                }
            }
        }

        /// <summary>
        /// 未完了の目標が完了した・目標から消えたら(ボスの撃破と段の切り替わり)、予告のバーを全部消す。
        /// ゲームはどちらのときも予告を消す通知を送らないので、消さないとバーの終わりに通知とタイムラインの行が出る。
        /// </summary>
        private static void ClearAnnouncementBarsOnTargetProgress(IReadOnlyList<int> progressedTargetIds)
        {
            if (progressedTargetIds.Count == 0)
            {
                return;
            }

            Log.Information("Dungeon targets {TargetIds} completed or were removed. Boss announcement bars cleared", progressedTargetIds);
            BossDbmBarStore.Instance.Clear();
        }
    }
}
