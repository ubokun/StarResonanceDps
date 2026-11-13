// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime.DataTypes;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime
{
    public static class BattleStateMachine
    {
        public static ConcurrentQueue<KeyValuePair<EDungeonState, DateTime>> DungeonStateHistory { get; private set; } = new();
        public static DateTime? DeferredEncounterEndFinalTime { get; private set; } = null;
        public static EncounterEndFinalData? DeferredEncounterEndFinalData { get; private set; } = null;

        /// <summary>
        /// ダンジョンの状態の履歴(<see cref="DungeonStateHistory"/>)が変わった(状態が届いた・マップ移動とログアウトで消した)。パケットのスレッドで上がる。
        /// </summary>
        public static event Action? DungeonStateChanged;

        public static void StartNewMap()
        {
            CustomCombatFeatures.ClearQuest();
            Log.Information($"{DateTime.Now} - BattleStateMachine.StartNewMap");

            // 始まった計測はマップ移動で終える(下の作り直しが計測の注記つきで保存する)。待機中はそのまま待つ。
            EncounterManager.EndBenchmarkBeforeSplit("map move", onlyIfBegun: true);

            DeferredEncounterEndFinalTime = null;

            DungeonStateHistory.Clear();
            DungeonStateChanged?.Invoke();

            EncounterManager.StartNewMap();
            // マップ移動は前の結果を保持してよい区切り(ここで終えた計測の結果も、次の記録まで出す)。
            EncounterManager.EnterDungeon(keepPastEncounterInMeter: true, force: true, reason: EncounterStartReason.Force);

            // バフはエンカウンター境界では消さないが、マップ移動では持ち越さない。
            Services.ActiveBuffStore.Instance.Clear();
            Services.BuffInstanceIndex.Instance.Clear();
            Services.SummonSourceIndex.Instance.Clear();
            Services.SourceLandingResolver.Instance.Clear();
            Services.BuffOriginResolver.Instance.Clear();
            Services.NearbyMonsterIndex.Instance.Clear();
            Services.EnemyContributionBaseline.Instance.Clear();
            Services.EntityOriginIndex.Instance.Clear();
            Services.BossDbmBarStore.Instance.Clear();
            Services.WarningSkillCastStore.Instance.Clear();
            Services.DungeonTargetStore.Instance.Clear();
            Services.DungeonScoreStore.Instance.Clear();
        }

        /// <summary>
        /// ダンジョンの履歴・予約・前回の値を起動時の値に戻す(<see cref="StartNewMap"/> の前半と同じ消去)。ログアウト(ExitGame)で使う。
        /// エンカウンターと battle 行には触らない。
        /// </summary>
        internal static void ResetDungeonStateToStartup()
        {
            CustomCombatFeatures.ClearQuest();
            DeferredEncounterEndFinalTime = null;

            DungeonStateHistory.Clear();
            DungeonStateChanged?.Invoke();
        }

        /// <summary>
        /// 終わりの控え(前のエンカウンター)を起動時の値(null)に戻す。
        /// <b>エンカウンターを作り直した後に呼ぶこと。</b> 作り直しの中で前のエンカウンター用に入れ直され、その場で消化される。
        /// </summary>
        internal static void ClearEncounterEndFinalData()
        {
            DeferredEncounterEndFinalTime = null;
            DeferredEncounterEndFinalData = null;
        }

        public static void DungeonStateHistoryAdd(EDungeonState dungeonState, DateTime? arrivalUtc = null, int? serverStartSeconds = null)
        {
            var isNewPlaying = dungeonState == EDungeonState.DungeonStatePlaying
                && (DungeonStateHistory.IsEmpty || DungeonStateHistory.Last().Key != dungeonState);
            DungeonStateHistory.Enqueue(new KeyValuePair<EDungeonState, DateTime>(dungeonState, DateTime.Now));
            Log.Information($"{DateTime.Now} - BattleStateMachine.DungeonStateHistoryAdd: {dungeonState}");
            DungeonStateChanged?.Invoke();


            if (dungeonState == EDungeonState.DungeonStateNull)
            {
                CustomCombatFeatures.ClearQuest();

                // 届くのはマップ移動の直後で、保持はマップ移動の作り直しから引き継ぐ。
                EncounterManager.EnterDungeon(keepPastEncounterInMeter: true);
                PlayerRosterProjection.ResetNearbyPlayers();

            }
            else if (dungeonState == EDungeonState.DungeonStatePlaying)
            {
                if (!isNewPlaying) return;
                var receivedUtc = arrivalUtc ?? DateTime.UtcNow;
                var startUtc = CustomCombatFeatures.ResolveQuestStart(receivedUtc, serverStartSeconds);
                CustomCombatFeatures.BeginQuest(startUtc, startUtc != receivedUtc);
                // 開始までの待ち時間の記録を別の戦闘に分けるのは設定「進行で自動リセット」の一部なので、無効なら区切らずに続ける。
                // 記録が無ければ、作り直しても何も保存されない(回を作った時刻がダンジョンの開始になり、制限時間はそこから測る)ので、設定によらず行う。
                if (!EncounterManager.Current.HasStatsBeenRecorded())
                {
                    EncounterManager.EnterDungeon(keepPastEncounterInMeter: true);
                }
                else if (CombatRuntimeSettings.SplitEncountersOnNewPhases || CustomCombatFeatures.ResetOnQuestTransition || CustomCombatFeatures.SyncQuestClock)
                {
                    EncounterManager.EnterDungeon(keepPastEncounterInMeter: true, force: true, reason: EncounterStartReason.Force);
                }
            }
            else if (dungeonState == EDungeonState.DungeonStateEnd)
            {
                CustomCombatFeatures.ClearQuest();
                // ダンジョンが終わったら予告のバーを残さない(残すとバーの終わりに通知とタイムラインの行が出る)。計測中も同じ。
                Services.BossDbmBarStore.Instance.Clear();

                // 計測中は回を閉じない(窓の途中で時計が止まらないように。待機中の回は記録が無く、次の区切りで作り直す)。
                if (EncounterManager.IsBenchmarkActive)
                {
                    Log.Information("Dungeon end did not close the encounter during benchmark");
                    return;
                }

                EncounterManager.StopEncounter(true, EncounterStartReason.DungeonStateEnd);
            }

        }

        /// <summary>
        /// ダンジョンの目標が届いたことをログに残すだけ。区切りの判定には使わない
        /// (ボス部屋の入場はリセット系バフで見る。<see cref="Managers.MessageManager"/> の差分の処理)。
        /// 差分の更新で届かなかった項目は「-」。
        /// </summary>
        public static void LogDungeonTarget(int targetId, int? complete, int? nums)
        {
            Log.Information($"{DateTime.Now} - BattleStateMachine.LogDungeonTarget: TargetId={targetId}, Complete={complete?.ToString() ?? "-"}, Nums={nums?.ToString() ?? "-"}");
        }

        /// <summary>ダンジョンの目標が消えたことをログに残すだけ。</summary>
        public static void LogDungeonTargetRemoved(int targetId)
        {
            Log.Information($"{DateTime.Now} - BattleStateMachine.LogDungeonTarget: TargetId={targetId} removed");
        }

        public static void SetDeferredEncounterEndFinalData(DateTime dateTime, EncounterEndFinalData data)
        {
            if (DeferredEncounterEndFinalData != null && DeferredEncounterEndFinalData.EncounterId == data.EncounterId)
            {

                if (DeferredEncounterEndFinalTime == null)
                {

                    Log.Debug("SetDeferredEncounterEndFinalData - Encounter has already signaled the final end");
                    return;
                }
                else if (dateTime.CompareTo(DeferredEncounterEndFinalTime) >= 0)
                {

                    Log.Debug("SetDeferredEncounterEndFinalData - New time is not sooner than already set callback");
                    return;
                }
            }

            DeferredEncounterEndFinalTime = dateTime;
            DeferredEncounterEndFinalData = data;
        }

        public static void CheckDeferredCalls()
        {
            if (DeferredEncounterEndFinalTime.HasValue && DateTime.Now.CompareTo(DeferredEncounterEndFinalTime) >= 0)
            {
                DeferredEncounterEndFinalTime = null;

                EncounterManager.SignalEncounterEndFinal(DeferredEncounterEndFinalData!);

            }
        }

        /// <summary>
        /// ダンジョンが進行中(Playing)か。ボス部屋の入場の判定で使う。
        /// エンカウンターが持つ状態は区切りで消えるので、履歴の最後で見る。
        /// </summary>
        public static bool IsDungeonPlaying()
        {
            return !DungeonStateHistory.IsEmpty && DungeonStateHistory.Last().Key == EDungeonState.DungeonStatePlaying;
        }

        /// <summary>
        /// ダンジョンが終わった後(終了・結果画面・投票)か。予兆技(予告と警告)をここから先は受けない。
        /// マップ移動で履歴が消えるので、次のダンジョンでは元に戻る。
        /// </summary>
        public static bool IsDungeonFinished()
        {
            if (DungeonStateHistory.IsEmpty)
            {
                return false;
            }

            var state = DungeonStateHistory.Last().Key;
            return state is EDungeonState.DungeonStateEnd
                or EDungeonState.DungeonStateSettlement
                or EDungeonState.DungeonStateVote;
        }

        public static bool IsInOpenWorld()
        {
            if (DungeonStateHistory.IsEmpty)
            {
                return true;
            }
            else if (DungeonStateHistory.Last().Key == EDungeonState.DungeonStateNull)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public class EncounterEndFinalData
    {
        public ulong EncounterId;
        public int BattleId;
        public EncounterStartReason Reason;
        public Encounter? Encounter;
    }
}
