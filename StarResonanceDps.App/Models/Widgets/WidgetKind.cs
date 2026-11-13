// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.App.Models.Widgets;

public enum WidgetKind
{
    DpsMeter,
    HpsMeter,
    Timeline = 3,
    PlayerList = 4,
    PlayerInfo = 5,
    PlayerStatus = 6,
    PlayerEquipment = 7,
    BuffList = 8,
    DebuffList = 9,
    DamageSkillDetail = 10,
    DpsGraph = 11,
    HealingSkillDetail = 12,
    HpsGraph = 13,
    DamageSummary = 14,
    HealingSummary = 15,
    EntityList = 18,
    BuffDebuffCard = 19,

    /// <summary>アビリティ情報。プレイヤーのタレントの木(取っているアビリティは根のバフで決める)。</summary>
    PlayerTalent = 20,

    /// <summary>潜在心相晶情報。プレイヤーの心相晶のノード・上級と因子(根のバフ)の一覧。</summary>
    PlayerSeasonTalent = 21,

    /// <summary>モジュール情報。プレイヤーのモジュール効果(根のバフ)の一覧。</summary>
    PlayerModule = 22,

    /// <summary>バフ・デバフグループ。自分のバフ・デバフのうち、設定で選んだものだけの一覧。</summary>
    BuffDebuffGroup = 23,

    /// <summary>蘇生リスト。戦闘不能のプレイヤーと、蘇生不可デバフの残り時間の一覧。</summary>
    ReviveList = 24,

    /// <summary>被ダメログ。プレイヤーの被弾を新しい順に1行ずつ出す一覧(設定で1人1行の一番新しい被弾だけにもできる)。</summary>
    DamageTakenLog = 25,

    /// <summary>被ダメ詳細。プレイヤーの被ダメの合計・通常・会心・幸運と属性ごとの割合。</summary>
    DamageTakenSummary = 26,

    /// <summary>エンティティ情報。エンティティリストの実体1つの HP・ヘイト・バフ・デバフ。</summary>
    EntityInfo = 27,

    /// <summary>チャット。ワールド・ギルド・パーティ・Ch・初心者のチャンネルか、プライベートトークの相手1人のメッセージ。</summary>
    Chat = 28,

    /// <summary>
    /// プラグインが足したウィジェット(1枚だけ開く)。どのウィジェットかは <see cref="WidgetKey"/> で見分け、種別ごとの設定の節は持たない。
    /// </summary>
    Plugin = 29,
    UserBuildLibrary = 30,
    CombatExtensionTimeline = 31,
    // Retired custom meter: reserve 32 for saved-state compatibility; never register it.
    HatePs = 32,
    PartyDps = 33,
    EnemyCatalog = 34,
    /// <summary>ヘイトメーター。プレイヤーごとの、敵のヘイトの一覧の増えの合計と毎秒の値。</summary>
    HateMeter = 35,

    /// <summary>ブレイクメーター。プレイヤーごとの、敵の強靭(ブレイクゲージ)を削った量の合計と毎秒の値。</summary>
    BreakMeter = 36,

    /// <summary>スキル詳細(ヘイト値)。1人のヘイトを、増えた差分のその人の被弾にダメージの値の比で分けた技ごとの内訳。</summary>
    HateSkillDetail = 37,

    /// <summary>スキル詳細(ブレイク値)。1人のブレイク値を、被弾ごとに分けた技ごとの内訳。</summary>
    BreakSkillDetail = 38,

    /// <summary>TPS推移グラフ。1人のヘイトの1秒ごとの合計を描く。</summary>
    HateGraph = 39,

    /// <summary>BPS推移グラフ。1人のブレイク値の1秒ごとの合計を描く。</summary>
    BreakGraph = 40
}
