// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// ウィジェットの状態の印(緑・黄・赤)の判定。<b>条件はここにだけ書く。</b>各ウィジェットは事実を集めて呼ぶだけ。
///
/// <para>
/// 印は「今画面に出しているもの」の状態。既定は緑で、欠けたと分かるライブの受信の事実を、その直し方の理由(<see cref="WidgetDataStatusReasons"/>)にして足す。
/// 色は理由で決まる(窓ごとに赤・黄を決めない)。
/// </para>
///
/// <para>
/// 履歴: 中身の全部が履歴の回になる窓(メーター・タイムライン・詳細とグラフ、相手の行が記録から来たプレイヤーの窓)は、
/// 画面にライブの情報が無いので緑。一部だけ履歴になる窓(プレイヤーリスト)は、記録から来た行を数えず、ライブの部分を数える。
/// 記録の中身の欠けは数えない。回を読めなかったときは、その回を出すはずだった窓を <see cref="WidgetDataStatusReasons.Unreadable"/> にする(次に選び直すまで)。
/// </para>
///
/// <para>
/// 窓の相手が見つからないことは、ゲームの情報が届き始めた後でだけ数える(<see cref="IsTargetMissing"/>)。
/// 届かないのがゲームの仕様の項目(他人の能力値・装備の細部・装備中スキル、
/// ホームガーデンでの自分のクラスR1・無効、変身中の特化)は数えない。
/// </para>
/// </summary>
public static class WidgetDataStatusRules
{
    private const WidgetDataStatusReasons MapOrRelogin = WidgetDataStatusReasons.MapOrRelogin;
    private const WidgetDataStatusReasons PartyScreen = WidgetDataStatusReasons.PartyScreen;
    private const WidgetDataStatusReasons PlayerClick = WidgetDataStatusReasons.PlayerClick;
    private const WidgetDataStatusReasons PlayerApproach = WidgetDataStatusReasons.PlayerApproach;
    private const WidgetDataStatusReasons EnemyApproach = WidgetDataStatusReasons.EnemyApproach;
    private const WidgetDataStatusReasons Unreadable = WidgetDataStatusReasons.Unreadable;

    /// <summary>DPS メーター・HPS メーター。履歴を出している間は中身の全部が履歴。</summary>
    /// <param name="isHistory">履歴の回を開いている。</param>
    /// <param name="hasHistoryLoadFailed">履歴の回を読めなかった(次に選び直すまで)。</param>
    /// <param name="players">メーターに出ている行。</param>
    /// <param name="countsSeasonTalent">名前の書式に潜在心相晶(<c>{Psych}</c>)を出しているか。</param>
    /// <param name="nearbyPlayers">HPS メーターのときだけ渡す、名簿の近くの人(料理と自然回復の回復を数えられるか)。</param>
    public static WidgetDataStatus ForMeter(
        GameReceptionSnapshot reception,
        bool isHistory,
        bool hasHistoryLoadFailed,
        bool isPartyMembershipPending,
        IEnumerable<MeterPlayerSnapshot> players,
        bool countsSeasonTalent,
        IEnumerable<PlayerRosterEntry>? nearbyPlayers)
    {
        var status = WidgetDataStatus.Complete.WithIf(hasHistoryLoadFailed, Unreadable);
        if (isHistory)
        {
            return status;
        }

        var others = players.Where(player => !player.IsSelf);
        return status
            .WithIf(reception.IsMidLaunch, MapOrRelogin)
            .WithIf(isPartyMembershipPending, PartyScreen)
            .WithIf(
                others.Any(player => player.DataGaps.HasFlag(PlayerDataGaps.Identity)
                    || IsOtherClassUnknown(player.ClassSpec, player.SeasonTalentBuffId, player.IsSeasonTalentInactive, countsSeasonTalent)),
                PlayerApproach)
            .WithIf(
                nearbyPlayers?.Any(player => player.IsNearby && !player.IsSelf && player.DataGaps.HasFlag(PlayerDataGaps.AppearAttributes)) == true,
                PlayerApproach);
    }

    /// <summary>
    /// タイムライン。履歴を出している間は中身の全部が履歴。記録済みの行の欠け(未知の敵・名前の無い人)は記録の中身なので数えない
    /// (近づき直しても書き直されない。元の事実はエンティティリストとメーターが数える)。
    /// </summary>
    public static WidgetDataStatus ForTimeline(
        GameReceptionSnapshot reception,
        bool isHistory,
        bool hasHistoryLoadFailed)
    {
        var status = WidgetDataStatus.Complete.WithIf(hasHistoryLoadFailed, Unreadable);
        return isHistory
            ? status
            : status.WithIf(reception.IsMidLaunch, MapOrRelogin);
    }

    /// <summary>
    /// プレイヤーリスト。履歴を出している間は一部だけ履歴になるので、記録から来た行は数えず、ライブの部分(自分の行・ライブの行・受信)を数える。
    /// 人の欠けは、いま値が届いている(灰色でない)他の人だけ。
    /// </summary>
    /// <param name="countsSeasonTalent">潜在心相晶を出す設定か(絵の設定か、名前の書式の <c>{Psych}</c>)。</param>
    public static WidgetDataStatus ForPlayerList(
        GameReceptionSnapshot reception,
        bool hasHistoryLoadFailed,
        bool isPartyMembershipPending,
        IEnumerable<PlayerRosterEntry> rows,
        bool countsSeasonTalent)
    {
        return WidgetDataStatus.Complete
            .WithIf(hasHistoryLoadFailed, Unreadable)
            .WithIf(reception.IsSelfEntryMissing, MapOrRelogin)
            .WithIf(isPartyMembershipPending, PartyScreen)
            .WithIf(
                rows.Any(row => row.IsLive && !row.IsSelf && !row.IsFromRecord
                    && ((row.DataGaps & (PlayerDataGaps.Identity | PlayerDataGaps.SkillLevels)) != PlayerDataGaps.None
                        || IsOtherClassUnknown(row.ClassSpec, row.SeasonTalentBuffId, row.IsSeasonTalentInactive, countsSeasonTalent))),
                PlayerApproach);
    }

    /// <summary>
    /// 蘇生リスト(いつもライブ)。出している行(戦闘不能の人)だけを数える。
    /// 自分の行があれば自分のバフ(蘇生不可デバフの元)、他の人の行は蘇生不可・素性・特化が分からないか。
    /// </summary>
    /// <param name="rows">出している行の元の名簿の行。</param>
    /// <param name="hasOtherReviveBlockUnknown">他の人の行に、蘇生不可が分からない(右が「??秒」の)行がある。</param>
    /// <param name="countsSeasonTalent">名前の書式に潜在心相晶(<c>{Psych}</c>)を出しているか。</param>
    public static WidgetDataStatus ForReviveList(
        GameReceptionSnapshot reception,
        IReadOnlyCollection<PlayerRosterEntry> rows,
        bool hasOtherReviveBlockUnknown,
        bool countsSeasonTalent)
    {
        var status = WidgetDataStatus.Complete
            .WithIf(
                hasOtherReviveBlockUnknown
                    || rows.Any(row => !row.IsSelf
                        && (row.DataGaps.HasFlag(PlayerDataGaps.Identity)
                            || IsOtherClassUnknown(row.ClassSpec, row.SeasonTalentBuffId, row.IsSeasonTalentInactive, countsSeasonTalent))),
                PlayerApproach);
        return rows.Any(row => row.IsSelf)
            ? status.Combine(ForSelfBuffs(reception))
            : status;
    }

    /// <summary>エンティティリスト(いつもライブ)。</summary>
    /// <param name="hasUnknownEnemy">種別ID(<c>AttrId</c>)の分からない「未知の敵」の行がある。</param>
    public static WidgetDataStatus ForEntityList(GameReceptionSnapshot reception, bool hasUnknownEnemy)
    {
        // 味方と敵の区別に使う自分の陣営は、入場でしか届かない。
        return WidgetDataStatus.Complete
            .WithIf(reception.IsSelfEntryMissing, MapOrRelogin)
            .WithIf(hasUnknownEnemy, EnemyApproach);
    }

    /// <summary>ステータス詳細(自分だけ、いつもライブ)。値の出所は入場の全属性。</summary>
    public static WidgetDataStatus ForPlayerStatus(GameReceptionSnapshot reception)
    {
        return WidgetDataStatus.Complete
            .WithIf(reception.IsSelfEntryMissing, MapOrRelogin);
    }

    /// <summary>バフ・デバフグループ(自分だけ、いつもライブ)。</summary>
    public static WidgetDataStatus ForBuffDebuffGroup(GameReceptionSnapshot reception)
    {
        return ForSelfBuffs(reception);
    }

    /// <summary>
    /// プレイヤー情報。相手の行が記録から来ていれば中身の全部が履歴。
    /// 現在地(周りにいる人は自分のいる場所)は入場で、シーズンとシーズン階級の元の今のシーズンは全体コンテナの直後に届くので、
    /// 途中起動でどちらかがまだなら相手が誰でもマップ移動まで欠ける。
    /// </summary>
    /// <param name="hasHistoryLoadFailed">履歴の回を読めなかった(次に選び直すまで)。</param>
    /// <param name="player">窓の相手の行(一覧に居ない相手を分かっている値で引いた行を含む)。無ければ null。</param>
    /// <param name="hasPhoto">顔・名刺の写真の値を受けたか(写真が無い人の「無い」という値も含む)。</param>
    public static WidgetDataStatus ForPlayerInfo(
        GameReceptionSnapshot reception,
        bool hasHistoryLoadFailed,
        bool isSelfTarget,
        PlayerRosterEntry? player,
        bool hasPhoto)
    {
        var status = WidgetDataStatus.Complete.WithIf(hasHistoryLoadFailed, Unreadable);
        if (player is { IsFromRecord: true })
        {
            return status;
        }

        status = status.WithIf(reception.IsSelfEntryMissing, MapOrRelogin);
        if (isSelfTarget)
        {
            return status.Combine(ForSelfBuffs(reception));
        }

        return status
            .WithIf(
                IsTargetMissing(reception, player is not null)
                    || (player is not null
                        && (player.DataGaps.HasFlag(PlayerDataGaps.Identity)
                            || IsOtherClassUnknown(player.ClassSpec, player.SeasonTalentBuffId, player.IsSeasonTalentInactive, countsSeasonTalent: true))),
                PlayerApproach)
            .WithIf(player is not null && !hasPhoto, PlayerClick)
            .WithIf(player is { IsPartyMember: true, IsNearby: false, PartySceneId: 0 }, PartyScreen);
    }

    /// <summary>
    /// 装備情報。相手の行が記録から来ていれば中身の全部が履歴。
    /// シーズン強度の有効・無効を決める今のシーズンは全体コンテナの直後に届くので、途中起動の間は相手が誰でもマップ移動まで欠ける。
    /// </summary>
    /// <param name="hasHistoryLoadFailed">履歴の回を読めなかった(次に選び直すまで)。</param>
    /// <param name="hasSelfEquipmentDetails">自分の装備の具体値(持ち物の中身)を受けたか。自分の窓だけ見る。</param>
    public static WidgetDataStatus ForPlayerEquipment(
        GameReceptionSnapshot reception,
        bool hasHistoryLoadFailed,
        bool isSelfTarget,
        PlayerRosterEntry? player,
        bool hasSelfEquipmentDetails)
    {
        var state = player?.EquipmentData?.State;
        var status = WidgetDataStatus.Complete
            .WithIf(hasHistoryLoadFailed || state == PlayerEquipmentDataState.Invalid, Unreadable);
        if (player is { IsFromRecord: true })
        {
            return status;
        }

        status = status.WithIf(reception.IsMidLaunch, MapOrRelogin);
        var isListMissing = state is null or PlayerEquipmentDataState.Missing;
        if (isSelfTarget)
        {
            // 自分の装備の一覧と具体値は全体コンテナで届く。全体コンテナを受けたのに無いときも欠け。
            return status
                .WithIf(reception.HasSelfContainer && (isListMissing || !hasSelfEquipmentDetails), MapOrRelogin);
        }

        return status
            .WithIf(
                IsTargetMissing(reception, player is not null)
                    || (player is not null && (isListMissing || player.ClassSpec == PlayerClassSpec.Unknown)),
                PlayerApproach);
    }

    /// <summary>アビリティ情報・潜在心相晶情報・モジュール情報。相手の行が記録から来ていれば中身の全部が履歴。</summary>
    /// <param name="hasHistoryLoadFailed">履歴の回を読めなかった(次に選び直すまで)。</param>
    /// <param name="countsAdvancedType">上級の絵を型で決める窓(潜在心相晶情報)で、一覧に上級の行があるか。</param>
    public static WidgetDataStatus ForRootBuffs(
        GameReceptionSnapshot reception,
        bool hasHistoryLoadFailed,
        bool isSelfTarget,
        PlayerRosterEntry? player,
        bool countsAdvancedType)
    {
        var status = WidgetDataStatus.Complete.WithIf(hasHistoryLoadFailed, Unreadable);
        if (player is { IsFromRecord: true })
        {
            return status;
        }

        if (isSelfTarget)
        {
            return status.Combine(ForSelfBuffs(reception));
        }

        return status
            .WithIf(
                IsTargetMissing(reception, player is not null)
                    || (player is not null
                        && (player.RootBuffData is null
                            || (countsAdvancedType && IsSeasonTalentUnknown(player.SeasonTalentBuffId, player.IsSeasonTalentInactive)))),
                PlayerApproach);
    }

    /// <summary>バフ一覧・デバフ一覧・バフ・デバフカード(プレイヤー、いつもライブ)。</summary>
    /// <param name="hasAppearBuffList">このマップで相手の出現時の全バフの一覧を受けたか。</param>
    public static WidgetDataStatus ForPlayerBuffs(
        GameReceptionSnapshot reception,
        bool isSelfTarget,
        PlayerRosterEntry? player,
        bool hasAppearBuffList)
    {
        if (isSelfTarget)
        {
            return ForSelfBuffs(reception);
        }

        return WidgetDataStatus.Complete
            .WithIf(
                IsTargetMissing(reception, player is not null)
                    || (player is not null && (!hasAppearBuffList || !player.IsNearby || player.DataGaps.HasFlag(PlayerDataGaps.Identity))),
                PlayerApproach);
    }

    /// <summary>バフ一覧・デバフ一覧・バフ・デバフカード(敵、いつもライブ)。</summary>
    /// <param name="isAcquired">窓の敵を捕まえたか。</param>
    /// <param name="hasAppearBuffList">このマップでその敵の出現時の全バフの一覧を受けたか。</param>
    /// <param name="isListed">その敵がいま周りにいるか。</param>
    /// <param name="isUnknownEnemy">種別ID の分からない「未知の敵」か。</param>
    public static WidgetDataStatus ForEntityBuffs(
        GameReceptionSnapshot reception,
        bool isAcquired,
        bool hasAppearBuffList,
        bool isListed,
        bool isUnknownEnemy)
    {
        return WidgetDataStatus.Complete
            .WithIf(
                IsTargetMissing(reception, isAcquired) || (isAcquired && (!hasAppearBuffList || !isListed || isUnknownEnemy)),
                EnemyApproach);
    }

    /// <summary>与ダメ詳細・ヒール詳細・被ダメ詳細・スキル詳細2種・推移グラフ2種。履歴を出している間は中身の全部が履歴。</summary>
    /// <param name="hasHistoryLoadFailed">履歴の回を読めなかった(次に選び直すまで)。</param>
    /// <param name="identity">窓の相手(今の回の実体から引いた素性)。相手が回にいなければ null。</param>
    /// <param name="countsSkillLevels">技の段階(G○)を出す窓(推移グラフ)か。</param>
    /// <param name="countsHealingSources">料理と自然回復の回復を数える窓(ヒール側)か。</param>
    public static WidgetDataStatus ForPlayerMetric(
        GameReceptionSnapshot reception,
        bool isHistory,
        bool hasHistoryLoadFailed,
        bool isSelfTarget,
        MeterPlayerIdentity? identity,
        bool isPartyMembershipPending,
        bool countsSkillLevels,
        bool countsHealingSources)
    {
        var status = WidgetDataStatus.Complete.WithIf(hasHistoryLoadFailed, Unreadable);
        if (isHistory)
        {
            return status;
        }

        if (isSelfTarget)
        {
            return status.WithIf(reception.IsMidLaunch, MapOrRelogin);
        }

        if (identity is null)
        {
            return status.WithIf(IsTargetMissing(reception, isTargetFound: false), PlayerApproach);
        }

        var gaps = identity.DataGaps;
        return status
            .WithIf(
                gaps.HasFlag(PlayerDataGaps.Identity)
                    || (countsSkillLevels && gaps.HasFlag(PlayerDataGaps.SkillLevels))
                    || (countsHealingSources && gaps.HasFlag(PlayerDataGaps.AppearAttributes)),
                PlayerApproach)
            .WithIf(isPartyMembershipPending && identity.IsNpc, PartyScreen);
    }

    /// <summary>自分のバフ・特化・潜在心相晶を出す窓。途中起動。</summary>
    private static WidgetDataStatus ForSelfBuffs(GameReceptionSnapshot reception)
    {
        return WidgetDataStatus.Complete
            .WithIf(reception.IsMidLaunch, MapOrRelogin);
    }

    /// <summary>
    /// チャット。新しいメッセージは1件ずつ完結した通知で届き、途中から読み始めても以後の分は欠けない。
    /// 過去のチャットはゲームがログインの後(と再接続)にまとめて読み直すので、接続を途中から読み始めたときは、マップを移動しても欠けたまま。
    /// ワールドと初心者はチャンネルの切り替え、プライベートトークはその相手を開いたときにも、その窓の分を読み直す。
    /// 最初から読んでいれば、届くまでの間は欠けではない。
    /// </summary>
    /// <param name="hasPastChat">その窓の過去のチャットを受け取った(<see cref="ChatMessageStore.HasPastChat"/>)。</param>
    public static WidgetDataStatus ForChat(GameReceptionSnapshot reception, bool hasPastChat)
    {
        return WidgetDataStatus.Complete
            .WithIf(reception.HasMidStreamConnection && !hasPastChat, WidgetDataStatusReasons.PastChatRelogin);
    }

    /// <summary>
    /// 窓の相手が見つからない。ゲームの情報が届き始めた後でだけ数える(<see cref="GameReceptionSnapshot.HasSessionStarted"/>)。
    /// 起動の直後に、前回開いていた窓の相手がまだ一覧にいないのは欠けではない。
    /// </summary>
    private static bool IsTargetMissing(GameReceptionSnapshot reception, bool isTargetFound)
        => !isTargetFound && reception.HasSessionStarted;

    /// <summary>他の人の特化(か、数えるなら潜在心相晶)がまだ分からない。変身中の特化は変身の値で、不明にはならない。</summary>
    private static bool IsOtherClassUnknown(PlayerClassSpec classSpec, int seasonTalentBuffId, bool isSeasonTalentInactive, bool countsSeasonTalent)
    {
        return classSpec == PlayerClassSpec.Unknown
            || (countsSeasonTalent && IsSeasonTalentUnknown(seasonTalentBuffId, isSeasonTalentInactive));
    }

    private static bool IsSeasonTalentUnknown(int seasonTalentBuffId, bool isSeasonTalentInactive)
        => PlayerInfoFormatFormatter.IsSeasonTalentUnknown(seasonTalentBuffId, isSeasonTalentInactive);
}
