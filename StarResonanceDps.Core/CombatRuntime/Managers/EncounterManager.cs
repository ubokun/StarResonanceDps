using StarResonanceDps.Core.CombatRuntime.Protocols;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using ZLinq;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime
{
    public static class EncounterManager
    {
        public static int SelectedEncounter = -1;

        public static Encounter Current = null!;

        public static int CurrentBattleId = 0;
        public static uint LevelMapId { get; private set; }
        public static bool AllowSceneUpdate = true;

        public static string SceneName { get; private set; } = null!;

        /// <summary>
        /// チャンネル(回線)番号。チャンネルの無い場所では 0。出所は <c>SceneData.LineId</c>。
        /// エンカウンターは頻繁に作り直されるため、今の値はここで保持し、回には作るたびに写す(<see cref="EncounterExData.ChannelLine"/>)。
        /// </summary>
        public static uint ChannelLineId { get; private set; }


        /// <summary>
        /// チャンネル番号を反映する。
        ///
        /// <para>
        /// <b><c>SceneData.SceneGuid</c> はシーンのキーに使えない</b> — 移動元の guid を
        /// 載せたまま遅れて届いたり、同じマップが別々の guid で届いたりする。
        /// シーン変化の判定は <c>LevelMapId</c> と <c>LineId</c> で行うこと。
        /// </para>
        /// </summary>
        public static void SetChannelLineId(uint lineId)
        {
            ChannelLineId = lineId;
        }

        /// <summary>
        /// シーンを起動時の値(マップ 0・シーン名 null・チャンネル 0)に戻す。ログアウト(<c>ExitGame</c>)で使う。
        /// <b>開いている記録(<see cref="Current"/> と battle 行)には押さない。</b> 直後の保存がログアウト前のマップのまま残るように。
        /// マップ 0 は「まだシーン通知を一度も受け取っていない」状態で、入り直したときの最初のシーン通知は移動ではなく初回確定になる。
        /// </summary>
        internal static void ResetSceneToStartup()
        {
            LevelMapId = 0;
            SceneName = null!;
            ChannelLineId = 0;
        }

        // 予告のバーの終わりの行を、どの到着時刻まで残したか。到着時刻とともに進むだけなので消さない。
        private static DateTime _announcementBarsRecordedUntil = DateTime.MinValue;

        /// <summary>
        /// パケットを1つ処理する直前に呼ぶ(<c>NetCap.BeforeParsePacket</c>)。そのパケットの到着時刻までに終わった予告のバーを、
        /// 今のエンカウンターの記録へ、タイムラインの終わりの行として残す。パケットの出来事より前に入るので、並びはバーの終わりの時刻どおりになる。
        /// 途中で消えたバー・上書きされたバーは控え(<see cref="Services.BossDbmBarStore"/>)に無いので残らない。
        /// </summary>
        public static void RecordEndedAnnouncementBars(DateTime arrivalTime)
        {
            if (arrivalTime <= _announcementBarsRecordedUntil)
            {
                return;
            }

            var ended = Services.BossDbmBarStore.Instance.GetEndedBetween(_announcementBarsRecordedUntil, arrivalTime);
            _announcementBarsRecordedUntil = arrivalTime;
            foreach (var bar in ended)
            {
                Current.AddSkillAnnouncementBarEnd(bar);
            }
        }

        /// <summary>
        /// 保持していた前の回(<see cref="Encounter.PastEncounterForMeter"/>)を、終わっていれば手放してプレイヤーリストの名簿を作り直す。
        /// 終わりは、今の回に記録が入ったか、設定「次のイベントまで結果を保持」がオフになったとき。
        /// パケットを1つ処理した後(<c>NetCap.AfterParsePacket</c>)と、設定をオフにしたとき(<see cref="CombatRuntimeSettings.Apply"/>)に呼ぶ。
        /// メーターと名簿は同じ回を映す(<c>MeterSnapshotProvider.ResolveActiveEncounter</c>)ので、手放したら名簿も作り直して、
        /// 前の回にだけいた人の灰色の行を外す。
        /// </summary>
        public static void ReleaseHeldPastEncounter()
        {
            var current = Current;
            if (current?.PastEncounterForMeter is null
                || (CombatRuntimeSettings.KeepPastEncounterInMeterUntilNextDamage && !current.HasStatsBeenRecorded()))
            {
                return;
            }

            current.PastEncounterForMeter = null;
            PlayerRosterProjection.RebuildRoster();
        }
        public delegate void BattleStartEventHandler(EventArgs e);
        public static event BattleStartEventHandler? BattleStart;
        public delegate void EncounterStartEventHandler(EncounterStartEventArgs e);
        public static event EncounterStartEventHandler? EncounterStart;
        public delegate void EncounterEndEventHandler(EventArgs e);
        public static event EncounterEndEventHandler? EncounterEnd;
        public delegate void EncounterEndFinalEventHandler(EncounterEndFinalData e);
        public static event EncounterEndFinalEventHandler? EncounterEndFinal;

        /// <summary>
        /// 記録のある回を閉じた(<see cref="EnterDungeon"/> の作り直しで、DB に保存する回と同じ回)。値は閉じた回で、
        /// 素性を焼き付けた後・新しい回に替える前に上がる。閉じた回にはもう記録が入らない。パケット処理のスレッドで上がる。
        /// </summary>
        public static event Action<Encounter>? EncounterClosing;

        /// <summary>
        /// 走っている計測の長さ(秒)と、最初に攻撃した敵だけを数えるか。計測の開始のときに設定(<see cref="CombatRuntimeSettings"/>)から控え、
        /// 計測中に作る回へ写す。計測の途中で設定を保存しても、その計測は控えた値のまま。
        /// </summary>
        private static int _benchmarkDurationSeconds;
        private static bool _benchmarkFirstTargetOnly;

        /// <summary>
        /// 計測中か(押してから、停止・ログアウト・始まった後のマップ移動まで)。書くのは計測の開始・停止・中断だけ。
        /// 立っている間の <see cref="Current"/> は計測の回(<see cref="EncounterExData.BenchmarkTime"/> を持つ)。
        /// </summary>
        public static bool IsBenchmarkActive { get; private set; }

        /// <summary>計測が始まったか(計測の回に起点が立った)。立つ前は待機中。</summary>
        public static bool HasBenchmarkBegun => IsBenchmarkActive && Current?.ExData.FirstDamageTimeStamp != null;

        static EncounterManager()
        {

            StartNewMap();
            EnterDungeon(keepPastEncounterInMeter: false);
        }

        /// <param name="keepPastEncounterInMeter">
        /// 設定「次のイベントまで結果を保持」で、新しい回に記録が入るまで前の回をメーターに出してよいか。
        /// 「進行で自動リセット」の区切りとマップ移動(直後のダンジョン状態の作り直しも)は true、
        /// 手動のリセット・計測の開始と停止・ログアウトは false(すぐ新しい回を出す)。
        /// 手動のリセットも <c>Force</c> / <c>NewObjective</c> を使うので <paramref name="reason"/> では分けられず、呼び手が渡す。
        /// </param>
        public static void EnterDungeon(
            bool keepPastEncounterInMeter,
            bool force = false,
            EncounterStartReason reason = EncounterStartReason.None,
            [System.Runtime.CompilerServices.CallerMemberName] string enterDungeonCaller = "")
        {
            // A synchronized quest is one aggregate; automatic phase splits would divide damage by the entire quest clock.
            if (Current?.ExData.UseQuestClock == true && CustomCombatFeatures.QuestStartUtc.HasValue
                && keepPastEncounterInMeter && (reason is EncounterStartReason.NewObjective or EncounterStartReason.Wipe))
                return;
            // 計測が始まった後は区切らない(窓の途中で回が割れないように)。終わらせる側(停止・ログアウト・マップ移動)は、
            // 先に印を下ろしてから呼ぶ。待機中は区切ってよい(記録が無く、作り直した回も計測の回になる)。
            if (HasBenchmarkBegun)
            {
                Serilog.Log.Information("Encounter split skipped during benchmark: reason={Reason} caller={Caller}", reason, enterDungeonCaller);
                return;
            }

            // 通知の途中(ボス部屋の区切り)で作り直すときは、それまでに控えた敵のバフを閉じる前の回に残す。
            MessageManager.FlushPendingEnemyBuffLogs();

            string priorBossName = "";
            int priorEncounterPhase = 0;

            if (Current != null)
            {
                bool hasStatsBeenRecorded = Current.HasStatsBeenRecorded();

                // 開いている回は、記録の有無に関係なく閉じてから作り直す。
                if (force || Current.EndTime == DateTime.MinValue)
                {
                    StopEncounter(true);
                }

                if (reason == EncounterStartReason.Wipe)
                {
                    // 全滅の印は、全滅で区切る回にだけ付ける。
                    Current.SetWipeState(true);

                    if (Current.Entities.TryGetValue(Current.BossUUID, out var bossEntity))
                    {
                        long lowestHp = bossEntity.MaxHp;

                        var recentHpHistory = bossEntity.RecentHpHistory;
                        int stackSize = recentHpHistory is null ? 0 : Math.Min(recentHpHistory.Count, 8);
                        for (int i = 0; i < stackSize; i++)
                        {
                            long historicalHp = recentHpHistory!.ElementAt(i);
                            if (historicalHp < lowestHp)
                            {
                                lowestHp = historicalHp;
                            }
                        }

                        bossEntity.SetHpNoUpdate(lowestHp);
                    }
                }

                if ((reason == EncounterStartReason.NewObjective) && hasStatsBeenRecorded)
                {

                    priorBossName = Current.BossName;

                    if (Current.ExData.EncounterPhase > 0)
                    {
                        priorEncounterPhase = Current.ExData.EncounterPhase;
                    }
                    else
                    {

                        Current.SceneSubName = "Phase 1";
                        Current.ExData.EncounterPhase = 1;
                        priorEncounterPhase = 1;
                    }
                }

                CheckTimeOutStatus(reason);

                BattleStateMachine.SetDeferredEncounterEndFinalData(DateTime.Now.Subtract(new TimeSpan(0, 0, 1)), new EncounterEndFinalData() { EncounterId = Current.EncounterId, BattleId = Current.BattleId, Reason = reason, Encounter = Current });
                BattleStateMachine.CheckDeferredCalls();
            }

            int currentDifficulty = 0;
            Encounter? priorEncounter = Current;
            ulong nextEncounterIdModifier = 0;
            if (Current != null)
            {
                currentDifficulty = Current.ExData.DungeonDifficulty;
                // 戦闘データの無いエンカウンターは保存もせず、採番も進めない。
                // 保存すると履歴の一覧が空の記録で埋まる。
                nextEncounterIdModifier = Current.HasStatsBeenRecorded() ? 1UL : 0UL;
            }

            if (MessageManager.currentUserUuid != 0)
            {
                Entity? priorSelf = null;
                priorEncounter?.Entities.TryGetValue(MessageManager.currentUserUuid, out priorSelf);
            }

            // 画面に出している素性を焼き付ける。<b>作り直しで運ぶより前に行うこと</b> —
            // 後ろに置くと次のエンカウンターへ運ばれるのは焼き付ける前の値になり、
            // 保存した回と次の回で同じ人の素性が食い違う。とくに NPC の印は実体に残らないと、
            // 社交データが切れた回(ダンジョン退出・途中からの起動)で失われる。
            if (priorEncounter != null && nextEncounterIdModifier != 0)
            {
                ApplyDisplayedIdentitiesForRecord(priorEncounter);
                EncounterClosing?.Invoke(priorEncounter);
            }

            // 記録が入るまでメーターに出す前の回。記録の無い前の回は飛ばし、それが持っていた回を引き継ぐ
            // (マップ移動の直後のダンジョン状態の作り直しで切れないように)。新しい回と一緒に入れて、空の回が一瞬出ないようにする。
            Encounter? pastEncounterForMeter = null;
            if (keepPastEncounterInMeter
                && CombatRuntimeSettings.KeepPastEncounterInMeterUntilNextDamage
                && priorEncounter != null)
            {
                pastEncounterForMeter = priorEncounter.HasStatsBeenRecorded()
                    ? priorEncounter
                    : priorEncounter.PastEncounterForMeter;
            }

            Current = new Encounter(CurrentBattleId) { PastEncounterForMeter = pastEncounterForMeter };
            CustomCombatFeatures.InitializeEncounter(Current);

            // 持つのは1つ前の回まで。前の回が持っていた回を手放す(新しい回を出した後に外す)。
            if (priorEncounter != null)
            {
                priorEncounter.PastEncounterForMeter = null;
            }

            Current.EncounterId = DB.GetNextEncounterId() + nextEncounterIdModifier;
            System.Diagnostics.Debug.WriteLine($"Created new encounter for EncounterId {Current.EncounterId} + ({nextEncounterIdModifier})");
            // 素性を捨ててよい区切りは、ログアウト(ExitGame)だけ。それ以外の理由(ダンジョン状態 Null・Playing の None を含む)は運ぶ。
            // 起動時は前の回が無いので何も運ばない。
            if (priorEncounter != null && reason != EncounterStartReason.ExitGame)
            {

                var priorCharacters = priorEncounter.Entities.AsValueEnumerable().Where(x => x.Value.EntityType == EEntityType.EntChar);
                foreach (var priorChar in priorCharacters)
                {
                    var newChar = Current.GetOrCreateEntity(priorChar.Key);
                    newChar.SetHpValuesNoUpdate(priorChar.Value.Hp, priorChar.Value.MaxHp);
                    newChar.Attributes = priorChar.Value.Attributes.ToDictionary();

                    // 素性(名前・職業・特化・マーカー等)を丸ごと引き継ぐ。統計は新品のまま。
                    // **Attributes のコピーだけでは足りない** — 特化とマーカーはそこに入らない。
                    newChar.CopyIdentityFrom(priorChar.Value);
                }

                if (reason == EncounterStartReason.NewObjective)
                {

                    var priorBosses = priorEncounter.Entities.AsValueEnumerable().Where(x => x.Value.EntityType == EEntityType.EntMonster && x.Value.MonsterType == EMonsterType.Boss);
                    foreach (var priorBoss in priorBosses)
                    {
                        if (priorBoss.Value.Hp > 0)
                        {
                            var newBoss = Current.GetOrCreateEntity(priorBoss.Key);
                            newBoss.SetHpValuesNoUpdate(priorBoss.Value.Hp, priorBoss.Value.MaxHp);
                            newBoss.Attributes = priorBoss.Value.Attributes.ToDictionary();
                            var attrId = newBoss.GetAttrKV("AttrId");
                            if (attrId != null)
                            {
                                Current.SetAttrKV(priorBoss.Key, "AttrId", (int)attrId);
                            }
                        }
                    }

                    // 全滅リセットの直後に目標が切り替わると、ボスはまだキャッシュのままで実体に戻っていない。
                    // 実体だけを運ぶとキャッシュごと失われ、出現時にしか届かない AttrId が無くなるので、残ったキャッシュも渡す。
                    foreach (var (uuid, bossCache) in priorEncounter.PreviousBossCache)
                    {
                        if (bossCache.Hp > 0 && !Current.Entities.ContainsKey(uuid))
                        {
                            Current.PreviousBossCache[uuid] = bossCache;
                        }
                    }
                }
                else if (reason == EncounterStartReason.Wipe)
                {
                    var priorBosses = priorEncounter.Entities.AsValueEnumerable().Where(x => x.Value.EntityType == EEntityType.EntMonster && x.Value.MonsterType == EMonsterType.Boss);
                    foreach (var priorBoss in priorBosses)
                    {
                        var bossCache = new EncounterBossDataCache()
                        {
                            UUID = priorBoss.Key,
                            Name = priorBoss.Value.Name,
                            Hp = priorBoss.Value.Hp,
                            MaxHp = priorBoss.Value.MaxHp,
                            Attrs = priorBoss.Value.Attributes.ToDictionary()
                        };
                        Current.PreviousBossCache[priorBoss.Key] = bossCache;
                    }
                }
            }
            if (reason == EncounterStartReason.NewObjective)
            {
                if (!string.IsNullOrEmpty(priorBossName))
                {
                    Current.BossName = priorBossName;
                }
                if (priorEncounterPhase > 0)
                {
                    Current.SceneSubName = $"Phase {priorEncounterPhase + 1}";
                    Current.ExData.EncounterPhase = priorEncounterPhase + 1;
                }
            }
            Current.SetWipeState(false);

            if (LevelMapId > 0)
            {
                // シーン名は難易度から難易度名を引くので、難易度を先に入れる。
                Current.SetDungeonDifficulty(currentDifficulty);
                SetSceneId(LevelMapId, true);
                Current.SetChannelLineNumber(ChannelLineId);
            }

            // 計測中に作った回は計測の回(待機中の作り直しも)。計測時間を写しておくと、履歴でも同じ窓で読める。
            // 自動一時停止の秒数も写す(履歴でも同じ時計で読める)。計測の回は窓で止まるので止めない。
            if (IsBenchmarkActive)
            {
                Current.ExData.BenchmarkTime = _benchmarkDurationSeconds;
                Current.BenchmarkFirstTargetOnly = _benchmarkFirstTargetOnly;
            }
            else
            {
                Current.ExData.CombatExitSeconds = CombatRuntimeSettings.CombatExitSeconds;
            }

            if (priorEncounter != null)
            {
                if (nextEncounterIdModifier != 0)
                {
                    // 素性の焼き付けは作り直しの前に済ませてある(持ち越しへ載せるため)。
                    DB.InsertEncounter(priorEncounter);
                    priorEncounter.ReportUnresolvedBlankSources();
                    GC.Collect();
                }
            }

            AllowSceneUpdate = true;

            // 統計が0になった人(AOI外でメーターにだけ居た灰色の行)をプレイヤーリストからも外す。
            // 素性の焼き付け(ApplyDisplayedIdentitiesForRecord がリストの表示値を読む)より後に置く。
            PlayerRosterProjection.RebuildRoster();

            // エンカウンターの作り直しも「次のイベント」。計測・リセット・マップ移動・
            // 「進行で自動リセット」の区切りは全部ここを通るので、ボタン側に専用の解除を書かない。
            EncounterHistoryProvider.NotifyLiveEncounterEvent();

            Serilog.Log.Debug("EncounterManager sending OnEncounterStart event");
            OnEncounterStart(new EncounterStartEventArgs() { Reason = reason });
        }

        public static void StopEncounter(bool isKnownFinal = false, EncounterStartReason reason = EncounterStartReason.None)
        {
            if (Current != null && Current.EndTime == DateTime.MinValue)
            {
                Current.SetEndTime(DateTime.Now);
            }

            CheckTimeOutStatus(reason);

            OnEncounterEnd(new EventArgs());

            if (isKnownFinal)
            {

                BattleStateMachine.SetDeferredEncounterEndFinalData(DateTime.Now.AddSeconds(2), new EncounterEndFinalData() { EncounterId = Current.EncounterId, BattleId = Current.BattleId, Reason = reason, Encounter = Current });
            }
            else
            {
                BattleStateMachine.SetDeferredEncounterEndFinalData(DateTime.Now.AddSeconds(5), new EncounterEndFinalData() { EncounterId = Current.EncounterId, BattleId = Current.BattleId, Reason = reason, Encounter = Current });
            }

        }

        static void CheckTimeOutStatus(EncounterStartReason reason)
        {
            if (!Current.ExData.IsTimedOut && reason == EncounterStartReason.DungeonStateEnd && Current.SceneId > 0)
            {
                if (HelperMethods.DataTables.SceneEventDungeonConfigs.Data.TryGetValue(Current.SceneId.ToString(), out var sceneEventDungeonConfig))
                {
                    if (sceneEventDungeonConfig.LimitTime > 0 && Current.BossUUID == 0)
                    {
                        // 制限時間はダンジョンの開始(回を作った時刻)から測る。呼ばれるのは終了時刻を入れた後。
                        if ((Current.EndTime - Current.StartTime).TotalSeconds >= (sceneEventDungeonConfig.LimitTime + Current.ExData.DungeonTimeDeathChange))
                        {
                            Current.SetTimedOutState(true);
                        }
                    }
                }
            }
        }

        public static void ShutdownManager()
        {
            if (Current != null && Current.EndTime == DateTime.MinValue)
            {
                Current.SetEndTime(DateTime.Now);
            }

            if (Current != null)
            {
                SaveCurrentForRecordAndCloseBattle();
            }

        }

        /// <summary>
        /// 今のエンカウンターを記録として締める。<b>終了時刻は呼ぶ側で入れておくこと。</b>
        ///
        /// <para>
        /// マップ移動の保存(<see cref="EnterDungeon"/> → <see cref="StartEncounter"/>、battle 行は <see cref="StartNewMap"/>)と同じ結果にそろえる:
        /// 画面に出している素性を焼き付けてから保存し、battle 行を閉じる。
        /// マップ移動の側は流れの途中に保存が組み込まれているので、この関数を通らない。手順を変えるときは両方を直す。
        /// </para>
        /// </summary>
        internal static void SaveCurrentForRecordAndCloseBattle()
        {
            if (Current.HasStatsBeenRecorded())
            {
                ApplyDisplayedIdentitiesForRecord(Current);
                DB.InsertEncounter(Current);
                Current.ReportUnresolvedBlankSources();
            }

            if (CurrentBattleId != 0)
            {
                DB.UpdateBattleEnd(CurrentBattleId);
            }
        }

        public static void SignalEncounterEndFinal(EncounterEndFinalData data)
        {
            OnEncounterEndFinal(data);
            if (Current != null)
            {
                Current.RemoveEventHandlers();
            }
        }

        /// <summary>
        /// 記録する回に、<b>いま画面に出している素性をエンティティへ焼き付ける。</b>写真のリンクは名刺の店から書く(<see cref="Entity.RecordedAvatar"/>)。
        /// あわせて今のシーズンとゲームのサーバーを回に書く(<see cref="EncounterExData.SeasonId"/>・<see cref="EncounterExData.GameServer"/>)。
        /// <b>エンカウンターの作り直しより前に呼ぶこと</b> — 焼いた値をそのまま次の回へ持ち越すため。
        ///
        /// <para>
        /// エンティティの生の値は表示値と一致しない。AOI退出で特化は消され
        /// (<c>ClearTransientHumanSubProfession</c>)、PT外の人の名前・職業・戦闘力は
        /// PT補完や各キャッシュが埋めている。**どちらも blob に載らない**ので、
        /// そのまま保存すると履歴が Unknown だらけになる。
        /// </para>
        ///
        /// <para>
        /// 出所は <see cref="PlayerRosterStore"/> のスナップショット。合成済みの表示値がそのまま入っている。
        /// この時点ではまだ生きている — マップ移動でも <c>StartNewMap</c> → <c>EnterDungeon</c>(ここ) の後に
        /// <c>PlayerRosterProjection.BeginMap()</c> がロスターとキャッシュを捨てる。
        /// </para>
        ///
        /// <para>
        /// ロスターに載らない人(統計ゼロ)はどこにも表示されないので埋めない。
        /// </para>
        /// </summary>
        private static void ApplyDisplayedIdentitiesForRecord(Encounter encounter)
        {
            // シーズンとサーバーは回に1つ。名簿が空でも書く。
            encounter.ExData.SeasonId = SeasonStateStore.CurrentSeasonId;
            encounter.ExData.GameServer = GameServerStore.Current;

            var roster = Services.PlayerRosterStore.Instance.Current.Entries;
            if (roster.Count == 0)
            {
                return;
            }

            var displayedByCharacterId = roster
                .Where(entry => entry.CharacterId != 0)
                .GroupBy(entry => entry.CharacterId)
                .ToDictionary(group => group.Key, group => group.Last());

            foreach (var (_, entity) in encounter.Entities)
            {
                if (entity.EntityType != EEntityType.EntChar)
                {
                    continue;
                }

                var characterId = entity.UID != 0
                    ? entity.UID
                    : Utils.UuidToEntityId(entity.UUID);
                if (characterId == 0
                    || !displayedByCharacterId.TryGetValue(characterId, out var displayed))
                {
                    continue;
                }

                // 特化と「未装着」は同じ行から揃えて渡す。Rank1(アビリティ未装着)は
                // 特化0 + スナップショット受信済み で表される。
                entity.ApplyDisplayedIdentityForRecord(
                    displayed.Name,
                    displayed.ProfessionId,
                    displayed.SubProfessionId,
                    displayed.ClassSpec == Models.PlayerClassSpec.Rank1,
                    displayed.SeasonTalentBuffId,
                    displayed.IsSeasonTalentInactive,
                    displayed.CombatPower,
                    displayed.Level,
                    displayed.SeasonLevel,
                    displayed.SeasonStrength,
                    displayed.MaxHp,
                    displayed.IsNpc,
                    SocialDataStore.GetAvatar(characterId),
                    Models.PlayerClassSpecResolver.GetBuffTransformation(entity.UUID) ?? Models.PlayerClassSpec.Unknown);
            }
        }

        /// <summary>
        /// <b>マップ移動のときだけ呼ぶ。</b>持ち越されたバリアを落とす。
        ///
        /// <para>
        /// 作り直しで属性の辞書ごと運ぶので <c>AttrShieldList</c> も引き継がれるが、
        /// <b>ゲーム側はマップ移動でバリアを消しても、消えたことを伝える属性更新を送ってこない。</b>
        /// 上書きされないまま表示が残るため、ここで空にする。
        /// </para>
        ///
        /// <para>
        /// 属性の持ち越しは外せない(特化とマーカーが失われる)。
        /// <c>Force</c> はダンジョン開始と手動リセットからも来て、そちらではバリアは消えないので、
        /// <b>区切りの理由では分けられない。</b>マップ移動だと確定している呼び出し元からだけ叩く。
        /// </para>
        /// </summary>
        public static void ClearCarriedOverShields()
        {
            var encounter = Current;
            if (encounter is null)
            {
                return;
            }

            foreach (var (_, entity) in encounter.Entities)
            {
                if (entity.EntityType != EEntityType.EntChar)
                {
                    continue;
                }

                // 空の一覧を入れる。キーごと消すと「未観測」と区別が付かなくなる。
                entity.SetAttrKV("AttrShieldList", new List<ShieldInfo>());
            }
        }

        public static void StartNewMap()
        {

            if (CurrentBattleId != 0)
            {
                DB.UpdateBattleEnd(CurrentBattleId);
            }

            var battleId = DB.StartBattle(LevelMapId, SceneName);
            CurrentBattleId = battleId;

            OnBattleStart(new EventArgs());
        }

        /// <param name="updateOpenRecords">
        /// いま開いている記録(<see cref="Current"/> と <see cref="CurrentBattleId"/> の battle 行)にも
        /// このシーンを押すか。<b>マップ移動の直後に <c>StartNewMap</c> → <c>EnterDungeon</c> が走る場面では
        /// false を渡すこと。</b> あの2つは「開いている記録＝移動前のマップで戦ったもの」を
        /// そのまま保存/終了するので、ここで移動先のシーンを押すと保存直前に書き換えてしまう。
        /// 新しい <see cref="Current"/> への押印は <c>EnterDungeon</c> の中の呼び出しが行うので抜けは出ない。
        /// </param>
        public static void SetSceneId(uint levelMapId, bool force = false, bool updateOpenRecords = true)
        {

            if (!AllowSceneUpdate && !force)
            {
                return;
            }

            LevelMapId = levelMapId;

            // 名前は翻訳テーブルだけが決める。生テーブルは持たない。
            SceneName = levelMapId > 0 ? CombatDataCatalog.GetSceneName(levelMapId, Current.ExData.DungeonDifficulty) : "";

            if (!updateOpenRecords)
            {
                return;
            }

            Current.SceneId = LevelMapId;
            Current.SceneName = SceneName;
            DB.UpdateBattleInfo(CurrentBattleId, LevelMapId, SceneName);
        }

        /// <summary>
        /// シーン名を表示中の言語で引き直し、投影へ流し直す。<b>言語を切り替えたときに呼ぶ。</b>
        ///
        /// <para>
        /// <see cref="SceneName"/> は解決済みの文字列を持っているので、
        /// <c>CombatDataCatalog</c> の言語を変えただけでは追従しない。
        /// </para>
        ///
        /// <para>
        /// エンカウンター側(<c>Current.SceneName</c>)とDBは<b>履歴なので触らない</b>。
        /// 履歴は <c>SceneId</c> を持っていて、表示時に引き直す方針。
        /// </para>
        /// </summary>
        public static void RefreshSceneName()
        {
            if (LevelMapId == 0)
            {
                return;
            }

            SceneName = CombatDataCatalog.GetSceneName(LevelMapId, Current.ExData.DungeonDifficulty);

            // 投影は internal なので App からは触れない。ここまでを1つの操作にする。
            PlayerRosterProjection.UpdateMapName();
            NearbyEntityProjection.UpdateMapName();
        }

        /// <summary>
        /// ダンジョン同期で届いた難易度を現在のエンカウンターへ入れる。<b>変わったらマップ名を組み立て直す。</b>
        ///
        /// <para>
        /// 難易度はシーン切替の後に届くので、そのときのマップ名には難易度名が付いていない。
        /// 現在のエンカウンターのシーン名と投影のマップ名の両方を直す。
        /// </para>
        /// </summary>
        public static void ApplyDungeonDifficulty(int difficulty)
        {
            if (Current.ExData.DungeonDifficulty == difficulty)
            {
                return;
            }

            Current.SetDungeonDifficulty(difficulty);
            if (LevelMapId == 0)
            {
                return;
            }

            SetSceneId(LevelMapId, force: true);
            PlayerRosterProjection.UpdateMapName();
            NearbyEntityProjection.UpdateMapName();
        }

        /// <summary>
        /// 計測の開始と停止を切り替える。<b>パケットを処理するスレッドで呼ぶ</b>(<c>MeterSnapshotProvider.ToggleBenchmark</c>)。
        ///
        /// <para>
        /// 開始は計測の回を作って待つだけ。始まるのは自分の与ダメか、自分が出した回復の通知で実際に HP が増えたものが来て起点が立ったとき
        /// (<c>MessageManager</c> の戦闘の出来事の判定)。停止は、始まる前でも後でも印を下ろしてから回を区切る
        /// (記録があれば計測の注記つきで保存される)。
        /// </para>
        /// </summary>
        internal static void ToggleBenchmark()
        {
            if (IsBenchmarkActive)
            {
                Serilog.Log.Information("Benchmark stopped by the user (begun={HasBegun})", HasBenchmarkBegun);
                IsBenchmarkActive = false;
                EnterDungeon(keepPastEncounterInMeter: false, force: true, reason: EncounterStartReason.BenchmarkEnd);
                return;
            }

            _benchmarkDurationSeconds = CombatRuntimeSettings.BenchmarkDurationSeconds;
            _benchmarkFirstTargetOnly = CombatRuntimeSettings.BenchmarkFirstTargetOnly;
            Serilog.Log.Information(
                "Benchmark started: waiting for the player's first damage or healing ({Seconds} s window, first target only={FirstTargetOnly})",
                _benchmarkDurationSeconds,
                _benchmarkFirstTargetOnly);
            IsBenchmarkActive = true;
            EnterDungeon(keepPastEncounterInMeter: false, force: true, reason: EncounterStartReason.BenchmarkStart);
        }

        /// <summary>
        /// 今の回を区切り直す(リセット)。<b>パケットを処理するスレッドで呼ぶ</b>(<c>MeterSnapshotProvider.ResetCurrentEncounter</c>)。
        /// 計測が始まった後は計測の停止、待機中は計測の回の作り直し。
        /// </summary>
        internal static void ResetCurrentEncounter()
        {
            if (IsBenchmarkActive)
            {
                if (HasBenchmarkBegun)
                {
                    Serilog.Log.Information("Benchmark ended by a manual reset");
                    IsBenchmarkActive = false;
                    EnterDungeon(keepPastEncounterInMeter: false, force: true, reason: EncounterStartReason.BenchmarkEnd);
                }
                else
                {
                    EnterDungeon(keepPastEncounterInMeter: false, force: true, reason: EncounterStartReason.BenchmarkStart);
                }

                return;
            }

            EnterDungeon(
                keepPastEncounterInMeter: false,
                force: true,
                reason: BattleStateMachine.IsInOpenWorld() ? EncounterStartReason.Force : EncounterStartReason.NewObjective);
        }

        /// <summary>
        /// 計測の印だけを下ろす。回の保存と作り直しは呼び手のこの後の <see cref="EnterDungeon"/> が行う(計測の注記つきで保存される)。
        /// 呼ぶのはログアウト(待機中でも終える)と、始まった後のマップ移動(<paramref name="onlyIfBegun"/>。待機中のマップ移動は待ち続ける)。
        /// </summary>
        internal static void EndBenchmarkBeforeSplit(string cause, bool onlyIfBegun)
        {
            if (!IsBenchmarkActive || (onlyIfBegun && !HasBenchmarkBegun))
            {
                return;
            }

            Serilog.Log.Information("Benchmark ended by {Cause} (begun={HasBegun})", cause, HasBenchmarkBegun);
            IsBenchmarkActive = false;
        }

        static void OnBattleStart(EventArgs e)
        {
            BattleStart?.Invoke(e);
        }

        static void OnEncounterStart(EncounterStartEventArgs e)
        {
            EncounterStart?.Invoke(e);
        }

        static void OnEncounterEnd(EventArgs e)
        {
            EncounterEnd?.Invoke(e);
        }

        static void OnEncounterEndFinal(EncounterEndFinalData e)
        {
            EncounterEndFinal?.Invoke(e);
        }
    }

    public enum EncounterStartReason : int
    {
        None = 0,
        NewObjective = 1,
        Wipe = 2,
        Force = 3,
        TimedOut = 5,
        BenchmarkStart = 6,
        BenchmarkEnd = 7,
        DungeonStateEnd = 8,

        /// <summary>ログアウト(ログイン画面へ戻った)。素性を次の回へ運ばない唯一の理由。</summary>
        ExitGame = 9,
    }

    public class EncounterStartEventArgs : EventArgs
    {
        public EncounterStartReason Reason;
    }

    /// <summary>
    /// 戦闘の時計を1回読んだ値(<see cref="Encounter.ReadCombatClock()"/>)。時刻は UTC。
    /// 起点が無ければ <see cref="StartUtc"/> / <see cref="EndUtc"/> は null で、経過は 0。
    /// 1回の表示の中では1回だけ読み、全部の値を同じ終点で出す。
    /// </summary>
    /// <param name="IsClosed">閉じた回(終了時刻がある)か、計測の窓が終わった。</param>
    public readonly record struct CombatClockReading(DateTime? StartUtc, DateTime? EndUtc, TimeSpan Elapsed, bool IsClosed)
    {
        /// <summary>秒間値。<paramref name="seconds"/> が1秒未満なら合計そのもの。</summary>
        public static double PerSecond(ulong total, double seconds)
        {
            return seconds >= 1d ? Math.Round(total / seconds, 0) : total;
        }
    }

    public class Encounter
    {
        public ulong EncounterId { get; set; }
        public int BattleId { get; set; }
        public uint SceneId { get; set; }
        public string SceneName { get; set; } = null!;
        public string SceneSubName { get; set; } = null!;
        public long BossUUID { get; set; }
        public long BossAttrId { get; set; }
        public string BossName { get; set; } = null!;
        public int BossHpPct { get; set; }
        public string Note { get; set; } = null!;

        /// <summary>
        /// 回を作った時刻(<c>DateTime.Now</c>、ローカル)。戦闘の経過には使わない(経過は <see cref="ReadCombatClock()"/>)。
        /// 使うのは履歴の一覧の時刻・DB の並び・ダンジョンの制限時間だけ。
        /// </summary>
        public DateTime StartTime { get; private set; }

        /// <summary>回を閉じた時刻(<c>DateTime.Now</c>、ローカル)。<c>DateTime.MinValue</c> なら開いている。閉じた回の時計の終点。</summary>
        public DateTime EndTime { get; private set; }
        public DateTime LastUpdate { get; set; }
        public ConcurrentDictionary<long, Entity> Entities { get; set; } = [];

        public ulong TotalDamage { get; set; } = 0;
        public ulong TotalShieldBreak { get; set; } = 0;
        public ulong TotalHealing { get; set; } = 0;
        public ulong TotalOverhealing { get; set; } = 0;

        /// <summary>
        /// タイムライン・被ダメログの記録があるか。記録すべき戦闘かの判定(<see cref="HasStatsBeenRecorded"/>)に使う。
        ///
        /// <para>
        /// <b>行数をそのまま見る。</b> 載る経路は予告・予告のバーの終わり・詠唱・被弾・ダメージの無い死亡・敵の回復・敵から敵へのダメージ・敵が付けたバフで、
        /// どれも戦闘の時計を動かす出来事(<see cref="RecordCombatEventForLog"/>)なので全部数える。
        /// 印を別に持つと経路が増えたときに立て忘れる。判定は実行中のエンカウンターでしか行わないので DB には持たない。
        /// </para>
        /// </summary>
        public bool HasCombatEventLogRecords
        {
            get
            {
                lock (_combatEventLogGate)
                {
                    return _combatEventLog.Count > 0 || _takenHitLog.Count > 0 || ExData.EnemyControlEvents.Count > 0;
                }
            }
        }
        public ulong TotalDeaths { get; set; } = 0;
        public ulong TotalNpcDeaths { get; set; } = 0;
        public bool IsWipe { get; set; } = false;

        /// <summary>
        /// 設定「次のイベントまで結果を保持」で、この回に記録が入るまでメーターとプレイヤーリストに代わりに出す前の回。実行中だけで保存しない。
        ///
        /// <para>
        /// 入れるのは <see cref="EncounterManager.EnterDungeon"/> だけで、保持してよい区切り(「進行で自動リセット」の区切りとマップ移動)のとき。
        /// 手動のリセット・計測の開始と停止・ログアウトでは入れない。持つのは1つ前の回までで、
        /// この回に記録が入ったパケットの後か設定をオフにしたときに <see cref="EncounterManager.ReleaseHeldPastEncounter"/> が外す
        /// (記録は減らないので戻らない)。画面の側は読むだけ(<c>MeterSnapshotProvider.ResolveActiveEncounter</c>)。
        /// </para>
        /// </summary>
        internal Encounter? PastEncounterForMeter { get; set; }

        public delegate void SkillActivatedEventHandler(object sender, SkillActivatedEventArgs e);
        public event SkillActivatedEventHandler? SkillActivated;
        public delegate void HpUpdatedEventHandler(object sender, HpUpdatedEventArgs e);
        public event HpUpdatedEventHandler? BossHpUpdated;
        public event HpUpdatedEventHandler? EntityHpUpdated;
        public delegate void ThreatListUpdatedEventHandler(object sender, ThreatListUpdatedEventArgs e);
        public event ThreatListUpdatedEventHandler? EntityThreatListUpdated;
        public delegate void BuffUpdatedEventHandler(object sender, BuffUpdatedEventArgs e);
        public event BuffUpdatedEventHandler? BuffUpdated;
        public delegate void AttributeUpdatedEventHandler(object sender, AttributeUpdatedEventArgs e);
        public event AttributeUpdatedEventHandler? AttributeUpdated;
        public delegate void SceneEventEventHandler(object sender, SceneEventEventArgs e);
        public event SceneEventEventHandler? SceneEvent;

        public EncounterExData ExData { get; set; } = new();
        public byte[] ExDataBlob { get; set; } = null!;

        public List<long> BossUUIDs { get; set; } = new();
        public EDungeonState DungeonState { get; set; } = EDungeonState.DungeonStateNull;
        public Dictionary<long, EncounterBossDataCache> PreviousBossCache = new();

        // タイムラインが読む記録の並びと、被ダメログが読むプレイヤーの被弾。記録した順に積むだけで、この形では DB に入れない。
        // 被弾は回のあいだ全部残し、続きから順に読むだけなので圧縮して持つ。解放は回と一緒。
        // DB から読んだエンカウンターは空なので、最初に読まれたときに保存された記録の Sequence から組み直す。
        private readonly object _combatEventLogGate = new();
        private readonly List<CombatEventLogRecord> _combatEventLog = [];
        private readonly CompressedRecordLog<TakenHitRecord> _takenHitLog = new();
        private bool _combatEventLogRebuiltFromSnapshots;
        private long _combatEventLogSequence;

        // 着地先が決まらなかった行の控え。鍵は(記録先の UUID, 行代表キー)、値は最初に着かなかった記録。
        // 後の記録で着けば消え、保存の直後に残りを検知ログへ送る(ReportUnresolvedBlankSources)。DB には入れない。
        private readonly object _unresolvedBlankSourcesGate = new();
        private readonly Dictionary<(long EntityUuid, long RowKey), UnresolvedBlankSource> _unresolvedBlankSources = [];

        private sealed record UnresolvedBlankSource(
            string KeyText,
            bool IsBuffSource,
            bool IsHealing,
            ulong ValueTotal,
            ulong HitsCount,
            long CharacterId,
            string Trace,
            DateTime FirstBlankAt);

        public Encounter()
        {

        }

        public Encounter(int battleId = 0)
        {
            StartTime = DateTime.Now;
            Entities = new();
            BattleId = battleId;
        }

        public void SetEndTime(DateTime end)
        {
            EndTime = end;
        }

        /// <summary>
        /// 集計を止める非戦闘エリアのシーンの番号: アステルリーズ(街)・ホームガーデン・ホームハウス。
        /// 表の種類では決めない(同じ種類に戦闘のできるシーンがある。街のスターリー島、ギルド活動所と同じ種類のギルドハント)。
        /// アステルリーズと同じ名前を持つダンジョンのシーンは別の番号で、対象に入れない。
        /// </summary>
        private static readonly FrozenSet<uint> NonCombatSceneIds = FrozenSet.ToFrozenSet<uint>([8, 30001, 40001]);

        /// <summary>
        /// 集計を止めている回か。設定「非戦闘エリアでの集計を停止」がオンで、回のシーンが非戦闘エリア(<see cref="NonCombatSceneIds"/>)のとき。
        /// 設定もシーンもメッセージとメッセージのあいだでしか変わらない(設定は <see cref="CombatRuntimeSettings"/> の説明)ので、
        /// 1つのメッセージの中で時計の判定(<see cref="RecordCombatEvent"/>)と記録の門(<see cref="IsRecorded"/>)は必ずそろう。
        /// </summary>
        internal bool IsAggregationStopped =>
            CombatRuntimeSettings.StopAggregationInNonCombatAreas && NonCombatSceneIds.Contains(SceneId);

        /// <summary>計測の回か(計測時間を持つ)。履歴でも同じ値(DB に載る)。</summary>
        public bool IsBenchmark => ExData.BenchmarkTime > 0;

        /// <summary>計測の窓の終わり(起点 + 計測時間、UTC)。計測の回で起点があるときだけ。</summary>
        public DateTime? BenchmarkWindowEndUtc =>
            IsBenchmark && ExData.FirstDamageTimeStamp is { } start ? start.AddSeconds(ExData.BenchmarkTime) : null;

        /// <summary>計測の窓が終わったか(計測の回で起点があり、今が窓の終わり以降)。</summary>
        public bool IsBenchmarkWindowElapsed(DateTime utcNow) => BenchmarkWindowEndUtc is { } windowEnd && utcNow >= windowEnd;

        /// <summary>
        /// 計測で、自分が最初にダメージを与えた敵へのダメージだけを数えるか。計測の開始のときの設定を
        /// <c>EncounterManager.EnterDungeon</c> が写す。DB には載らない。
        /// </summary>
        public bool BenchmarkFirstTargetOnly { get; internal set; }

        /// <summary>計測の回で最初にダメージを与えた敵(<see cref="BenchmarkFirstTargetOnly"/> のときだけ決める)。決まる前は 0。</summary>
        private long _benchmarkFirstTargetUuid;

        /// <summary>
        /// 最初に攻撃した敵だけを数える切り替えの門。計測の窓の中の自分の与ダメで呼ぶ(<see cref="AddDamage"/>)。
        /// 最初の1件で敵を決め、ほかの敵への与ダメは記録しない。
        /// </summary>
        private bool IsBenchmarkFirstTarget(long targetUuid)
        {
            if (!IsBenchmark || !BenchmarkFirstTargetOnly)
            {
                return true;
            }

            if (_benchmarkFirstTargetUuid == 0)
            {
                _benchmarkFirstTargetUuid = targetUuid;
                Serilog.Log.Information("Benchmark first target fixed: target={TargetUuid} (encounter={EncounterId})", targetUuid, EncounterId);
            }

            return targetUuid == _benchmarkFirstTargetUuid;
        }

        public CombatClockReading ReadCombatClock() => ReadCombatClock(DateTime.UtcNow);

        /// <summary>
        /// 戦闘の時計を読む。経過・DPS/HPS の分母・推移グラフ・タイムライン・被ダメログの時刻の起点はどれもここ。
        ///
        /// <para>
        /// 起点は <see cref="EncounterExData.FirstDamageTimeStamp"/>(最初の戦闘の出来事の到着時刻、UTC)。無ければ経過は 0。
        /// 終点はライブなら <paramref name="utcNow"/>、閉じた回なら <see cref="EndTime"/>(UTC へ直すのはここだけ)。
        /// 計測の回は窓の終わりを上限にし、窓が終われば閉じた扱い。どれも DB に載る値から決まるので、履歴でも同じ式。
        /// </para>
        ///
        /// <para>
        /// 自動一時停止: 終点が止まっている区間の中なら、終点は止まった時刻(<see cref="GetPauseStart"/>)。
        /// 経過は <see cref="ToCombatOffset"/> で、止まっていた区間を除いた長さ。
        /// </para>
        /// </summary>
        internal CombatClockReading ReadCombatClock(DateTime utcNow)
        {
            var isClosed = EndTime != DateTime.MinValue;
            if (ExData.UseQuestClock && ExData.QuestStartUtc is { } questStart)
            {
                var questEnd = isClosed ? EndTime.ToUniversalTime() : utcNow;
                if (questEnd < questStart) questEnd = questStart;
                return new CombatClockReading(questStart, questEnd, questEnd - questStart, isClosed);
            }
            if (ExData.FirstDamageTimeStamp is not { } start)
            {
                return new CombatClockReading(null, null, TimeSpan.Zero, isClosed);
            }

            var end = isClosed ? EndTime.ToUniversalTime() : utcNow;
            if (BenchmarkWindowEndUtc is { } windowEnd && end >= windowEnd)
            {
                end = windowEnd;
                isClosed = true;
            }

            // 時刻の出所(キャプチャ時刻・今・閉じた時刻)の食い違い。経過を 0 にして、回ごとに1回だけ残す。
            if (end < start)
            {
                if (!_combatClockOrderWarned)
                {
                    _combatClockOrderWarned = true;
                    Serilog.Log.Warning("Combat clock end {End:o} precedes its start {Start:o} (encounter={EncounterId}); elapsed clamped to zero",
                        end, start, EncounterId);
                }

                end = start;
            }

            if (GetOpenPauseStart() is { } pausedAt && end > pausedAt)
            {
                end = pausedAt;
            }

            return new CombatClockReading(start, end, ToCombatOffset(end)!.Value, isClosed);
        }

        private bool _combatClockOrderWarned;

        private readonly object _combatPauseGate = new();

        /// <summary>
        /// 戦闘の出来事を1回受ける。判定は <c>MessageManager</c>(通知は <c>IsCombatEvent</c>、薬・料理・自然回復は <c>AddBuffHealing</c>)と、
        /// タイムラインと被ダメログの記録の入口(<see cref="RecordCombatEventForLog"/>)。
        /// 最初の出来事で戦闘の時計の起点を立て、一度立てたら動かさない。前の出来事から自動一時停止の秒数を超えて空いていたら、
        /// 止まり始めた時刻(<see cref="GetPauseStart"/>)からこの出来事までを止まっていた区間として残す。
        /// 集計を止めている回(<see cref="IsAggregationStopped"/>)では受けない(時計を立てず、進めない)。
        /// </summary>
        internal void RecordCombatEvent(DateTime arrivalUtc)
        {
            if (IsAggregationStopped)
            {
                return;
            }

            ExData.FirstDamageTimeStamp ??= arrivalUtc;

            lock (_combatPauseGate)
            {
                if (ExData.LastCombatEventTimeStamp is { } last)
                {
                    // 接続をまたいで到着順が前後した出来事。最後の出来事は戻さない。
                    if (arrivalUtc <= last)
                    {
                        return;
                    }

                    if (ExData.CombatExitSeconds > 0)
                    {
                        var pauseStart = GetPauseStart(last);
                        if (arrivalUtc > pauseStart)
                        {
                            ExData.CombatPauses.Add(new CombatPause { Start = pauseStart, End = arrivalUtc });
                        }
                    }
                }

                ExData.LastCombatEventTimeStamp = arrivalUtc;
            }
        }

        /// <summary>
        /// 到着時刻(UTC)を戦闘の経過に直す。起点からの時間から、それより前に止まっていた区間を除いたもの。止まっている区間の中の時刻は、止まった時点の経過になる。
        /// 経過・推移グラフの1秒の区切りと技のアイコン・タイムライン・被ダメログの時刻・有効DPS と毎分キャストはどれもこれを通す。起点が無ければ null、起点より前は負。
        /// </summary>
        public TimeSpan? ToCombatOffset(DateTime utc)
        {
            if (ExData.UseQuestClock && ExData.QuestStartUtc is { } questStart) return utc - questStart;
            if (ExData.FirstDamageTimeStamp is not { } start)
            {
                return null;
            }

            var offset = utc - start;
            if (offset <= TimeSpan.Zero)
            {
                return offset;
            }

            lock (_combatPauseGate)
            {
                foreach (var pause in ExData.CombatPauses)
                {
                    if (utc <= pause.Start)
                    {
                        break;
                    }

                    offset -= (utc < pause.End ? utc : pause.End) - pause.Start;
                }

                if (GetOpenPauseStartLocked() is { } pausedAt && utc > pausedAt)
                {
                    offset -= utc - pausedAt;
                }
            }

            return offset;
        }

        /// <summary>今止まっている区間の始まり(<see cref="GetPauseStart"/>)。止めない回と、出来事がまだ無い回は null。</summary>
        private DateTime? GetOpenPauseStart()
        {
            lock (_combatPauseGate)
            {
                return GetOpenPauseStartLocked();
            }
        }

        private DateTime? GetOpenPauseStartLocked()
        {
            return ExData.CombatExitSeconds > 0 && ExData.LastCombatEventTimeStamp is { } last
                ? GetPauseStart(last)
                : null;
        }

        /// <summary>最後の戦闘の出来事 <paramref name="lastEvent"/> の後に止まり始める時刻(最後の出来事 + 自動一時停止の秒数)。</summary>
        private DateTime GetPauseStart(DateTime lastEvent)
        {
            return lastEvent.AddSeconds(ExData.CombatExitSeconds);
        }

        /// <summary>
        /// 起点の後の出来事か。回復・行動時刻・発動の数は、起点より前を数えない
        /// (過剰回復だけの回復は起点を立てないので、起点の前の過剰回復は数えない)。
        /// </summary>
        private bool IsAfterCombatClockStart(DateTime arrivalUtc)
        {
            return ExData.FirstDamageTimeStamp is { } start && arrivalUtc >= start;
        }

        /// <summary>
        /// 記録してよい出来事か(記録の入口の門)。集計を止めている回(<see cref="IsAggregationStopped"/>)では常に偽、
        /// それ以外で計測の回でなければ常に真。
        ///
        /// <para>
        /// 計測の回は、窓 [起点, 起点 + 計測時間) の中で、かつ自分の出来事のときだけ真。待機中(起点の前)も窓の後も記録しない。
        /// <paramref name="playerUuid"/> は記録の主のプレイヤー(与ダメ・回復の出し手、被弾・死亡の本人、発動したプレイヤー)。
        /// プレイヤーに属さない記録(敵の詠唱・予告・予告のバーの終わり・敵の死亡・敵の回復・敵から敵へのダメージ・敵が付けたバフ)は null で、窓だけを見る。
        /// </para>
        ///
        /// <para>
        /// 止めるのは記録だけ。特化・職業の判定、CD の推定、バフ、予告のバーの控えは止めない。
        /// </para>
        /// </summary>
        internal bool IsRecorded(DateTime arrivalUtc, long? playerUuid)
        {
            if (IsAggregationStopped)
            {
                return false;
            }

            if (!IsBenchmark)
            {
                return true;
            }

            if (ExData.FirstDamageTimeStamp is not { } start
                || arrivalUtc < start
                || arrivalUtc >= start.AddSeconds(ExData.BenchmarkTime))
            {
                return false;
            }

            return playerUuid is not { } uuid
                || (uuid != 0 && uuid == AppState.PlayerUUID);
        }

        public Entity GetOrCreateEntity(long uuid)
        {
            if (Entities.TryGetValue(uuid, out var entity))
            {
                return entity;
            }
            else
            {
                entity = new Entity(uuid, null, this);
                Entities.TryAdd(uuid, entity);
                return entity;
            }
        }

        /// <summary>
        /// 周りから消えた実体の控えと記録を手放し、弾・仮想体・領域・ペット・モンスターは回から外す。
        /// 消えた後に読むのは種別IDと召喚者だけなので、それを控えへ移す(<see cref="Services.EntityOriginIndex"/>)。
        /// タイムライン・被ダメログに名前が出た実体(<see cref="Entity.IsNamedInCombatEventLog"/>)は履歴で読むので残す。
        /// ボスは全滅と目標の切り替わりで前の回から読むので残す。プレイヤーは回から外さない。
        /// </summary>
        public void RemoveDisappearedEntity(long uuid)
        {
            var entityType = (EEntityType)Utils.UuidToEntityType(uuid);

            // プレイヤーのバフの控えは名簿の行の機能が読むので、行が無くなったときに手放す(PlayerRosterProjection.RebuildRoster)。
            if (entityType != EEntityType.EntChar)
            {
                Services.ActiveBuffStore.Instance.RemoveEntity(uuid);
            }

            if (!Entities.TryGetValue(uuid, out var entity))
            {
                return;
            }

            // 記録を読むのは通知が届いたときだけで、見えない相手には届かない。また現れたら出現時の一覧で作り直す。
            entity.BuffEvents.Clear();

            if (entity.IsNamedInCombatEventLog)
            {
                return;
            }

            var topSummonerUuid = entity.GetAttrKV("AttrTopSummonerId") as long? ?? 0;
            Services.DisappearedEntity disappeared;
            switch (entityType)
            {
                case EEntityType.EntBullet or EEntityType.EntDummy or EEntityType.EntField or EEntityType.EntPet:
                    disappeared = new Services.DisappearedEntity(0, 0, topSummonerUuid);
                    break;

                case EEntityType.EntMonster when entity.MonsterType != EMonsterType.Boss:
                    disappeared = new Services.DisappearedEntity(
                        entity.GetAttrKV("AttrId") as int? ?? 0,
                        entity.GetAttrKV("AttrSummonerId") as long? ?? 0,
                        topSummonerUuid);
                    break;

                default:
                    return;
            }

            if (disappeared != default)
            {
                Services.EntityOriginIndex.Instance.SetDisappeared(uuid, disappeared);
            }

            Entities.TryRemove(uuid, out _);
        }

        public void SetName(long uuid, string name)
        {
            GetOrCreateEntity(uuid).SetName(name);
        }

        public void SetAbilityScore(long uuid, int power)
        {
            GetOrCreateEntity(uuid).SetAbilityScore(power);
        }

        /// <inheritdoc cref="Entity.ReplaceTalentNodes"/>
        public void ReplaceTalentNodes(long uuid, IReadOnlyDictionary<int, int[]> nodeIdsByProfession)
        {
            GetOrCreateEntity(uuid).ReplaceTalentNodes(nodeIdsByProfession);
        }

        /// <inheritdoc cref="Entity.ApplyTalentNodeChanges"/>
        public void ApplyTalentNodeChanges(
            long uuid,
            bool replacesExisting,
            IReadOnlyList<int> removedProfessionIds,
            IReadOnlyDictionary<int, int[]> changedNodeIds)
        {
            GetOrCreateEntity(uuid).ApplyTalentNodeChanges(replacesExisting, removedProfessionIds, changedNodeIds);
        }

        public void SetProfessionId(long uuid,  int professionId)
        {
            GetOrCreateEntity(uuid).SetProfessionId(professionId);
        }

        public void SetEntityType(long uuid, EEntityType etype)
        {
            var entity = GetOrCreateEntity(uuid);
            entity.SetEntityType(etype);

            var attr_id = entity.GetAttrKV("AttrId");
            if (attr_id != null && etype != EEntityType.EntChar)
            {

                entity.UpdateUID((int)attr_id);

                if (etype == EEntityType.EntMonster)
                {
                    if (HelperMethods.DataTables.Monsters.Data.TryGetValue(attr_id.ToString()!, out var monsterEntry))
                    {
                        entity.SetName(monsterEntry.Name);
                        entity.SetMonsterType(monsterEntry.MonsterType);
                        UpdateEncounterBossData(entity, (int)attr_id);
                    }
                }
            }
        }

        public void SetAttrKV(long uuid, string key, object value)
        {
            var entity = GetOrCreateEntity(uuid);

            // 死亡状態のプレイヤーの HP は 0。ダメージの無い即死では、サーバは死亡中も
            // 死ぬ前の HP を送ってくるので、届いた値をそのまま書くと死んだ人に HP が残る。
            // 復活では状態が先に死亡から外れ、HP はその後に届く。
            if (key == "AttrHp"
                && (EEntityType)Utils.UuidToEntityType(uuid) == EEntityType.EntChar
                && entity.GetAttrKV("AttrState") is EActorState.ActorStateDead
                && value is long hpValue
                && hpValue != 0)
            {
                value = 0L;
            }

            entity.SetAttrKV(key, value);

            if (key == "AttrId" && entity.EntityType != EEntityType.EntChar)
            {
                entity.UpdateUID((int)value);

                if (entity.EntityType == EEntityType.EntMonster)
                {
                    if (HelperMethods.DataTables.Monsters.Data.TryGetValue(value.ToString()!, out var monsterEntry))
                    {
                        entity.SetName(monsterEntry.Name);
                        entity.SetMonsterType(monsterEntry.MonsterType);
                        UpdateEncounterBossData(entity, (int)value);
                    }
                }
            }
            else if (key == "AttrName")
            {
                entity.SetName((string)value);
            }
            else if (key == "AttrProfessionId")
            {
                entity.SetProfessionId((int)value);
            }
            else if (key == "AttrFightPoint")
            {
                entity.SetAbilityScore((int)value);
            }
            else if (key == "AttrLevel")
            {
                entity.SetLevel((int)value);
            }
            else if (key == "AttrSeasonLevel")
            {
                entity.SetSeasonLevel((int)value);
            }
            else if (key == "AttrSeasonStrength")
            {
                entity.SetSeasonStrength((int)value);
            }
            else if (key == "AttrState")
            {
                // 死亡数は数えない(プレイヤー以外は RecordNonPlayerDeath、プレイヤーは RecordPlayerDeath)。HP はいつでも 0 にする。
                if ((EActorState)value == EActorState.ActorStateDead)
                {
                    SetAttrKV(uuid, "AttrHp", 0L);
                }
            }
            else if (key == "AttrShieldList")
            {
                var shieldList = (List<ShieldInfo>)value;
                foreach (var shield in shieldList)
                {
                    entity.AddBuffEventAttribute((int)shield.Uuid, "AttrShieldList", shield);
                }
            }
            else if (key == "AttrHp")
            {
                if (!entity.IsHpUpdatedHandlerSubscribed(OnEntityHpUpdated))
                {
                    UpdateEncounterBossData(entity, -1);
                    entity.HpUpdated += OnEntityHpUpdated;
                }
                entity.SetHpValues((long)value, -1);
            }
            else if (key == "AttrMaxHp")
            {
                entity.SetHpValues(-1, (long)value);
            }
            else if (key == "AttrHateList")
            {
                if(!entity.IsThreatListUpdatedHandlerSubscribed(OnEntityThreatListUpdated))
                {
                    entity.ThreatListUpdated += OnEntityThreatListUpdated;
                }

                var hateInfoList = (List<HateInfo>)value;
                var threatInfoList = new List<ThreatInfo>();
                foreach (var hateInfo in hateInfoList)
                {
                    ThreatInfo threatInfo = new() { EntityUuid = hateInfo.Uuid, ThreatValue = hateInfo.HateVal };
                    threatInfoList.Add(threatInfo);
                }

                entity.SetThreatList(threatInfoList);
            }
            else if (key == "AttrTopSummonerId")
            {
                if (entity.EntityType != EEntityType.EntChar)
                {
                    var summoner = GetOrCreateEntity((long)value);
                    entity.SummonerEntityType = summoner.EntityType;
                }
            }

            OnAttributeUpdated(this, new AttributeUpdatedEventArgs() { EntityUuid = uuid, Entity = entity, AttributeName = key, AttributeValue = value });
        }

        public void AddSceneEvent(Zproto.EventData sceneEvent, ExtraPacketData extraPacketData)
        {
            if (sceneEvent.EventType == (int)WorldEventType.BossDbm)
            {

                int skillId = 0;
                int duration = 0;
                int insertion = 0;
                if (sceneEvent.IntParams != null)
                {
                    for (int i = 0; i < sceneEvent.IntParams.Count; i++)
                    {
                        switch (i)
                        {
                            case 0:
                                skillId = sceneEvent.IntParams[i];
                                break;
                            case 1:
                                duration = sceneEvent.IntParams[i];
                                break;
                            case 2:
                                insertion = sceneEvent.IntParams[i];
                                break;
                            default:
                                Serilog.Log.Debug($"Unexpected item({i}) in SceneEvent[BossDbm] IntParams = {sceneEvent.IntParams[i]}");
                                break;
                        }
                    }
                }

                long timestamp = 0;
                if (sceneEvent.LongParams != null)
                {
                    for (int i = 0; i < sceneEvent.LongParams.Count; ++i)
                    {
                        switch (i)
                        {
                            case 0:
                                timestamp = sceneEvent.LongParams[i];
                                break;
                            default:
                                Serilog.Log.Debug($"Unexpected item({i}) in SceneEvent[BossDbm] LongParams = {sceneEvent.LongParams[i]}");
                                break;
                        }
                    }
                }

                // ダンジョンが終わった後の予告は、バーにもタイムラインにも入れない。
                if (!BattleStateMachine.IsDungeonFinished())
                {
                    RecordBossDbmBar(sceneEvent.IntParams?.Count ?? 0, skillId, duration, insertion, extraPacketData);
                    AddSkillAnnouncement(skillId, extraPacketData);
                }
                OnSceneEvent(this, new SceneEventBossDbmEventArgs() { EventType = WorldEventType.BossDbm, SkillId = skillId, Duration = duration, Insertion = insertion, Timestamp = timestamp });
            }
            else if (sceneEvent.EventType == (int)WorldEventType.NoticeTip)
            {

                string messageId = "";
                string extraId = "";
                if (sceneEvent.StrParams != null)
                {
                    for (int i = 0; i < sceneEvent.StrParams.Count; i++)
                    {
                        switch (i)
                        {
                            case 0:
                                messageId = sceneEvent.StrParams[i];
                                break;
                            case 1:
                                extraId = sceneEvent.StrParams[i];
                                break;
                            default:
                                Serilog.Log.Debug($"Unexpected item({i}) in SceneEvent[NoticeTip] StrParams = {sceneEvent.StrParams[i]}");
                                break;
                        }
                    }
                }

                OnSceneEvent(this, new SceneEventNoticeTipEventArgs() { EventType = WorldEventType.NoticeTip, MessageId = messageId, ExtraId = extraId });
            }
        }

        public void UpdateEncounterBossData(Entity entity, int attr_id)
        {
            if (entity.MonsterType == EMonsterType.Boss)
            {
                if (!BossUUIDs.Contains(entity.UUID))
                {
                    BossUUIDs.Add(entity.UUID);
                    if (!entity.IsHpUpdatedHandlerSubscribed(OnBossHpUpdated))
                    {
                        entity.HpUpdated += OnBossHpUpdated;
                    }
                }

                if (BossUUID == 0)
                {

                    BossUUID = entity.UUID;
                    BossName = entity.Name;
                    BossAttrId = (long)attr_id;
                }
            }
        }

        public void SetChannelLineNumber(uint line)
        {
            ExData.ChannelLine = line;
        }

        public void SetDungeonDifficulty(int difficulty)
        {
            ExData.DungeonDifficulty = difficulty;
        }

        public void IncrementDeaths()
        {
            TotalDeaths++;
        }

        /// <summary>
        /// プレイヤーの死亡を1回数える。呼ぶのは差分で <c>AttrDeadTime</c> が新しい値になったとき(<c>MessageManager</c>)。
        /// 計測の回では窓の中の自分の死亡だけ数える(<see cref="IsRecorded"/>)。
        /// </summary>
        public void RecordPlayerDeath(long playerUuid, DateTime arrivalUtc)
        {
            if (!IsRecorded(arrivalUtc, playerUuid))
            {
                return;
            }

            GetOrCreateEntity(playerUuid).IncrementDeaths();
            IncrementDeaths();
        }

        /// <summary>
        /// プレイヤー以外の死亡(状態が「死亡」になった)を1回数える。呼ぶのは <c>MessageManager</c> の属性の処理。
        /// プレイヤーは状態の「死亡」が、死亡の印つきの被弾と同じ差分でいきなり復活(27)になる回に届かず、
        /// 同じ人への再送も届くので、ここでは数えない(<see cref="RecordPlayerDeath"/>)。
        /// </summary>
        public void RecordNonPlayerDeath(long uuid, DateTime arrivalUtc)
        {
            var entity = GetOrCreateEntity(uuid);
            if (entity.EntityType == EEntityType.EntChar || !IsRecorded(arrivalUtc, null))
            {
                return;
            }

            entity.IncrementDeaths();
            if (entity.EntityType == EEntityType.EntMonster)
            {
                IncrementNpcDeaths();
            }
        }

        public void IncrementNpcDeaths()
        {
            TotalNpcDeaths++;
        }

        public void SetWipeState(bool state)
        {
            IsWipe = state;
        }

        public void SetTimedOutState(bool state)
        {
            ExData.IsTimedOut = state;
        }

        public object? GetAttrKV(long uuid, string key)
        {
            return GetOrCreateEntity(uuid).GetAttrKV(key);
        }

        /// <summary>
        /// このエンカウンターに記録すべき戦闘があったか。**プレイヤーのダメージか回復か、タイムライン・被ダメログの記録が1件でもあれば真。**
        ///
        /// <para>
        /// <b>ダメージだけで判定しない</b> — 回復のみの戦闘が落ちる。
        /// <b>タイムライン・被ダメログの記録も数える</b> — 殴られただけの戦闘に加えて、ギミックの即死・詠唱・構えだけの回も残す
        /// (被弾を印にすると、ダメージの無い死亡や予告だけの回が履歴に残らない)。敵の攻撃そのもの(敵同士の戦闘を含む)は数えない。
        /// 見るのは <c>TotalDamage</c> / <c>TotalHealing</c> / <see cref="HasCombatEventLogRecords"/> の3つ。
        /// </para>
        /// </summary>
        public bool HasStatsBeenRecorded()
        {
            return TotalDamage > 0 || TotalHealing > 0 || HasCombatEventLogRecords
                || ExData.DefeatedEnemies.Count > 0 || ExData.HateObservations.Any(x=>x.Change>0);
        }

        /// <summary>
        /// 技の発動を1回受ける(<c>AttrSkillId</c> に技が入った)。CD の推定へ流し(<see cref="SkillActivated"/>)、特化を判定し、
        /// 記録してよければ発動の数を数える。数えるのは起点の後だけで、計測の回では窓の中の自分の発動(プレイヤー以外は窓の中)だけ。
        /// </summary>
        /// <param name="activationUtc">発動が届いたメッセージの到着時刻。CD の推定の時計もこれ。</param>
        public void RegisterSkillActivation(long uuid, int skillId, DateTime activationUtc)
        {
            var entity = GetOrCreateEntity(uuid);
            OnSkillActivated(new SkillActivatedEventArgs { CasterUuid = uuid, SkillId = skillId, ActivationDateTime = activationUtc });

            var playerUuid = (EEntityType)Utils.UuidToEntityType(uuid) == EEntityType.EntChar ? uuid : (long?)null;
            entity.RegisterSkillActivation(
                skillId,
                activationUtc,
                IsAfterCombatClockStart(activationUtc) && IsRecorded(activationUtc, playerUuid));
        }

        /// <summary>
        /// 畳めずバフIDのまま記録した行に印を付ける。
        /// 記録(<c>AddDamage</c> 等)の後に呼ぶこと。行が無ければ何もしない。
        /// </summary>
        public void MarkBuffSourcedSkill(long entityUuid, long skillId)
        {
            if (entityUuid == 0 || skillId == 0)
            {
                return;
            }

            if (Entities.TryGetValue(entityUuid, out var entity)
                && entity.SkillMetrics.TryGetValue(skillId, out var container))
            {
                container.IsBuffSource = true;
            }
        }

        /// <summary>
        /// 着地先より前に名前が決まらない行(見出し表にも料理の表にも名前が無い行)の出どころ(着地先)を決めて控える。
        /// 技に着いたもの・着地先が食い違ったものは常設の検知ログ(<c>BlankSourceNameProbe</c>)へすぐ送る。オプションに着いたものは送らない。
        /// 着かなかったものは送らずにこのエンカウンターに控え、後の記録で着けば控えを消す。後で着けば DB に空欄は残らないので、
        /// 残ったものだけを保存の直後に <see cref="ReportUnresolvedBlankSources"/> が送る。
        /// 記録(<c>AddDamage</c> 等)と <see cref="MarkBuffSourcedSkill"/> の後に、プレイヤーの記録にだけ呼ぶこと。
        ///
        /// <para>
        /// 対象はスキル詳細ウィジェットに行が出る条件(その種別の累計が 0 より大きい)の行だけ。
        /// 名前の有無は表示と同じ <see cref="CombatDataCatalog.GetSourceNameBeforeLanding"/>(注記を付ける前の名前)で見る。
        /// 表示名は空欄でも内部ID注記が付いて空文字にならず、注記は表示設定で消えるので、表示名で見ると設定次第で結果が変わる。
        /// </para>
        ///
        /// <para>
        /// 着地先は最初に着いた先で決め、上書きしない。
        /// </para>
        /// </summary>
        public void ResolveSourceLanding(
            long entityUuid,
            long skillId,
            bool isHealing,
            EDamageSource damageSource,
            int ownerId,
            long attackerRawUuid,
            DateTime arrivalTime)
        {
            if (!Entities.TryGetValue(entityUuid, out var entity)
                || !entity.SkillMetrics.TryGetValue(skillId, out var container))
            {
                return;
            }

            var stats = isHealing ? container.Healing : container.Damage;
            if (stats.ValueTotal == 0UL
                || !string.IsNullOrEmpty(CombatDataCatalog.GetSourceNameBeforeLanding(skillId, container.IsBuffSource)))
            {
                return;
            }

            var landing = Services.SourceLandingResolver.Instance.Resolve(
                damageSource, ownerId, attackerRawUuid, entityUuid, arrivalTime, out var trace);
            var keyText = CombatDataCatalog.FormatSourceKey(skillId);

            if (container.LandingKind != SourceLandingKind.None)
            {
                if (landing.Kind != SourceLandingKind.None && landing != container.Landing)
                {
                    Diagnostics.BlankSourceNameProbe.CaptureConflict(
                        skillId, keyText, isHealing, entity.UID, container.Landing, landing, trace);
                }

                return;
            }

            if (landing.Kind == SourceLandingKind.None)
            {
                lock (_unresolvedBlankSourcesGate)
                {
                    _unresolvedBlankSources.TryAdd(
                        (entityUuid, skillId),
                        new UnresolvedBlankSource(
                            keyText,
                            container.IsBuffSource,
                            isHealing,
                            stats.ValueTotal,
                            stats.HitsCount,
                            entity.UID,
                            trace,
                            arrivalTime));
                }

                return;
            }

            container.LandingKind = landing.Kind;
            container.LandingId = landing.Id;
            lock (_unresolvedBlankSourcesGate)
            {
                _unresolvedBlankSources.Remove((entityUuid, skillId));
            }

            if (landing.Kind != SourceLandingKind.RogueEntry)
            {
                Diagnostics.BlankSourceNameProbe.Capture(
                    skillId,
                    keyText,
                    container.IsBuffSource,
                    isHealing,
                    stats.ValueTotal,
                    stats.HitsCount,
                    entity.UID,
                    landing,
                    trace);
            }
        }

        /// <summary>
        /// 着地先が決まらないまま残った行(<see cref="ResolveSourceLanding"/> の控え)を常設の検知ログ(<c>BlankSourceNameProbe</c>)へ送り、控えを空にする。
        /// <b>DB へ保存した直後に呼ぶ。</b> 保存しないエンカウンターでは呼ばない(DB に空欄が残らない)。
        /// </summary>
        public void ReportUnresolvedBlankSources()
        {
            List<(long RowKey, UnresolvedBlankSource Blank)> unresolved;
            lock (_unresolvedBlankSourcesGate)
            {
                unresolved = _unresolvedBlankSources.Select(pair => (pair.Key.RowKey, pair.Value)).ToList();
                _unresolvedBlankSources.Clear();
            }

            foreach (var (rowKey, blank) in unresolved)
            {
                Diagnostics.BlankSourceNameProbe.Capture(
                    rowKey,
                    blank.KeyText,
                    blank.IsBuffSource,
                    blank.IsHealing,
                    blank.ValueTotal,
                    blank.HitsCount,
                    blank.CharacterId,
                    SourceLanding.None,
                    blank.Trace,
                    blank.FirstBlankAt);
            }
        }

        /// <param name="identitySkillId">
        /// 特化判定に使う<b>ゲームが実際に発動したスキルID</b>。判定できないときは 0。
        ///
        /// <para>
        /// <b><paramref name="skillId"/> を渡してはいけない。</b>あちらは表示・集計用に
        /// 弾・バフ・コンボ段を1つへ畳んだIDで、置換後スキル表が想定していない入力まで当たる。
        /// 味方に発生させるスキル(安可など)が畳まれて表に当たると、その人の特化と職業を書き換える。
        /// </para>
        /// </param>
        public void AddDamage(
            long attackerUuid, long targetUuid, long skillId, int identitySkillId, int skillLevel, long damage, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, ExtraPacketData extraPacketData)
            => AddDamage(attackerUuid,targetUuid,skillId,identitySkillId,skillLevel,damage,hpLessen,shieldBreak,damageElement,damageType,damageMode,
                isCrit,isLucky,isCauseLucky,isMiss,isDead,extraPacketData,false,false,identitySkillId);

        public void AddDamage(
            long attackerUuid, long targetUuid, long skillId, int identitySkillId, int skillLevel, long damage, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, ExtraPacketData extraPacketData,
            bool isBuffSource, bool isEffectDamage, int originSkillId)
        {
            var attacker = GetOrCreateEntity(attackerUuid);

            // ダメージは RegisterSkillActivation を通らない別経路なので、ここでも引く。
            // 詠唱の属性を取りこぼした場合の受け皿。同じ特化なら中で何もしない。判定は止めないので、記録の門より前に置く。
            // 渡すのは畳む前の生のスキルID。理由は identitySkillId の説明を見ること。
            attacker.UpdateSubProfessionFromReplacedSkill(identitySkillId);

            if (!IsRecorded(extraPacketData.ArrivalTime, attackerUuid)
                || !IsBenchmarkFirstTarget(targetUuid))
            {
                return;
            }

            // プレイヤーからプレイヤー以外への与ダメは、戦闘の出来事の判定(MessageManager)が同じ差分で先に時計を立てている。
            // 無いなら判定と振り分けの条件が食い違っている。
            var combatOffset = ToCombatOffset(extraPacketData.ArrivalTime)
                ?? throw new InvalidOperationException(
                    $"Damage recorded before the combat clock started (encounter={EncounterId}, attacker={attackerUuid}, target={targetUuid}).");

            // 戦闘は「次のイベント」なので履歴表示を解除する。開いていなければ即戻る。
            EncounterHistoryProvider.NotifyLiveEncounterEvent();

            LastUpdate = extraPacketData.ArrivalTime;

            if (damageType != EDamageType.Immune)
            {
                TotalDamage += (ulong)damage;
                if (damageType == EDamageType.Absorbed)
                {
                    TotalShieldBreak += (ulong)shieldBreak;
                }
            }

            attacker.RecordCombatAction(extraPacketData.ArrivalTime, ToCombatOffset);
            attacker.AddDamage(skillId, skillLevel, damage, hpLessen, shieldBreak, damageElement, damageType, damageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead);

            if (damage > 0 && damageType != EDamageType.Immune)
            {
                attacker.DamageStats.AddPerSecondValue(combatOffset, extraPacketData.ArrivalTime, damage);
                if (!isMiss) HateCauseRecorder.Record(this, new() { PlayerUuid = attackerUuid, TargetUuid = targetUuid,
                    Timestamp = extraPacketData.ArrivalTime, SourceKey = skillId, IsBuffSource = isBuffSource,
                    Kind = isEffectDamage ? HateCauseKind.EffectDamage : HateCauseKind.Hit, Value = damage, OriginSkillId = originSkillId });
            }
        }

        /// <param name="damage">
        /// 足す回復量。回復の通知なら実際に増えた HP(<c>ActualValue</c>、ゲーム内メーターと同じ)、通知の無い回復(薬・料理・自然回復)なら HP の増え。
        /// </param>
        /// <param name="nominalHealing">
        /// 名目の回復量(通知の <c>Value</c>、0 なら <c>LuckyValue</c>)。<paramref name="damage"/> との差を過剰回復として数える。
        /// 通知の無い回復は <paramref name="damage"/> と同じ値を渡す(過剰回復は 0)。
        /// </param>
        public void AddHealing(
            long attackerUuid, long targetUuid, long skillId, int skillLevel, long damage, long nominalHealing, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, ExtraPacketData extraPacketData)
            => AddHealing(attackerUuid,targetUuid,skillId,skillLevel,damage,nominalHealing,hpLessen,shieldBreak,damageElement,damageType,damageMode,
                isCrit,isLucky,isCauseLucky,isMiss,isDead,extraPacketData,false,0);

        public void AddHealing(
            long attackerUuid, long targetUuid, long skillId, int skillLevel, long damage, long nominalHealing, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, ExtraPacketData extraPacketData,
            bool isBuffSource, int originSkillId)
        {
            // 実際に HP が増えた回復は戦闘の出来事で、MessageManager が記録の前に当てている(最初なら起点を立てる。回復だけの戦闘でも HPS メーターが動く)。
            // 起点より前の回復は、何も記録しない(過剰回復・行動時刻・履歴表示の解除も)。計測の回は窓の中の自分の回復だけ。
            // 渡されるのはプレイヤー(とその召喚)の回復だけ。敵の回復は回の合計に入れず、タイムラインにだけ残す(AddEnemyHeal)。
            if (!IsAfterCombatClockStart(extraPacketData.ArrivalTime)
                || !IsRecorded(extraPacketData.ArrivalTime, attackerUuid))
            {
                return;
            }

            var combatOffset = ToCombatOffset(extraPacketData.ArrivalTime)!.Value;

            // 戦闘は「次のイベント」なので履歴表示を解除する。開いていなければ即戻る。
            EncounterHistoryProvider.NotifyLiveEncounterEvent();

            LastUpdate = extraPacketData.ArrivalTime;

            var overhealing = Math.Max(0L, nominalHealing - damage);
            TotalOverhealing += (ulong)overhealing;

            var entity = GetOrCreateEntity(attackerUuid);
            GetOrCreateEntity(targetUuid);

            // 実際に増えた HP が 0 の回復(満タンの人への回復)は、ゲーム内メーターと同じく回復として数えない。
            // 名目の全額を過剰回復に足すだけで、回数・会心・幸運・秒ごとの値・行動時間には入れない。
            if (damage <= 0)
            {
                entity.TotalOverhealing += (ulong)overhealing;
                return;
            }

            TotalHealing += (ulong)damage;

            entity.RecordCombatAction(extraPacketData.ArrivalTime, ToCombatOffset);
            entity.AddHealing(skillId, skillLevel, damage, overhealing, damage, hpLessen, shieldBreak, damageElement, damageType, damageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead);

            if (damage > 0)
            {
                entity.HealingStats.AddPerSecondValue(combatOffset, extraPacketData.ArrivalTime, damage);
                if (!isMiss) HateCauseRecorder.Record(this, new() { PlayerUuid = attackerUuid, TargetUuid = targetUuid,
                    Timestamp = extraPacketData.ArrivalTime, SourceKey = skillId, IsBuffSource = isBuffSource,
                    Kind = HateCauseKind.Healing, Value = damage, OriginSkillId = originSkillId });
            }
        }

        /// <param name="ownerId">
        /// <c>SyncDamageInfo.OwnerId</c> の生の値。<paramref name="skillId"/> はメーター用に畳んだ鍵なので、
        /// タイムライン・被ダメログの技名はこちらと <paramref name="damageSource"/> で引く。
        /// </param>
        /// <param name="buffSourceSkillId">
        /// バフ由来のとき、そのバフを付けた技(<see cref="Services.BuffInstanceIndex.TryResolveSourceSkill"/>)。決まらなければ 0。
        /// </param>
        /// <param name="summonSourceSkillId">
        /// ダメージを出した実体(仮想体など)を出した技(<see cref="Services.SummonSourceIndex"/>)。決まらなければ 0。
        /// </param>
        public void AddTakenDamage(
            long attackerUuid, long targetUuid, long skillId, int ownerId, EDamageSource damageSource, int buffSourceSkillId, int summonSourceSkillId, int skillLevel, long damage, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead, ExtraPacketData extraPacketData)
        {
            var arrivalUtc = extraPacketData.ArrivalTime;
            var isTargetPlayer = (EEntityType)Utils.UuidToEntityType(targetUuid) == EEntityType.EntChar;
            if (!IsRecorded(arrivalUtc, isTargetPlayer ? targetUuid : null))
            {
                return;
            }

            // 被ダメログに載る被弾は、加害者を問わず(自傷・フレンドリーファイア・落下も)戦闘の出来事にする。行動時刻より先に当てる。
            var isLogged = IsDamageTakenLogged(targetUuid, damage);
            if (isLogged)
            {
                RecordCombatEventForLog(arrivalUtc);
            }

            // 被弾も「次のイベント」。自傷(加害者の無いバフ・落下・自分の技が自分に当たった被弾)は AddDamage を通らないので、ここで解除する。
            EncounterHistoryProvider.NotifyLiveEncounterEvent();

            LastUpdate = arrivalUtc;

            var targetEntity = GetOrCreateEntity(targetUuid);

            // 行動時刻は被ダメログに載らない被弾でも対象に効かせる。起点より前は動かさない。
            if (IsAfterCombatClockStart(arrivalUtc))
            {
                targetEntity.RecordCombatAction(arrivalUtc, ToCombatOffset);
            }

            if (!isLogged)
            {
                return;
            }

            // 加害者の実体を作っておく。被ダメログの加害者名はこの実体(AttrId)から引くので、
            // エンカウンターを作り直した直後に殴ってきた相手でも名前が引けるようにする。
            if (attackerUuid != 0)
            {
                GetOrCreateEntity(attackerUuid).IsNamedInCombatEventLog = true;
            }

            targetEntity.AddTakenDamage(skillId, skillLevel, damage, hpLessen, shieldBreak, damageElement, damageType, damageMode, isCrit, isLucky, isCauseLucky, isMiss, isDead);

            var hit = new TakenHitRecord(
                targetUuid,
                attackerUuid,
                skillId,
                damage,
                arrivalUtc,
                NextCombatEventLogSequence(),
                ownerId,
                buffSourceSkillId,
                summonSourceSkillId,
                damageElement,
                damageMode,
                damageSource,
                isDead);
            lock (_combatEventLogGate)
            {
                _takenHitLog.Add(hit);
            }
        }

        /// <summary>
        /// 被ダメログに載る被弾か。対象がプレイヤーで、値が 0 でないもの。加害者は問わない
        /// (自傷・落下・フレンドリーファイアも載せる)。値 0 は HP もバリアも減っていない被弾。
        /// </summary>
        internal static bool IsDamageTakenLogged(long targetUuid, long damage)
        {
            return damage != 0
                && (EEntityType)Utils.UuidToEntityType(targetUuid) == EEntityType.EntChar;
        }

        /// <summary>
        /// タイムライン・被ダメログに載せる加害者・詠唱者を決める。召喚体は大元の召喚者へ寄せるが、
        /// <b>プレイヤーから見えて表に名前がある召喚体は、その召喚体自身</b>にする。
        ///
        /// <para>
        /// 見えるかは実体の種類で決める。モンスター(<c>MonsterTable</c>)は全行が見た目のモデルを持ち、
        /// 仮想体(<c>DummyTable</c>)はモデルを指す項目を持たない。弾なども見える実体ではない。
        /// </para>
        ///
        /// <para>
        /// 大元の召喚者がプレイヤーなら、召喚体の名前に関係なくプレイヤーを返す(フレンドリーファイアは打ったプレイヤーとして出す)。
        /// </para>
        /// </summary>
        internal long ResolveCombatEventLogActor(long rawUuid, long topSummonerUuid)
        {
            if (topSummonerUuid == 0 || topSummonerUuid == rawUuid)
            {
                return rawUuid;
            }

            if ((EEntityType)Utils.UuidToEntityType(topSummonerUuid) == EEntityType.EntChar
                || (EEntityType)Utils.UuidToEntityType(rawUuid) != EEntityType.EntMonster)
            {
                return topSummonerUuid;
            }

            int monsterId;
            if (Entities.TryGetValue(rawUuid, out var summoned) && summoned.GetAttrKV("AttrId") is int attrId && attrId > 0)
            {
                monsterId = attrId;
            }
            else if (!Services.NearbyMonsterIndex.Instance.TryGetMonsterId(rawUuid, out monsterId)
                && !Services.EntityOriginIndex.Instance.TryGetDisappearedMonsterId(rawUuid, out monsterId))
            {
                Serilog.Log.Warning("Summon has no AttrId uuid={SummonUuid}. Cannot tell whether it has a name, so folding it into the root summoner {TopSummonerUuid}",
                    rawUuid, topSummonerUuid);
                return topSummonerUuid;
            }

            return CombatDataCatalog.HasMonsterName(monsterId) ? rawUuid : topSummonerUuid;
        }

        /// <summary>
        /// 技の開始をタイムラインの詠唱として残す。<b>詠唱バーを持つ技</b>と<b>戦闘画面の警告の技</b>だけで、
        /// プレイヤーとプレイヤーの召喚物は対象外。召喚物の扱いは <see cref="ResolveCombatEventLogActor"/>。
        /// </summary>
        /// <param name="skillLevel">技の開始と同じ差分で届く <c>AttrSkillLevel</c>。届いていなければ 0。</param>
        public void AddSkillCast(long entityUuid, int skillId, int skillLevel, ExtraPacketData extraPacketData)
        {
            if (skillId <= 0)
            {
                return;
            }

            if (!CombatDataCatalog.HasSingOrGuideTime(skillId))
            {
                // 警告の技かは技レベルで決まるので、レベルが無ければ判定できない。
                if (skillLevel <= 0)
                {
                    Serilog.Log.Warning("Skill start without AttrSkillLevel uuid={EntityUuid} skillId={SkillId}. Cannot tell whether it is a warned skill, so not recording it",
                        entityUuid, skillId);
                    return;
                }

                if (!CombatDataCatalog.IsWarningSkill(skillId, skillLevel))
                {
                    return;
                }
            }

            var topSummonerUuid = GetOrCreateEntity(entityUuid).GetAttrKV("AttrTopSummonerId") is long summonerUuid
                ? summonerUuid
                : 0;
            var casterUuid = ResolveCombatEventLogActor(entityUuid, topSummonerUuid);
            if ((EEntityType)Utils.UuidToEntityType(casterUuid) == EEntityType.EntChar)
            {
                return;
            }

            var isWarningSkill = CombatDataCatalog.IsWarningSkillId(skillId);

            // ダンジョンが終わった後の警告の技は、通知にもタイムラインにも入れない。
            if (isWarningSkill && BattleStateMachine.IsDungeonFinished())
            {
                return;
            }

            // 警告の通知は記録とは別に流す(計測の窓の外でも通知は止めない)。詠唱者の名前はこの回の実体から決める。
            if (isWarningSkill)
            {
                GetOrCreateEntity(casterUuid);
                Services.WarningSkillCastStore.Instance.Add(
                    MeterSnapshotProvider.CreateCombatEventLogParty(this, casterUuid),
                    skillId,
                    extraPacketData.ArrivalTime);
            }

            if (!IsRecorded(extraPacketData.ArrivalTime, null))
            {
                return;
            }

            RecordCombatEventForLog(extraPacketData.ArrivalTime);

            // 詠唱も「次のイベント」。AddDamage を通らないので、ここで解除する。
            EncounterHistoryProvider.NotifyLiveEncounterEvent();

            var cast = new SkillCastRecord
            {
                SkillId = skillId,
                Timestamp = extraPacketData.ArrivalTime,
                Sequence = NextCombatEventLogSequence(),
                IsWarning = skillLevel > 0 && CombatDataCatalog.IsWarningSkill(skillId, skillLevel),
            };
            var caster = GetOrCreateEntity(casterUuid);
            caster.AddSkillCast(cast);
            caster.IsNamedInCombatEventLog = true;

            lock (_combatEventLogGate)
            {
                _combatEventLog.Add(CombatEventLogRecord.ForCast(casterUuid, cast));
            }
        }

        private readonly object _skillActivationGate = new();

        /// <summary>人ごとの技の開始。回のあいだ全部残す。解放は回と一緒。DB の形は保存のときに作り、履歴は最初に読むときに戻す。</summary>
        private readonly Dictionary<long, PlayerSkillActivationLog> _skillActivationsByPlayer = [];

        /// <summary>
        /// プレイヤーの技の開始を残す(推移グラフのアイコン)。自分以外のプレイヤーは差分の技の開始(<c>AttrSkillId</c>)から呼び(出現・入場の全属性は呼ばない)、
        /// 自分は上りの要求 <c>UseSlot</c> の成功の応答から呼ぶ(<c>MessageManager.ProcessUseSlotReturn</c>)。
        /// 門は <see cref="IsRecorded"/> だけで、戦闘の時計の起点より前の開始も残す(最初の一撃の技の開始は最初のダメージより先に届く)。
        /// </summary>
        public void AddSkillActivation(long playerUuid, int skillId, DateTime arrivalUtc)
        {
            if (skillId <= 0 || !IsRecorded(arrivalUtc, playerUuid))
            {
                return;
            }

            lock (_skillActivationGate)
            {
                AddSkillActivationNoLock(playerUuid, skillId, arrivalUtc);
            }
        }

        /// <summary>
        /// <paramref name="playerUuid"/> の技の開始のうち、その人の <paramref name="startIndex"/> 番目から後(届いた順)。無ければ何も作らない。
        /// <paramref name="nextIndex"/> を次に渡せば続きから読める。
        /// </summary>
        public SkillActivation[] GetSkillActivationsFrom(long playerUuid, int startIndex, out int nextIndex)
        {
            lock (_skillActivationGate)
            {
                MoveLoadedSkillActivationsNoLock();
                if (!_skillActivationsByPlayer.TryGetValue(playerUuid, out var log))
                {
                    nextIndex = 0;
                    return [];
                }

                nextIndex = log.Count;
                return log.ReadFrom(startIndex);
            }
        }

        /// <summary>保存の直前に、技の開始を DB の形(全員の分を届いた順)へ写す。書いたら <see cref="ClearSkillActivationsAfterSave"/> で捨てる。</summary>
        internal void FillSkillActivationsForSave()
        {
            lock (_skillActivationGate)
            {
                MoveLoadedSkillActivationsNoLock();
                var records = new List<SkillActivationRecord>();
                foreach (var (playerUuid, log) in _skillActivationsByPlayer)
                {
                    log.AppendRecords(playerUuid, records);
                }

                // 同じ時刻の順を変えない並べ替え(読み戻すと同じ人の順がそのまま残る)。
                ExData.SkillActivations = [.. records.OrderBy(record => record.Timestamp)];
            }
        }

        internal void ClearSkillActivationsAfterSave()
        {
            lock (_skillActivationGate)
            {
                ExData.SkillActivations = [];
            }
        }

        /// <summary>
        /// DB から読んだ回の技の開始を人ごとの記録へ移す。実行中の回は記録があるので移さない
        /// (保存の間だけ <see cref="EncounterExData.SkillActivations"/> が埋まっていても読み手が二重にしない)。
        /// </summary>
        private void MoveLoadedSkillActivationsNoLock()
        {
            if (_skillActivationsByPlayer.Count > 0 || ExData.SkillActivations.Count == 0)
            {
                return;
            }

            foreach (var record in ExData.SkillActivations)
            {
                AddSkillActivationNoLock(record.PlayerUuid, record.SkillId, record.Timestamp);
            }

            ExData.SkillActivations = [];
        }

        private void AddSkillActivationNoLock(long playerUuid, int skillId, DateTime arrivalUtc)
        {
            if (!_skillActivationsByPlayer.TryGetValue(playerUuid, out var log))
            {
                log = new PlayerSkillActivationLog(arrivalUtc.Ticks);
                _skillActivationsByPlayer.Add(playerUuid, log);
            }

            log.Add(skillId, arrivalUtc);
        }

        /// <summary>
        /// ボス大技の予告をタイムラインの予告の行として残す。通知の番号(<c>DbmTable.Id</c>)から技を引き、
        /// 周囲にいるモンスターからその技を持つものを構えた側とする。
        /// </summary>
        public void AddSkillAnnouncement(int dbmId, ExtraPacketData extraPacketData)
        {
            if (dbmId <= 0 || !IsRecorded(extraPacketData.ArrivalTime, null))
            {
                return;
            }

            RecordCombatEventForLog(extraPacketData.ArrivalTime);

            // 予告も「次のイベント」。AddDamage を通らないので、ここで解除する。
            EncounterHistoryProvider.NotifyLiveEncounterEvent();

            var (skillId, ownerMonsterId) = ResolveAnnouncementSource(dbmId);

            var announcement = new SkillAnnouncementRecord
            {
                SkillId = skillId,
                OwnerMonsterId = ownerMonsterId,
                Timestamp = extraPacketData.ArrivalTime,
                Sequence = NextCombatEventLogSequence(),
            };

            lock (_combatEventLogGate)
            {
                ExData.SkillAnnouncements.Add(announcement);
                _combatEventLog.Add(CombatEventLogRecord.ForAnnouncement(announcement));
            }
        }

        /// <summary>
        /// 予告のバーが終わった時刻に、タイムラインへ「主の予告技」の行を残す。技と主はバーに控えた値(予告の行と同じ決め方)。
        /// 呼ぶのは <see cref="EncounterManager.RecordEndedAnnouncementBars"/> だけ。
        /// </summary>
        public void AddSkillAnnouncementBarEnd(Services.BossDbmBarStore.BossDbmBar bar)
        {
            if (!IsRecorded(bar.EndTimeUtc, null))
            {
                return;
            }

            RecordCombatEventForLog(bar.EndTimeUtc);

            // 予告のバーの終わりも「次のイベント」。AddDamage を通らないので、ここで解除する。
            EncounterHistoryProvider.NotifyLiveEncounterEvent();

            var announcement = new SkillAnnouncementRecord
            {
                SkillId = bar.SkillId,
                OwnerMonsterId = bar.OwnerMonsterId,
                Timestamp = bar.EndTimeUtc,
                Sequence = NextCombatEventLogSequence(),
                IsBarEnd = true,
            };

            lock (_combatEventLogGate)
            {
                ExData.SkillAnnouncements.Add(announcement);
                _combatEventLog.Add(CombatEventLogRecord.ForAnnouncement(announcement));
            }
        }

        /// <summary>
        /// 予告の番号から技を引き、周囲にいるモンスターからその技を持つものの種別IDを引く。引けなければどちらも 0。
        /// タイムラインの予告の行と予告のバーの控えが同じ決め方を使う。
        /// </summary>
        private static (int SkillId, int OwnerMonsterId) ResolveAnnouncementSource(int dbmId)
        {
            var ownerMonsterId = 0;
            if (CombatDataCatalog.TryResolveDbmSkillId(dbmId, out var skillId))
            {
                Services.NearbyMonsterIndex.Instance.TryFindMonsterId(
                    monsterId => CombatDataCatalog.MonsterHasSkill(monsterId, skillId),
                    out ownerMonsterId);
            }

            return (skillId, ownerMonsterId);
        }

        /// <summary>
        /// ボス大技の予告のバーを控える(<see cref="Services.BossDbmBarStore"/>)。決め方はゲームと同じ:
        /// 番号が無い通知は何もしない、番号・持続・insertion が全部 0 なら全部消す、
        /// バーの長さは持続の秒数(0 なら予告の表の <c>CountCDTime</c>)、表に無い番号と長さ 0 のバーは作らない。
        /// </summary>
        private static void RecordBossDbmBar(int intParamCount, int dbmId, int duration, int insertion, ExtraPacketData extraPacketData)
        {
            if (intParamCount == 0)
            {
                return;
            }

            if (intParamCount >= 3 && dbmId == 0 && duration == 0 && insertion == 0)
            {
                Services.BossDbmBarStore.Instance.Clear();
                return;
            }

            if (!CombatDataCatalog.TryGetDbmCountCdTime(dbmId, out var countCdTime))
            {
                Serilog.Log.Warning("Boss announcement {DbmId} is not in DbmTable. No announcement bar is timed for it", dbmId);
                return;
            }

            var seconds = duration != 0 ? duration : countCdTime;
            if (seconds <= 0)
            {
                Serilog.Log.Warning("Boss announcement {DbmId} has no duration. No announcement bar is timed for it", dbmId);
                return;
            }

            var (skillId, ownerMonsterId) = ResolveAnnouncementSource(dbmId);
            Services.BossDbmBarStore.Instance.Set(new Services.BossDbmBarStore.BossDbmBar(
                dbmId,
                skillId,
                ownerMonsterId,
                extraPacketData.ArrivalTime.AddSeconds(seconds)));
        }

        /// <summary>
        /// ダメージの無い死亡(死亡の印つきの被弾が無いまま死亡したもの)を記録に残す。
        /// 死亡かどうか・印つきの被弾があったかは <c>MessageManager</c> が差分全体を見て決める。
        /// </summary>
        /// <param name="healthBefore">死亡を伝えた同期を当てる前の HP・最大HP・バリア量。</param>
        public void AddPlayerDeathWithoutDamage(long playerUuid, TargetHealth healthBefore, ExtraPacketData extraPacketData)
        {
            if (!IsRecorded(extraPacketData.ArrivalTime, playerUuid))
            {
                return;
            }

            RecordCombatEventForLog(extraPacketData.ArrivalTime);

            // ダメージの無い死亡も「次のイベント」。AddDamage を通らないので、ここで解除する。
            EncounterHistoryProvider.NotifyLiveEncounterEvent();

            var death = new PlayerDeathRecord
            {
                PlayerUuid = playerUuid,
                MaxHp = GetOrCreateEntity(playerUuid).GetAttrKV("AttrMaxHp") as long?,
                Timestamp = extraPacketData.ArrivalTime,
                Sequence = NextCombatEventLogSequence(),
                HpBefore = healthBefore.Hp,
                MaxHpBefore = healthBefore.MaxHp,
                ShieldBefore = healthBefore.Shield,
            };

            lock (_combatEventLogGate)
            {
                ExData.PlayerDeaths.Add(death);
                _combatEventLog.Add(CombatEventLogRecord.ForDeath(death));
            }
        }

        /// <summary>
        /// この回のタイムラインに、そのプレイヤーが出る記録があるか。ダメージの無い戦闘不能の記録と、
        /// 敵の回復・敵が付けたバフの相手。
        /// </summary>
        public bool HasPlayerTimelineRecord(long playerUuid)
        {
            lock (_combatEventLogGate)
            {
                return ExData.PlayerDeaths.Any(death => death.PlayerUuid == playerUuid)
                    || ExData.EnemyHeals.Any(heal => heal.TargetUuid == playerUuid)
                    || ExData.EnemyBuffs.Any(buff => buff.TargetUuid == playerUuid);
            }
        }

        /// <summary>
        /// 敵の回復をタイムラインに残す。回復した側がプレイヤーでない、実際に HP が増えた回復を <c>MessageManager</c> が渡し、
        /// 回復した側と相手の種類(<see cref="IsEnemyActionRecorded"/>)と発生源の有無(<see cref="HasEnemyActionSource"/>)はここで見る。
        /// </summary>
        /// <param name="healerUuid">回復した側。召喚体は寄せた後(<see cref="ResolveCombatEventLogActor"/>)。</param>
        /// <param name="value">実際に増えた HP。</param>
        public void AddEnemyHeal(
            long healerUuid, long targetUuid, int ownerId, EDamageSource damageSource, int buffSourceSkillId, int summonSourceSkillId,
            long value, ExtraPacketData extraPacketData)
        {
            if (!IsRecorded(extraPacketData.ArrivalTime, null)
                || !IsEnemyActionRecorded(healerUuid, targetUuid)
                || !HasEnemyActionSource(ownerId, summonSourceSkillId))
            {
                return;
            }

            RecordCombatEventForLog(extraPacketData.ArrivalTime);

            // 敵の回復も「次のイベント」。AddHealing を通らないので、ここで解除する。
            EncounterHistoryProvider.NotifyLiveEncounterEvent();

            LastUpdate = extraPacketData.ArrivalTime;

            // 名前は実体(AttrId)から引くので、回を作り直した直後の回復でも引けるように実体を作っておく。
            GetOrCreateEntity(healerUuid).IsNamedInCombatEventLog = true;
            GetOrCreateEntity(targetUuid).IsNamedInCombatEventLog = true;

            var heal = new EnemyHealRecord
            {
                HealerUuid = healerUuid,
                TargetUuid = targetUuid,
                OwnerId = ownerId,
                DamageSource = damageSource,
                BuffSourceSkillId = buffSourceSkillId,
                SummonSourceSkillId = summonSourceSkillId,
                Value = value,
                Timestamp = extraPacketData.ArrivalTime,
                Sequence = NextCombatEventLogSequence(),
                CastSequence = FindLinkedCastSequence(healerUuid, damageSource, ownerId, buffSourceSkillId, summonSourceSkillId),
            };

            lock (_combatEventLogGate)
            {
                ExData.EnemyHeals.Add(heal);
                _combatEventLog.Add(CombatEventLogRecord.ForEnemyHeal(heal));
            }
        }

        /// <summary>
        /// 敵から敵へのダメージをタイムラインに残す。加害者も対象もプレイヤーでない、値が 0 でないダメージを <c>MessageManager</c> が渡し、
        /// 加害者と対象の種類(<see cref="IsEnemyActionRecorded"/>)と発生源の有無(<see cref="HasEnemyActionSource"/>)はここで見る。
        /// </summary>
        /// <param name="attackerUuid">加害者。召喚体は寄せた後(<see cref="ResolveCombatEventLogActor"/>)。</param>
        public void AddEnemyDamage(
            long attackerUuid, long targetUuid, int ownerId, EDamageSource damageSource, int buffSourceSkillId, int summonSourceSkillId,
            long value, ExtraPacketData extraPacketData)
        {
            if (!IsRecorded(extraPacketData.ArrivalTime, null)
                || !IsEnemyActionRecorded(attackerUuid, targetUuid)
                || !HasEnemyActionSource(ownerId, summonSourceSkillId))
            {
                return;
            }

            RecordCombatEventForLog(extraPacketData.ArrivalTime);

            // 敵から敵へのダメージも「次のイベント」。AddDamage を通らないので、ここで解除する。
            EncounterHistoryProvider.NotifyLiveEncounterEvent();

            GetOrCreateEntity(attackerUuid).IsNamedInCombatEventLog = true;
            GetOrCreateEntity(targetUuid).IsNamedInCombatEventLog = true;

            var damage = new EnemyDamageRecord
            {
                AttackerUuid = attackerUuid,
                TargetUuid = targetUuid,
                OwnerId = ownerId,
                DamageSource = damageSource,
                BuffSourceSkillId = buffSourceSkillId,
                SummonSourceSkillId = summonSourceSkillId,
                Value = value,
                Timestamp = extraPacketData.ArrivalTime,
                Sequence = NextCombatEventLogSequence(),
                CastSequence = FindLinkedCastSequence(attackerUuid, damageSource, ownerId, buffSourceSkillId, summonSourceSkillId),
            };

            lock (_combatEventLogGate)
            {
                ExData.EnemyDamages.Add(damage);
                _combatEventLog.Add(CombatEventLogRecord.ForEnemyDamage(damage));
            }
        }

        /// <summary>
        /// 敵が付けたバフをタイムラインに残す。付けた側がプレイヤーでない差分の付与を、付与元を発生源の形に置き換えて <c>MessageManager</c> が渡し、
        /// 付けた側と相手の種類(<see cref="IsEnemyActionRecorded"/>)と発生源の有無(<see cref="HasEnemyActionSource"/>)はここで見る。
        /// </summary>
        /// <param name="casterUuid">付けた敵。召喚体は寄せた後(<see cref="ResolveCombatEventLogActor"/>)。</param>
        public void AddEnemyBuff(
            long casterUuid, long targetUuid, int buffId, int durationMs, int ownerId, EDamageSource damageSource, int buffSourceSkillId,
            int summonSourceSkillId, ExtraPacketData extraPacketData)
        {
            if (!IsRecorded(extraPacketData.ArrivalTime, null)
                || !IsEnemyActionRecorded(casterUuid, targetUuid)
                || !HasEnemyActionSource(ownerId, summonSourceSkillId))
            {
                return;
            }

            RecordCombatEventForLog(extraPacketData.ArrivalTime);

            // 敵が付けたバフも「次のイベント」。AddDamage を通らないので、ここで解除する。
            EncounterHistoryProvider.NotifyLiveEncounterEvent();

            GetOrCreateEntity(casterUuid).IsNamedInCombatEventLog = true;
            GetOrCreateEntity(targetUuid).IsNamedInCombatEventLog = true;

            var buff = new EnemyBuffRecord
            {
                CasterUuid = casterUuid,
                TargetUuid = targetUuid,
                BuffId = buffId,
                DurationMs = durationMs,
                OwnerId = ownerId,
                DamageSource = damageSource,
                BuffSourceSkillId = buffSourceSkillId,
                SummonSourceSkillId = summonSourceSkillId,
                Timestamp = extraPacketData.ArrivalTime,
                Sequence = NextCombatEventLogSequence(),
                CastSequence = FindLinkedCastSequence(casterUuid, damageSource, ownerId, buffSourceSkillId, summonSourceSkillId),
            };

            lock (_combatEventLogGate)
            {
                ExData.EnemyBuffs.Add(buff);
                _combatEventLog.Add(CombatEventLogRecord.ForEnemyBuff(buff));
            }
        }

        /// <summary>
        /// タイムラインに敵の行動(回復・ダメージ・バフ)として残す組か。行動した側はモンスター、相手はプレイヤーかモンスター。
        /// 種類の決まりはエンティティリストと同じ(<see cref="NearbyEntityProjection.IsListedType"/>)で、名前の無いモンスターも残す(「未知の敵」と出す)。
        /// システム(仮想体・種類の分からない実体)・NPC・設置物・領域は残さない。
        /// </summary>
        private static bool IsEnemyActionRecorded(long actorUuid, long targetUuid)
        {
            var targetType = (EEntityType)Utils.UuidToEntityType(targetUuid);
            return NearbyEntityProjection.IsListedType((EEntityType)Utils.UuidToEntityType(actorUuid))
                && (targetType == EEntityType.EntChar || NearbyEntityProjection.IsListedType(targetType));
        }

        /// <summary>
        /// 敵の行動が由来の技を持つか。付与元の番号か、行動した敵を出した技の番号があるとき。
        /// 持たない行動(技を使わずに付いたバフなど)はタイムラインに出さないので、残さない(タイマーも動かさず、DB にも書かない)。
        /// </summary>
        private static bool HasEnemyActionSource(int ownerId, int summonSourceSkillId)
        {
            return ownerId != 0 || summonSourceSkillId > 0;
        }

        /// <summary>
        /// タイムラインと被ダメログに載る記録を戦闘の出来事にする(<see cref="RecordCombatEvent"/>)。メーターの与ダメ・被弾・回復と同じく、
        /// 最初なら時計を始め、その後は自動一時停止の時計を進める。記録の入口が門を通った直後、ほかの処理より前に呼ぶ。
        /// 計測の回は出来事にしない(計測の時計は自分の与ダメと回復だけで動き、記録は窓の中だけ)。
        /// </summary>
        private void RecordCombatEventForLog(DateTime timestampUtc)
        {
            if (!IsBenchmark)
            {
                RecordCombatEvent(timestampUtc);
            }
        }

        /// <summary>
        /// 敵の行動(回復・ダメージ・バフ)を起こした詠唱。行動の発生源の技(技そのもの・弾の親の技・バフを付けた技)か、行動した実体を出した技と
        /// 同じ技を、同じ敵が唱えた一番新しい詠唱(<see cref="AddSkillCast"/> が残したもの)の通し番号。無ければ 0。時間では区切らない。
        /// </summary>
        private long FindLinkedCastSequence(long casterUuid, EDamageSource damageSource, int ownerId, int buffSourceSkillId, int summonSourceSkillId)
        {
            var sourceSkillId = damageSource switch
            {
                EDamageSource.Skill => ownerId,
                EDamageSource.Bullet or EDamageSource.FakeBullet =>
                    CombatDataCatalog.TryResolveBulletParentSkillId(ownerId, out var parentSkillId) ? parentSkillId : 0,
                EDamageSource.Buff => buffSourceSkillId,
                _ => 0
            };

            if ((sourceSkillId <= 0 && summonSourceSkillId <= 0) || !Entities.TryGetValue(casterUuid, out var caster))
            {
                return 0;
            }

            return caster.FindLatestSkillCastSequence(sourceSkillId, summonSourceSkillId);
        }

        /// <summary>敵の行動(回復・ダメージ・バフ)に出てくる実体(行動した側と相手)。DB に保存する実体を選ぶのに使う。</summary>
        public HashSet<long> GetEnemyActionEntityUuids()
        {
            lock (_combatEventLogGate)
            {
                var uuids = new HashSet<long>();
                foreach (var heal in ExData.EnemyHeals)
                {
                    uuids.Add(heal.HealerUuid);
                    uuids.Add(heal.TargetUuid);
                }

                foreach (var damage in ExData.EnemyDamages)
                {
                    uuids.Add(damage.AttackerUuid);
                    uuids.Add(damage.TargetUuid);
                }

                foreach (var buff in ExData.EnemyBuffs)
                {
                    uuids.Add(buff.CasterUuid);
                    uuids.Add(buff.TargetUuid);
                }

                return uuids;
            }
        }

        private long NextCombatEventLogSequence()
        {
            return Interlocked.Increment(ref _combatEventLogSequence);
        }

        /// <summary>タイムラインの記録の <paramref name="startIndex"/> 番目以降を返す。ライブ中は差分の追記に使う。</summary>
        public CombatEventLogRecord[] GetCombatEventLogRecords(int startIndex)
        {
            lock (_combatEventLogGate)
            {
                EnsureCombatEventLogRebuiltNoLock();

                if (startIndex >= _combatEventLog.Count)
                {
                    return [];
                }

                return _combatEventLog.GetRange(startIndex, _combatEventLog.Count - startIndex).ToArray();
            }
        }

        /// <summary>プレイヤーの被弾の <paramref name="startIndex"/> 番目以降を返す。ライブ中は差分の追記に使う。</summary>
        public TakenHitRecord[] GetTakenHitRecords(int startIndex)
        {
            lock (_combatEventLogGate)
            {
                EnsureCombatEventLogRebuiltNoLock();
                return _takenHitLog.ToArray(startIndex);
            }
        }

        /// <summary>
        /// 保存の直前に、被弾を実体ごとの DB の形(<see cref="CombatStats.SkillSnapshots"/>)へ写す。書いたら <see cref="ClearTakenSnapshotsAfterSave"/> で捨てる。
        /// 実行中の回の被弾は記録にあるので、この間に読まれても組み直さない(<see cref="EnsureCombatEventLogRebuiltNoLock"/>)。
        /// </summary>
        internal void FillTakenSnapshotsForSave()
        {
            TakenHitRecord[] hits;
            lock (_combatEventLogGate)
            {
                hits = _takenHitLog.ToArray(0);
            }

            var missingTargets = 0;
            foreach (var hit in hits)
            {
                if (!Entities.TryGetValue(hit.TargetUuid, out var target))
                {
                    missingTargets++;
                    continue;
                }

                target.TakenStats.AddSkillSnapshotForSave(new SkillSnapshot
                {
                    OtherUUID = hit.OtherUuid,
                    Id = hit.Id,
                    Value = hit.Value,
                    DamageElement = hit.DamageElement,
                    DamageMode = hit.DamageMode,
                    Timestamp = hit.Timestamp,
                    Sequence = hit.Sequence,
                    OwnerId = hit.OwnerId,
                    DamageSource = hit.DamageSource,
                    BuffSourceSkillId = hit.BuffSourceSkillId,
                    SummonSourceSkillId = hit.SummonSourceSkillId,
                    IsKill = hit.IsKill,
                });
            }

            if (missingTargets > 0)
            {
                Serilog.Log.Error("{Count} taken hits have no target entity and were not saved (encounter={EncounterId})", missingTargets, EncounterId);
            }
        }

        internal void ClearTakenSnapshotsAfterSave()
        {
            foreach (var entity in Entities.Values)
            {
                entity.TakenStats.TakeSkillSnapshots();
            }
        }

        /// <summary>
        /// <b>DB から読んだエンカウンターは並びを持たない</b>ので、実行中のエンカウンター以外で空なら
        /// 1回だけ、予告とダメージの無い死亡と敵の回復・ダメージ・バフと全エンティティの詠唱とプレイヤーの被弾を、それぞれ通し番号順に並べ直す。
        /// </summary>
        private void EnsureCombatEventLogRebuiltNoLock()
        {
            if (_combatEventLog.Count == 0
                && _takenHitLog.Count == 0
                && !_combatEventLogRebuiltFromSnapshots
                && !ReferenceEquals(this, EncounterManager.Current))
            {
                _combatEventLogRebuiltFromSnapshots = true;
                RebuildCombatEventLogFromSnapshots();
            }
        }

        private void RebuildCombatEventLogFromSnapshots()
        {
            var records = new List<CombatEventLogRecord>();
            foreach (var announcement in ExData.SkillAnnouncements)
            {
                records.Add(CombatEventLogRecord.ForAnnouncement(announcement));
            }

            foreach (var death in ExData.PlayerDeaths)
            {
                records.Add(CombatEventLogRecord.ForDeath(death));
            }

            foreach (var heal in ExData.EnemyHeals)
            {
                records.Add(CombatEventLogRecord.ForEnemyHeal(heal));
            }

            foreach (var damage in ExData.EnemyDamages)
            {
                records.Add(CombatEventLogRecord.ForEnemyDamage(damage));
            }

            foreach (var buff in ExData.EnemyBuffs)
            {
                records.Add(CombatEventLogRecord.ForEnemyBuff(buff));
            }

            var hits = new List<TakenHitRecord>();
            foreach (var (uuid, entity) in Entities)
            {
                foreach (var cast in entity.GetSkillCastsCopy())
                {
                    records.Add(CombatEventLogRecord.ForCast(uuid, cast));
                }

                if ((EEntityType)Utils.UuidToEntityType(uuid) != EEntityType.EntChar
                    || entity.TakenStats.TakeSkillSnapshots() is not { } snapshots)
                {
                    continue;
                }

                foreach (var snapshot in snapshots)
                {
                    // 通し番号が 0 の古い記録は並べる順が決まらず、時刻の無い古い記録は行にできない。
                    if (snapshot.Sequence == 0 || snapshot.Timestamp is not { } timestamp)
                    {
                        continue;
                    }

                    hits.Add(new TakenHitRecord(
                        uuid,
                        snapshot.OtherUUID,
                        snapshot.Id,
                        snapshot.Value,
                        timestamp,
                        snapshot.Sequence,
                        snapshot.OwnerId,
                        snapshot.BuffSourceSkillId,
                        snapshot.SummonSourceSkillId,
                        snapshot.DamageElement,
                        snapshot.DamageMode,
                        snapshot.DamageSource,
                        snapshot.IsKill));
                }
            }

            records.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
            _combatEventLog.AddRange(records);

            hits.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
            foreach (var hit in hits)
            {
                _takenHitLog.Add(hit);
            }
        }

        public void AddShieldGained(long entityUuid, long shieldBuffUuid, long value, long initialValue, long maxValue = 0)
        {

        }

        /// <param name="carriesBuffInfo">
        /// このイベントが <c>BuffInfo</c>(または <c>BuffChange</c>)を伴っていたか。
        /// <b><c>false</c> なら <c>baseId</c> 以下の値は「サーバが送ってこなかった」ことを表すゼロ</b>で、
        /// バフの実際の状態ではない。<c>LogicEffect</c> を持たない回は珍しくないので、
        /// ここで持続や層を書き込むと時限バフが持続0＝無期限に化けて消えなくなる。
        /// </param>
        /// <param name="payload">通知が運んだ中身。ライブ表示の項目を新しく作れるのは <see cref="BuffEventPayload.BuffInfo"/> だけ。</param>
        /// <param name="fightSourceType">付与元の種類。<c>null</c> は「この通知は運んでいない」で、記録済みの値を上書きしない。</param>
        public void NotifyBuffEvent(long entityUuid, EBuffEventType buffEventType, int buffUuid, int baseId, int level, long fireUuid, int layer, int duration, int sourceConfigId, DateTime? creationTime, ExtraPacketData extraPacketData, BuffEventPayload payload, int? fightSourceType)
        {
            string entityCasterName = "";
            if (fireUuid > 0)
            {
                var caster = GetOrCreateEntity(fireUuid);
                if (!string.IsNullOrEmpty(caster.Name))
                {
                    entityCasterName = caster.Name;
                }
            }

            OnBuffUpdated(this, new BuffUpdatedEventArgs()
            {
                EntityUuid = entityUuid,
                BuffEventType = buffEventType,
                BuffUuid = buffUuid,
                BaseId = baseId,
                Level = level,
                FireUuid = fireUuid,
                Layer = layer,
                Duration = duration,
                SourceConfigId = sourceConfigId,
                EntityCasterName = entityCasterName,
                UpdateDateTime = extraPacketData.ArrivalTime,
                CreationDateTime = creationTime,
                Payload = payload,
            });
            GetOrCreateEntity(entityUuid).NotifyBuffEvent(buffEventType, buffUuid, baseId, level, fireUuid, entityCasterName, layer, duration, sourceConfigId, creationTime, extraPacketData, payload, fightSourceType);

            // 強化する9特化は、特化アビリティ本体が付与するバフ(と、その実行時変種)で判定する。
            // 紐付け先は保持者ではなく術者(FireUuid)。味方に配られるバフは受け手が保持するので、
            // 保持者に付けると回復を受けた人まで同じ特化になる。
            //
            // 除去は見ない。「あることしか示さない」判定なので、消えても未装着の根拠にならない。
            if (buffEventType == EBuffEventType.BuffEventRemove)
            {
                ApplySpecMarkerRemoval(entityUuid, buffUuid);
                ApplySeasonTalentRemoval(entityUuid, buffUuid);
            }
            else if (baseId > 0)
            {
                ApplySpecFromTalentBuff(baseId, buffUuid, fireUuid, fightSourceType ?? -1, entityUuid, sourceConfigId);
                ApplySeasonTalentFromRootBuff(baseId, buffUuid, fireUuid);
            }
        }

        /// <summary>
        /// シーズンタレントの型の根ノードのバフから、<b>そのバフを張った本人</b>の有効化している型を確定する。
        /// 根以外のノードや因子のバフからは推さない(シーズンごとに木が違うことがあるため)。
        /// 帰属先は特化と同じく術者(<c>FireUuid</c>)。根ノードのバフは本人が本人に付ける。
        /// </summary>
        public void ApplySeasonTalentFromRootBuff(int baseId, int buffUuid, long fireUuid)
        {
            if (!CombatDataCatalog.IsSeasonTalentRootBuff(baseId))
            {
                return;
            }

            if (fireUuid == 0 || (EEntityType)Utils.UuidToEntityType(fireUuid) != EEntityType.EntChar)
            {
                return;
            }

            GetOrCreateEntity(fireUuid).UpdateSeasonTalentFromRootBuff(baseId, buffUuid);
        }

        /// <summary>
        /// シーズンタレントの型の根ノードのバフの除去を受けて、型を無効へ戻す。
        /// 除去イベントは <c>BaseId</c> を運ばないので、付与時に控えた実体UUIDとの一致で判定する。
        /// 保持者で突き合わせる(根ノードのバフは術者と保持者が同じ人)。
        /// </summary>
        private void ApplySeasonTalentRemoval(long entityUuid, int buffUuid)
        {
            if (entityUuid == 0 || buffUuid == 0)
            {
                return;
            }

            if (!Entities.TryGetValue(entityUuid, out var entity)
                || !entity.ClearSeasonTalentFromRootRemoval(buffUuid))
            {
                return;
            }

            var characterId = Utils.UuidToEntityId(entityUuid);
            Services.PartyMemberCache.Instance.SetSeasonTalentInactive(characterId);
            Services.MeterPlayerSpecCache.Instance.SetSeasonTalentInactive(characterId);
            PlayerRosterProjection.AddOrUpdateNearbyPlayer(entityUuid);
        }

        /// <summary>
        /// タレント由来のバフから、<b>そのバフを張った本人</b>の特化を確定する。全18特化ぶん。
        ///
        /// <para>
        /// <b>帰属先は保持者ではなく術者(<c>BuffInfo.FireUuid</c>)。</b>
        /// プロトコル上バフの術者を名乗るフィールドはこれ1つ。
        /// 味方に配られるバフでも、術者として出した本人が入る。
        /// </para>
        ///
        /// <para>
        /// <b><c>FightSourceType</c> を採用条件にしない。</b><c>srcType==6</c> を必須にすると
        /// 強化側のツリー副産物が全部落ちる。観測のためログには残す。
        /// </para>
        ///
        /// <para>あることしか示さない。除去は見ない。</para>
        ///
        /// <para>術者の今の職の特化でなければ何も書かない(<see cref="Entity.AcceptsSubProfessionId"/>)。</para>
        /// </summary>
        public void ApplySpecFromTalentBuff(int observedBuffId, int buffUuid, long fireUuid, int fightSourceType, long holderUuid = 0, int sourceConfigId = 0)
        {
            if (!DataTypes.SpecDetectionTables.TryResolveSpecTalentBuff(
                    observedBuffId, out var grantedBuffId, out var spec, out var distanceFromRoot))
            {
                return;
            }

            // 術者がプレイヤーでないものは拾わない。モンスターやシーンオブジェクトが張った
            // バフのIDがたまたま一致したときに、そちらを特化持ちとして作らないため。
            if (fireUuid == 0 || (EEntityType)Utils.UuidToEntityType(fireUuid) != EEntityType.EntChar)
            {
                return;
            }

            // 変種バフ(+1〜+9)は、ワイヤが名乗る親で特化を引き直す。10刻み丸めは推定でしかなく、
            // 別ツリーの下流効果が隣の特化のバフに化けることがある。
            //
            // 書き換えるのは特化だけ。grantedBuffId / distanceFromRoot / isMarker は観測ID由来の
            // ままにする — 親がマーカー本体のことがあり(2203291 の親は鷹弓の根 2203290)、
            // 親の距離0を採ると派生バフでマーカーを latch してしまう。
            if (DataTypes.SpecDetectionTables.TryResolveSpecByParentBuff(
                    observedBuffId, fightSourceType, sourceConfigId, out var parentSpec, out _))
            {
                spec = parentSpec;
            }

            // 除去を突き合わせられるのはマーカー本体(ツリーの根)だけ。距離1以上の派生バフは
            // procで付いたり消えたりするので、それが消えても未装着の根拠にならない。
            //
            // 根の +1〜+9 の変種も、10刻みに丸めると根と同じ距離0になる。変種は死亡や時間切れで
            // 消えるので、控えるとその除去で特化を落としてしまう。控えるのは観測IDが根そのものの時だけ。
            // 変種は下の書き込みで特化の判定には使う(丸めは外さない)。
            var isMarker = distanceFromRoot == 0 && observedBuffId == grantedBuffId;
            var target = GetOrCreateEntity(fireUuid);

            if (!target.AcceptsSubProfessionId((int)spec))
            {
                return;
            }

            // 常設の食い違い検知。枝タレント(距離1以上)が、控えてあるマーカーと違う特化を
            // 書こうとしたら記録する(同じ職の2特化のあいだだけ)。鳴ったら判定表か10刻み丸めの誤りを疑う。
            //
            // マーカー自身(距離0)は除く。クラス変更でも、ビルドプリセットの一括配信でも
            // 別のマーカーが正当に来るため、食い違いの証拠にならない。
            if (!isMarker
                && target.SpecMarkerBuffId != 0
                && DataTypes.SpecDetectionTables.TryResolveSpecTalentBuff(
                    target.SpecMarkerBuffId, out _, out var markerSpec, out _)
                && markerSpec != spec)
            {
                Diagnostics.SpecConflictProbe.CaptureTalentConflict(
                    observedBuffId,
                    grantedBuffId,
                    spec.ToString(),
                    distanceFromRoot,
                    fightSourceType,
                    sourceConfigId,
                    target.SpecMarkerBuffId,
                    markerSpec.ToString(),
                    fireUuid,
                    holderUuid);
            }

            target.UpdateSubProfessionFromTalentBuff(
                (int)spec,
                isMarker ? grantedBuffId : 0,
                isMarker ? buffUuid : 0);
        }

        /// <summary>
        /// 特化マーカーバフの除去を受けて、特化を未装着(クラスR1)へ戻す。
        ///
        /// <para>
        /// 除去イベントは <c>BaseId</c> を運ばない(0 で届く)ので、付与時に控えた
        /// バフ実体UUIDとの一致でしか判定できない。控えてあるのはマーカー本体だけなので、
        /// 派生バフが消えただけで特化を落とすことはない。
        /// </para>
        ///
        /// <para>
        /// <b>保持者(<paramref name="entityUuid"/>)で突き合わせる。</b> 除去イベントは術者を
        /// 運ばないため、マーカーが「自分のアビリティが自分に付けるバフ」で保持者＝術者である
        /// ことを前提にしている。<b>この前提は未検証。</b>
        /// </para>
        /// </summary>
        private void ApplySpecMarkerRemoval(long entityUuid, int buffUuid)
        {
            if (entityUuid == 0 || buffUuid == 0)
            {
                return;
            }

            // ここでエンティティを作らない。見たことのない相手の除去は突き合わせようがない。
            if (!Entities.TryGetValue(entityUuid, out var entity)
                || !entity.ClearSubProfessionFromMarkerRemoval(buffUuid))
            {
                return;
            }

            // 保持している補完値も同時に落とす。落とさないと、次の解決でキャッシュが
            // 古い特化を返して元に戻ってしまう(自分はキャッシュを読まないので影響しない)。
            var characterId = Utils.UuidToEntityId(entityUuid);
            Services.PartyMemberCache.Instance.SetSpecAbilityUnequipped(characterId);
            Services.MeterPlayerSpecCache.Instance.SetSpecAbilityUnequipped(characterId);
            PlayerRosterProjection.AddOrUpdateNearbyPlayer(entityUuid);
        }

        protected virtual void OnSkillActivated(SkillActivatedEventArgs e)
        {
            SkillActivated?.Invoke(this, e);
        }

        protected virtual void OnBossHpUpdated(object sender, HpUpdatedEventArgs e)
        {
            Entity entity = (Entity)sender;

            if (entity.Hp > -1 && entity.MaxHp > 0 && entity.Hp <= entity.MaxHp)
            {
                long attrId = 0;
                var attr_id = entity.GetAttrKV("AttrId");
                if (attr_id != null)
                {
                    attrId = (long)(int)attr_id;
                }

                if (BossUUID != entity.UUID || BossAttrId != attrId)
                {
                    BossUUID = entity.UUID;
                    BossName = entity.Name;
                    BossAttrId = attrId;

                }

                BossHpPct = (int)(((double)entity.Hp / (double)entity.MaxHp) * 100000.0);
            }

            BossHpUpdated?.Invoke(sender, e);
        }

        protected virtual void OnEntityHpUpdated(object sender, HpUpdatedEventArgs e)
        {
            EntityHpUpdated?.Invoke(sender, e);
        }

        protected virtual void OnEntityThreatListUpdated(object sender, ThreatListUpdatedEventArgs e)
        {
            EntityThreatListUpdated?.Invoke(sender, e);
        }

        protected virtual void OnBuffUpdated(object sender, BuffUpdatedEventArgs e)
        {
            BuffUpdated?.Invoke(sender, e);
        }

        protected virtual void OnAttributeUpdated(object sender, AttributeUpdatedEventArgs e)
        {
            AttributeUpdated?.Invoke(sender, e);
        }

        protected virtual void OnSceneEvent(object sender, SceneEventEventArgs e)
        {
            SceneEvent?.Invoke(sender, e);
        }

        public void RemoveEntityHandlers()
        {
            foreach (var entity in Entities)
            {
                entity.Value.RemoveEventHandlers();
            }
        }

        public void RemoveEventHandlers()
        {
            SkillActivated = null;
            BossHpUpdated = null;
            EntityHpUpdated = null;
            BuffUpdated = null;
            AttributeUpdated = null;
            SceneEvent = null;

            RemoveEntityHandlers();
        }
    }

    [ProtoContract]
    public class EncounterExData
    {
        [ProtoMember(20)] public DateTime? QuestStartUtc { get; set; }
        [ProtoMember(21)] public bool UseQuestClock { get; set; }
        [ProtoMember(22)] public int IdleResetSeconds { get; set; }
        [ProtoMember(23)] public List<FactorActivation> FactorActivations { get; set; } = [];
        [ProtoMember(24)] public List<long> PartyCharacterIds { get; set; } = [];
        [ProtoMember(25)] public int IntervalResetSeconds { get; set; }
        [ProtoMember(26)] public bool QuestStartFromServer { get; set; }
        [ProtoMember(29)] public List<DefeatedEnemyRecord> DefeatedEnemies { get; set; } = [];
        [ProtoMember(28)] public List<HateObservation> HateObservations { get; set; } = [];
        [ProtoMember(30)] public List<HateCauseRecord> HateCauses { get; set; } = [];
        [ProtoMember(27)] public List<EnemyControlEvent> EnemyControlEvents { get; set; } = [];
        [ProtoMember(2)]
        public int DungeonDifficulty { get; set; } = 0;
        [ProtoMember(3)]
        public bool IsTimedOut { get; set; } = false;
        [ProtoMember(4)]
        public int DungeonTimeDeathChange { get; set; } = 0;
        [ProtoMember(5)]
        public int EncounterPhase { get; set; } = 0;
        [ProtoMember(6)]
        public int BenchmarkTime { get; set; } = 0;
        [ProtoMember(7)]
        public DateTime? FirstDamageTimeStamp { get; set; } = null;
        /// <summary>タイムラインの予告の行(<see cref="Encounter.AddSkillAnnouncement"/>)。実体に属さないのでここに持つ。</summary>
        [ProtoMember(8)]
        public List<SkillAnnouncementRecord> SkillAnnouncements { get; set; } = [];
        /// <summary>記録のダメージの無い死亡(<see cref="Encounter.AddPlayerDeathWithoutDamage"/>)。予告と同じくここに持つ。</summary>
        [ProtoMember(9)]
        public List<PlayerDeathRecord> PlayerDeaths { get; set; } = [];
        /// <summary>この回の自動一時停止の秒数(回を作ったときの <see cref="CombatRuntimeSettings.CombatExitSeconds"/>)。0 は止めない。計測の回は 0。</summary>
        [ProtoMember(10)]
        public int CombatExitSeconds { get; set; } = 0;
        /// <summary>最後の戦闘の出来事の到着時刻(UTC)。書くのは <see cref="Encounter.RecordCombatEvent"/> だけ。</summary>
        [ProtoMember(11)]
        public DateTime? LastCombatEventTimeStamp { get; set; } = null;
        /// <summary>
        /// 戦闘の時計が止まっていた区間(古い順)。読み書きは <see cref="Encounter"/> の中でだけ行う(ロックがそこにある)。
        /// 今止まっている区間は入れない(最後の戦闘の出来事から決まる)。
        /// </summary>
        [ProtoMember(12)]
        public List<CombatPause> CombatPauses { get; set; } = [];
        /// <summary>
        /// プレイヤーの技の開始の DB の形(全員の分を届いた順)。実行中は空で、保存の間と DB から読んだ直後だけ入る。
        /// 読み書きは <see cref="Encounter"/> の中でだけ行う(ロックがそこにある)。
        /// </summary>
        [ProtoMember(13)]
        public List<SkillActivationRecord> SkillActivations { get; set; } = [];
        /// <summary>
        /// この回のシーズン番号(サーバー全体で1つ、<see cref="SeasonStateStore.CurrentSeasonId"/>)。保存の直前に書く(<c>ApplyDisplayedIdentitiesForRecord</c>)。
        /// 履歴から来たプレイヤーの行は、今のシーズンではなくこの値で出す。分からなければ 0。
        /// </summary>
        [ProtoMember(14)]
        public int SeasonId { get; set; } = 0;
        /// <summary>タイムラインの敵の回復(<see cref="Encounter.AddEnemyHeal"/>)。予告と同じくここに持つ。</summary>
        [ProtoMember(15)]
        public List<EnemyHealRecord> EnemyHeals { get; set; } = [];
        /// <summary>タイムラインの敵から敵へのダメージ(<see cref="Encounter.AddEnemyDamage"/>)。</summary>
        [ProtoMember(16)]
        public List<EnemyDamageRecord> EnemyDamages { get; set; } = [];
        /// <summary>タイムラインの敵が付けたバフ(<see cref="Encounter.AddEnemyBuff"/>)。</summary>
        [ProtoMember(17)]
        public List<EnemyBuffRecord> EnemyBuffs { get; set; } = [];
        /// <summary>
        /// この回のゲームのサーバー(<see cref="GameServerStore.Current"/>)。保存の直前に書く(<c>ApplyDisplayedIdentitiesForRecord</c>)。
        /// 履歴から来たプレイヤーの行のタレントの木は、今のサーバーではなくこの値で出す。分からなければ <see cref="Models.GameServer.Unknown"/>。
        /// </summary>
        [ProtoMember(18)]
        public Models.GameServer GameServer { get; set; } = Models.GameServer.Unknown;
        /// <summary>
        /// この回のチャンネル番号。回を作るときとチャンネルが届いたときに書く(<see cref="Encounter.SetChannelLineNumber"/>)。
        /// 保存の直前には書かない(マップ移動では保存より先に <see cref="EncounterManager.ChannelLineId"/> が移動先へ変わる)。チャンネルの無い場所では 0。
        /// </summary>
        [ProtoMember(19)]
        public uint ChannelLine { get; set; } = 0;

        public EncounterExData() { }
    }

    /// <summary>
    /// 戦闘の時計が止まっていた区間。始まりは最後の戦闘の出来事 + 自動一時停止の秒数、終わりは続きを始めた出来事の到着時刻(どちらも UTC)。
    /// </summary>
    [ProtoContract]
    public sealed class CombatPause
    {
        [ProtoMember(1)]
        public DateTime Start { get; set; }

        [ProtoMember(2)]
        public DateTime End { get; set; }
    }

    public class EncounterBossDataCache
    {
        public long UUID;
        public string Name = null!;
        public long Hp;
        public long MaxHp;
        public Dictionary<string, object> Attrs = null!;
    }

    public class SceneEventEventArgs : EventArgs
    {
        public Zproto.WorldEventType EventType;
    }

    public class SceneEventBossDbmEventArgs : SceneEventEventArgs
    {
        public int SkillId;
        public int Duration;
        public int Insertion;
        public long Timestamp;
    }

    public class SceneEventNoticeTipEventArgs : SceneEventEventArgs
    {
        public string MessageId = null!;
        public string ExtraId = null!;
    }

    /// <summary>
    /// エンカウンターをまたいでも変わらない素性。
    ///
    /// <para>
    /// <see cref="Entity"/> は戦闘統計の入れ物でもあり、「進行で自動リセット」の区切りなどで
    /// まるごと作り直される。そのとき統計は捨ててよいが素性は残す必要がある。
    /// 素性をこの型に集約し、引き継ぎは <see cref="Entity.CopyIdentityFrom"/> の1呼び出しで済ませる。
    /// </para>
    ///
    /// <para>
    /// <b>素性を足すときは必ずここに足すこと。</b> record のコピーで丸ごと運ばれるので、
    /// 引き継ぎ側に書き足す必要はない。<see cref="Entity"/> に直接プロパティを足すと運ばれず、
    /// 作り直しのたびに失われる。
    /// </para>
    ///
    /// <para>Hp / MaxHp は戦闘中に変わる値なので素性に含めない。従来どおり個別に引き継ぐ。</para>
    /// </summary>
    public sealed record EntityIdentity
    {
        public long UUID { get; set; }
        public long UID { get; set; }
        public EEntityType EntityType { get; set; }
        public EMonsterType MonsterType { get; set; } = EMonsterType.Unknown;
        public EEntityType SummonerEntityType { get; set; } = EEntityType.EntErrType;
        public string Name { get; set; } = null!;
        public int AbilityScore { get; set; }
        public int ProfessionId { get; set; }
        public int SubProfessionId { get; set; }

        /// <summary>いま乗っている特化マーカーバフのID。0 なら未観測。</summary>
        public int SpecMarkerBuffId { get; set; }

        /// <summary>上のマーカーバフの実体UUID。除去イベントは BaseId を運ばないため、これで突き合わせる。</summary>
        public int SpecMarkerBuffUuid { get; set; }

        /// <summary>
        /// 全バフスナップショットを受信済みか。「マーカーが無い＝未装着」と言えるのは、
        /// 全バフを一度に受け取った上でそこに無かったときだけ。
        /// </summary>
        public bool HasBuffSnapshot { get; set; }

        /// <summary>
        /// 有効化しているシーズンタレントの型の、根ノードのバフID。0 なら未観測か無効。
        /// 型の判定は根ノードのバフだけで行う(根以外のノードや因子からは推さない)。
        /// </summary>
        public int SeasonTalentBuffId { get; set; }

        /// <summary>上の根ノードのバフの実体UUID。除去イベントは BaseId を運ばないため、これで突き合わせる。</summary>
        public int SeasonTalentBuffUuid { get; set; }

        /// <summary>
        /// シーズンタレントについて、全バフスナップショットを受信済みか。立て方と落とし方は
        /// <see cref="HasBuffSnapshot"/> と同じだが、記録では特化の未装着(Rank1)とは別に焼き付くので分けて持つ。
        /// </summary>
        public bool HasSeasonTalentSnapshot { get; set; }

        /// <summary>
        /// NPC(助っ人)か。判定はパーティの社交データ(<c>BotAiId</c>)と実体の <c>AttrId</c> で決まり、社交データは履歴に残らないので、
        /// 作り直しの前に表示している値を焼き付ける(<see cref="Entity.ApplyDisplayedIdentityForRecord"/>)。
        /// </summary>
        public bool IsNpc { get; set; }

        /// <summary>
        /// 乗っている根のバフ(<see cref="CombatDataCatalog.IsRootBuff"/>)の番号。昇順・重なりなし。null は全部をまだ受けていない。
        /// <b>配列は書き換えず差し替える</b>(素性の写しが同じ配列を持つため)。
        /// </summary>
        public int[]? RootBuffIds { get; set; }

        /// <summary>
        /// 自分の職業ごとの取ったタレントの木の節の番号(昇順)。null はまだ受けていない。
        /// <b>辞書も配列も書き換えず差し替える</b>(素性の写しが同じものを持つため)。
        /// </summary>
        public IReadOnlyDictionary<int, int[]>? TalentNodeIdsByProfession { get; set; }

        /// <summary>
        /// バフを付けないアビリティの節の技の置き換えに出てくる技の、元の技 → 最後に使ったほう。null はどれもまだ使っていない。
        /// <b>辞書は書き換えず差し替える</b>(素性の写しが同じものを持つため)。
        /// </summary>
        public IReadOnlyDictionary<int, int>? LastReplaceableSkills { get; set; }

        /// <summary>
        /// プレイヤーの出現時の情報(全属性)を受けたか。名前・クラス・能力スコアなどの出所で、ウィジェットの状態の印が欠けを見るのに使う。
        /// 素性なので回の作り直しで運ばれ、ログアウトでは運ばれない。周りから外れても下ろさない(値は実体に残る)。
        /// </summary>
        public bool HasAppearAttributes { get; set; }

        public int Level { get; set; }
        public long SeasonLevel { get; set; }
        public long SeasonStrength { get; set; }
    }

    public class Entity : System.ICloneable
    {
        private EntityIdentity _identity = new();

        /// <summary>
        /// 素性を丸ごと引き継ぐ。エンカウンター作り直しで統計だけ捨てたいときに使う。
        /// record のコピーなので <see cref="EntityIdentity"/> に足したものは自動で運ばれる。
        /// </summary>
        public void CopyIdentityFrom(Entity other)
        {
            ArgumentNullException.ThrowIfNull(other);
            _identity = other._identity with { };
        }

        public long UUID { get => _identity.UUID; set => _identity.UUID = value; }
        public long UID { get => _identity.UID; set => _identity.UID = value; }
        public EEntityType EntityType { get => _identity.EntityType; private set => _identity.EntityType = value; }
        public string Name { get => _identity.Name; private set => _identity.Name = value; }
        public int AbilityScore { get => _identity.AbilityScore; private set => _identity.AbilityScore = value; }
        public int ProfessionId { get => _identity.ProfessionId; private set => _identity.ProfessionId = value; }
        public int SubProfessionId { get => _identity.SubProfessionId; private set => _identity.SubProfessionId = value; }

        public int SpecMarkerBuffId { get => _identity.SpecMarkerBuffId; private set => _identity.SpecMarkerBuffId = value; }
        public int SpecMarkerBuffUuid { get => _identity.SpecMarkerBuffUuid; private set => _identity.SpecMarkerBuffUuid = value; }
        public bool HasBuffSnapshot { get => _identity.HasBuffSnapshot; private set => _identity.HasBuffSnapshot = value; }

        public int SeasonTalentBuffId { get => _identity.SeasonTalentBuffId; private set => _identity.SeasonTalentBuffId = value; }
        public int SeasonTalentBuffUuid { get => _identity.SeasonTalentBuffUuid; private set => _identity.SeasonTalentBuffUuid = value; }
        public bool HasSeasonTalentSnapshot { get => _identity.HasSeasonTalentSnapshot; private set => _identity.HasSeasonTalentSnapshot = value; }

        /// <summary>プレイヤーの出現時の情報を受けたか(<see cref="EntityIdentity.HasAppearAttributes"/>)。</summary>
        public bool HasAppearAttributes { get => _identity.HasAppearAttributes; private set => _identity.HasAppearAttributes = value; }

        /// <summary>出現時の全属性を当てた(プレイヤーだけ)。</summary>
        internal void MarkAppearAttributesReceived()
        {
            HasAppearAttributes = true;
        }

        /// <summary>
        /// シーズンタレントの型が無効(どの型も有効化していない)と確定できる状態か。
        /// 全バフスナップショットを受け取った上で、そこに根ノードのバフが無かったときだけ true。
        /// 特化の <see cref="IsSpecAbilityUnequipped"/> と同じ作り。
        /// </summary>
        public bool IsSeasonTalentInactive =>
            ProfessionId > 0 && HasSeasonTalentSnapshot && SeasonTalentBuffId == 0;

        /// <summary>
        /// NPC(助っ人)か。表示に出していた印(パーティの社交データか <c>AttrId</c> で決まる)を、
        /// 作り直しの前に焼き付けて次の回へ持ち越す。
        /// </summary>
        public bool IsNpc { get => _identity.IsNpc; private set => _identity.IsNpc = value; }

        /// <summary>
        /// 乗っている根のバフの番号(昇順・重なりなし)。アビリティ情報・潜在心相晶情報・モジュール情報が出す。
        ///
        /// <para>
        /// 全部を受けたときに作り直す(他人は出現時の全バフの一覧、自分は自分のバフの差分を受けるたび)。
        /// その後は付与・除去の差分で増やしたり減らしたりする。null は全部をまだ受けていない(何も出さない)。
        /// 周りから外れても消さない(最後に受けた値を出し、そのまま回に保存する)。
        /// </para>
        /// </summary>
        public int[]? RootBuffIds { get => _identity.RootBuffIds; private set => _identity.RootBuffIds = value; }

        /// <summary>
        /// 自分の職業ごとの取ったタレントの木の節の番号(昇順)。アビリティ情報が今の職業の一覧で取った節を決める。
        ///
        /// <para>
        /// 全体コンテナ(マップ移動・ログイン)で置き換え、タレントを付け替えたときの差分(変えた職業の一覧を丸ごと運ぶ)で重ねる。
        /// null はどちらもまだ受けていない(ほかのプレイヤーはいつも null)。回に保存する。
        /// </para>
        /// </summary>
        public IReadOnlyDictionary<int, int[]>? TalentNodeIdsByProfession { get => _identity.TalentNodeIdsByProfession; private set => _identity.TalentNodeIdsByProfession = value; }

        /// <summary>
        /// バフを付けないアビリティの節の技の置き換えに出てくる技の、元の技 → 最後に使ったほう(元の技か置き換え後の技)。
        /// 取った節の一覧が無いプレイヤー(ほかのプレイヤー)の、その節を取っているかを決める。null はどれもまだ使っていない。
        /// 周りから外れても消さない(最後に使った技を出し、そのまま回に保存する)。
        /// </summary>
        public IReadOnlyDictionary<int, int>? LastReplaceableSkills { get => _identity.LastReplaceableSkills; private set => _identity.LastReplaceableSkills = value; }

        /// <summary>
        /// 保存した回の顔と名刺の写真のリンク(名刺の店 <see cref="SocialDataStore"/> の保存の時点の値、届いていなければ null)。
        /// 書くのは保存の直前の焼き付け(<see cref="ApplyDisplayedIdentityForRecord"/>)だけで、回の作り直しでは運ばない。
        /// 履歴から来たプレイヤーの行は、今の店の値ではなくこの値で写真を取る。画像そのものは持たない。
        /// </summary>
        public SocialAvatarState? RecordedAvatar { get; private set; }

        /// <summary>
        /// 保存した回の、バフで決まる変身(ミーン・ゴーレム。変身していなければ <see cref="Models.PlayerClassSpec.Unknown"/>)。null は保存の焼き付けを通っていない。
        /// 書くのは保存の直前の焼き付け(<see cref="ApplyDisplayedIdentityForRecord"/>)だけで、回の作り直しでは運ばない。
        /// バフは保存しないので、履歴はこの値で変身を決める。
        /// </summary>
        public Models.PlayerClassSpec? RecordedBuffTransformation { get; private set; }

        /// <summary>
        /// 特化が「アビリティ未装着」(ゲーム内呼称 クラスR1)と確定できる状態か。
        ///
        /// <para>
        /// 全バフスナップショットを受け取った上で、そこにタレント由来のバフが1つも無かったときだけ true。
        /// スナップショットは「いまこのエンティティが持っている全バフ」なので、
        /// そこに無いことは「持っていない」を意味する。
        /// </para>
        ///
        /// <para>
        /// <b>ID判定式が特化を確定させたら Rank1 は成立しない。</b>
        /// スナップショットは AOI出現とマップロードでしか来ないため、
        /// ロード済みマップで途中起動すると「マーカーが無い」状態から始まる。
        /// そこへ置換後スキルやタレントバフが到着したら、そちらを優先する。
        /// </para>
        /// </summary>
        public bool IsSpecAbilityUnequipped =>
            ProfessionId > 0 && HasBuffSnapshot && SubProfessionId == 0;
        public int Level { get => _identity.Level; set => _identity.Level = value; }

        public long SeasonLevel { get => _identity.SeasonLevel; set => _identity.SeasonLevel = value; }
        public long SeasonStrength { get => _identity.SeasonStrength; set => _identity.SeasonStrength = value; }

        public CombatStats DamageStats { get; set; } = new();
        public CombatStats HealingStats { get; set; } = new();

        /// <summary>
        /// 被ダメ詳細と被弾の合計の材料。1件ずつの被弾は回の記録(<see cref="TakenHitRecord"/>)に持ち、
        /// ここには保存の間と DB から読んだ直後だけ入る(<see cref="CombatStats.SkillSnapshots"/>)。
        /// </summary>
        public CombatStats TakenStats { get; set; } = new();

        /// <summary>
        /// このエンティティが始めた詠唱。タイムラインの詠唱の行の材料で、
        /// <see cref="Encounter.AddSkillCast"/> がプレイヤー以外の詠唱バーを持つ技だけを積む。
        /// </summary>
        public List<SkillCastRecord> SkillCasts { get; private set; } = new();
        private readonly object _skillCastsGate = new();

        public bool ShouldSerializeSkillCasts() => SkillCasts.Count > 0;

        /// <summary>
        /// タイムライン・被ダメログに名前が出たか(加害者・詠唱者・敵の行動の行動した側と相手)。出た実体は周りから消えても回から外さない
        /// (<see cref="Encounter.RemoveDisappearedEntity"/>)。
        /// </summary>
        public bool IsNamedInCombatEventLog { get; set; }

        public void AddSkillCast(SkillCastRecord cast)
        {
            lock (_skillCastsGate)
            {
                SkillCasts.Add(cast);
            }
        }

        public SkillCastRecord[] GetSkillCastsCopy()
        {
            lock (_skillCastsGate)
            {
                return SkillCasts.ToArray();
            }
        }

        /// <summary>技 <paramref name="skillId"/> か <paramref name="otherSkillId"/>(0 以下は見ない)の一番新しい詠唱の通し番号。無ければ 0。</summary>
        public long FindLatestSkillCastSequence(int skillId, int otherSkillId)
        {
            lock (_skillCastsGate)
            {
                for (var index = SkillCasts.Count - 1; index >= 0; index--)
                {
                    var cast = SkillCasts[index];
                    if ((skillId > 0 && cast.SkillId == skillId) || (otherSkillId > 0 && cast.SkillId == otherSkillId))
                    {
                        return cast.Sequence;
                    }
                }

                return 0;
            }
        }

        /// <summary>中身が入るのは一部の実体だけ。既定の作り方は CPU の数だけロックを持つので、1つにする。</summary>
        public ConcurrentDictionary<long, MetricsContainer> SkillMetrics { get; set; } = new(concurrencyLevel: 1, capacity: 1);

        public ulong TotalDamage { get; set; } = 0;
        public ulong TotalShieldBreak { get; set; } = 0;
        public ulong TotalHealing { get; set; } = 0;
        public ulong TotalOverhealing { get; set; } = 0;
        public ulong TotalShield { get; set; } = 0;
        public ulong TotalCasts { get; set; } = 0;
        public ulong TotalDeaths { get; set; } = 0;

        /// <summary>行動と行動の間のうち、<see cref="InactiveGapSeconds"/> を超えた分の合計(秒)。有効DPS の分母から除く。</summary>
        public double TotalInactiveTime { get; set; } = 0.0;

        /// <summary>行動の間がこれを超えた分を非行動として除く(秒)。</summary>
        private const double InactiveGapSeconds = 10.0;

        /// <summary>この人の最初の行動(与ダメ・回復・被弾)の到着時刻。起点より前は動かさない。</summary>
        public DateTime? FirstCombatActionTime { get; set; } = null;
        public DateTime? LastCombatActionTime { get; set; } = null;

        public ThreadSafeOrderedDictionary<ulong, BuffEvent> BuffEvents { get; set; } = new();

        /// <summary>最近のバフの記録。読むのは技の枠のバッジだけで、バッジはプレイヤーに出るので、ほかの実体は null のまま。</summary>
        [JsonIgnore]
        public ThreadSafeOrderedDictionary<ulong, BuffEvent>? RecentBuffEventHistory { get; private set; }

        public EMonsterType MonsterType { get => _identity.MonsterType; set => _identity.MonsterType = value; }

        public long Hp { get; private set; } = 0;
        public long MaxHp { get; private set; } = 0;

        /// <summary>最近の HP。読むのはボスの全滅の処理だけで、ボスはモンスターなので、ほかの実体は null のまま。</summary>
        public ConcurrentQueue<long>? RecentHpHistory { get; private set; }

        public List<ThreatInfo> ThreatInfoList { get; private set; } = new();

        private Dictionary<string, object> _attributes = new();

        public Dictionary<string, object> Attributes
        {
            get => _attributes;
            set
            {
                _attributes = value ?? new Dictionary<string, object>();
            }
        }

        public EEntityType SummonerEntityType { get => _identity.SummonerEntityType; set => _identity.SummonerEntityType = value; }

        public delegate void SkillActivatedEventHandler(object sender, SkillActivatedEventArgs e);
        public event SkillActivatedEventHandler? SkillActivated;
        public delegate void HpUpdatedEventHandler(object sender, HpUpdatedEventArgs e);
        public event HpUpdatedEventHandler? HpUpdated;
        public delegate void ThreatListUpdatedEventHandler(object sender, ThreatListUpdatedEventArgs e);
        public event ThreatListUpdatedEventHandler? ThreatListUpdated;

        public object Clone()
        {
            var cloned = this.MemberwiseClone();
            ((Entity)cloned).DamageStats = (CombatStats)this.DamageStats.Clone();
            ((Entity)cloned).HealingStats = (CombatStats)this.HealingStats.Clone();
            ((Entity)cloned).TakenStats = (CombatStats)this.TakenStats.Clone();

            foreach (var container in this.SkillMetrics)
            {
                var metrics = new MetricsContainer
                {
                    Damage = (CombatStats)container.Value.Damage.Clone(),
                    Healing = (CombatStats)container.Value.Healing.Clone()
                };
                ((Entity)cloned).SkillMetrics.AddOrUpdate(container.Key, metrics, (key, value) => metrics);
            }
            return cloned;
        }

        public void RemoveEventHandlers()
        {
            SkillActivated = null;
            HpUpdated = null;
            ThreatListUpdated = null;
        }

        [JsonConstructor]
        public Entity(long uuid, string? name = null, Encounter? encounter = null)
        {
            UUID = uuid;
            UID = Utils.UuidToEntityId(uuid);
            Name = name!;

            if (encounter != null)
            {

                if (encounter.PreviousBossCache.Count > 0 && encounter.PreviousBossCache.TryGetValue(uuid, out var foundEnt))
                {
                    SetHpValuesNoUpdate(foundEnt.Hp, foundEnt.MaxHp);
                    Attributes = foundEnt.Attrs.ToDictionary();

                    encounter.PreviousBossCache.Remove(uuid);
                }

                // AttrId と召喚者は出現時にしか届かず、区切りをまたいで運ばれない敵もいる。周囲にいる敵なら索引から戻す。
                // 索引はいまの周囲を表すので、実行中のエンカウンターの実体にだけ入れる。
                if (ReferenceEquals(encounter, EncounterManager.Current)
                    && (EEntityType)Utils.UuidToEntityType(uuid) == EEntityType.EntMonster
                    && Services.NearbyMonsterIndex.Instance.TryGet(uuid, out var nearbyMonster))
                {
                    Attributes.TryAdd("AttrId", nearbyMonster.MonsterId);
                    UID = nearbyMonster.MonsterId;
                    if (nearbyMonster.SummonerUuid != 0)
                    {
                        Attributes.TryAdd("AttrSummonerId", nearbyMonster.SummonerUuid);
                    }

                    if (nearbyMonster.TopSummonerUuid != 0)
                    {
                        Attributes.TryAdd("AttrTopSummonerId", nearbyMonster.TopSummonerUuid);
                    }
                }

                // 周りから消えて回から外した実体を後から届く通知で作り直すときは、種別IDと召喚者を控えから戻す(名前と、その実体が付けたバフの付与者を引くため)。
                if (ReferenceEquals(encounter, EncounterManager.Current)
                    && Services.EntityOriginIndex.Instance.TryGetDisappeared(uuid, out var disappeared))
                {
                    if (disappeared.AttrId > 0)
                    {
                        Attributes.TryAdd("AttrId", disappeared.AttrId);
                        UID = disappeared.AttrId;
                    }

                    if (disappeared.SummonerUuid != 0)
                    {
                        Attributes.TryAdd("AttrSummonerId", disappeared.SummonerUuid);
                    }

                    if (disappeared.TopSummonerUuid != 0)
                    {
                        Attributes.TryAdd("AttrTopSummonerId", disappeared.TopSummonerUuid);
                    }
                }
            }

            SetEntityType((EEntityType)Utils.UuidToEntityType(uuid));

            // キャッシュから初期値を流し込まない。ここで注入すると、その値が Entity のライブ値と
            // 見分けられなくなり、PlayerDataSourceResolver の優先順位(ライブ > パーティ情報 > 補完)を
            // 追い越して新しい情報を握り潰す。補完が要るものは PartyMemberCache から明示的に引く。

        }

        public void UpdateUID(long uid)
        {
            UID = uid;
        }

        public void SetName(string name)
        {
            Name = name;
        }

        public void SetEntityType(EEntityType type)
        {
            EntityType = type;

            if (type != EEntityType.EntChar)
            {

            }

            if (type == EEntityType.EntMonster)
            {

                var attr_id = GetAttrKV("AttrId");
                if (attr_id != null)
                {
                    UID = (int)attr_id;
                    if (HelperMethods.DataTables.Monsters.Data.TryGetValue(attr_id.ToString()!, out var monsterEntry))
                    {
                        SetName(monsterEntry.Name);
                        SetMonsterType(monsterEntry.MonsterType);
                    }
                }
            }
            else if (type == EEntityType.EntDummy)
            {
                var attr_id = GetAttrKV("AttrId");
                if (attr_id != null)
                {
                    UID = (int)attr_id;
                }
            }
        }

        public void SetAbilityScore(int abilityScore)
        {
            AbilityScore = abilityScore;
        }

        public void SetProfessionId(int id)
        {
            ProfessionId = id;

            var subProfessionBaseId = Professions.GetProfessionIdFromSubProfessionId(SubProfessionId);
            var clearSubProfession = SubProfessionId != 0 && subProfessionBaseId != id;
            if (clearSubProfession)
            {
                SetSubProfessionUnknown();
                Services.PartyMemberCache.Instance.ClearSubProfession(Utils.UuidToEntityId(UUID));
            }
        }

        /// <summary>
        /// その特化をこの実体へ書いてよいか。職が分かっているなら、今の職の2特化だけを受け付ける。
        /// 職を替えた後も前の職の効果が届くので、受け付けると特化と一緒に職まで前の職へ戻る。職を替えるのは <c>AttrProfessionId</c>(同じ差分の新しい職の木より先に当たる)。
        /// 職がまだ分からない実体(途中起動など)は受け付ける。変身クラスの間は <see cref="SetSubProfessionId"/> の関門に任せる。
        /// </summary>
        public bool AcceptsSubProfessionId(int subProfessionId)
        {
            return ProfessionId <= 0
                || Models.PlayerClassSpecResolver.TryResolveTransformation(ProfessionId, out _)
                || Professions.GetProfessionIdFromSubProfessionId(subProfessionId) == ProfessionId;
        }

        public void SetSubProfessionId(int id)
        {
            if (id <= 0)
            {
                SetSubProfessionUnknown();
                return;
            }

            // 変身クラス(8/14/15)の間は、いかなる特化変更も受け付けない。
            // これらは特化(クラスR2)を持たず、表示は職業IDから固定で決まる。
            //
            // ここで弾かないと職業ごと戻される。この直後の分岐が
            // 「特化が属する職」で ProfessionId を書き換えるため、変身中に
            // タレントバフや置換後スキルを1つ拾っただけで元のクラスに戻ってしまう。
            if (Models.PlayerClassSpecResolver.TryResolveTransformation(ProfessionId, out _))
            {
                return;
            }

            SubProfessionId = id;

            int profId = Professions.GetProfessionIdFromSubProfessionId(id);
            if (ProfessionId != profId)
            {
                ProfessionId = profId;
            }

            // AOI外に出ると観測できなくなるので、自分以外のパーティメンバーの分だけ控える。
            // 自分かどうか・パーティ内かどうかの判定はキャッシュ側が持つ。
            Services.PartyMemberCache.Instance.SetSubProfession(Utils.UuidToEntityId(UUID), id);
        }

        /// <summary>
        /// <b>記録する回に対して、作り直しの前にだけ呼ぶ。いま画面に出している素性をそのまま記録へ焼き付ける。</b>
        ///
        /// <para>
        /// エンティティが持つ生の値は、AOI退出で <see cref="SetSubProfessionUnknown"/> に消される
        /// (消すこと自体は必要 — 次に現れたときの全バフスナップショットを権威にするため)。
        /// 表示はキャッシュとPT補完が補っているが、そちらは blob に載らない。
        /// だから保存するときは<b>合成後の値</b>を書く。
        /// </para>
        ///
        /// <para>
        /// <b><see cref="SetSubProfessionId"/> や <see cref="SetProfessionId"/> は使わない。</b>
        /// あれらは職業を導き直したり <c>PartyMemberCache</c> へ書いたりする副作用を持つ。
        /// ここが欲しいのは値の書き込みだけ。
        /// </para>
        ///
        /// <para>
        /// 特化と「未装着」は同じ1行から揃えて渡すこと。<see cref="IsSpecAbilityUnequipped"/> は
        /// <c>ProfessionId &gt; 0 &amp;&amp; HasBuffSnapshot &amp;&amp; SubProfessionId == 0</c> で導出されるので、
        /// 未装着を記録するには <paramref name="hasBuffSnapshot"/> を立てて特化を0にする。
        /// シーズンタレントも同じで、無効を記録するには <paramref name="seasonTalentInactive"/> を立てて根ノードのバフを0にする。
        /// </para>
        /// </summary>
        public void ApplyDisplayedIdentityForRecord(
            string name,
            int professionId,
            int subProfessionId,
            bool hasBuffSnapshot,
            int seasonTalentBuffId,
            bool seasonTalentInactive,
            int abilityScore,
            int level,
            long seasonLevel,
            long seasonStrength,
            long maxHp,
            bool isNpc,
            SocialAvatarState? avatar,
            Models.PlayerClassSpec buffTransformation)
        {
            // NPC の印はパーティの社交データ(BotAiId)でも決まり、社交データは履歴に残らない。
            // 表示に出している値と一緒にここで焼き付ける。
            IsNpc = isNpc;

            // 写真のリンクも同じく名刺の店にしか無い。届いていなければ null のまま書く。
            RecordedAvatar = avatar;

            RecordedBuffTransformation = buffTransformation;

            if (!string.IsNullOrEmpty(name))
            {
                Name = name;
            }

            if (professionId > 0)
            {
                ProfessionId = professionId;
            }

            SubProfessionId = subProfessionId;
            HasBuffSnapshot = hasBuffSnapshot;
            SeasonTalentBuffId = seasonTalentBuffId;
            HasSeasonTalentSnapshot = seasonTalentInactive;

            if (abilityScore > 0)
            {
                AbilityScore = abilityScore;
            }

            if (level > 0)
            {
                Level = level;
            }

            if (seasonLevel > 0)
            {
                SeasonLevel = seasonLevel;
            }

            if (seasonStrength > 0)
            {
                SeasonStrength = seasonStrength;
            }

            if (maxHp > 0)
            {
                MaxHp = maxHp;
            }
        }

        public void SetSubProfessionUnknown()
        {
            SubProfessionId = 0;

            // 特化を「観測できていない」に戻すときは、その根拠だったマーカーと
            // スナップショット受信済みフラグも一緒に落とす。
            // スナップショットだけ残すと「受信済みなのにマーカーが無い」= 未装着確定、という
            // 実際には確かめていない結論が立ってしまう。
            SpecMarkerBuffId = 0;
            SpecMarkerBuffUuid = 0;
            HasBuffSnapshot = false;
        }

        /// <summary>
        /// シーズンタレントの型を「観測できていない」に戻す。根ノードのバフとスナップショット受信済みの印も落とす
        /// (<see cref="SetSubProfessionUnknown"/> と同じ理由)。
        /// </summary>
        public void SetSeasonTalentUnknown()
        {
            SeasonTalentBuffId = 0;
            SeasonTalentBuffUuid = 0;
            HasSeasonTalentSnapshot = false;
        }

        public void SetLevel(int level)
        {
            Level = level;
        }

        public void SetSeasonLevel(int level)
        {
            SeasonLevel = level;
        }

        public void SetSeasonStrength(int strength)
        {
            SeasonStrength = strength;
        }

        public void SetHpNoUpdate(long hp)
        {
            Hp = hp;
        }

        public void SetHpValuesNoUpdate(long hp, long maxHp)
        {
            Hp = hp;
            MaxHp = maxHp;
        }

        public void SetHpValues(long hp = -1, long maxHp = -1)
        {
            if (hp != -1)
            {
                Hp = hp;

                if (Utils.UuidToEntityType(UUID) == (long)EEntityType.EntMonster)
                {
                    var recentHpHistory = RecentHpHistory ??= new();
                    if (recentHpHistory.Count > 10)
                    {
                        recentHpHistory.TryDequeue(out _);
                    }
                    recentHpHistory.Enqueue(hp);
                }
            }

            if (maxHp != -1)
            {
                MaxHp = maxHp;
            }

            if ((Hp > -1) && (MaxHp > -1))
            {
                OnHpUpdated(new HpUpdatedEventArgs() { EntityUuid = UUID, Hp = Hp, MaxHp = MaxHp, UpdateDateTime = DateTime.Now });
            }
        }

        protected virtual void OnHpUpdated(HpUpdatedEventArgs e)
        {
            HpUpdated?.Invoke(this, e);
        }

        public void SetThreatList(List<ThreatInfo> threatInfoList)
        {
            ThreatInfoList = threatInfoList;

            OnThreatListUpdated(new ThreatListUpdatedEventArgs() { EntityUuid = UUID, ThreatInfoList = ThreatInfoList });
        }

        protected virtual void OnThreatListUpdated(ThreatListUpdatedEventArgs e)
        {
            ThreatListUpdated?.Invoke(this, e);
        }

        public void IncrementDeaths()
        {
            TotalDeaths++;
        }

        public void SetDeaths(ulong deaths)
        {
            TotalDeaths = deaths;
        }

        public void SetMonsterType(int type)
        {
            MonsterType = (EMonsterType)type;
        }

        public void AddRecentBuffEventHistory(int uuid, BuffEvent buffEvent)
        {
            if ((EEntityType)Utils.UuidToEntityType(UUID) != EEntityType.EntChar)
            {
                return;
            }

            var history = RecentBuffEventHistory ??= new(6);
            if (history.Count > 20)
            {
                history.RemoveFirst();
            }
            history[(ulong)uuid] = buffEvent;
        }

        /// <param name="countsTowardStats">発動の数に入れるか。決めるのは <see cref="Encounter.RegisterSkillActivation"/>(起点の後・計測の窓)。</param>
        public void RegisterSkillActivation(int skillId, DateTime activationUtc, bool countsTowardStats)
        {
            // 特化判定はここに置く。詠唱の AttrSkillId 経由でも AddDamage 経由でも必ず通るため、
            // 空振り(当たらなかった一撃)でも特定できる。記録の門とは関係なく判定する。
            UpdateSubProfessionFromReplacedSkill(skillId);
            RecordReplaceableSkillUse(skillId);

            if (countsTowardStats)
            {
                CountSkillActivation(skillId);
            }

            OnSkillActivated(new SkillActivatedEventArgs { CasterUuid = UUID, SkillId = skillId, ActivationDateTime = activationUtc });
        }

        private void CountSkillActivation(int skillId)
        {
            if (!SkillMetrics.TryGetValue(skillId, out var container))
            {
                container = new();

                // 名前は翻訳テーブルだけが決める。生テーブルの Name は言語が混ざっていて追従しない。
                var registeredName = CombatDataCatalog.GetSkillName(skillId);
                container.Damage.SetName(registeredName);
                container.Healing.SetName(registeredName);

                container.Damage.RegisterActivation();
                container.Healing.RegisterActivation();
            }
            else
            {
                container.Damage.RegisterActivation();
                container.Healing.RegisterActivation();
            }

            TotalCasts++;
        }

        protected virtual void OnSkillActivated(SkillActivatedEventArgs e)
        {
            SkillActivated?.Invoke(this, e);
        }

        public void RegisterSkillData(ESkillType skillType, long skillId, int skillLevel, long value, bool isCrit, bool isLucky, long hpLessenValue, long shieldBreak, bool isCauseLucky, EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode, bool isDead)
        {
            if(!SkillMetrics.TryGetValue(skillId, out var container))
            {
                var combatStats = new CombatStats();

                combatStats.SetName(CombatDataCatalog.GetSourceName(skillId));

                container = new();
                if (skillType == ESkillType.Damage)
                {
                    container.Damage = combatStats;
                }
                else if (skillType == ESkillType.Healing)
                {
                    container.Healing = combatStats;
                }
                else
                {
                    Serilog.Log.Warning($"RegisterSkillData SkillId {skillId} was an Unknown skill type and was not registered.");
                }

                combatStats.AddData(skillId, skillLevel, value, isCrit, isLucky, hpLessenValue, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead);

                SkillMetrics.TryAdd(skillId, container);
            }
            else
            {
                CombatStats combatStats;
                if (skillType == ESkillType.Damage)
                {
                    combatStats = container.Damage;
                }
                else if (skillType == ESkillType.Healing)
                {
                    combatStats = container.Healing;
                }
                else
                {
                    combatStats = new();
                    Serilog.Log.Warning($"RegisterSkillData SkillId {skillId} was an Unknown skill type and was not updated.");
                }

                if (string.IsNullOrEmpty(combatStats.Name))
                {
                    combatStats.SetName(CombatDataCatalog.GetSourceName(skillId));
                }

                combatStats.AddData(skillId, skillLevel, value, isCrit, isLucky, hpLessenValue, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead);
            }
        }

        /// <summary>
        /// 行動(与ダメ・回復・被弾)を1回受けて行動時刻を進める。前の行動から <see cref="InactiveGapSeconds"/> を超えた分を非行動に積む。
        /// 呼ぶのは <see cref="Encounter"/> の記録の入口だけで、起点の後の出来事に限る。
        /// 間は戦闘の経過(<paramref name="toCombatOffset"/> = <see cref="Encounter.ToCombatOffset"/>、自動一時停止で止まっていた区間を除く)で測る。
        /// </summary>
        internal void RecordCombatAction(DateTime arrivalUtc, Func<DateTime, TimeSpan?> toCombatOffset)
        {
            if (LastCombatActionTime is { } last
                && toCombatOffset(arrivalUtc) is { } current
                && toCombatOffset(last) is { } previous)
            {
                var idleSeconds = (current - previous).TotalSeconds;
                if (idleSeconds > InactiveGapSeconds)
                {
                    TotalInactiveTime += idleSeconds - InactiveGapSeconds;
                }
            }

            FirstCombatActionTime ??= arrivalUtc;
            LastCombatActionTime = arrivalUtc;
        }

        /// <summary>
        /// 有効な秒数(有効DPS の分母)。この人の最初の行動から時計の終点までのうち、行動の間が <see cref="InactiveGapSeconds"/> を
        /// 超えた分(最後の行動から終点までを含む)を除いた長さ。終点は戦闘の時計にそろえる(ライブは今、閉じた回は終了時刻、計測は窓の終わり)。
        /// 時刻は戦闘の経過(<paramref name="toCombatOffset"/> = <see cref="Encounter.ToCombatOffset"/>)で測る。
        /// 止まっていた区間を除かないと、自動一時停止の秒数が <see cref="InactiveGapSeconds"/> より短いときに有効DPS が DPS を下回る。
        /// </summary>
        public double GetActiveSeconds(CombatClockReading clock, Func<DateTime, TimeSpan?> toCombatOffset)
        {
            if (clock.EndUtc is null
                || FirstCombatActionTime is not { } first
                || toCombatOffset(first) is not { } firstOffset
                || clock.Elapsed <= firstOffset)
            {
                return 0d;
            }

            var end = clock.Elapsed;
            var openIdleSeconds = LastCombatActionTime is { } last && toCombatOffset(last) is { } lastOffset
                ? Math.Max((end - lastOffset).TotalSeconds - InactiveGapSeconds, 0d)
                : 0d;
            return Math.Max((end - firstOffset).TotalSeconds - TotalInactiveTime - openIdleSeconds, 0d);
        }

        public void AddDamage(long skillId, int skillLevel, long damage, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead)
        {
            if (damageType != EDamageType.Immune)
            {
                TotalDamage += (ulong)damage;
            }

            if (damageType == EDamageType.Absorbed)
            {
                TotalShieldBreak += (ulong)shieldBreak;
            }

            DamageStats.AddData(skillId, skillLevel, damage, isCrit, isLucky, hpLessen, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead);

            RegisterSkillData(ESkillType.Damage, skillId, skillLevel, damage, isCrit, isLucky, hpLessen, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead);
        }

        public void AddHealing(
            long skillId, int skillLevel, long damage, long overhealing, long effectiveHealing, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead)
        {
            TotalHealing += (ulong)damage;
            TotalOverhealing += (ulong)overhealing;

            HealingStats.AddData(skillId, skillLevel, damage, isCrit, isLucky, hpLessen, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead);

            RegisterSkillData(ESkillType.Healing, skillId, skillLevel, damage, isCrit, isLucky, overhealing, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead);
        }

        /// <summary>
        /// 置換後スキルIDから特化を確定する。置換する9特化＋光砕。
        ///
        /// <para>
        /// 根ノードの <c>TalentEffect [6, 置換元, 置換先]</c> による置換なので、
        /// 置換後のIDが飛んだ時点で装着が確定する。素のIDは未装着と区別がつかないため見ない。
        /// 光砕だけは置換を持たないが、装着中のみ特殊攻撃に随伴する 2450 を同じ扱いにしている。
        /// </para>
        /// </summary>
        public void UpdateSubProfessionFromReplacedSkill(int skillId)
        {
            var resolved = DataTypes.SpecDetectionTables.GetSubProfessionIdByReplacedSkillId(skillId);
            var subProfessionId = (int)resolved;
            if (subProfessionId <= 0
                || SubProfessionId == subProfessionId
                || !AcceptsSubProfessionId(subProfessionId))
            {
                return;
            }

            // 常設の食い違い検知。置換後スキルが示す特化と、控えてあるマーカーが違うなら記録する(同じ職の2特化のあいだだけ)。
            // 味方に発生させるスキル(安可など)を自分のものとして数えていると、この形で出る。
            if (SpecMarkerBuffId != 0
                && DataTypes.SpecDetectionTables.TryResolveSpecTalentBuff(
                    SpecMarkerBuffId, out _, out var markerSpec, out _)
                && markerSpec != resolved)
            {
                Diagnostics.SpecConflictProbe.CaptureReplacedSkillConflict(
                    skillId, resolved.ToString(), SpecMarkerBuffId, markerSpec.ToString(), UUID);
            }

            SetSubProfessionId(subProfessionId);

            // 行を作り直すだけで、周りにいる人には入れない(周りにいない人のダメージや付与からも決まるため)。
            PlayerRosterProjection.UpsertPlayer(UUID);
        }

        /// <summary>
        /// タレント由来のバフから特化を確定する。<b>このエンティティが術者であること</b>は
        /// 呼び出し側(<see cref="EncounterManager.ApplySpecFromTalentBuff"/>)が保証する。
        ///
        /// <para>
        /// <paramref name="markerBuffUuid"/> が 0 以外なら、それはマーカー本体(ツリーの根)。
        /// 除去を突き合わせるために実体UUIDを控える。
        /// <b>特化が変わらない再付与でも控え直す</b>ので、シーン切替の再付与にも追従する。
        /// </para>
        /// </summary>
        public void UpdateSubProfessionFromTalentBuff(int subProfessionId, int markerBuffId, int markerBuffUuid)
        {
            if (subProfessionId <= 0)
            {
                return;
            }

            if (markerBuffUuid != 0)
            {
                SpecMarkerBuffId = markerBuffId;
                SpecMarkerBuffUuid = markerBuffUuid;
            }

            if (SubProfessionId == subProfessionId)
            {
                return;
            }

            SetSubProfessionId(subProfessionId);

            // 術者が周りにいるとは限らない(敵や味方に付いたバフの術者)ので、行を作り直すだけにする。
            PlayerRosterProjection.UpsertPlayer(UUID);
        }

        /// <summary>
        /// 控えてあるマーカーバフが除去されたら、特化を未装着(クラスR1)へ戻す。
        /// 一致しなければ何もしないで <c>false</c>。
        ///
        /// <para>
        /// <b><see cref="SetSubProfessionUnknown"/> は使えない。</b> あれは
        /// <see cref="HasBuffSnapshot"/> も落とすので <see cref="IsSpecAbilityUnequipped"/> が
        /// 成立せず、クラスR1ではなく「観測できていない」になる。ここは
        /// 「全部見えている状態でマーカーだけ消えた」と分かっているので、受信済みフラグは残す。
        /// </para>
        /// </summary>
        public bool ClearSubProfessionFromMarkerRemoval(int buffUuid)
        {
            if (buffUuid == 0 || SpecMarkerBuffUuid != buffUuid)
            {
                return false;
            }

            SubProfessionId = 0;
            SpecMarkerBuffId = 0;
            SpecMarkerBuffUuid = 0;
            return true;
        }

        /// <summary>
        /// シーズンタレントの型の根ノードのバフから、有効化している型を確定する。
        /// <b>このエンティティが術者であること</b>は呼び出し側(<see cref="EncounterManager.ApplySeasonTalentFromRootBuff"/>)が保証する。
        /// 同じ型の再付与でも実体UUIDを控え直すので、シーン切替の再付与にも追従する。
        /// </summary>
        public void UpdateSeasonTalentFromRootBuff(int rootBuffId, int rootBuffUuid)
        {
            if (rootBuffId <= 0)
            {
                return;
            }

            if (rootBuffUuid != 0)
            {
                SeasonTalentBuffUuid = rootBuffUuid;
            }

            if (SeasonTalentBuffId == rootBuffId)
            {
                return;
            }

            SeasonTalentBuffId = rootBuffId;
            Services.PartyMemberCache.Instance.SetSeasonTalent(Utils.UuidToEntityId(UUID), rootBuffId);

            // 術者が周りにいるとは限らないので、行を作り直すだけにする。
            PlayerRosterProjection.UpsertPlayer(UUID);
        }

        /// <summary>
        /// 控えてある根ノードのバフが除去されたら、型を無効へ戻す。一致しなければ何もしないで <c>false</c>。
        /// <see cref="SetSeasonTalentUnknown"/> は使えない(受信済みの印まで落とし、無効ではなく「観測できていない」になる)。
        /// </summary>
        public bool ClearSeasonTalentFromRootRemoval(int buffUuid)
        {
            if (buffUuid == 0 || SeasonTalentBuffUuid != buffUuid)
            {
                return false;
            }

            SeasonTalentBuffId = 0;
            SeasonTalentBuffUuid = 0;
            return true;
        }

        /// <summary>
        /// AOI出現時に届く全バフスナップショットを特化判定に通す。
        ///
        /// <para>
        /// 1件ずつ術者へ帰属させるので、このエンティティが持っているだけの他人のバフは
        /// その術者の判定になり、ここには効かない。
        /// </para>
        ///
        /// <para>
        /// スナップショットは「いまこのエンティティが持っている全バフ」なので、
        /// 処理後もこのエンティティの特化が未確定なら
        /// <b>自分が張ったタレントバフを1つも持っていない＝未装着(Rank1)が確定する</b>。
        /// 差分だけでは「まだ観測していない」と区別できないため、この経路だけが Rank1 を成立させる。
        /// </para>
        /// </summary>
        public void ApplyBuffSnapshotForSpec(
            IReadOnlyList<(int BaseId, int BuffUuid, long FireUuid, int FightSourceType, int SourceConfigId)> buffs)
        {
            HasBuffSnapshot = true;
            // シーズンタレントの型も同じスナップショットで決める(無効が言えるのもこの経路だけ)。
            HasSeasonTalentSnapshot = true;
            var manager = EncounterManager.Current;
            for (var index = 0; index < buffs.Count; index++)
            {
                manager?.ApplySpecFromTalentBuff(
                    buffs[index].BaseId, buffs[index].BuffUuid, buffs[index].FireUuid, buffs[index].FightSourceType,
                    UUID, buffs[index].SourceConfigId);
                manager?.ApplySeasonTalentFromRootBuff(buffs[index].BaseId, buffs[index].BuffUuid, buffs[index].FireUuid);
            }

            ReplaceRootBuffIds(buffs.Select(buff => buff.BaseId));

            // 自分が術者のタレントバフが1件も無かった＝未装着。PTメンバーぶんはキャッシュにも残す。
            if (SubProfessionId == 0 && ProfessionId > 0)
            {
                Services.PartyMemberCache.Instance.SetSpecAbilityUnequipped(Utils.UuidToEntityId(UUID));
                PlayerRosterProjection.AddOrUpdateNearbyPlayer(UUID);
            }

            // 根ノードのバフが1件も無かった＝シーズンタレントの型が無効。
            if (SeasonTalentBuffId == 0 && ProfessionId > 0)
            {
                Services.PartyMemberCache.Instance.SetSeasonTalentInactive(Utils.UuidToEntityId(UUID));
                PlayerRosterProjection.AddOrUpdateNearbyPlayer(UUID);
            }
        }

        /// <summary>
        /// 自分のバフ集合を受信したとみなした瞬間を記録する(自分のバフの付与を含む差分のたびに呼ばれる)。
        ///
        /// <para>
        /// 特化の未装着・潜在心相晶の無効の判定と、根のバフの一覧(<see cref="RootBuffIds"/>)がこれを使う。
        /// 自分のバフの付与を含む差分なら何でも立つので、途中で起動した回は、入場で付いたバフ(根のバフなど)が
        /// 次のマップ移動まで欠けている。
        /// </para>
        /// </summary>
        public void MarkSelfBuffStreamReceived()
        {
            // 根のバフは今乗っているバフから作り直す(このデルタの付与は先に処理されている)。
            // マップ移動では今乗っているバフが空になり、入場の差分で全部が付き直すので、受けるたびに作り直す。
            ReplaceRootBuffIds(Services.ActiveBuffStore.Instance.GetActive(UUID).Select(buff => buff.BaseId));

            // シーズンタレントの印も同じ時点で立てる(特化と同じ扱い)。
            HasSeasonTalentSnapshot = true;
            HasBuffSnapshot = true;
        }

        public void AddTakenDamage(
            long skillId, int skillLevel, long damage, long hpLessen, long shieldBreak,
            EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode,
            bool isCrit, bool isLucky, bool isCauseLucky, bool isMiss, bool isDead)
        {
            TakenStats.AddData(skillId, skillLevel, damage, isCrit, isLucky, hpLessen, shieldBreak, isCauseLucky, damageElement, damageType, damageMode, isDead);
            TakenStats.MarkTakenHitRecorded();
        }

        /// <param name="carriesBuffInfo">
        /// このイベントが <c>BuffInfo</c>(または <c>BuffChange</c>)を伴っていたか。
        /// <c>false</c> のとき渡ってくる 0 は「送られてこなかった」という意味なので、
        /// 既に知っているバフの持続・層・付与時刻を上書きしない。
        /// </param>
        public void NotifyBuffEvent(EBuffEventType buffEventType, int buffUuid, int baseId, int level, long fireUuid, string entityCasterName, int layer, int duration, int sourceConfigId, DateTime? creationTime, ExtraPacketData extraPacketData, BuffEventPayload payload, int? fightSourceType)
        {
            var carriesBuffInfo = payload != BuffEventPayload.None;
            if (buffEventType == EBuffEventType.BuffEventRemove)
            {
                // 前の回で付いた実体なら、控えを消す前にその記録を写して番号を引き継ぐ(根のバフの一覧から外すのに番号が要る)。
                if (!BuffEvents.TryGetValue((ulong)buffUuid, out var buffEvent)
                    && !TryCopyLiveBuffEvent(buffUuid, out buffEvent))
                {
                    buffEvent = new BuffEvent(buffUuid);
                }
                buffEvent.SetRemoveTime(extraPacketData.ArrivalTime);

                BuffEvents.Remove((ulong)buffUuid);
                AddRecentBuffEventHistory(buffUuid, buffEvent);

                // ライブ表示用の状態はエンカウンター境界を跨いで保持する。
                Services.ActiveBuffStore.Instance.Remove(UUID, (ulong)buffUuid);

                // 除去は BaseId を運ばないので、付与で控えたバフの番号で外す。
                RemoveRootBuff(buffEvent.BaseId);
            }
            else
            {
                var isKnownBuff = BuffEvents.TryGetValue((ulong)buffUuid, out var buffEvent);

                // 前の回で付いた実体への、中身(BuffInfo)を伴わない通知(変化・中身なし)は番号を運ばない。
                // 番号 0 の記録を作ると控えの項目がそれに置き換わり、効いているバフが一覧から消えるので、控えの記録を写して引き継ぐ。
                // 写した後は知っている実体として扱う(変化の通知だけが層・持続・付与時刻を書き換える)。
                if (!isKnownBuff
                    && payload != BuffEventPayload.BuffInfo
                    && TryCopyLiveBuffEvent(buffUuid, out var liveBuffEvent))
                {
                    buffEvent = liveBuffEvent;
                    isKnownBuff = true;
                }

                if (!isKnownBuff)
                {
                    buffEvent = new BuffEvent(buffUuid, baseId, level, fireUuid, entityCasterName, layer, duration, sourceConfigId, fightSourceType ?? BuffEvent.UnknownFightSourceType);
                }
                else if (carriesBuffInfo)
                {
                    buffEvent.SetEvent(buffUuid, baseId, level, fireUuid, entityCasterName, layer, duration, sourceConfigId, fightSourceType);
                }

                // BuffInfo を伴わない回は、持続・層・付与時刻を「知らされていない」まま据え置く。
                // ここで書き込むと、10秒のバフが持続0(=無期限)に化けて除去が来るまで消えなくなり、
                // 付与時刻が毎回ずれることで ActiveBuffStore の同一実体判定も落ちて残り時間が巻き戻る。
                if (!isKnownBuff || carriesBuffInfo)
                {
                    if (creationTime != null)
                    {
                        var diff = extraPacketData.ArrivalTime.Subtract(creationTime.Value).TotalSeconds;
                        if (diff < -5 || diff > 0)
                        {
                            buffEvent.SetAddTime(creationTime.Value);
                        }
                        else
                        {
                            buffEvent.SetAddTime(extraPacketData.ArrivalTime);
                        }
                    }
                    else
                    {
                        buffEvent.SetAddTime(extraPacketData.ArrivalTime);
                    }
                }

                BuffEvents[(ulong)buffUuid] = buffEvent;
                AddRecentBuffEventHistory(buffUuid, buffEvent);

                // ライブ表示用の状態はエンカウンター境界を跨いで保持する。
                // creationTime はサーバが名乗る付与時刻。ストア側が「もう何秒経ったか」を
                // 差し引くのに使う(AOI出現で受け取る、見る前から乗っているバフのため)。
                Services.ActiveBuffStore.Instance.AddOrUpdate(
                    UUID, (ulong)buffUuid, buffEvent, creationTime, createsInstance: payload == BuffEventPayload.BuffInfo);

                // 表に名前の無いバフは、付与元をたどった由来の名前で出す。付与元が届くのは付与だけで、たどるのはこの通知の最後。
                if (payload == BuffEventPayload.BuffInfo)
                {
                    Services.BuffOriginResolver.Instance.Enqueue(buffEvent, extraPacketData.ArrivalTime);
                    AddRootBuff(baseId);
                }
            }
        }

        /// <summary>
        /// 前の回(エンカウンター)で付いて、今の回の記録(<see cref="BuffEvents"/>)がまだ知らない実体の記録の写し。
        /// 回をまたいで持つ控え(<see cref="Services.ActiveBuffStore"/>)の項目から作る。控えに無ければ偽。
        /// 写しにするのは、控えが持つ記録は前の回のもので、書き換えると保持中の前の回の表示まで変わるため。
        /// </summary>
        private bool TryCopyLiveBuffEvent(int buffUuid, out BuffEvent buffEvent)
        {
            if (Services.ActiveBuffStore.Instance.TryGetBuffEvent(UUID, (ulong)buffUuid, out var liveBuffEvent))
            {
                buffEvent = liveBuffEvent.CopyForNextEncounter();
                return true;
            }

            buffEvent = null!;
            return false;
        }

        /// <summary>
        /// 全部を受けたバフの番号から、根のバフの一覧(<see cref="RootBuffIds"/>)を作り直す。中身が同じなら差し替えない。
        /// </summary>
        private void ReplaceRootBuffIds(IEnumerable<int> buffIds)
        {
            var rootBuffIds = buffIds.Where(CombatDataCatalog.IsRootBuff).Distinct().Order().ToArray();
            if (RootBuffIds is { } current && current.AsSpan().SequenceEqual(rootBuffIds))
            {
                return;
            }

            RootBuffIds = rootBuffIds;
            NotifyRootBuffsChanged();
        }

        /// <summary>付与された根のバフを一覧へ足す。全部をまだ受けていなければ何もしない(全部を受けたときに作る)。</summary>
        private void AddRootBuff(int buffId)
        {
            if (RootBuffIds is not { } current
                || !CombatDataCatalog.IsRootBuff(buffId)
                || Array.BinarySearch(current, buffId) >= 0)
            {
                return;
            }

            RootBuffIds = [.. current.Append(buffId).Order()];
            NotifyRootBuffsChanged();
        }

        /// <summary>外れた根のバフを一覧から外す。同じ番号の別の実体がまだ乗っていれば残す(付け直しで新しい実体が先に付く)。</summary>
        private void RemoveRootBuff(int buffId)
        {
            if (RootBuffIds is not { } current
                || Array.BinarySearch(current, buffId) < 0
                || Services.ActiveBuffStore.Instance.HasActive(UUID, buffId))
            {
                return;
            }

            RootBuffIds = [.. current.Where(id => id != buffId)];
            NotifyRootBuffsChanged();
        }

        /// <summary>根のバフの一覧が変わった。プレイヤーなら名簿の行を作り直す(バフの変化は名簿を作り直す合図に無い)。</summary>
        private void NotifyRootBuffsChanged()
        {
            if (Utils.UuidToEntityType(UUID) == (long)EEntityType.EntChar)
            {
                PlayerRosterProjection.UpsertPlayer(UUID);
            }
        }

        /// <summary>
        /// 自分の職業ごとの取ったタレントの木の節を、全体コンテナの一覧で置き換える。
        /// 名簿の行は呼び出し側(自分の全体コンテナの処理)が作り直す。
        /// </summary>
        /// <param name="nodeIdsByProfession">職業 → 取った節の番号(昇順・重なりなし)。</param>
        public void ReplaceTalentNodes(IReadOnlyDictionary<int, int[]> nodeIdsByProfession)
        {
            SetTalentNodes(new Dictionary<int, int[]>(nodeIdsByProfession.Count), nodeIdsByProfession);
        }

        /// <summary>
        /// 自分の職業ごとの取ったタレントの木の節に、差分を重ねる。差分は変わった職業の一覧を丸ごと運ぶ。
        /// まだ全体コンテナを受けていなければ、差分で届いた職業だけを持つ。名簿の行は呼び出し側(自分の差分の処理)が作り直す。
        /// </summary>
        /// <param name="replacesExisting">差分が前の中身を捨てて置き換えるか。</param>
        /// <param name="removedProfessionIds">外れた職業。</param>
        /// <param name="changedNodeIds">一覧が届いた職業 → 取った節の番号(昇順・重なりなし)。</param>
        public void ApplyTalentNodeChanges(
            bool replacesExisting,
            IReadOnlyList<int> removedProfessionIds,
            IReadOnlyDictionary<int, int[]> changedNodeIds)
        {
            var next = replacesExisting || TalentNodeIdsByProfession is null
                ? new Dictionary<int, int[]>(changedNodeIds.Count)
                : new Dictionary<int, int[]>(TalentNodeIdsByProfession);
            foreach (var professionId in removedProfessionIds)
            {
                next.Remove(professionId);
            }

            SetTalentNodes(next, changedNodeIds);
        }

        /// <summary>
        /// <paramref name="next"/> に届いた職業の一覧を入れて差し替える。中身が前と同じ職業は前の配列を使い(名簿の行の参照が変わらない)、
        /// どの職業も前と同じなら差し替えない。
        /// </summary>
        private void SetTalentNodes(Dictionary<int, int[]> next, IReadOnlyDictionary<int, int[]> nodeIdsByProfession)
        {
            var current = TalentNodeIdsByProfession;
            foreach (var (professionId, nodeIds) in nodeIdsByProfession)
            {
                next[professionId] = current is not null
                    && current.TryGetValue(professionId, out var known)
                    && known.AsSpan().SequenceEqual(nodeIds)
                        ? known
                        : nodeIds;
            }

            if (current is not null
                && current.Count == next.Count
                && next.All(pair => current.TryGetValue(pair.Key, out var known) && ReferenceEquals(known, pair.Value)))
            {
                return;
            }

            TalentNodeIdsByProfession = next;
        }

        /// <summary>
        /// プレイヤーの技の開始のうち、バフを付けないアビリティの節の技の置き換えに出てくる技を控える(元の技 → 最後に使ったほう)。
        /// 元の技を使ったら置き換えていない(その節を取っていない)側へ戻す。
        /// </summary>
        private void RecordReplaceableSkillUse(int skillId)
        {
            if (Utils.UuidToEntityType(UUID) != (long)EEntityType.EntChar
                || !CombatDataCatalog.TryGetReplaceableSkillOriginal(skillId, out var originalSkillId))
            {
                return;
            }

            var current = LastReplaceableSkills;
            if (current is not null && current.TryGetValue(originalSkillId, out var lastSkillId) && lastSkillId == skillId)
            {
                return;
            }

            var next = current is null ? new Dictionary<int, int>() : new Dictionary<int, int>(current);
            next[originalSkillId] = skillId;
            LastReplaceableSkills = next;
            PlayerRosterProjection.UpsertPlayer(UUID);
        }

        public void AddBuffEventAttribute(int buffUuid, string attributeName, object? attributeValue)
        {
            if (!BuffEvents.TryGetValue((ulong)buffUuid, out var buffEvent))
            {
                // 前の回で付いた実体なら、控えの記録を写して番号を引き継ぐ。番号 0 の記録を作ると、
                // 後から来る変化・中身なしの通知が知っている実体としてそれを控えへ入れ、効いているバフが一覧から消える。
                if (!TryCopyLiveBuffEvent(buffUuid, out buffEvent))
                {
                    buffEvent = new BuffEvent(buffUuid);
                }

                if (attributeName == "AttrShieldList" && attributeValue != null)
                {
                    var shieldInfo = (ShieldInfo)attributeValue;
                    TotalShield += (ulong)shieldInfo.InitialValue;
                }
            }
            buffEvent.AddData(attributeName, attributeValue);

            BuffEvents[(ulong)buffUuid] = buffEvent;
        }

        public void SetAttrKV(string key, object value)
        {
            Attributes[key] = value;
        }

        public object? GetAttrKV(string key)
        {
            var value = Attributes.TryGetValue(key, out var val) ? val : null;
            return value;
        }

        private bool HasPositiveAttribute(string key)
        {
            return GetAttrKV(key) switch
            {
                int value => value > 0,
                long value => value > 0,
                uint value => value > 0,
                ulong value => value > 0,
                short value => value > 0,
                ushort value => value > 0,
                byte value => value > 0,
                sbyte value => value > 0,
                _ => false
            };
        }

        public bool IsHpUpdatedHandlerSubscribed(HpUpdatedEventHandler handler)
        {
            Delegate[]? invocationList = HpUpdated?.GetInvocationList();
            return invocationList != null && invocationList.Contains(handler);
        }

        public bool IsThreatListUpdatedHandlerSubscribed(ThreatListUpdatedEventHandler handler)
        {
            Delegate[]? invocationList = ThreatListUpdated?.GetInvocationList();
            return invocationList != null && invocationList.Contains(handler);
        }
    }

    public enum EMonsterType : int
    {
        Unknown = -1,
        Monster = 0,
        Elite = 1,
        Boss = 2
    }

    public class SkillActivatedEventArgs : EventArgs
    {
        public long CasterUuid { get; set; }
        public int SkillId { get; set; }

        /// <summary>発動が届いたメッセージの到着時刻(UTC)。</summary>
        public DateTime ActivationDateTime { get; set; }
    }

    public class HpUpdatedEventArgs : EventArgs
    {
        public long EntityUuid { get; set; }
        public long Hp { get; set; }
        public long MaxHp { get; set; }
        public DateTime UpdateDateTime { get; set; }
    }

    public class ThreatInfo
    {
        public long EntityUuid { get; set; }
        public long ThreatValue { get; set; }
    }

    public class ThreatListUpdatedEventArgs : EventArgs
    {
        public long EntityUuid { get; set; }
        public List<ThreatInfo> ThreatInfoList { get; set; } = new();
    }

    public class BuffUpdatedEventArgs : EventArgs
    {
        public long EntityUuid { get; set; }
        public EBuffEventType BuffEventType { get; set; }
        public int BuffUuid { get; set; }
        public int BaseId { get; set; }
        public int Level { get; set; }
        public long FireUuid { get; set; }
        public int Layer { get; set; }
        public int Duration { get; set; }
        public int SourceConfigId { get; set; }
        public string EntityCasterName { get; set; } = null!;

        /// <summary>通知が届いたメッセージの到着時刻(UTC)。</summary>
        public DateTime UpdateDateTime { get; set; }
        public DateTime? CreationDateTime { get; set; }

        /// <summary>
        /// 通知が運んだ中身。<c>None</c> の回の <see cref="BaseId"/> 以下の 0 は「送られてこなかった」で、バフの状態ではない。
        /// </summary>
        public BuffEventPayload Payload { get; set; }
    }

    public class AttributeUpdatedEventArgs : EventArgs
    {
        public long EntityUuid { get; set; }
        public Entity? Entity { get; set; }
        public string AttributeName { get; set; } = null!;
        public object AttributeValue { get; set; } = null!;
    }

    public enum ESkillType : int
    {
        Unknown = 0,
        Damage = 1,
        Healing = 2,
        Taken = 3
    }

    public class MetricsContainer
    {
        /// <summary>スキル単位の与ダメージ統計。</summary>
        public CombatStats Damage { get; set; } = new();

        /// <summary>スキル単位の回復統計。</summary>
        public CombatStats Healing { get; set; } = new();

        /// <summary>
        /// この鍵がスキルIDではなくバフIDであることを示す。
        /// 畳み先が決まらなかったバフは生のIDのまま行になるので、名前を
        /// <c>GetSkillName</c> ではなく <c>GetBuffName</c> で引く必要がある。
        /// <c>SkillTable</c> と <c>BuffTable</c> は90IDが重複するため、種別を持たないと取り違える。
        /// </summary>
        public bool IsBuffSource { get; set; }

        /// <summary>
        /// 見出し表で名前を持たない行の着地先の種類(<see cref="Services.SourceLandingResolver"/>)。
        /// 最初に着いた先で決め、上書きしない。履歴でも同じ名前を出すので保存する。
        /// </summary>
        public SourceLandingKind LandingKind { get; set; }

        /// <summary>着地先のID。<see cref="LandingKind"/> がオプションならバフID、技なら技ID。</summary>
        public int LandingId { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public SourceLanding Landing => new(LandingKind, LandingId);
    }

    public class CombatStats : System.ICloneable
    {
        public string Name { get; private set; } = null!;

        /// <summary>
        /// メーターの行のキー。<c>ownerId</c> と <c>HitEventId</c> を詰めた値で、
        /// <c>CombatDataCatalog.FormatSourceKey</c> で <c>ownerId:枝番</c> に戻る。
        /// </summary>
        public long Id { get; private set; }
        public int Level { get; private set; }

        public ulong ValueTotal { get; private set; }
        public ulong ValueNormalTotal { get; private set; }
        public ulong ValueCritTotal { get; private set; }
        public ulong ValueLuckyTotal { get; private set; }
        public ulong ValueCritLuckyTotal { get; private set; }
        public ulong ValueImmuneTotal { get; private set; }
        public long ValueMax { get; private set; }
        public long ValueMin { get; private set; }
        public double ValueAverage { get; private set; }

        public ulong HpLessenTotal { get; private set; }
        public ulong ShieldBreakTotal { get; private set; }

        public uint MissCount { get; private set; }
        public double MissRate { get; private set; }

        public uint CritCount { get; private set; }
        public double CritRate { get; private set; }

        public uint LuckyCount { get; private set; }
        public uint LuckyHitCount { get; private set; }
        public double LuckyRate { get; private set; }

        public uint CritLuckyCount { get; private set; }

        public uint NormalCount { get; private set; }
        public double NormalRate { get; private set; }
        public uint KillCount { get; private set; }
        public ulong HitsCount { get; private set; }
        public uint CastsCount { get; private set; }
        public ulong ImmuneCount { get; private set; }

        /// <summary>
        /// 被弾1件ずつの DB の形。<see cref="Entity.TakenStats"/> にだけ、保存の間と DB から読んだ直後に入る。
        /// 実行中の被弾は回の記録(<see cref="TakenHitRecord"/>)に持つ。
        /// </summary>
        public List<SkillSnapshot>? SkillSnapshots { get; private set; }

        private bool _hasTakenHits;

        /// <summary>Newtonsoft の条件付き直列化。無ければ <c>SkillSnapshots</c> を項目ごと書かない。</summary>
        public bool ShouldSerializeSkillSnapshots() => SkillSnapshots is { Count: > 0 };

        internal void AddSkillSnapshotForSave(SkillSnapshot snapshot)
        {
            (SkillSnapshots ??= []).Add(snapshot);
        }

        /// <summary>DB の形の被弾を取り出して手放す。無ければ null。被弾があった印(<see cref="HasSkillSnapshots"/>)は残る。</summary>
        internal List<SkillSnapshot>? TakeSkillSnapshots()
        {
            var taken = SkillSnapshots;
            SkillSnapshots = null;
            _hasTakenHits |= taken is { Count: > 0 };
            return taken;
        }

        internal void MarkTakenHitRecorded()
        {
            _hasTakenHits = true;
        }

        /// <summary>被弾の記録が1件以上あるか。</summary>
        public bool HasSkillSnapshots()
        {
            return _hasTakenHits || SkillSnapshots is { Count: > 0 };
        }

        /// <summary>
        /// DPS/HPS推移グラフの材料の DB の形(読むのは保存のときだけ、書くのは DB から読むときだけ)。
        /// キーは戦闘の経過(<see cref="Encounter.ToCombatOffset"/>、自動一時停止で止まっていた区間を除く)の
        /// 1秒の区切り(<c>max(ceil(経過秒) - 1, 0)</c>)、値はその1秒に入った合計。止まっていた間は区切りが進まない。
        ///
        /// <para>
        /// グラフは1秒ごとの合計しか使わないので、1件ずつは持たない。
        /// 溜めるのはエンティティ直下の <c>DamageStats</c> / <c>HealingStats</c> だけで、
        /// 足す条件(値が正、ダメージなら <c>Immune</c> 以外)は呼び出し側が決める。
        /// </para>
        /// </summary>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Dictionary<int, long> PerSecondTotals
        {
            get
            {
                lock (_perSecondTotalsGate)
                {
                    var totals = new Dictionary<int, long>(_perSecondCount);
                    for (var index = 0; index < _perSecondCount; index++)
                    {
                        totals.Add(_perSecondEntries[index].Second, _perSecondEntries[index].Value);
                    }

                    return totals;
                }
            }
            private set
            {
                lock (_perSecondTotalsGate)
                {
                    _perSecondEntries = new PerSecondEntry[value.Count];
                    _perSecondCount = 0;
                    foreach (var (second, total) in value.OrderBy(pair => pair.Key))
                    {
                        _perSecondEntries[_perSecondCount++] = new PerSecondEntry(second, total);
                    }
                }
            }
        }

        /// <summary>
        /// 1秒ごとの合計に足したイベントのうち、最も遅い到着時刻。
        /// グラフの終端がこれより前なら、ここまで伸ばす。
        /// </summary>
        public DateTime? LastPerSecondTimestamp { get; private set; }

        private readonly object _perSecondTotalsGate = new();

        /// <summary>1秒ごとの合計。秒の昇順。解放は回と一緒。グラフが全部を足し直すので、圧縮せず配列で持つ。</summary>
        private PerSecondEntry[] _perSecondEntries = [];
        private int _perSecondCount;

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PerSecondEntry(int second, long value)
        {
            public readonly int Second = second;
            public long Value = value;
        }

        public bool ShouldSerializePerSecondTotals() => _perSecondCount > 0;

        public bool ShouldSerializeLastPerSecondTimestamp() => LastPerSecondTimestamp.HasValue;

        /// <param name="combatOffset">出来事の戦闘の経過(<see cref="Encounter.ToCombatOffset"/>)。</param>
        /// <param name="timestamp">出来事の到着時刻(UTC)。<see cref="LastPerSecondTimestamp"/> に使う。</param>
        public void AddPerSecondValue(TimeSpan combatOffset, DateTime timestamp, long value)
        {
            var seconds = Math.Max(combatOffset.TotalSeconds, 0d);
            var second = Math.Max((int)Math.Ceiling(seconds) - 1, 0);

            lock (_perSecondTotalsGate)
            {
                AddPerSecondEntryNoLock(second, value);
                if (LastPerSecondTimestamp is not { } last || timestamp > last)
                {
                    LastPerSecondTimestamp = timestamp;
                }
            }
        }

        private void AddPerSecondEntryNoLock(int second, long value)
        {
            // 到着はほぼ時刻順なので、末尾から探す。
            var index = _perSecondCount;
            while (index > 0 && _perSecondEntries[index - 1].Second > second)
            {
                index--;
            }

            if (index > 0 && _perSecondEntries[index - 1].Second == second)
            {
                _perSecondEntries[index - 1].Value += value;
                return;
            }

            if (_perSecondCount == _perSecondEntries.Length)
            {
                Array.Resize(ref _perSecondEntries, Math.Max(_perSecondCount + _perSecondCount / 2, 8));
            }

            Array.Copy(_perSecondEntries, index, _perSecondEntries, index + 1, _perSecondCount - index);
            _perSecondEntries[index] = new PerSecondEntry(second, value);
            _perSecondCount++;
        }

        /// <summary>1秒ごとの合計があれば、最後の到着時刻を返す。</summary>
        public bool TryGetLastPerSecondTimestamp(out DateTime lastTimestamp)
        {
            lock (_perSecondTotalsGate)
            {
                if (_perSecondCount > 0 && LastPerSecondTimestamp is { } last)
                {
                    lastTimestamp = last;
                    return true;
                }

                lastTimestamp = default;
                return false;
            }
        }

        /// <summary>
        /// 1秒ごとの合計を、<paramref name="intervalSeconds"/> 秒の区切りごとに <paramref name="bucketTotals"/> へ足す。
        /// 1秒の鍵 k は (k, k+1] 秒なので、区切り j に入るのは k / N == j の鍵。<paramref name="bucketCount"/> より後の区切りは足さない。
        /// </summary>
        public void AddPerSecondTotalsTo(double[] bucketTotals, int bucketCount, int intervalSeconds)
        {
            lock (_perSecondTotalsGate)
            {
                for (var index = 0; index < _perSecondCount; index++)
                {
                    var bucket = _perSecondEntries[index].Second / intervalSeconds;
                    if (bucket >= bucketCount)
                    {
                        break;
                    }

                    bucketTotals[bucket] += _perSecondEntries[index].Value;
                }
            }
        }

        /// <summary>
        /// 属性ごとの累計。スキル詳細の属性列(割合)の材料。
        ///
        /// <para>
        /// 足す条件は <see cref="ValueTotal"/> と同じで、<c>Immune</c> と <c>Miss</c> は足さない。
        /// したがって全部の合計は <see cref="ValueTotal"/> に一致し、行の中で割合を足すと 100% になる。
        /// </para>
        /// </summary>
        public Dictionary<EDamageProperty, ulong> ValueTotalByElement { get; private set; } = new();

        /// <summary>
        /// 物理・魔法ごとの累計。足す条件は <see cref="ValueTotalByElement"/> と同じ。
        /// <c>DamageNormal</c> は「物理でも魔法でもない」で、表示では「無分類」に当たる。
        /// </summary>
        public Dictionary<EDamageMode, ulong> ValueTotalByMode { get; private set; } = new();

        private readonly object _valueBreakdownGate = new();

        public bool ShouldSerializeValueTotalByElement() => ValueTotalByElement.Count > 0;

        public bool ShouldSerializeValueTotalByMode() => ValueTotalByMode.Count > 0;

        public void AddValueBreakdown(EDamageProperty damageElement, EDamageMode damageMode, long value)
        {
            lock (_valueBreakdownGate)
            {
                ValueTotalByElement[damageElement] =
                    ValueTotalByElement.GetValueOrDefault(damageElement) + (ulong)value;
                ValueTotalByMode[damageMode] =
                    ValueTotalByMode.GetValueOrDefault(damageMode) + (ulong)value;
            }
        }

        public KeyValuePair<EDamageProperty, ulong>[] GetValueTotalByElementCopy()
        {
            lock (_valueBreakdownGate)
            {
                return ValueTotalByElement.ToArray();
            }
        }

        public KeyValuePair<EDamageMode, ulong>[] GetValueTotalByModeCopy()
        {
            lock (_valueBreakdownGate)
            {
                return ValueTotalByMode.ToArray();
            }
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public void SetName(string name)
        {
            Name = name;
        }

        public void RegisterActivation()
        {
            CastsCount++;
        }

        private void AddValue(long value)
        {
            ValueTotal += (ulong)value;

            if (value > 0)
            {
                if (value < ValueMin)
                {
                    ValueMin = value;
                }
                if (value > ValueMax)
                {
                    ValueMax = value;
                }
            }
        }

        private void AddNormalValue(long value)
        {
            ValueNormalTotal += (ulong)value;
        }

        private void AddCritValue(long value)
        {
            ValueCritTotal += (ulong)value;
        }

        private void AddLuckyValue(long value)
        {
            ValueLuckyTotal += (ulong)value;
        }

        private void AddImmuneValue(long value)
        {
            ValueImmuneTotal += (ulong)value;
        }

        /// <summary>
        /// 1件を足す。持つのは合計と回数だけで、秒間値は持たない(読むときに戦闘の時計で割る。<see cref="Encounter.ReadCombatClock()"/>)。
        /// </summary>
        public void AddData(long skillId, int level, long value, bool isCrit, bool isLucky, long hpLessenValue, long shieldBreak, bool isCauseLucky, EDamageProperty damageElement, EDamageType damageType, EDamageMode damageMode, bool isDead)
        {
            Id = skillId;
            Level = level;

            if (damageType == EDamageType.Immune)
            {
                ImmuneCount++;
                AddImmuneValue(value);
            }
            else if (damageType == EDamageType.Miss)
            {
                MissCount++;
                if (MissCount > 0 && HitsCount == 0)
                {
                    MissRate = 100.0;
                }
                else
                {
                    MissRate = MissCount > 0 ? Math.Round(((double)MissCount / (double)HitsCount) * 100.0, 0) : 0.0;
                }
            }
            else
            {
                AddValue(value);

                // 内訳は ValueTotal と同じ門の内側で足す。合計すると ValueTotal に一致する。
                AddValueBreakdown(damageElement, damageMode, value);

                HpLessenTotal += (ulong)hpLessenValue;
                ShieldBreakTotal += (ulong)shieldBreak;

                if (isCrit)
                {
                    CritCount++;
                    AddCritValue(value);
                }

                if (isCauseLucky)
                {
                    LuckyCount++;
                }
                if (isLucky)
                {
                    LuckyHitCount++;
                    AddLuckyValue(value);
                }

                if (!isCrit && !isLucky)
                {
                    NormalCount++;
                    AddNormalValue(value);
                }
                else if (isCrit && isLucky)
                {
                    CritLuckyCount++;
                    ValueCritLuckyTotal += (ulong)value;
                }

                if (isDead)
                {
                    KillCount++;
                }

                HitsCount++;
            }

            ValueAverage = HitsCount > 0 ? Math.Round(((double)ValueTotal / (double)HitsCount), 0) : 0.0;
            // 率は小数点以下2位まで持つ。表示側は F2 で固定2桁にする。
            CritRate = HitsCount > 0 ? Math.Round(((double)CritCount / (double)HitsCount) * 100.0, 2) : 0.0;
            NormalRate = HitsCount > 0 ? Math.Round(((double)NormalCount / (double)HitsCount) * 100.0, 2) : 0.0;
            LuckyRate = HitsCount > 0 && HitsCount >= LuckyHitCount ? Math.Round(((double)LuckyHitCount / Math.Clamp((double)(HitsCount - LuckyHitCount), 1, double.MaxValue)) * 100.0, 2) : 0.0;
        }
    }

    /// <summary>
    /// プレイヤーの被弾1件の DB の形。項目の意味は <see cref="TakenHitRecord"/> と同じ。
    /// 実行中は持たず、保存のときに写し、履歴を読むときに <see cref="TakenHitRecord"/> へ戻す。
    /// </summary>
    public class SkillSnapshot
    {
        public long OtherUUID { get; set; }

        public long Id { get; set; }

        public long Value { get; set; }

        public EDamageProperty? DamageElement { get; set; }
        public EDamageMode DamageMode { get; set; }

        public bool IsKill { get; set; }

        public DateTime? Timestamp { get; set; } = null;

        /// <summary><b>0 は通し番号を持たない古い記録</b>で、ログには載らない。</summary>
        public long Sequence { get; set; }

        public int OwnerId { get; set; }

        public EDamageSource DamageSource { get; set; }

        public int BuffSourceSkillId { get; set; }

        public int SummonSourceSkillId { get; set; }
    }

    /// <summary>バフの通知が運んだ中身。</summary>
    public enum BuffEventPayload
    {
        /// <summary>中身なし。持続・層・付与時刻を知らされていない。</summary>
        None,

        /// <summary>付与(<c>BuffInfo</c>)。ライブ表示の項目を新しく作れるのはこれだけ。</summary>
        BuffInfo,

        /// <summary>層・持続の変化(<c>BuffChange</c>)。既にある項目を更新するだけ。</summary>
        BuffChange,
    }

    public class BuffEvent
    {
        public DataTypes.Enum.EBuffType BuffType { get; private set; }
        public DataTypes.Enum.EBuffPriority BuffPriority { get; private set; }
        public int BuffVisibility { get; private set; }
        public long Uuid { get; private set; }
        public int BaseId { get; private set; }
        public int Level { get; private set; }
        public long FireUuid { get; private set; }
        public string EntityCasterName { get; private set; } = null!;
        public int Layer { get; private set; }
        public int Duration { get; private set; }
        public int SourceConfigId { get; private set; }

        /// <summary>
        /// この実体を作ったものの種別(<c>EFightSource</c>)。<c>SourceConfigId</c> の中身が
        /// スキルIDかバフIDかはこれで決まるので、片方だけでは発生源を辿れない。
        /// 中身付きの付与を一度も受け取っていない実体は <see cref="UnknownFightSourceType"/>。
        /// </summary>
        public int FightSourceType { get; private set; }

        /// <summary>付与元の種類をまだ知らない。<c>EFightSource</c> に負の値は無い。</summary>
        public const int UnknownFightSourceType = -1;
        public string Name { get; private set; } = null!;
        [JsonIgnore]
        public string Description { get; private set; } = "";
        public string Icon { get; private set; } = null!;
        public int BuffAbilityType { get; private set; }
        public int BuffAbilitySubType { get; private set; }
        public string AttributeName { get; private set; } = null!;
        public object? Data { get; private set; }
        public DateTime AddDateTime { get; private set; }
        public DateTime RemoveDateTime { get; private set; }

        /// <summary>
        /// 表に名前の無いバフの由来(付与元をたどって着いた先)。<c>null</c> はまだたどっていないか、表に名前がある。
        /// 付与のたびに消し、その通知の最後にたどり直す(<see cref="Services.BuffOriginResolver"/>)。
        /// 名前は <c>CombatDataCatalog.GetBuffDisplayName</c> がこの由来から引く。
        /// </summary>
        [JsonIgnore]
        public Services.BuffOrigin? Origin { get; private set; }

        public BuffEvent(long uuid)
        {
            Uuid = uuid;
        }

        public void SetOrigin(Services.BuffOrigin? origin)
        {
            Origin = origin;
        }

        /// <summary>
        /// 回(エンカウンター)をまたいで続く実体の記録の写し(浅い写し)。前の回の記録を書き換えずに、次の回の記録として使う。
        /// </summary>
        public BuffEvent CopyForNextEncounter()
        {
            return (BuffEvent)MemberwiseClone();
        }

        [JsonConstructor]
        public BuffEvent(int uuid, int baseId, int level, long fireUuid, string entityCasterName, int layer, int duration, int sourceConfigId, int fightSourceType = 0)
        {
            FightSourceType = fightSourceType;
            Uuid = uuid;
            BaseId = baseId;
            Level = level;
            FireUuid = fireUuid;
            EntityCasterName = entityCasterName;
            Layer = layer;
            Duration = duration;
            SourceConfigId = sourceConfigId;

            if (BaseId > 0)
            {
                if (HelperMethods.DataTables.Buffs.Data.TryGetValue(baseId.ToString(), out var buffTableData))
                {
                    Name = buffTableData.Name;
                    Icon = buffTableData.GetIconName();
                    BuffType = buffTableData.BuffType!.Value;
                    BuffPriority = buffTableData.BuffPriority!.Value;
                    BuffVisibility = buffTableData.Visible;
                    BuffAbilityType = buffTableData.BuffAbilityType;
                    BuffAbilitySubType = buffTableData.BuffAbilitySubType;
                }
            }

            if (sourceConfigId > 0)
            {
                if (HelperMethods.DataTables.Skills.Data.TryGetValue(sourceConfigId.ToString(), out var skillTableData))
                {

                    if (string.IsNullOrWhiteSpace(Icon))
                    {
                        Icon = skillTableData.GetIconName();
                    }
                }
            }
        }

        public void AddData(string attributeName, object? data)
        {
            AttributeName = attributeName;
            Data = data;
        }

        public void SetAddTime(DateTime dateTime)
        {
            AddDateTime = dateTime;
        }

        public void SetRemoveTime(DateTime dateTime)
        {
            RemoveDateTime = dateTime;
        }

        public void SetEntitySourceNameFromUuid(string name)
        {

        }

        public void SetDescription(string value)
        {
            Description = value;
        }

        /// <param name="fightSourceType">
        /// <c>null</c> は通知が種類を運んでいない(層の変化の通知など)。記録済みの種類を 0(技)で上書きしない。
        /// </param>
        public void SetEvent(int uuid, int baseId, int level, long fireUuid, string entityCasterName, int layer, int duration, int sourceConfigId, int? fightSourceType)
        {
            if (fightSourceType is { } providedFightSourceType)
            {
                FightSourceType = providedFightSourceType;
            }
            Uuid = uuid;
            if (baseId > 0)
            {
                BaseId = baseId;
            }
            if (level > 0)
            {
                Level = level;
            }
            if (fireUuid > 0)
            {
                FireUuid = fireUuid;
            }
            if (!string.IsNullOrEmpty(entityCasterName))
            {
                EntityCasterName = entityCasterName;
            }
            Layer = layer;
            Duration = duration;
            if (sourceConfigId > 0)
            {
                SourceConfigId = sourceConfigId;
            }

            if (BaseId > 0)
            {
                if (HelperMethods.DataTables.Buffs.Data.TryGetValue(baseId.ToString(), out var buffTableData))
                {
                    Name = buffTableData.Name;
                    Icon = buffTableData.GetIconName();

                    BuffType = buffTableData.BuffType!.Value;
                    BuffPriority = buffTableData.BuffPriority!.Value;
                    BuffVisibility = buffTableData.Visible;
                    BuffAbilityType = buffTableData.BuffAbilityType;
                    BuffAbilitySubType = buffTableData.BuffAbilitySubType;
                }
            }
        }
    }
}
