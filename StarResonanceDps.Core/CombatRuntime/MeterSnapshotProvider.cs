using Newtonsoft.Json.Linq;
using Serilog;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using ZLinq;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime;

public enum MeterSnapshotKind
{
    Damage,
    Healing
}

/// <summary>
/// 与ダメ詳細・ヒール詳細・被ダメ詳細が読む統計。<see cref="MeterSnapshotKind"/> とは別にしてあるのは、
/// メーターの側が「与ダメでなければヒール」と分けている所に被ダメを入れないため。
/// </summary>
public enum PlayerMetricSummaryKind
{
    Damage,
    Healing,
    DamageTaken
}

public enum PlayerBuffListKind
{
    Buff,
    Debuff
}

public sealed record MeterPlayerSnapshot(
    long CharacterId,
    long UserId,
    string Name,
    int ProfessionId,
    int SubProfessionId,
    PlayerClassSpec ClassSpec,
    int AbilityScore,
    int SeasonStrength,
    int Level,
    int SeasonLevel,
    bool IsSelf,
    bool IsNpc,
    ulong TotalValue,
    double ValuePerSecond,
    double Contribution,
    double BarRatio,
    // 有効化しているシーズンタレントの型の根ノードのバフID(0 = 不明か無効)と、無効が確定しているか。
    int SeasonTalentBuffId = 0,
    bool IsSeasonTalentInactive = false,
    // 他のプレイヤーについて、まだ届いていない情報(ウィジェットの状態の印の材料)。自分はいつも無し。
    PlayerDataGaps DataGaps = PlayerDataGaps.None);

public sealed record MeterSnapshot(
    MeterSnapshotKind Kind,
    TimeSpan Duration,
    ulong TotalValue,
    double ValuePerSecond,
    IReadOnlyList<MeterPlayerSnapshot> Players);

public sealed record BenchmarkStateSnapshot(
    bool IsActive,
    bool HasBegun,
    bool IsCompleted);

/// <summary>
/// 推移の区切り1つ。<see cref="StartSeconds"/>〜<see cref="EndSeconds"/> は <c>FirstDamageTimeStamp</c> からの秒(右閉じ)、
/// <see cref="ValuePerSecond"/> はその区切りの合計 ÷ 区切りの長さ。
/// </summary>
public readonly record struct MetricGraphPoint(double StartSeconds, double EndSeconds, double ValuePerSecond);

/// <summary>
/// 推移グラフの横軸の下に出す、プレイヤーの技の開始1件。<see cref="Seconds"/> は戦闘の時計の経過(<c>Encounter.ToCombatOffset</c>)で、
/// 起点より前の開始は 0、自動一時停止の中の開始は止まった時点の経過。<see cref="IconName"/> と <see cref="Name"/> は
/// メーターと同じ見出し表の行で畳んだもの(<c>CombatDataCatalog.GetSkillActivationDisplay</c>、内部IDの注記は設定どおり。名前が無ければ空)。
/// <see cref="Frame"/>(アイコンの背景の枠)は押した技が属する、枠の番号を持つ技で決めたもの(<c>CombatDataCatalog.GetSkillIconFrame</c>)。
/// <see cref="UsesImagineAsset"/>(イマジンの絵か)は出す絵で決めたもの(<c>CombatDataCatalog.IsImagineArtIcon</c>)。
/// <see cref="Grade"/>(G○ の数字)はプレイヤーリストの技の枠と同じ規則で、押した技がその人の技の一覧にあるときだけ持つ
/// (イマジンは改造段階、CD がレベルで変わるロールスキルはレベル。今の、履歴なら保存した時点の値で、押した時点の値ではない)。
/// </summary>
public sealed record MetricGraphSkillActivation(
    double Seconds,
    int SkillId,
    string IconName,
    string Name,
    SkillIconFrame Frame,
    bool UsesImagineAsset,
    int? Grade);

/// <summary>
/// <see cref="MeterSnapshotProvider.GetPlayerSkillActivations"/> の結果。
/// 読み手は <see cref="Encounter"/> と <see cref="GradeSource"/> を控えて次に渡す。どちらかが控えと違えば <see cref="Added"/> は先頭からの全部。
/// </summary>
/// <param name="Encounter">読んだ回。回か相手が無ければ null。</param>
/// <param name="GradeSource">G○ の元の一覧(参照を比べる)。</param>
/// <param name="NextIndex">次に読む番号。</param>
/// <param name="Added">足す分(届いた順)。</param>
public readonly record struct MetricGraphSkillActivationSlice(
    Encounter? Encounter,
    object? GradeSource,
    int NextIndex,
    IReadOnlyList<MetricGraphSkillActivation> Added);

/// <summary>
/// スキル詳細の1行。
///
/// <para>
/// <see cref="ValueByElement"/> と <see cref="ValueByMode"/> は、この行の累計の内訳。
/// 足す条件は <see cref="TotalValue"/> と同じ(<c>Immune</c> と <c>Miss</c> は入らない)ので、
/// それぞれの合計は <see cref="TotalValue"/> に一致する。<b>並びは enum の値の順</b>で、
/// 表示の都合で並べ替えるのは読む側。<b>値が入った種類だけ</b>が載る。
/// </para>
///
/// <para>
/// 内訳を溜め始める前に保存した履歴では、どちらも空になる。
/// </para>
/// </summary>
/// <param name="Name">表示する行名(設定しだいで内部名・内部ID注記つき)。</param>
/// <param name="PlainName">内部名と内部ID注記の無い行名(<see cref="CombatDataCatalog.GetSourceNameWithoutInternalId"/>)。無ければ空。</param>
public sealed record MetricSkillTableRowSnapshot(
    long SkillId,
    string SkillIdText,
    string Name,
    string PlainName,
    ulong TotalValue,
    double ValuePerSecondActive,
    double ValuePerSecond,
    ulong HitCount,
    double CritRate,
    double AverageValue,
    double Percentage,
    IReadOnlyList<KeyValuePair<EDamageProperty, ulong>> ValueByElement,
    IReadOnlyList<KeyValuePair<EDamageMode, ulong>> ValueByMode);

public sealed record MetricSkillTableSnapshot(
    IReadOnlyList<MetricSkillTableRowSnapshot> Entries);

/// <summary>
/// 回1つの全員の集計(与ダメ・回復・被ダメ)。メーター・スキル詳細と同じ数で、時計は1回だけ読む。
/// </summary>
/// <param name="IsHistory">開いている履歴の回か。</param>
/// <param name="StartUtc">戦闘の時計の起点。まだ起点が無ければ null。</param>
/// <param name="IsClosed">閉じた回(終了時刻がある)か、計測の窓が終わった。</param>
/// <param name="Players">どれかの合計が 0 より大きいプレイヤー。パーティで絞らない。</param>
public sealed record EncounterTotalsSnapshot(
    bool IsHistory,
    DateTime? StartUtc,
    TimeSpan Elapsed,
    bool IsClosed,
    IReadOnlyList<EncounterPlayerTotals> Players);

/// <param name="CharacterId">キャラ番号(名簿の <c>PlayerRosterEntry.CharacterId</c> と同じ)。</param>
public sealed record EncounterPlayerTotals(
    long CharacterId,
    CombatTotalsSnapshot Damage,
    CombatTotalsSnapshot Healing,
    CombatTotalsSnapshot DamageTaken);

/// <param name="Total">合計。与ダメと回復はメーターの行と同じ値(<c>Entity.TotalDamage</c> / <c>TotalHealing</c>)。</param>
/// <param name="PerSecond">メーターの秒間値と同じ(統計の合計 ÷ 回の経過)。</param>
/// <param name="ActivePerSecond">スキル詳細の有効の秒間値と同じ(統計の合計 ÷ その人の有効な秒数)。</param>
public readonly record struct CombatTotalsSnapshot(
    ulong Total,
    double PerSecond,
    double ActivePerSecond,
    ulong Hits,
    ulong CritTotal,
    uint CritCount,
    ulong LuckyTotal,
    uint LuckyCount);

/// <summary>実体に付いているバフ1つ(見せ方の決まりを通さない、控えのまま)。</summary>
/// <param name="DurationSeconds">効果時間の総量(延長で伸びる)。持続が無ければ null。</param>
/// <param name="EndUtc">切れる時刻。持続が無いか、残りが分からなければ null。</param>
/// <param name="IsRemainingUnknown">経過が分からないまま受けたバフ(残り時間を出せない)。</param>
/// <param name="CasterCharacterId">付けた実体がプレイヤーならそのキャラ番号。</param>
public sealed record ActiveBuffSnapshot(
    long BuffUuid,
    int BuffId,
    int Level,
    int Layer,
    double? DurationSeconds,
    DateTime? EndUtc,
    bool IsRemainingUnknown,
    long CasterEntityUuid,
    long? CasterCharacterId);

/// <summary>
/// 与ダメ詳細・ヒール詳細・被ダメ詳細の中身。合計・通常・会心・幸運の値・率・ヒット数と、属性ごとの合計。
///
/// <para>
/// 会心と幸運が同時に出た1発は、会心と幸運の両方に入る(通常には入らない)。
/// 幸運のヒット数は幸運ダメージの数(<c>LuckyHitCount</c>)で、幸運を起こした数ではない。
/// <see cref="ValueByElement"/> と <see cref="ValueByMode"/> は <see cref="MetricSkillTableRowSnapshot"/> の内訳と同じく enum の値の順で、
/// 値が入った種類だけが載る。どちらも合計は <see cref="TotalValue"/> に一致する。
/// </para>
/// </summary>
public sealed record PlayerMetricSummarySnapshot(
    ulong TotalValue,
    ulong HitsCount,
    ulong NormalValue,
    double NormalRate,
    uint NormalCount,
    ulong CritValue,
    double CritRate,
    uint CritCount,
    ulong LuckyValue,
    double LuckyRate,
    uint LuckyHitCount,
    IReadOnlyList<KeyValuePair<EDamageProperty, ulong>> ValueByElement,
    IReadOnlyList<KeyValuePair<EDamageMode, ulong>> ValueByMode);


/// <param name="BaseId">
/// バフの番号(非表示リストとカードが引く)。手修正 <c>BuffOverrides.json</c> の <c>FoldInto</c> で畳んだバフは畳み先の番号
/// (<see cref="CombatDataCatalog.GetBuffDisplayId"/>)。
/// </param>
/// <param name="Key">
/// 行の鍵。同じ番号のバフをまとめる読み方(リストの設定「同種バフの表示」)なら、バフの番号ごと(付与者に関係なく1行)。
/// まとめない読み方なら、プレイヤーが付けたものは番号と付与したプレイヤーごと、それ以外は番号ごと。畳んだバフは畳み先の番号の行にまとまる。
/// 同じ鍵の実体が複数あれば、残り時間の一番長い実体を出す。プレイヤーリストの枠のバッジは <paramref name="CardKey"/> と同じ。
/// </param>
/// <param name="CardKey">
/// カードが追う鍵(<c>base:バフ番号</c>)。実体に関係なく同じで、保存して次の起動でも使える。
/// 同じ鍵の行が複数(まとめない読み方で付与したプレイヤーごと)あるときは <paramref name="IsCardRepresentative"/> の行をカードに出す。
/// </param>
/// <param name="Caster">
/// 出している実体の付与者。召喚物は大元の召喚者のプレイヤー。
/// 付与者がプレイヤーでなければ null。名前を出すかは表示側が決める(リストの設定「付与者プレイヤー名の表示」、カードの書式 <c>{CasterName}</c>)。
/// </param>
/// <param name="IsCardRepresentative">同じ <paramref name="CardKey"/> の行のうち、カードに出す行か(残り時間の一番長いもの)。</param>
public sealed record PlayerBuffSnapshot(
    long Uuid,
    int BaseId,
    string Key,
    string CardKey,
    string Name,
    /// <summary>内部ID注記を付けない名前。記憶・保存する値(カードの見出しと通知)に使う。</summary>
    string NameWithoutInternalId,
    string IconName,
    int Layer,
    double? RemainingSeconds,
    /// <summary>
    /// 残り時間を名乗れない状態。<b><see cref="RemainingSeconds"/> の null(持続時間なし)とは別物。</b>
    /// 表示側は空欄ではなく「?」を出す。
    /// </summary>
    bool IsRemainingUnknown = false,
    /// <summary>付与したプレイヤー(召喚物なら大元の召喚者)。付与者がプレイヤーでなければ null。名前を出すかは表示側が決める。</summary>
    CombatEventLogParty? Caster = null,
    bool IsCardRepresentative = true,
    /// <summary>付与者が対象者本人(本人の召喚物を含む)か。リストの「付与者プレイヤー名の表示」の「他者付与のみ」が使う。</summary>
    bool IsCasterTarget = false,
    /// <summary>
    /// リストの設定「ゲージの長さ」が自動のときの長さ(秒)。出している実体の効果時間の総量で、延長すると残り時間と一緒に伸びる。
    /// 持続の無いバフは null。リストの行だけが入れる。
    /// </summary>
    double? GaugeLengthSeconds = null);

/// <param name="Grade">技の枠の TIPS に出す G○ の数字(<see cref="MeterSnapshotProvider.ResolveSkillGrade"/>)。出さないなら null。</param>
public sealed record PlayerCooldownSkillSnapshot(
    int SkillId,
    string Name,
    string IconName,
    bool IsImagine,
    double CooldownSeconds,
    int MaxCharges,
    double ChargeCooldownSeconds,
    int? Grade);

public sealed record PlayerSkillEffectSnapshot(
    PlayerBuffSnapshot? Buff,
    PlayerBuffSnapshot? Debuff);

/// <summary>戦闘不能の人の、蘇生不可デバフから見た状態(<see cref="MeterSnapshotProvider.GetReviveBlockState"/>)。</summary>
public enum ReviveBlockStateKind
{
    /// <summary>付いているのに残り時間が分からないか、付いているかどうかが分からない(その人のバフが全部は届いていない)。</summary>
    Unknown,

    /// <summary>付いていない(その人のバフが全部届いている)。</summary>
    Revivable,

    /// <summary>付いていて、残り時間が分かる。</summary>
    Blocked
}

/// <param name="RemainingSeconds">蘇生不可デバフの残り秒。<see cref="ReviveBlockStateKind.Blocked"/> のときだけ意味を持つ。</param>
/// <param name="DurationSeconds">
/// 残り秒を出した蘇生不可デバフの効果時間の総量(秒)。<see cref="ReviveBlockStateKind.Blocked"/> のときだけ意味を持つ。
/// 蘇生リストのゲージの長さの「自動」に使う。
/// </param>
/// <param name="EndTimeUtc">
/// 蘇生不可デバフの切れる時刻(UTC)。<see cref="ReviveBlockStateKind.Blocked"/> のときだけ意味を持つ。
/// 蘇生リストの並びはこれで比べる(残り秒は人ごとに読む瞬間が違い、切れる時刻のほぼ同じ人どうしで大小が入れ替わる)。
/// </param>
public readonly record struct ReviveBlockState(
    ReviveBlockStateKind Kind,
    double RemainingSeconds = 0d,
    double DurationSeconds = 0d,
    DateTime EndTimeUtc = default);

/// <param name="RoleSlotCount">
/// ロールスキルの枠数。<b>自分と他人で意味が違う。</b>
///
/// <para>
/// 自分は装備スロットが分かるので 4 枠固定。装備していない枠は空欄として出す。
/// 他人は AOI の <c>AttrSkillLevelIdList</c>(習得済みの集合)しか届かず、
/// そこから装備状態を判別できない(CLAUDE.md「他プレイヤーの装備中ロールスキルも取得不可能」)。
/// 空欄を出す根拠が無いので、習得できていた数だけ枠を作る。
/// </para>
/// </param>
public sealed record PlayerImagineRoleSkillLoadoutSnapshot(
    long EntityUuid,
    IReadOnlyList<PlayerCooldownSkillSnapshot?> ImagineSkills,
    IReadOnlyList<PlayerCooldownSkillSnapshot?> RoleSkills,
    int RoleSlotCount);

internal sealed record PlayerBuffCandidate(
    PlayerBuffSnapshot Snapshot,
    DateTime EffectiveRemoveTime);

/// <summary>
/// ロスターを通らない表示(ウィンドウのタイトルなど)が使う相手の素性。名前の規則(NPC は職業名)を当てるので NPC の印と職業も持つ。
/// <see cref="ClassSpec"/> はクラスカラーの鍵を決めるため(変身中か。求め方はメーターの行と同じ)。
/// </summary>
public sealed record MeterPlayerIdentity(string Name, long UserId, bool IsNpc, int ProfessionId, PlayerClassSpec ClassSpec, PlayerDataGaps DataGaps = PlayerDataGaps.None);

/// <summary>タイムライン・被ダメログの登場人物1人。プレイヤーなら名前は伏せ字にする前の生の名前。<see cref="ClassSpec"/> はプレイヤーのときだけ意味を持つ。</summary>
/// <param name="IsUnnamedEnemy">
/// 名前の無いモンスターで、ゲーム内で HP バーが見えるもの。表示側が「敵」と出す(名前は内部ID注記だけか空)。
/// </param>
/// <param name="IsUnknownEnemy">
/// プレイヤーでも仮想体でもなく、実体か種別ID(AttrId)が無くて何者か分からない加害者。表示側が「未知の敵」と出す(名前は空)。
/// AttrId は実体が現れたときの通知でしか届かないので、アプリの起動前から居た敵がこうなる。
/// </param>
public sealed record CombatEventLogParty(long Uuid, long CharacterId, string Name, bool IsPlayer, bool IsSelf, bool IsNpc, int ProfessionId, PlayerClassSpec ClassSpec, bool IsSystem = false, bool IsUnnamedEnemy = false, bool IsUnknownEnemy = false);

/// <summary>
/// タイムラインの1件(予告か予告のバーの終わりか詠唱か、敵の回復・敵から敵へのダメージ・敵が付けたバフ)。
///
/// <para>
/// <see cref="Timestamp"/> はパケットの到着時刻(UTC。予告のバーの終わりはバーが消える時刻)。経過は持たず、表示側が
/// <see cref="Encounter.ToCombatOffset"/> で戦闘の経過(メーターの経過と同じ。自動一時停止で止まっていた区間を除く)に直す。
/// 起点が立つまでは直せない(<see cref="TimelineSnapshot.CombatStartUtc"/>)。同じ到着時刻の行動は画面で1つの技の下にまとめる。
/// </para>
///
/// <para>
/// <see cref="Attacker"/> は予告を構えた敵(名前だけで UUID は 0)、詠唱した敵か、行動した敵(回復した側・加害者・バフを付けた側)。
/// <see cref="Target"/> は行動の相手で、予告と詠唱には無い。<see cref="Value"/> は回復なら実際に増えた HP、ダメージならその値。
/// <see cref="SourceName"/> は内部ID注記を付けない発生源の名前。名前が無ければ、全体設定で内部名を出すとき
/// (<see cref="CombatDataCatalog.ShowsUnnamedSkillInternalNames"/>)は技かバフの内部名、出さなければ空。
/// <see cref="SourceId"/> は発生源の番号(回復とダメージは届いた <c>OwnerId</c>、バフは付与元の番号、分からなければ 0)。<see cref="IsBuffSource"/> が真ならバフID、
/// 偽なら技IDか弾ID(弾のときの <see cref="SourceName"/> は親の技の名前)。
/// </para>
///
/// <para>
/// <see cref="CastSequence"/> は行動を起こした詠唱の通し番号(詠唱の行の <see cref="Sequence"/>)。詠唱から起きたのでなければ 0。
/// <see cref="IsWarning"/> は詠唱の行が戦闘画面の警告の技か。<see cref="BuffId"/> と <see cref="BuffName"/> は付けたバフとその名前(名前の決め方は <see cref="SourceName"/> と同じで、空もある)、
/// <see cref="BuffDurationMs"/> は付与の持続(ミリ秒、0 以下は持続なし)。
/// </para>
/// </summary>
public sealed record TimelineLine(
    CombatEventLogRecordKind Kind,
    long Sequence,
    DateTime Timestamp,
    CombatEventLogParty Attacker,
    CombatEventLogParty? Target,
    int SourceId,
    bool IsBuffSource,
    string SourceName,
    long Value,
    long CastSequence = 0,
    bool IsWarning = false,
    int BuffId = 0,
    string BuffName = "",
    int BuffDurationMs = 0);

/// <summary>
/// <see cref="MeterSnapshotProvider.GetTimeline"/> の結果。
/// <see cref="Encounter"/> が前回と違えば、呼び出し側は持っている行を捨てて読み直す。
/// </summary>
/// <param name="CombatStartUtc">
/// 読んだ時点の戦闘の時計の起点(メーターの経過と同じ)。まだ無ければ null。起点の前の行は負の経過になる。
/// 同じエンカウンターでも後から立つので、呼び出し側は変わったら持っている行の時刻を作り直す。
/// </param>
public sealed record TimelineSnapshot(
    Encounter? Encounter,
    int NextIndex,
    IReadOnlyList<TimelineLine> Lines,
    DateTime? CombatStartUtc);

/// <summary>
/// 被ダメログの1行(プレイヤーの被弾1回。回の記録 <see cref="TakenHitRecord"/>)。
///
/// <para>
/// 加害者はタイムライン・被ダメログの登場人物(<see cref="CombatEventLogParty"/>)と同じ決め方、技名は被ダメの発生源の名前の引き方で、
/// <see cref="SourceName"/> は名前が無ければ空(全体設定で内部名を出すときは内部名)。<see cref="SourceId"/> と <see cref="IsBuffSource"/> は内部ID注記に使う。
/// </para>
/// </summary>
/// <param name="Sequence">
/// その被弾の通し番号(記録と同じ番号)。大きいほど新しく、行の並びに使う。
/// 同じなら中身も同じ(呼び出し側は行の文字を作り直さない)。
/// </param>
/// <param name="Timestamp">その被弾の到着時刻(UTC)。経過は持たず、表示側が戦闘の経過(<see cref="Encounter.ToCombatOffset"/>)に直す。</param>
/// <param name="IsLethal">その被弾で戦闘不能になった。</param>
public sealed record DamageTakenLogHit(
    long CharacterId,
    long Sequence,
    DateTime Timestamp,
    CombatEventLogParty Attacker,
    int SourceId,
    bool IsBuffSource,
    string SourceName,
    bool IsFall,
    long Value,
    EDamageProperty? DamageElement,
    EDamageMode DamageMode,
    bool IsLethal);

/// <summary>
/// <see cref="MeterSnapshotProvider.GetDamageTakenLog"/> の結果。被弾からの経過は、表示側が <see cref="Encounter"/> の戦闘の時計で数える
/// (<see cref="Encounter.ReadCombatClock()"/> の経過 − <see cref="Encounter.ToCombatOffset"/> で直した被弾の時刻)。
/// </summary>
/// <param name="NextIndex">次に読む記録の位置。次の呼び出しにそのまま渡す。</param>
/// <param name="Hits">読んだ範囲の、対象がプレイヤーの被弾。古い順(同じ人が複数あれば後ろほど新しい)。</param>
public sealed record DamageTakenLogSnapshot(
    Encounter? Encounter,
    int NextIndex,
    IReadOnlyList<DamageTakenLogHit> Hits);

/// <summary><see cref="MeterSnapshotProvider.GetDamageTakenPlayers"/> の1人。</summary>
public sealed record DamageTakenPlayer(long CharacterId, bool IsSelf);

public static class MeterSnapshotProvider
{
    /// <summary>
    /// 蘇生不可デバフ(<c>虚弱·祈愿禁止</c> / <c>Weakened: Wish Sealed</c>)。
    ///
    /// <para>
    /// 説明文が「この間は 奥義！ライフブレス と 復活の祈り を再度かけられない」。
    /// <b>持続時間はパケットの <c>Duration</c> からしか分からない</b> —
    /// テーブルの <c>DestroyParam</c> は <c>[[0,0]]</c>。
    /// </para>
    ///
    /// <para>
    /// 復活系スキルはどれもこの同じ BaseId を出すので、発生元ごとに分ける必要はない。
    /// 同時に届く <c>2110032</c> / <c>2110093</c> / <c>2100412</c> は
    /// いずれも自前アイコンが無く表示経路で落ちる。
    /// </para>
    /// </summary>
    private const int ReviveBlockDebuffBaseId = 2110057;

    // 推移グラフの記録が時計の終点より後ろにあったのを、どの回について残したか(回ごとに1回だけ)。
    private static Encounter? _graphEndErrorEncounter;

    public static MeterSnapshot GetSnapshot(
        MeterSnapshotKind kind,
        PartyDisplayMode partyDisplayMode = PartyDisplayMode.All)
    {
        var encounter = ResolveActiveEncounter();
        if (encounter is null)
        {
            return new MeterSnapshot(kind, TimeSpan.Zero, 0, 0, Array.Empty<MeterPlayerSnapshot>());
        }

        // 経過・行の秒間値・フッターの合計を同じ終点で出すため、時計は1回だけ読む。
        var clock = encounter.ReadCombatClock();
        var source = encounter.Entities
            .Where(pair => pair.Value.EntityType == EEntityType.EntChar)
            .Select(pair => CreatePlayerValue(pair.Key, pair.Value, kind, clock))
            .Where(player => player.TotalValue > 0UL)
            .OrderByDescending(player => player.TotalValue)
            .ThenBy(player => player.Name, StringComparer.Ordinal)
            .ThenBy(player => player.CharacterId)
            .ToArray();

        UpdatePlayerMeterState(source);

        // 履歴表示中はフィルターを掛けない。判定に使えるのは今のパーティ状態だけで、
        // 履歴当時の所属は保存していない。
        var effectivePartyDisplayMode = AppState.OpenedHistoricalEncounter is null
            ? partyDisplayMode
            : PartyDisplayMode.All;
        var party = PartyStateStore.Instance.Current;
        var visibleSource = source
            .Where(player => party.ShouldInclude(player.UserId, player.IsSelf, effectivePartyDisplayMode))
            .ToArray();
        var totalValue = effectivePartyDisplayMode == PartyDisplayMode.All
            ? kind == MeterSnapshotKind.Damage
                ? encounter.TotalDamage
                : encounter.TotalHealing
            : visibleSource.Aggregate(0UL, (total, player) => total + player.TotalValue);
        var topValue = visibleSource.Length == 0
            ? 0UL
            : visibleSource[0].TotalValue;
        var players = visibleSource
            .Select(player => player with
            {
                Contribution = totalValue == 0
                    ? 0d
                    : player.TotalValue / (double)totalValue * 100d,
                BarRatio = topValue == 0
                    ? 0d
                    : player.TotalValue / (double)topValue
            })
            .ToArray();

        return new MeterSnapshot(
            kind,
            clock.Elapsed,
            totalValue,
            players.Sum(player => player.ValuePerSecond),
            players);
    }

    /// <summary>
    /// メーターに行が1つでも出るか(<see cref="GetSnapshot"/> と同じ条件)。行は作らない。
    /// メイン画面のカードが、窓の相手の選択リストに並べるものがあるかを確かめるのに使う。
    /// </summary>
    public static bool HasPlayers(
        MeterSnapshotKind kind,
        PartyDisplayMode partyDisplayMode = PartyDisplayMode.All)
    {
        var encounter = ResolveActiveEncounter();
        if (encounter is null)
        {
            return false;
        }

        var effectivePartyDisplayMode = AppState.OpenedHistoricalEncounter is null
            ? partyDisplayMode
            : PartyDisplayMode.All;
        var party = PartyStateStore.Instance.Current;
        foreach (var (_, entity) in encounter.Entities)
        {
            if (entity.EntityType != EEntityType.EntChar)
            {
                continue;
            }

            var totalValue = kind == MeterSnapshotKind.Damage
                ? entity.TotalDamage
                : entity.TotalHealing;
            if (totalValue == 0)
            {
                continue;
            }

            if (effectivePartyDisplayMode == PartyDisplayMode.All)
            {
                return true;
            }

            var isSelf = IsSelf(entity);
            if (party.ShouldInclude(PlayerDataSourceResolver.Resolve(entity, isSelf).CharacterId, isSelf, effectivePartyDisplayMode))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 表示中の回で被弾の記録があるプレイヤー(被ダメログに出る人)を、被ダメの合計の多い順に返す。
    /// 相手の指定は <c>DamageTakenLogHit.CharacterId</c> と同じ番号。メイン画面の被ダメ詳細のカードが、窓の相手の選択リストに並べる。
    /// </summary>
    public static IReadOnlyList<DamageTakenPlayer> GetDamageTakenPlayers()
    {
        var encounter = ResolveActiveEncounter();
        if (encounter is null)
        {
            return [];
        }

        return [.. encounter.Entities
            .Select(pair => pair.Value)
            .Where(entity => entity.EntityType == EEntityType.EntChar && entity.TakenStats.HitsCount > 0UL)
            .OrderByDescending(entity => entity.TakenStats.ValueTotal)
            .ThenBy(entity => entity.UUID)
            .Select(entity =>
            {
                var isSelf = IsSelf(entity);
                return new DamageTakenPlayer(PlayerDataSourceResolver.Resolve(entity, isSelf).CharacterId, isSelf);
            })];
    }

    /// <summary>被弾の記録があるプレイヤーが1人でもいるか(<see cref="GetDamageTakenPlayers"/> と同じ条件)。一覧は作らない。</summary>
    public static bool HasDamageTakenPlayers()
    {
        var encounter = ResolveActiveEncounter();
        if (encounter is null)
        {
            return false;
        }

        foreach (var (_, entity) in encounter.Entities)
        {
            if (entity.EntityType == EEntityType.EntChar && entity.TakenStats.HitsCount > 0UL)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 記録を <paramref name="startIndex"/> 件目から読み、タイムラインに出す記録(予告・予告のバーの終わり・詠唱・敵の回復・敵から敵へのダメージ・敵が付けたバフ)の行を返す。
    /// 被弾とダメージの無い死亡の記録は読み進めるだけで行にしない。表示中のエンカウンターは
    /// メーターと同じく <see cref="ResolveActiveEncounter"/> で決まるので、履歴表示にも追従する。
    ///
    /// <para>
    /// 表示中のエンカウンターが <paramref name="knownEncounter"/> と違えば先頭から返す。
    /// 名前はこの呼び出しの時点の言語と内部ID表示で引く。
    /// </para>
    /// </summary>
    public static TimelineSnapshot GetTimeline(Encounter? knownEncounter, int startIndex)
    {
        return BuildTimeline(ResolveActiveEncounter(), knownEncounter, startIndex);
    }

    /// <summary>
    /// 被ダメログの材料。表示中の回(タイムラインと同じ <see cref="ResolveActiveEncounter"/>)の被弾の記録を、タイムラインと同じく
    /// <paramref name="startIndex"/> 件目から読み、対象がプレイヤーの被弾だけを返す。表示中の回が <paramref name="knownEncounter"/> と違えば先頭から読む。
    /// 新しい被弾が無ければ空で返る。1人ずつの最後の被弾を持つのと並びを決めるのは呼び出し側。
    /// </summary>
    public static DamageTakenLogSnapshot GetDamageTakenLog(Encounter? knownEncounter, int startIndex)
    {
        var encounter = ResolveActiveEncounter();
        if (encounter is null)
        {
            return new DamageTakenLogSnapshot(null, 0, Array.Empty<DamageTakenLogHit>());
        }

        if (!ReferenceEquals(encounter, knownEncounter))
        {
            startIndex = 0;
        }

        var records = encounter.GetTakenHitRecords(startIndex);
        var parties = new Dictionary<long, CombatEventLogParty>();
        var hits = new List<DamageTakenLogHit>(records.Length);
        foreach (var record in records)
        {
            var target = ResolveCombatEventLogParty(encounter, record.TargetUuid, parties);
            if (!target.IsPlayer)
            {
                continue;
            }

            var attacker = ResolveCombatEventLogParty(encounter, record.OtherUuid, parties);
            hits.Add(new DamageTakenLogHit(
                target.CharacterId,
                record.Sequence,
                record.Timestamp,
                attacker,
                record.OwnerId,
                record.DamageSource == Zproto.EDamageSource.Buff,
                ResolveTakenDamageSourceName(attacker.IsPlayer, record.Id, record.DamageSource, record.OwnerId, record.BuffSourceSkillId, record.SummonSourceSkillId),
                record.DamageSource == Zproto.EDamageSource.Fall,
                record.Value,
                record.DamageElement,
                record.DamageMode,
                record.IsKill));
        }

        return new DamageTakenLogSnapshot(encounter, startIndex + records.Length, hits);
    }

    /// <summary>
    /// 予兆技の通知の予告に出す技の名前(<see cref="ResolveAnnouncementSkillName"/>)。
    /// 内部名は使わない(読み上げで日本語以外を読めないため)。タイムラインの行は名前が無ければ内部名になることがある。
    /// </summary>
    public static string GetTelegraphedAnnouncementSkillName(int skillId)
    {
        return ResolveAnnouncementSkillName(skillId);
    }

    /// <summary>
    /// 予兆技の通知の警告に出す技の名前(<see cref="ResolveCastSkillName"/>)。内部名は使わない(<see cref="GetTelegraphedAnnouncementSkillName"/> と同じ)。
    /// </summary>
    public static string GetTelegraphedCastSkillName(int skillId)
    {
        return ResolveCastSkillName(skillId);
    }

    /// <summary>
    /// 詠唱の技の名前(<see cref="ResolveCastSkillName"/>。名前が無ければ、全体設定で内部名を出すときは内部名)。タイムラインの詠唱の行と同じ。
    /// エンティティ情報の詠唱バーの左の技名と、エンティティ詠唱の通知が対象の名前と比べるのに使う。
    /// </summary>
    public static string GetCastSkillName(int skillId)
    {
        return ResolveCastLineSkillName(skillId);
    }

    /// <summary>予告の主(予告が届いた時点で周囲にいる、その技を持つモンスターの種別ID。0 なら名前なし)。タイムラインの予告の行の加害者。</summary>
    public static CombatEventLogParty CreateCombatEventLogAnnouncementParty(int ownerMonsterId)
    {
        return new CombatEventLogParty(0, 0, CombatDataCatalog.GetMonsterName(ownerMonsterId), false, false, false, 0, PlayerClassSpec.Unknown);
    }

    private static TimelineSnapshot BuildTimeline(Encounter? encounter, Encounter? knownEncounter, int startIndex)
    {
        if (encounter is null)
        {
            return new TimelineSnapshot(null, 0, Array.Empty<TimelineLine>(), null);
        }

        if (!ReferenceEquals(encounter, knownEncounter))
        {
            startIndex = 0;
        }

        var combatStartUtc = encounter.ExData.FirstDamageTimeStamp;
        var records = encounter.GetCombatEventLogRecords(startIndex);
        var parties = new Dictionary<long, CombatEventLogParty>();
        var lines = new List<TimelineLine>(records.Length);

        foreach (var record in records)
        {
            if (record.Kind is CombatEventLogRecordKind.Announcement or CombatEventLogRecordKind.AnnouncementEnd)
            {
                var announcement = record.Announcement!;
                lines.Add(new TimelineLine(
                    record.Kind,
                    announcement.Sequence,
                    announcement.Timestamp,
                    CreateCombatEventLogAnnouncementParty(announcement.OwnerMonsterId),
                    null,
                    announcement.SkillId,
                    false,
                    ResolveAnnouncementLineSkillName(announcement.SkillId),
                    0));
                continue;
            }

            if (record.Kind == CombatEventLogRecordKind.Cast)
            {
                var cast = record.Cast!;
                lines.Add(new TimelineLine(
                    record.Kind,
                    cast.Sequence,
                    cast.Timestamp,
                    ResolveCombatEventLogParty(encounter, record.EntityUuid, parties),
                    null,
                    cast.SkillId,
                    false,
                    ResolveCastLineSkillName(cast.SkillId),
                    0,
                    IsWarning: cast.IsWarning));
                continue;
            }

            if (record.Kind == CombatEventLogRecordKind.EnemyHeal)
            {
                var heal = record.EnemyHeal!;
                lines.Add(new TimelineLine(
                    record.Kind,
                    heal.Sequence,
                    heal.Timestamp,
                    ResolveCombatEventLogParty(encounter, heal.HealerUuid, parties),
                    ResolveCombatEventLogParty(encounter, heal.TargetUuid, parties),
                    heal.OwnerId,
                    heal.DamageSource == Zproto.EDamageSource.Buff,
                    ResolveTakenDamageSourceName(false, 0, heal.DamageSource, heal.OwnerId, heal.BuffSourceSkillId, heal.SummonSourceSkillId),
                    heal.Value,
                    heal.CastSequence));
                continue;
            }

            if (record.Kind == CombatEventLogRecordKind.EnemyDamage)
            {
                var damage = record.EnemyDamage!;
                lines.Add(new TimelineLine(
                    record.Kind,
                    damage.Sequence,
                    damage.Timestamp,
                    ResolveCombatEventLogParty(encounter, damage.AttackerUuid, parties),
                    ResolveCombatEventLogParty(encounter, damage.TargetUuid, parties),
                    damage.OwnerId,
                    damage.DamageSource == Zproto.EDamageSource.Buff,
                    ResolveTakenDamageSourceName(false, 0, damage.DamageSource, damage.OwnerId, damage.BuffSourceSkillId, damage.SummonSourceSkillId),
                    damage.Value,
                    damage.CastSequence));
                continue;
            }

            if (record.Kind == CombatEventLogRecordKind.EnemyBuff)
            {
                var buff = record.EnemyBuff!;
                lines.Add(new TimelineLine(
                    record.Kind,
                    buff.Sequence,
                    buff.Timestamp,
                    ResolveCombatEventLogParty(encounter, buff.CasterUuid, parties),
                    ResolveCombatEventLogParty(encounter, buff.TargetUuid, parties),
                    buff.OwnerId,
                    buff.DamageSource == Zproto.EDamageSource.Buff,
                    ResolveTakenDamageSourceName(false, 0, buff.DamageSource, buff.OwnerId, buff.BuffSourceSkillId, buff.SummonSourceSkillId),
                    0,
                    buff.CastSequence,
                    BuffId: buff.BuffId,
                    BuffName: ResolveTimelineBuffName(buff.BuffId),
                    BuffDurationMs: buff.DurationMs));
                continue;
            }

            // ダメージの無い死亡はタイムラインに出さない(プレイヤーリストの顔ぶれが読む)。
            if (record.Kind != CombatEventLogRecordKind.Death)
            {
                throw new InvalidOperationException($"Combat event log record kind {record.Kind} has no timeline handling (sequence {record.Sequence}).");
            }
        }

        return new TimelineSnapshot(encounter, startIndex + records.Length, lines, combatStartUtc);
    }

    /// <summary>
    /// 被ダメの発生源の名前。<c>OwnerId</c> の中身は <c>DamageSource</c> で変わる。
    ///
    /// <para>
    /// <b>加害者がプレイヤーなら、先にメーターの見出し表(手修正込み)を <paramref name="meterKey"/> で引く。</b>
    /// プレイヤーの技の派生(イマジンのパッシブ・幸運の一撃など)はバフ名が空で、名前は見出し表にしか無い。
    /// 見出し表が空か、鍵を保存していなかった頃の記録(0)なら下の引き方に戻る。
    /// </para>
    ///
    /// <list type="bullet">
    ///   <item>バフ — バフID。記録時に付与元の技(<paramref name="buffSourceSkillId"/>)が決まっていれば技として引き、
    ///   無いか名前が空なら <c>BuffNames</c> で引く</item>
    ///   <item>弾(<c>Bullet</c> / <c>FakeBullet</c>) — <c>BulletTable</c> の番号。親の技へ辿って技として引き、辿れなければ空</item>
    ///   <item>それ以外 — 技ID</item>
    /// </list>
    ///
    /// <para>
    /// ここまでで名前が空なら、ダメージを出した実体(仮想体など)を出した技(<paramref name="summonSourceSkillId"/>)の名前にする。
    /// ボスの予告の技は仮想体を出すだけで、ダメージは名前の無い仮想体の技で届くことが多い。
    /// </para>
    ///
    /// <para>
    /// 弾の番号を <c>SkillNames</c> でそのまま引くと、同じ番号の無関係な技の名前が出る
    /// (<c>3920</c> は弾 普攻假子弹 / 技 奥義！ライフブレス)。
    /// </para>
    ///
    /// <para>
    /// どの名前も空なら、全体設定で内部名を出すときは内部名(<see cref="ResolveTakenDamageSourceInternalName"/>)。
    /// 加害者がプレイヤーでも同じ。表示名はどの段でも内部名より先に勝つ。
    /// </para>
    /// </summary>
    private static string ResolveTakenDamageSourceName(bool isPlayerAttacker, long meterKey, Zproto.EDamageSource damageSource, int ownerId, int buffSourceSkillId, int summonSourceSkillId)
    {
        if (isPlayerAttacker && meterKey != 0)
        {
            var meterName = CombatDataCatalog.GetSourceName(meterKey);
            if (!string.IsNullOrEmpty(meterName))
            {
                return meterName;
            }
        }

        var name = damageSource switch
        {
            Zproto.EDamageSource.Buff => ResolveTakenDamageBuffName(ownerId, buffSourceSkillId),
            Zproto.EDamageSource.Bullet or Zproto.EDamageSource.FakeBullet =>
                CombatDataCatalog.TryResolveBulletParentSkillId(ownerId, out var parentSkillId)
                    ? ResolveTakenDamageSkillName(parentSkillId)
                    : string.Empty,
            _ => ResolveTakenDamageSkillName(ownerId)
        };

        // 当たった技そのものに名前が無いときだけ、ダメージを出した実体(仮想体など)を出した技の名前にする。
        if (string.IsNullOrEmpty(name) && summonSourceSkillId > 0)
        {
            name = ResolveTakenDamageSkillName(summonSourceSkillId);
        }

        return string.IsNullOrEmpty(name) && CombatDataCatalog.ShowsUnnamedSkillInternalNames
            ? ResolveTakenDamageSourceInternalName(damageSource, ownerId, buffSourceSkillId, summonSourceSkillId)
            : name;
    }

    private static string ResolveTakenDamageBuffName(int buffId, int buffSourceSkillId)
    {
        var sourceSkillName = buffSourceSkillId > 0
            ? ResolveTakenDamageSkillName(buffSourceSkillId)
            : string.Empty;
        return string.IsNullOrEmpty(sourceSkillName)
            ? CombatDataCatalog.GetBuffNameWithoutInternalId(buffId)
            : sourceSkillName;
    }

    /// <summary>技の名前。ボス大技の予告(<c>DbmTable</c>)の正式名を先に引き、無ければ技の名前(<c>SkillTalkNames</c> → <c>SkillNames</c>)。</summary>
    private static string ResolveTakenDamageSkillName(int skillId)
    {
        var dbmName = CombatDataCatalog.GetDbmNameWithoutInternalId(skillId);
        return string.IsNullOrEmpty(dbmName)
            ? CombatDataCatalog.GetSkillNameWithoutInternalId(skillId)
            : dbmName;
    }

    /// <summary>
    /// 敵が付けたバフの行のバフの名前。空なら、全体設定で内部名を出すときはバフの内部名(出さなければ空のまま。表示側が「何かの効果」と出す)。
    /// </summary>
    private static string ResolveTimelineBuffName(int buffId)
    {
        var name = CombatDataCatalog.GetBuffNameWithoutInternalId(buffId);
        return string.IsNullOrEmpty(name) && CombatDataCatalog.ShowsUnnamedSkillInternalNames
            ? CombatDataCatalog.GetBuffInternalName(buffId)
            : name;
    }

    /// <summary>
    /// 予告の技の名前。予告の名前(<c>DbmTable.Content</c>)→ 技の表の名前(古い版の名前を含む)→ <c>SkillTalk</c>。
    /// 予告の名前はゲームの予告のバーに出る名前。<c>SkillTalk</c> は詠唱のテキストなので一番後ろ。
    /// </summary>
    private static string ResolveAnnouncementSkillName(int skillId)
    {
        var name = CombatDataCatalog.GetDbmNameWithoutInternalId(skillId);
        if (string.IsNullOrEmpty(name))
        {
            name = CombatDataCatalog.GetSkillTableNameWithoutInternalId(skillId);
        }

        return string.IsNullOrEmpty(name)
            ? CombatDataCatalog.GetSkillTalkNameWithoutInternalId(skillId)
            : name;
    }

    /// <summary>
    /// 詠唱と警告の技の名前。技の名前(<c>SkillTalk</c> → 技の表の名前 → 古い版の名前)→ 予告の名前。
    /// ゲームの詠唱バー(ボスの HP バーの詠唱欄)と警告バー(ボス技の発動バー)は技の名前を出す。予告の名前は技の名前が無いときだけ。
    /// </summary>
    private static string ResolveCastSkillName(int skillId)
    {
        var name = CombatDataCatalog.GetSkillNameWithoutInternalId(skillId);
        return string.IsNullOrEmpty(name)
            ? CombatDataCatalog.GetDbmNameWithoutInternalId(skillId)
            : name;
    }

    /// <summary>タイムラインの予告の行の技の名前(<see cref="ResolveAnnouncementSkillName"/>)。空なら、全体設定で内部名を出すときは技の内部名。</summary>
    private static string ResolveAnnouncementLineSkillName(int skillId)
    {
        return WithSkillInternalName(ResolveAnnouncementSkillName(skillId), skillId);
    }

    /// <summary>タイムラインの詠唱の行の技の名前(<see cref="ResolveCastSkillName"/>)。空なら、全体設定で内部名を出すときは技の内部名。</summary>
    private static string ResolveCastLineSkillName(int skillId)
    {
        return WithSkillInternalName(ResolveCastSkillName(skillId), skillId);
    }

    private static string WithSkillInternalName(string name, int skillId)
    {
        return string.IsNullOrEmpty(name) && CombatDataCatalog.ShowsUnnamedSkillInternalNames
            ? CombatDataCatalog.GetSkillInternalName(skillId)
            : name;
    }

    /// <summary>
    /// 被弾・敵の行動の行の内部名。引く順は <see cref="ResolveTakenDamageSourceName"/> と同じ
    /// (バフは付与元の技、無いか内部名が空ならそのバフ。弾は親の技、それ以外はその技。空なら召喚元の技)。
    /// 弾の番号をそのまま技の表で引かない(同じ番号の無関係な技の内部名が出る)。
    /// </summary>
    private static string ResolveTakenDamageSourceInternalName(
        Zproto.EDamageSource damageSource,
        int ownerId,
        int buffSourceSkillId,
        int summonSourceSkillId)
    {
        var name = damageSource switch
        {
            Zproto.EDamageSource.Buff => ResolveTakenDamageBuffInternalName(ownerId, buffSourceSkillId),
            Zproto.EDamageSource.Bullet or Zproto.EDamageSource.FakeBullet =>
                CombatDataCatalog.TryResolveBulletParentSkillId(ownerId, out var parentSkillId)
                    ? CombatDataCatalog.GetSkillInternalName(parentSkillId)
                    : string.Empty,
            _ => CombatDataCatalog.GetSkillInternalName(ownerId)
        };

        return name.Length == 0 && summonSourceSkillId > 0
            ? CombatDataCatalog.GetSkillInternalName(summonSourceSkillId)
            : name;
    }

    private static string ResolveTakenDamageBuffInternalName(int buffId, int buffSourceSkillId)
    {
        var sourceSkillName = buffSourceSkillId > 0
            ? CombatDataCatalog.GetSkillInternalName(buffSourceSkillId)
            : string.Empty;
        return sourceSkillName.Length == 0
            ? CombatDataCatalog.GetBuffInternalName(buffId)
            : sourceSkillName;
    }

    /// <summary>タイムライン・被ダメログの登場人物1人を、<paramref name="encounter"/> の実体から決める。</summary>
    internal static CombatEventLogParty CreateCombatEventLogParty(Encounter encounter, long uuid)
    {
        return ResolveCombatEventLogParty(encounter, uuid, []);
    }

    private static CombatEventLogParty ResolveCombatEventLogParty(
        Encounter encounter,
        long uuid,
        Dictionary<long, CombatEventLogParty> cache)
    {
        if (cache.TryGetValue(uuid, out var cached))
        {
            return cached;
        }

        var isPlayer = Utils.UuidToEntityType(uuid) == (long)EEntityType.EntChar;
        CombatEventLogParty party;
        if (!encounter.Entities.TryGetValue(uuid, out var entity))
        {
            var isUnknownEnemy = !isPlayer && (EEntityType)Utils.UuidToEntityType(uuid) != EEntityType.EntDummy;
            party = new CombatEventLogParty(uuid, isPlayer ? Utils.UuidToEntityId(uuid) : 0, string.Empty, isPlayer, IsSelfEntity(uuid), false, 0, PlayerClassSpec.Unknown, IsUnknownEnemy: isUnknownEnemy);
        }
        else if (isPlayer)
        {
            var isSelf = IsSelf(entity);
            var source = PlayerDataSourceResolver.Resolve(entity, isSelf);
            // NPC は名前ではなく職業名を出す(プレイヤーリストと同じ規則)。判定と職業IDは表示側が使う。
            party = new CombatEventLogParty(
                uuid,
                source.CharacterId,
                source.Name,
                true,
                isSelf,
                source.IsNpc,
                source.ProfessionId,
                source.ClassSpec);
        }
        else
        {
            // 名前はモンスターの実体だけ、種別ID(AttrId)から表示言語で引く。
            // 同じ番号が仮想体や NPC では別のものを指すので、モンスター以外と属性が無いものは名前を空のままにする。
            // 仮想体が加害者として残るのは親(召喚者)を特定できなかったときだけなので(Encounter.ResolveCombatEventLogActor)、
            // 仕掛けそのものを指す名前を出す。
            // 名前の無いモンスターでも、ゲーム内で HP バーが見えるものは「敵」と出す(エンティティリストと同じ条件)。
            // 表に行が無い番号には付けない(表が古いことを、名前の無い雑魚と見分けるため)。
            // AttrId が届いていない加害者と、それ以外の名前の無いモンスター(表に行が無い・名前も HP バーも無い)は「未知の敵」と出す。
            var attrId = entity.GetAttrKV("AttrId");
            var entityType = (EEntityType)Utils.UuidToEntityType(uuid);
            var isSystem = entityType == EEntityType.EntDummy;
            var monsterId = attrId is null || entityType != EEntityType.EntMonster ? 0 : Convert.ToInt64(attrId);
            var name = monsterId == 0
                ? string.Empty
                : CombatDataCatalog.GetMonsterName(monsterId);
            var isUnnamedEnemy = monsterId > 0
                && CombatDataCatalog.HasEntityRow(EEntityType.EntMonster, monsterId)
                && !CombatDataCatalog.HasEntityName(EEntityType.EntMonster, monsterId)
                && CombatDataCatalog.HasMonsterHpBar(monsterId);
            var isUnknownEnemy = !isSystem
                && (attrId is null
                    || (entityType == EEntityType.EntMonster
                        && !isUnnamedEnemy
                        && !CombatDataCatalog.HasEntityName(EEntityType.EntMonster, monsterId)));
            party = new CombatEventLogParty(uuid, 0, name, false, false, false, 0, PlayerClassSpec.Unknown, isSystem, isUnnamedEnemy, isUnknownEnemy);
        }

        cache[uuid] = party;
        return party;
    }

    public static MeterPlayerIdentity? GetPlayerIdentity(long characterId)
    {
        var encounter = ResolveActiveEncounter();
        if (encounter is null
            || !TryResolvePlayerEntity(encounter, characterId, out var entityUuid, out var entity))
        {
            return null;
        }

        var source = PlayerDataSourceResolver.Resolve(entity, IsSelf(entity));
        return new MeterPlayerIdentity(
            source.Name,
            source.CharacterId,
            source.IsNpc,
            source.ProfessionId,
            source.ClassSpec,
            source.DataGaps);
    }


    /// <summary>
    /// 自分の素性。
    ///
    /// <para>
    /// <b>ロスターに載るのを待たない。</b>自分のIDは <c>AppState.PlayerUID</c> が常に持っているので、
    /// プレイヤー一覧の投影が空でも(街で待機中など)名前を引ける。
    /// 自分を対象にした窓のタイトルがロスター次第で出たり出なかったりするのを避ける。
    /// </para>
    /// </summary>
    public static MeterPlayerIdentity? GetSelfPlayerIdentity()
    {
        var characterId = AppState.PlayerUID;
        return characterId == 0 ? null : GetPlayerIdentity(characterId);
    }

    /// <summary>
    /// バフ/デバフリストとカードが読む。<b>履歴を開いても固めない</b> —
    /// バフの変化はDBから復元できないので、止めても意味のある絵にならない。
    /// 周りにいない他のプレイヤーは空(見えない相手の除去は届かない。また現れたら出現時の一覧の残り時間で出る)。
    /// </summary>
    /// <param name="groupSameBuffs">
    /// 同じ番号のバフを1行にまとめるか(リストの設定「同種バフの表示」)。カードはどちらでも出す実体が同じなので、まとめて読む。
    /// </param>
    public static IReadOnlyList<PlayerBuffSnapshot> GetPlayerBuffs(long characterId, PlayerBuffListKind kind, bool groupSameBuffs)
    {
        return TryGetPlayerBuffs(characterId, kind, groupSameBuffs, out var snapshots)
            ? snapshots
            : Array.Empty<PlayerBuffSnapshot>();
    }

    /// <summary>
    /// <see cref="GetPlayerBuffs"/> と同じ行。プレイヤーの実体が見つからないとき、周りにいない他のプレイヤーのときは偽(バフが1つも無いときは真で空)。
    /// 付いていないバフも行に出すバフ・デバフグループが、相手の分からない間を「何も付いていない」と分けるのに使う。
    /// </summary>
    public static bool TryGetPlayerBuffs(
        long characterId,
        PlayerBuffListKind kind,
        bool groupSameBuffs,
        out IReadOnlyList<PlayerBuffSnapshot> snapshots)
    {
        var encounter = ResolvePlayerDetailEncounter();
        if (encounter is null
            || !TryResolvePlayerEntity(encounter, characterId, out _, out var entity)
            || !IsSelfOrNearbyPlayer(entity.UUID))
        {
            snapshots = Array.Empty<PlayerBuffSnapshot>();
            return false;
        }

        snapshots = CreateBuffSnapshots(encounter, entity.UUID, kind, groupSameBuffs ? BuffRowGrouping.ByBuff : BuffRowGrouping.ByPlayerCaster);
        return true;
    }

    /// <summary>バフの番号のカードの鍵(<see cref="PlayerBuffSnapshot.CardKey"/>)。番号は畳み先の番号(行の <c>BaseId</c>)。</summary>
    public static string CreateBuffCardKey(int buffId)
        => $"{BuffCardKeyBasePrefix}{buffId}";

    /// <summary>カードの鍵(<see cref="PlayerBuffSnapshot.CardKey"/>)からバフの番号を読む。番号の無い実体の鍵なら偽。</summary>
    public static bool TryGetBuffIdFromCardKey(string? cardKey, out int buffId)
    {
        buffId = 0;
        return cardKey is not null
            && cardKey.StartsWith(BuffCardKeyBasePrefix, StringComparison.Ordinal)
            && int.TryParse(
                cardKey.AsSpan(BuffCardKeyBasePrefix.Length),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out buffId)
            && buffId > 0;
    }

    /// <summary>
    /// エンティティリストに載る実体のバフ/デバフリストとカードが読む。
    /// バフは回の外の控え(<see cref="Services.ActiveBuffStore"/>)から作るので、今の回に実体が無くても出す
    /// (回が区切られた直後は、まだ動いていない敵の実体が今の回に無い)。周りから消えた実体は空。
    /// </summary>
    /// <param name="groupSameBuffs"><see cref="GetPlayerBuffs"/> と同じ。</param>
    public static IReadOnlyList<PlayerBuffSnapshot> GetEntityBuffs(long entityUuid, PlayerBuffListKind kind, bool groupSameBuffs)
    {
        var encounter = EncounterManager.Current;
        if (encounter is null || entityUuid == 0)
        {
            return Array.Empty<PlayerBuffSnapshot>();
        }

        return CreateBuffSnapshots(encounter, entityUuid, kind, groupSameBuffs ? BuffRowGrouping.ByBuff : BuffRowGrouping.ByPlayerCaster);
    }

    /// <summary>このマップで、そのプレイヤーの出現時の全バフの一覧を受けたか(自分は出現しないので偽)。</summary>
    public static bool HasReceivedPlayerAppearBuffList(long characterId)
    {
        var encounter = EncounterManager.Current;
        return encounter is not null
            && TryResolvePlayerEntity(encounter, characterId, out _, out var entity)
            && Services.ActiveBuffStore.Instance.HasReceivedAppearList(entity.UUID);
    }

    /// <summary>このマップで、その実体(敵)の出現時の全バフの一覧を受けたか。</summary>
    public static bool HasReceivedEntityAppearBuffList(long entityUuid)
        => Services.ActiveBuffStore.Instance.HasReceivedAppearList(entityUuid);

    /// <summary>バフ/デバフリストの行のまとめ方(リストの設定「同種バフの表示」で、対象がプレイヤーかエンティティかごとに選ぶ)。</summary>
    private enum BuffRowGrouping
    {
        /// <summary>バフの番号ごとに1行。付与者に関係なく、残り時間の一番長い実体で上書きする(「まとめる」)。</summary>
        ByBuff,

        /// <summary>
        /// プレイヤーが付けたものは番号と付与したプレイヤーごとに1行(「まとめない」)。
        /// それ以外(付与者なし・モンスター・召喚者の分からない召喚物)は名前で見分けられないので、番号ごとに1行。
        /// </summary>
        ByPlayerCaster,
    }

    /// <summary>
    /// バフ/デバフリストの行。出す条件は <see cref="BuffDisplayRules.IsShownByGameRule"/>(ゲーム内のバフバーと同じ条件)。
    /// 表に名前が無ければ付与元をたどった由来の名前を出し(<see cref="CombatDataCatalog.GetBuffDisplayNameWithoutInternalId"/>)、
    /// 絵はバフの絵(<see cref="CombatDataCatalog.GetBuffOwnIconName"/>)。
    ///
    /// <para>
    /// 行のまとめ方は <paramref name="grouping"/>(<see cref="BuffRowGrouping"/>)。同じ行にまとまる実体が複数あれば、
    /// 残り時間の一番長い実体(持続の無いもの・残りの分からないものが勝つ、同じなら先の実体)の層・残り時間・付与者を出し、足さない。
    /// カードはバフの番号で追い、同じ番号の行のうち残り時間の一番長い行を出す。
    /// </para>
    /// </summary>
    private static IReadOnlyList<PlayerBuffSnapshot> CreateBuffSnapshots(
        Encounter encounter,
        long entityUuid,
        PlayerBuffListKind kind,
        BuffRowGrouping grouping)
    {
        var buffEvents = ResolveDisplayBuffEvents(entityUuid);
        var rowsByKey = new Dictionary<string, PlayerBuffCandidate>(StringComparer.Ordinal);
        var rowKeys = new List<string>(buffEvents.Length);
        var partyCache = new Dictionary<long, CombatEventLogParty>();

        // 初出順(古い順)で回す。ソートせず、新しいバフが末尾に付く並びにする。
        // ストアが初出順で返すので、ここで逆順にすると並びが反転する。
        for (var index = 0; index < buffEvents.Length; index++)
        {
            var buffEvent = buffEvents[index];
            if (!IsIncludedBuff(kind, buffEvent) || !BuffDisplayRules.IsShownByGameRule(buffEvent))
            {
                continue;
            }

            var iconName = buffEvent.BaseId > 0 ? CombatDataCatalog.GetBuffOwnIconName(buffEvent.BaseId) : string.Empty;
            var nameWithoutInternalId = buffEvent.BaseId > 0
                ? CombatDataCatalog.GetBuffDisplayNameWithoutInternalId(buffEvent.BaseId, buffEvent.Origin?.Landing ?? SourceLanding.None)
                : string.Empty;
            var name = buffEvent.BaseId > 0
                ? CombatDataCatalog.AppendBuffInternalId(nameWithoutInternalId, buffEvent.BaseId)
                : string.Empty;
            if (string.IsNullOrWhiteSpace(name) && buffEvent.BaseId <= 0)
            {
                continue;
            }

            DateTime effectiveRemoveTime;
            double? remainingSeconds;
            bool remainingUnknown;
            var timingResolved = TryResolveLiveBuffTiming(
                entityUuid, buffEvent, out effectiveRemoveTime, out remainingSeconds, out remainingUnknown, out var durationSeconds);
            if (!timingResolved)
            {
                continue;
            }

            var cardKey = ResolveBuffCardKey(buffEvent);
            var playerCasterUuid = ResolveBuffPlayerCasterUuid(encounter, buffEvent.FireUuid);
            var key = ResolveBuffRowKey(cardKey, grouping == BuffRowGrouping.ByPlayerCaster ? playerCasterUuid : 0);
            var isNewRow = !rowsByKey.TryGetValue(key, out var currentRow);
            if (!isNewRow && currentRow!.EffectiveRemoveTime >= effectiveRemoveTime)
            {
                continue;
            }

            var snapshot = new PlayerBuffSnapshot(
                buffEvent.Uuid,
                ResolveBuffDisplayId(buffEvent),
                key,
                cardKey,
                name,
                nameWithoutInternalId,
                iconName,
                buffEvent.Layer,
                remainingSeconds,
                remainingUnknown,
                playerCasterUuid != 0 ? ResolveCombatEventLogParty(encounter, playerCasterUuid, partyCache) : null,
                IsCasterTarget: playerCasterUuid != 0 && playerCasterUuid == entityUuid,
                GaugeLengthSeconds: durationSeconds > 0d ? durationSeconds : null);
            rowsByKey[key] = new PlayerBuffCandidate(snapshot, effectiveRemoveTime);
            if (isNewRow)
            {
                rowKeys.Add(key);
            }
        }

        // カードの鍵(バフの番号)ごとに、カードに出す行(残り時間の一番長い行、同じなら先の行)を決める。
        var representativeByCardKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in rowKeys)
        {
            var row = rowsByKey[key];
            if (!representativeByCardKey.TryGetValue(row.Snapshot.CardKey, out var representativeKey)
                || row.EffectiveRemoveTime > rowsByKey[representativeKey].EffectiveRemoveTime)
            {
                representativeByCardKey[row.Snapshot.CardKey] = key;
            }
        }

        var snapshots = new PlayerBuffSnapshot[rowKeys.Count];
        for (var index = 0; index < rowKeys.Count; index++)
        {
            var key = rowKeys[index];
            var snapshot = rowsByKey[key].Snapshot;
            snapshots[index] = string.Equals(representativeByCardKey[snapshot.CardKey], key, StringComparison.Ordinal)
                ? snapshot
                : snapshot with { IsCardRepresentative = false };
        }

        return snapshots;
    }

    /// <summary>
    /// バフを付けたプレイヤーの実体。付与者の実体がプレイヤーならその人、召喚物で大元の召喚者(付与者の実体の <c>AttrTopSummonerId</c>)が
    /// プレイヤーならその人(タイムライン・被ダメログの加害者の寄せ方 <see cref="Encounter.ResolveCombatEventLogActor"/> と同じ結果)。
    /// 付与者が周りから消えて回から外れた後は、召喚者を控え<see cref="Services.EntityOriginIndex"/>から読む(バフは実体より長く残る)。
    /// プレイヤーでなければ 0(付与者なし・モンスター・召喚者の分からない召喚物)。
    /// </summary>
    private static long ResolveBuffPlayerCasterUuid(Encounter encounter, long fireUuid)
    {
        if (fireUuid == 0)
        {
            return 0;
        }

        if ((EEntityType)Utils.UuidToEntityType(fireUuid) == EEntityType.EntChar)
        {
            return fireUuid;
        }

        long summonerUuid;
        if (encounter.Entities.TryGetValue(fireUuid, out var fireEntity))
        {
            summonerUuid = fireEntity.GetAttrKV("AttrTopSummonerId") as long? ?? 0;
        }
        else if (Services.EntityOriginIndex.Instance.TryGetDisappeared(fireUuid, out var disappeared))
        {
            summonerUuid = disappeared.TopSummonerUuid;
        }
        else
        {
            return 0;
        }

        return (EEntityType)Utils.UuidToEntityType(summonerUuid) == EEntityType.EntChar
            ? summonerUuid
            : 0;
    }

    public static PlayerImagineRoleSkillLoadoutSnapshot GetPlayerImagineRoleSkills(long characterId)
    {
        var encounter = ResolvePlayerDetailEncounter();
        if (encounter is null
            || !TryResolvePlayerEntity(encounter, characterId, out var entityUuid, out var entity))
        {
            return EmptyPlayerImagineRoleSkillLoadout();
        }

        return CreatePlayerImagineRoleSkillLoadout(entityUuid, entity, characterId);
    }

    public static bool TryGetPlayerListImagineRoleSkillSourceState(
        long characterId,
        long preferredEntityUuid,
        out long entityUuid,
        out int professionId,
        out object? skillSourceToken,
        out object? roleFilterToken)
    {
        entityUuid = 0;
        professionId = 0;
        skillSourceToken = null;
        roleFilterToken = null;

        // ライブが null でも打ち切らない。下のメーター側からの拾い直しへ進む。
        var encounter = ResolvePlayerDetailEncounter();

        Entity? entity = null;
        if (encounter is not null
            && preferredEntityUuid != 0
            && TryResolvePlayerEntityByUuid(
                encounter,
                characterId,
                preferredEntityUuid,
                out entity))
        {
            entityUuid = preferredEntityUuid;
        }
        else if (encounter is not null
            && PlayerRosterProjection.TryGetPlayerEntityUuid(characterId, out var rosterEntityUuid)
            && TryResolvePlayerEntityByUuid(
                encounter,
                characterId,
                rosterEntityUuid,
                out entity))
        {
            entityUuid = rosterEntityUuid;
        }
        else if (ResolveFallbackDetailEncounter() is { } fallbackEncounter
            && TryResolvePlayerEntity(fallbackEncounter, characterId, out var fallbackEntityUuid, out entity))
        {
            // ライブに居ない灰色の行。メーターが映しているエンカウンターに枠が残っている。
            entityUuid = fallbackEntityUuid;
        }
        else if (PartyMemberCache.Instance.TryGetSkillLevels(characterId, out _))
        {
            // AOI外のパーティメンバーは Entity が存在しないことがある。
            // 保持しているスキル一覧があるなら、それを表示元として続行する。
            entity = null;
            entityUuid = preferredEntityUuid;
        }
        else
        {
            return false;
        }

        professionId = entity?.ProfessionId ?? 0;
        if (professionId <= 0
            && PartyStateStore.Instance.Current.TryGetSupplement(characterId, out var supplement))
        {
            professionId = supplement.ProfessionId;
        }
        if (IsSelfEntity(entityUuid)
            && PlayerSkillLevelStateStore.TryGetSelfCurrentSkillLevels(
                out var currentSkillLevels))
        {
            // 自分の枠はアクションバーから組むので、アクションバーの差し替えでも組み直す。
            skillSourceToken = PlayerSkillLevelStateStore.SelfActionBarToken;
            roleFilterToken = currentSkillLevels;
            return true;
        }

        // 変更検知のトークンは実際の表示元と一致させる。
        // Entity 側に一覧が無いときは保持している一覧が表示元になるので、そちらを指す。
        skillSourceToken = entity?.GetAttrKV("AttrSkillLevelIdList");
        if (skillSourceToken is null
            && PartyMemberCache.Instance.TryGetSkillLevels(characterId, out var cachedSkillLevels))
        {
            skillSourceToken = cachedSkillLevels;
        }

        PlayerSkillLevelStateStore.TryGetSelfCurrentSkillLevels(
            out var roleFilterSkillLevels);
        roleFilterToken = roleFilterSkillLevels;
        return true;
    }

    public static PlayerImagineRoleSkillLoadoutSnapshot GetPlayerListImagineRoleSkills(
        long characterId,
        long entityUuid)
    {
        // ライブに居なければメーターのエンカウンターから拾う。
        // 履歴にしか居ない灰色の行でも、枠はDBに残っているので出せる。
        TryResolveDetailEntity(characterId, entityUuid, out var entity, out _);

        // AOI外のパーティメンバーは Entity が存在しないことがある。
        // その場合でも保持しているスキル一覧から組み立てる。
        return CreatePlayerImagineRoleSkillLoadout(entityUuid, entity, characterId);
    }

    private static PlayerImagineRoleSkillLoadoutSnapshot CreatePlayerImagineRoleSkillLoadout(
        long entityUuid,
        Entity? entity,
        long characterId)
    {
        // 自分はアクションバー(AttrSlot)から枠番号ごと組み立てる。
        // 並び順と空欄がゲームと一致するのはこの経路だけ。
        var isSelf = IsSelfEntity(entityUuid);
        if (isSelf)
        {
            return CreateSelfActionBarLoadout(entityUuid);
        }

        var skillLevels = ResolvePlayerSkillLevels(entityUuid, entity, characterId);

        // 他人は習得済みの集合しか届かず装備を判別できないので、上限を設けず取れた数だけ返す。
        const int SelfRoleSlotCount = 4;

        var imagineSkills = new List<PlayerCooldownSkillSnapshot?>(2);
        var roleSkills = new List<PlayerCooldownSkillSnapshot>();
        var includedSkillIds = new HashSet<int>();

        foreach (var skillLevel in skillLevels)
        {
            if (skillLevel.SkillId <= 0
                || !includedSkillIds.Add(skillLevel.SkillId))
            {
                continue;
            }

            var iconName = CombatDataCatalog.GetSkillIconName(skillLevel.SkillId, skillLevel.Icon);
            var isImagine = CombatDataCatalog.IsSkillImagine(skillLevel.SkillId, iconName);
            var isRole = CombatDataCatalog.IsSkillRole(skillLevel.SkillId);

            var roleFull = isSelf && roleSkills.Count >= SelfRoleSlotCount;
            if ((!isImagine || imagineSkills.Count >= 2)
                && (!isRole || roleFull))
            {
                continue;
            }

            var currentLevel = ResolvePlayerSkillCurrentLevel(entityUuid, skillLevel);
            var showLevel = isRole
                && CombatDataCatalog.HasLevelDependentCooldown(skillLevel.SkillId);
            // スタック数はイマジン固有ではない。ロールスキルにも複数チャージのものがある
            // (例: 3612 不屈の闘志 = MaxEnergyChargeNum 2)。
            var maxCharges = CombatDataCatalog.GetSkillMaxCharges(skillLevel.SkillId);
            var chargeCooldownSeconds = maxCharges > 1
                ? CombatDataCatalog.GetSkillChargeCooldownSeconds(
                    skillLevel.SkillId,
                    skillLevel.Tier)
                : 0d;
            var snapshot = new PlayerCooldownSkillSnapshot(
                skillLevel.SkillId,
                CombatDataCatalog.GetSkillDisplayName(skillLevel.SkillId),
                iconName,
                isImagine,
                CombatDataCatalog.GetSkillPveCooldownSeconds(
                    skillLevel.SkillId,
                    currentLevel,
                    skillLevel.Tier),
                maxCharges,
                chargeCooldownSeconds,
                ResolveSkillGrade(isImagine, showLevel, skillLevel.Tier, currentLevel));

            if (isImagine && imagineSkills.Count < 2)
            {
                imagineSkills.Add(snapshot);
            }
            else if (isRole && !roleFull)
            {
                roleSkills.Add(snapshot);
            }

            if (isSelf
                && imagineSkills.Count == 2
                && roleSkills.Count == SelfRoleSlotCount)
            {
                break;
            }

            // 他人はロールスキルに上限が無いので打ち切らない。
        }

        // 一覧に入るイマジンは装備中のものだけなので、一覧が届いていれば見つからない枠は未装着。
        // null で埋めて2枠にし、一覧が届いていない(不明)の0枠と分ける。自分のアクションバーの空枠と同じ形。
        if (skillLevels.Count > 0)
        {
            while (imagineSkills.Count < 2)
            {
                imagineSkills.Add(null);
            }
        }

        return new PlayerImagineRoleSkillLoadoutSnapshot(
            entityUuid,
            imagineSkills,
            roleSkills,
            isSelf ? SelfRoleSlotCount : roleSkills.Count);
    }

    /// <summary>イマジンの枠番号 7 / 8(左 / 右)。</summary>
    private static readonly int[] SelfImagineSlotIds = [7, 8];

    /// <summary>ロールスキルの枠番号 21〜24。</summary>
    private static readonly int[] SelfRoleSlotIds = [21, 22, 23, 24];

    /// <summary>
    /// 自分のイマジン/ロール枠を、アクションバーの枠番号どおりに組み立てる。
    ///
    /// <para>
    /// 空枠は <c>null</c> のまま返す。<b>詰めない。</b> 詰めると並び順がゲームとずれる。
    /// </para>
    ///
    /// <para>
    /// <c>AttrSkillLevelIdList</c>(習得済みの集合)へは落ちない。同じ AOI 属性でありながら
    /// 枠も装備状態も持たない下位互換なので、自分の枠を作るのに使う理由が無い。
    /// アクションバーが未受信なら空で返し、埋めない。
    /// </para>
    /// </summary>
    private static PlayerImagineRoleSkillLoadoutSnapshot CreateSelfActionBarLoadout(long entityUuid)
    {
        if (!PlayerSkillLevelStateStore.HasSelfActionBarSlots)
        {
            // 未受信。それらしい値で埋めると、受信できていないことが空欄と同じ見た目になる。
            return new PlayerImagineRoleSkillLoadoutSnapshot(
                entityUuid,
                Array.Empty<PlayerCooldownSkillSnapshot?>(),
                Array.Empty<PlayerCooldownSkillSnapshot?>(),
                0);
        }

        var imagineSkills = BuildSelfActionBarSlots(entityUuid, SelfImagineSlotIds);
        var roleSkills = BuildSelfActionBarSlots(entityUuid, SelfRoleSlotIds);
        return new PlayerImagineRoleSkillLoadoutSnapshot(
            entityUuid,
            imagineSkills,
            roleSkills,
            roleSkills.Length);
    }

    private static PlayerCooldownSkillSnapshot?[] BuildSelfActionBarSlots(
        long entityUuid,
        IReadOnlyList<int> slotIds)
    {
        var result = new PlayerCooldownSkillSnapshot?[slotIds.Count];
        for (var index = 0; index < slotIds.Count; index++)
        {
            var skillId = PlayerSkillLevelStateStore.GetSelfActionBarSkillId(slotIds[index]);
            result[index] = skillId > 0
                ? CreateSelfSlotSnapshot(entityUuid, skillId)
                : null;
        }

        return result;
    }

    private static PlayerCooldownSkillSnapshot CreateSelfSlotSnapshot(long entityUuid, int skillId)
    {
        PlayerSkillLevelStateStore.TryGetSelfLearnedSkill(skillId, out var learnedLevel, out var tier);
        var skillLevel = new DataTypes.Skills.SkillLevelInfo
        {
            SkillId = skillId,
            CurrentLevel = learnedLevel,
            Tier = tier
        };

        var iconName = CombatDataCatalog.GetSkillIconName(skillId, string.Empty);
        var isImagine = CombatDataCatalog.IsSkillImagine(skillId, iconName);
        var isRole = CombatDataCatalog.IsSkillRole(skillId);
        var showLevel = isRole && CombatDataCatalog.HasLevelDependentCooldown(skillId);
        var currentLevel = ResolvePlayerSkillCurrentLevel(entityUuid, skillLevel);
        var maxCharges = CombatDataCatalog.GetSkillMaxCharges(skillId);
        return new PlayerCooldownSkillSnapshot(
            skillId,
            CombatDataCatalog.GetSkillDisplayName(skillId),
            iconName,
            isImagine,
            CombatDataCatalog.GetSkillPveCooldownSeconds(skillId, currentLevel, tier),
            maxCharges,
            maxCharges > 1
                ? CombatDataCatalog.GetSkillChargeCooldownSeconds(skillId, tier)
                : 0d,
            ResolveSkillGrade(isImagine, showLevel, tier, currentLevel));
    }

    /// <summary>
    /// 技の G○ の数字。プレイヤーリストの技の枠の TIPS と推移グラフの技のアイコンの TIPS が使う。
    /// イマジンは改造段階、CD がレベルで変わるロールスキルはレベル、それ以外は無し。
    /// 値が負なら無し(G を出さない)。負の値は通信でそのまま届いたもので、観測は NPC(助っ人)のイマジンの段階 -1 だけ。
    /// </summary>
    internal static int? ResolveSkillGrade(bool isImagine, bool showLevel, int tier, int currentLevel)
    {
        int? grade = isImagine
            ? tier
            : showLevel
                ? currentLevel
                : null;
        return grade is >= 0 ? grade : null;
    }

    /// <summary>
    /// 技の枠と推移グラフの技のアイコンの TIPS に出す名前。G○ の数字(<see cref="ResolveSkillGrade"/>)があれば「名前 G○」、無ければ名前だけ。
    /// </summary>
    public static string FormatSkillNameWithGrade(string name, int? grade)
    {
        return grade is { } value ? $"{name} G{value}" : name;
    }

    private static PlayerImagineRoleSkillLoadoutSnapshot EmptyPlayerImagineRoleSkillLoadout()
    {
        return new PlayerImagineRoleSkillLoadoutSnapshot(
            0,
            Array.Empty<PlayerCooldownSkillSnapshot>(),
            Array.Empty<PlayerCooldownSkillSnapshot>(),
            0);
    }

    public static IReadOnlyDictionary<int, PlayerSkillEffectSnapshot> GetPlayerListSkillEffects(
        long characterId,
        long entityUuid,
        IReadOnlySet<int> skillIds)
    {
        if (skillIds.Count == 0 || entityUuid == 0)
        {
            return new Dictionary<int, PlayerSkillEffectSnapshot>();
        }

        var encounter = ResolvePlayerDetailEncounter();
        if (encounter is null
            || !TryResolvePlayerEntityByUuid(
                encounter,
                characterId,
                entityUuid,
                out var entity))
        {
            return new Dictionary<int, PlayerSkillEffectSnapshot>();
        }

        return CreateSkillEffectSnapshots(
            encounter,
            entity,
            skillIds);
    }

    /// <summary>
    /// 蘇生不可デバフの残り秒。
    ///
    /// <para>
    /// <b>スキル枠を経由しない。</b> エンティティが保持しているバフを直接見るので、
    /// そのプレイヤーが復活スキルを装備しているかに関係なく拾える。
    /// スキル枠経由(<see cref="CreateSkillEffectSnapshots"/>)は
    /// <c>trackedSkillIds</c> に当たったものしか残さないため、装備者しか出せない。
    /// </para>
    ///
    /// <para>
    /// 残り秒が出せないバフは <c>false</c> を返す。持続が分からないものを
    /// 蘇生不可として表示すると、切れた後も出しっぱなしになる。
    /// </para>
    /// </summary>
    public static bool TryGetReviveBlockSeconds(
        long characterId,
        long entityUuid,
        out double remainingSeconds)
    {
        remainingSeconds = 0d;
        var encounter = ResolvePlayerDetailEncounter();
        if (encounter is null
            || entityUuid == 0
            || !TryResolvePlayerEntityByUuid(encounter, characterId, entityUuid, out var entity))
        {
            return false;
        }

        return TryScanReviveBlockDebuff(entity.UUID, out remainingSeconds, out _, out _, out _);
    }

    /// <summary>
    /// 戦闘不能の人の、蘇生不可デバフから見た状態。蘇生リストの右の文字と並びに使う。
    ///
    /// <para>
    /// 「付いていない」と言えるのは、その人のバフが全部届いているときだけ。自分は途中起動でないとき、
    /// 他の人は周りにいて、このマップで出現時の全バフの一覧を受けたとき。
    /// それ以外で見つからないのは、付いていないのか届いていないのか分からないので不明にする(付いていないことにすると、蘇生できない人を蘇生できると出す)。
    /// </para>
    /// </summary>
    public static ReviveBlockState GetReviveBlockState(PlayerRosterEntry player)
    {
        if (!PlayerRosterProjection.TryGetPlayerEntityUuid(player.CharacterId, out var entityUuid) || entityUuid == 0)
        {
            return new ReviveBlockState(ReviveBlockStateKind.Unknown);
        }

        if (TryScanReviveBlockDebuff(entityUuid, out var remainingSeconds, out var durationSeconds, out var endTimeUtc, out var hasUnresolved))
        {
            return new ReviveBlockState(ReviveBlockStateKind.Blocked, remainingSeconds, durationSeconds, endTimeUtc);
        }

        if (hasUnresolved)
        {
            return new ReviveBlockState(ReviveBlockStateKind.Unknown);
        }

        var reception = GameReceptionStateStore.Current;
        var hasAllBuffs = player.IsSelf
            ? !reception.IsMidLaunch
            : player.IsNearby && Services.ActiveBuffStore.Instance.HasReceivedAppearList(entityUuid);
        return new ReviveBlockState(hasAllBuffs ? ReviveBlockStateKind.Revivable : ReviveBlockStateKind.Unknown);
    }

    /// <summary>
    /// 実体に付いている蘇生不可デバフを探す。残り秒の分かるものがあれば true で、重ね掛けなら遅く切れるほうの残り秒。
    /// </summary>
    /// <param name="durationSeconds">残り秒を出したデバフの効果時間の総量(秒)。</param>
    /// <param name="endTimeUtc">残り秒を出したデバフの切れる時刻(UTC)。</param>
    /// <param name="hasUnresolved">付いているのに残り秒の出せないもの(持続が負・経過が分からない)があった。</param>
    private static bool TryScanReviveBlockDebuff(
        long entityUuid,
        out double remainingSeconds,
        out double durationSeconds,
        out DateTime endTimeUtc,
        out bool hasUnresolved)
    {
        remainingSeconds = 0d;
        durationSeconds = 0d;
        endTimeUtc = default;
        hasUnresolved = false;
        var buffEvents = ResolveDisplayBuffEvents(entityUuid);
        var found = false;
        var latestRemoveTime = DateTime.MinValue;

        for (var index = 0; index < buffEvents.Length; index++)
        {
            var buffEvent = buffEvents[index];
            if (buffEvent.BaseId != ReviveBlockDebuffBaseId)
            {
                continue;
            }

            if (buffEvent.Duration < 0)
            {
                hasUnresolved = true;
                continue;
            }

            DateTime effectiveRemoveTime;
            double? seconds;
            var timingResolved = TryResolveLiveBuffTiming(
                entityUuid, buffEvent, out effectiveRemoveTime, out seconds, out var secondsUnknown, out var instanceDurationSeconds);
            // 経過が分からないものはカウントに使わない。出すと切れた後も残る。
            if (!timingResolved || secondsUnknown || seconds is null)
            {
                hasUnresolved = true;
                continue;
            }

            // 重ね掛けされたら遅く切れるほうを採る。バッジの選び方と同じ。
            if (found && effectiveRemoveTime <= latestRemoveTime)
            {
                continue;
            }

            latestRemoveTime = effectiveRemoveTime;
            remainingSeconds = seconds.Value;
            durationSeconds = instanceDurationSeconds;
            endTimeUtc = effectiveRemoveTime;
            found = true;
        }

        return found;
    }

    private static IReadOnlyDictionary<int, PlayerSkillEffectSnapshot> CreateSkillEffectSnapshots(
        Encounter encounter,
        Entity entity,
        IReadOnlySet<int> trackedSkillIds)
    {
        var buffEvents = ResolveDisplayBuffEvents(entity.UUID);
        var runtimeSourceParentsByBaseId = BuildRuntimeSourceParentsByBaseId(
            entity,
            buffEvents);
        var candidatesBySkill = new Dictionary<
            int,
            (PlayerBuffCandidate? Buff, PlayerBuffCandidate? Debuff)>();
        var resolvedTrackedSkillIdsBySource = new Dictionary<Services.BuffSource, int>();
        var resolvedTrackedSkillIdsBySummonUuid = new Dictionary<long, int>();

        foreach (var buffEvent in buffEvents)
        {
            if (buffEvent.Duration < 0
                || buffEvent.SourceConfigId <= 0
                || BuffDisplayRules.IsHiddenFromGameBuffBar(buffEvent))
            {
                continue;
            }

            // 蘇生不可はスキル枠のバッジには出さない。HPテキストのほうで扱う。
            if (buffEvent.BaseId == ReviveBlockDebuffBaseId)
            {
                continue;
            }

            var source = new Services.BuffSource(buffEvent.FightSourceType, buffEvent.SourceConfigId);
            if (!resolvedTrackedSkillIdsBySource.TryGetValue(source, out var trackedSkillId))
            {
                trackedSkillId = ResolveTrackedSourceSkillId(
                    source,
                    trackedSkillIds,
                    runtimeSourceParentsByBaseId);
                resolvedTrackedSkillIdsBySource[source] = trackedSkillId;
            }

            if (trackedSkillId <= 0)
            {
                // 召喚したエンティティが付けたバフは、鎖が装備中スキルまで届かない。
                // 術者側から辿り直す。
                trackedSkillId = ResolveTrackedSkillFromSummonCaster(
                    encounter,
                    buffEvent.FireUuid,
                    trackedSkillIds,
                    resolvedTrackedSkillIdsBySummonUuid);
            }

            if (trackedSkillId <= 0)
            {
                continue;
            }

            var isBuff = IsIncludedBuff(PlayerBuffListKind.Buff, buffEvent);
            var isDebuff = IsIncludedBuff(PlayerBuffListKind.Debuff, buffEvent);
            if (!isBuff && !isDebuff)
            {
                continue;
            }

            var iconName = ResolveBuffOwnIconName(buffEvent);
            if (string.IsNullOrWhiteSpace(iconName))
            {
                continue;
            }

            var name = ResolveBuffName(buffEvent);
            if (string.IsNullOrWhiteSpace(name) && buffEvent.BaseId <= 0)
            {
                continue;
            }

            DateTime effectiveRemoveTime;
            double? remainingSeconds;
            bool remainingUnknown;
            var timingResolved = TryResolveLiveBuffTiming(
                entity.UUID, buffEvent, out effectiveRemoveTime, out remainingSeconds, out remainingUnknown, out _);
            if (!timingResolved)
            {
                continue;
            }

            candidatesBySkill.TryGetValue(trackedSkillId, out var candidates);

            var key = ResolveBuffCardKey(buffEvent);
            var snapshot = new PlayerBuffSnapshot(
                buffEvent.Uuid,
                ResolveBuffDisplayId(buffEvent),
                key,
                key,
                name,
                CombatDataCatalog.GetBuffNameWithoutInternalId(buffEvent.BaseId),
                iconName,
                buffEvent.Layer,
                remainingSeconds,
                remainingUnknown);
            // 持続なし・経過不明は、残り時間を持つバフに譲る。同点なら先に並ぶものが残る。
            var badgeOrderingKey = effectiveRemoveTime == DateTime.MaxValue ? DateTime.MinValue : effectiveRemoveTime;
            var candidate = new PlayerBuffCandidate(snapshot, badgeOrderingKey);

            if (isDebuff)
            {
                if (candidates.Debuff is null
                    || candidate.EffectiveRemoveTime > candidates.Debuff.EffectiveRemoveTime)
                {
                    candidates.Debuff = candidate;
                }
            }
            else if (candidates.Buff is null
                     || candidate.EffectiveRemoveTime > candidates.Buff.EffectiveRemoveTime)
            {
                candidates.Buff = candidate;
            }

            candidatesBySkill[trackedSkillId] = candidates;
        }

        return candidatesBySkill.ToDictionary(
            pair => pair.Key,
            pair => new PlayerSkillEffectSnapshot(
                pair.Value.Buff?.Snapshot,
                pair.Value.Debuff?.Snapshot));
    }

    /// <summary>
    /// バフの付与元から、装備中のスキル(イマジン・ロール)を辿る。
    ///
    /// <para>
    /// <b>付与元は種類と番号の組で扱う。</b>スキルの表とバフの表は番号が重なるので、番号だけで引くと
    /// バフの番号をスキルとして、スキルの番号をバフとして辿ってしまう。
    /// スキルの表(装備中との一致・<c>SkillLevelGroup</c> などのつながり・同じイマジンアイコン)を引くのは技のときだけ、
    /// 実行時の親の対応を辿るのはバフのときだけ。弾・タレントなど他の種類はそこで止め、帰属させない。
    /// </para>
    /// </summary>
    private static int ResolveTrackedSourceSkillId(
        Services.BuffSource source,
        IReadOnlySet<int> trackedSkillIds,
        IReadOnlyDictionary<int, HashSet<Services.BuffSource>> runtimeSourceParentsByBaseId)
    {
        if (source.SourceConfigId <= 0 || !IsTraceableSourceType(source.FightSourceType))
        {
            return 0;
        }

        if (source.FightSourceType == (int)EFightSource.Skill)
        {
            var directTrackedSkillId = ResolveTrackedSourceSkillDirectly(
                source.SourceConfigId,
                trackedSkillIds);
            if (directTrackedSkillId > 0)
            {
                return directTrackedSkillId;
            }
        }

        var pendingSources = new Queue<Services.BuffSource>();
        var visitedSources = new HashSet<Services.BuffSource>();
        var resolvedTrackedSkillIds = new HashSet<int>();
        pendingSources.Enqueue(source);

        while (pendingSources.Count > 0)
        {
            var current = pendingSources.Dequeue();
            if (!visitedSources.Add(current))
            {
                continue;
            }

            if (current.FightSourceType == (int)EFightSource.Skill)
            {
                if (current != source)
                {
                    var resolvedTrackedSkillId = ResolveTrackedSourceSkillDirectly(
                        current.SourceConfigId,
                        trackedSkillIds);
                    if (resolvedTrackedSkillId > 0)
                    {
                        resolvedTrackedSkillIds.Add(resolvedTrackedSkillId);
                        if (resolvedTrackedSkillIds.Count > 1)
                        {
                            return 0;
                        }
                    }
                }

                if (HelperMethods.DataTables.Skills.Data.TryGetValue(
                        current.SourceConfigId.ToString(),
                        out var sourceSkill))
                {
                    EnqueueLinkedSkill(pendingSources, sourceSkill.SkillLevelGroup, current.SourceConfigId);
                    EnqueueLinkedSkill(pendingSources, sourceSkill.SwitchSkillId, current.SourceConfigId);
                    EnqueueLinkedSkill(pendingSources, sourceSkill.NextSkillId, current.SourceConfigId);
                }

                continue;
            }

            if (runtimeSourceParentsByBaseId.TryGetValue(
                    current.SourceConfigId,
                    out var runtimeParents))
            {
                foreach (var runtimeParent in runtimeParents)
                {
                    if (runtimeParent.SourceConfigId > 0
                        && IsTraceableSourceType(runtimeParent.FightSourceType)
                        && runtimeParent != current)
                    {
                        pendingSources.Enqueue(runtimeParent);
                    }
                }
            }
        }

        return resolvedTrackedSkillIds.Count == 1
            ? resolvedTrackedSkillIds.First()
            : 0;
    }

    private static bool IsTraceableSourceType(int fightSourceType)
    {
        return fightSourceType == (int)EFightSource.Skill
            || fightSourceType == (int)EFightSource.Buff;
    }

    private static void EnqueueLinkedSkill(
        Queue<Services.BuffSource> sources,
        int linkedSkillId,
        int currentSkillId)
    {
        if (linkedSkillId > 0 && linkedSkillId != currentSkillId)
        {
            sources.Enqueue(new Services.BuffSource((int)EFightSource.Skill, linkedSkillId));
        }
    }

    /// <summary>
    /// 召喚エンティティが付けたバフを、召喚元のスキルへ帰属させる。
    ///
    /// <para>
    /// 召喚を伴うイマジンのバフは <c>SourceConfigId</c> が<b>バフID</b>を指すので、
    /// <c>SkillLevelGroup</c> 等の既存の鎖はそこで途切れる(<c>BuffTable.SkillId</c> も 0)。
    /// </para>
    ///
    /// <para>
    /// 繋がるのは召喚体側のテーブル。<c>MonsterTable.BornSkillId</c> が
    /// <c>SkillTable.EffectIDs</c> に入っているエフェクトIDなので、そこを逆引きする。
    /// <b><c>エフェクトID / 100</c> の規則には頼らない。</b>
    /// </para>
    ///
    /// <para>
    /// 術者が召喚体かどうかは <b>UUIDのビット</b>(<see cref="Utils.IsSummonByUuid"/>)で判る。
    /// 推測ではなく、テーブルとワイヤに書かれた参照だけを辿る。
    /// 複数の装備中スキルに当たったときは<b>帰属させない</b>(既存の鎖と同じ方針)。
    /// </para>
    /// </summary>
    private static int ResolveTrackedSkillFromSummonCaster(
        Encounter encounter,
        long fireUuid,
        IReadOnlySet<int> trackedSkillIds,
        Dictionary<long, int> resolvedBySummonUuid)
    {
        if (fireUuid == 0 || !Utils.IsSummonByUuid(fireUuid))
        {
            return 0;
        }

        if (resolvedBySummonUuid.TryGetValue(fireUuid, out var cached))
        {
            return cached;
        }

        var resolved = 0;

        // 召喚体が周りから消えて回から外れた後は、種別IDを控えから読む(バフは実体より長く残る)。
        long summonMonsterId = 0;
        if (encounter.Entities.TryGetValue(fireUuid, out var caster))
        {
            summonMonsterId = caster.UID;
        }
        else if (Services.EntityOriginIndex.Instance.TryGetDisappearedMonsterId(fireUuid, out var disappearedMonsterId))
        {
            summonMonsterId = disappearedMonsterId;
        }

        if (summonMonsterId > 0
            && HelperMethods.DataTables.Monsters.Data.TryGetValue(
                summonMonsterId.ToString(),
                out var monster))
        {
            var summonSkillIds = new HashSet<int>();
            if (monster.BornSkillId > 0)
            {
                summonSkillIds.Add(monster.BornSkillId);
            }

            if (monster.SkillIds is not null)
            {
                foreach (var summonSkillId in monster.SkillIds)
                {
                    if (summonSkillId > 0)
                    {
                        summonSkillIds.Add(summonSkillId);
                    }
                }
            }

            foreach (var trackedSkillId in trackedSkillIds)
            {
                if (!SummonBelongsToSkill(trackedSkillId, summonSkillIds))
                {
                    continue;
                }

                if (resolved > 0 && resolved != trackedSkillId)
                {
                    resolved = 0;
                    break;
                }

                resolved = trackedSkillId;
            }
        }

        resolvedBySummonUuid[fireUuid] = resolved;
        return resolved;
    }

    /// <summary>
    /// 召喚体が持つスキル/エフェクトIDが、その装備中スキルのものか。
    /// エフェクトIDは <c>SkillTable.EffectIDs</c> の逆引きで確認する
    /// (<c>エフェクトID / 100</c> のような規則には頼らない)。
    /// </summary>
    private static bool SummonBelongsToSkill(int trackedSkillId, HashSet<int> summonSkillIds)
    {
        if (summonSkillIds.Contains(trackedSkillId))
        {
            return true;
        }

        if (!HelperMethods.DataTables.Skills.Data.TryGetValue(
                trackedSkillId.ToString(),
                out var trackedSkill)
            || trackedSkill.EffectIDs is null)
        {
            return false;
        }

        foreach (var effectId in trackedSkill.EffectIDs)
        {
            if (summonSkillIds.Contains(effectId))
            {
                return true;
            }
        }

        return false;
    }

    private static int ResolveTrackedSourceSkillDirectly(
        int sourceConfigId,
        IReadOnlySet<int> trackedSkillIds)
    {
        if (trackedSkillIds.Contains(sourceConfigId))
        {
            return sourceConfigId;
        }

        HelperMethods.DataTables.Skills.Data.TryGetValue(
            sourceConfigId.ToString(),
            out var sourceSkill);

        if (sourceSkill is not null)
        {
            if (sourceSkill.SkillLevelGroup > 0
                && trackedSkillIds.Contains(sourceSkill.SkillLevelGroup))
            {
                return sourceSkill.SkillLevelGroup;
            }

            if (sourceSkill.SwitchSkillId > 0
                && trackedSkillIds.Contains(sourceSkill.SwitchSkillId))
            {
                return sourceSkill.SwitchSkillId;
            }

            if (sourceSkill.NextSkillId > 0
                && trackedSkillIds.Contains(sourceSkill.NextSkillId))
            {
                return sourceSkill.NextSkillId;
            }
        }

        var reverseLinkedTrackedSkillId = 0;
        foreach (var trackedSkillId in trackedSkillIds)
        {
            if (!HelperMethods.DataTables.Skills.Data.TryGetValue(
                    trackedSkillId.ToString(),
                    out var trackedSkill)
                || (trackedSkill.SwitchSkillId != sourceConfigId
                    && trackedSkill.NextSkillId != sourceConfigId))
            {
                continue;
            }

            if (reverseLinkedTrackedSkillId > 0
                && reverseLinkedTrackedSkillId != trackedSkillId)
            {
                return 0;
            }

            reverseLinkedTrackedSkillId = trackedSkillId;
        }

        if (reverseLinkedTrackedSkillId > 0)
        {
            return reverseLinkedTrackedSkillId;
        }

        return ResolveTrackedSourceSkillBySharedImagineIcon(
            sourceSkill,
            trackedSkillIds);
    }

    private static int ResolveTrackedSourceSkillBySharedImagineIcon(
        DataTypes.Skill? sourceSkill,
        IReadOnlySet<int> trackedSkillIds)
    {
        if (sourceSkill is null)
        {
            return 0;
        }

        var sourceIcon = NormalizeSkillFamilyIcon(sourceSkill.GetIconName());
        if (string.IsNullOrWhiteSpace(sourceIcon)
            || !sourceIcon.Contains("skill_aoyi", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var matchedTrackedSkillId = 0;
        foreach (var trackedSkillId in trackedSkillIds)
        {
            if (!HelperMethods.DataTables.Skills.Data.TryGetValue(
                    trackedSkillId.ToString(),
                    out var trackedSkill)
                || !string.Equals(
                    sourceIcon,
                    NormalizeSkillFamilyIcon(trackedSkill.GetIconName()),
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (matchedTrackedSkillId > 0 && matchedTrackedSkillId != trackedSkillId)
            {
                return 0;
            }

            matchedTrackedSkillId = trackedSkillId;
        }

        return matchedTrackedSkillId;
    }

    private static Dictionary<int, HashSet<Services.BuffSource>> BuildRuntimeSourceParentsByBaseId(
        Entity entity,
        IReadOnlyCollection<BuffEvent> buffEvents)
    {
        var result = new Dictionary<int, HashSet<Services.BuffSource>>();

        foreach (var buffEvent in buffEvents)
        {
            AddRuntimeSourceParent(result, buffEvent);
        }

        var recentBuffEvents = entity.RecentBuffEventHistory;
        if (recentBuffEvents is not null)
        {
            foreach (var buffEvent in recentBuffEvents.Values)
            {
                AddRuntimeSourceParent(result, buffEvent);
            }
        }

        // 既に切れた/解除されたバフの対応も逆引きには必要。
        // エンカウンターが作り直されると entity 側の履歴は空になるため、
        // 境界を跨いで蓄積しているストア側の索引を合流させる。
        Services.ActiveBuffStore.Instance.CopySourceParentsInto(entity.UUID, result);

        return result;
    }

    private static void AddRuntimeSourceParent(
        Dictionary<int, HashSet<Services.BuffSource>> runtimeSourceParentsByBaseId,
        BuffEvent buffEvent)
    {
        if (buffEvent.BaseId <= 0
            || buffEvent.SourceConfigId <= 0
            || (buffEvent.FightSourceType == (int)EFightSource.Buff && buffEvent.BaseId == buffEvent.SourceConfigId))
        {
            return;
        }

        if (!runtimeSourceParentsByBaseId.TryGetValue(buffEvent.BaseId, out var sources))
        {
            sources = new HashSet<Services.BuffSource>();
            runtimeSourceParentsByBaseId.Add(buffEvent.BaseId, sources);
        }

        sources.Add(new Services.BuffSource(buffEvent.FightSourceType, buffEvent.SourceConfigId));
    }

    private static string NormalizeSkillFamilyIcon(string? iconName)
    {
        if (string.IsNullOrWhiteSpace(iconName))
        {
            return string.Empty;
        }

        var normalized = iconName.Trim().Replace('\\', '/');
        var separatorIndex = normalized.LastIndexOf('/');
        return separatorIndex >= 0
            ? normalized[(separatorIndex + 1)..]
            : normalized;
    }

    /// <summary>
    /// 推移を <paramref name="aggregationIntervalSeconds"/> 秒ごとの重ならない区切りで返す。
    ///
    /// <para>
    /// 区切り j は (jN, (j+1)N] 秒で、値は中の1秒の合計の和 ÷ N。1件のダメージは1つの区切りにだけ入る。
    /// ライブでは満ちていない最後の区切りを出さない。戦闘が閉じているときだけ、最後の半端な区切りを
    /// 実際の長さ(1秒未満の端数を含む)で割って出す。選べる N は App の一覧が決め、ここは1秒以上なら受ける。
    /// </para>
    ///
    /// <para>
    /// 読むたびに全部の区切りを計算し直すが(遅れて届いた分も反映する)、<paramref name="buffer"/> は値が変わった区切りだけ書き換え、何も作らない。
    /// 点が変わったら true を返すので、読み手はそのときだけ表示へ渡す。
    /// </para>
    /// </summary>
    public static bool ReadPlayerGraph(
        MeterSnapshotKind kind,
        long characterId,
        int aggregationIntervalSeconds,
        MetricGraphPointBuffer buffer)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(aggregationIntervalSeconds, 1);
        var intervalSeconds = aggregationIntervalSeconds;
        var encounter = ResolveActiveEncounter();
        if (encounter is null
            || !TryResolvePlayerEntity(encounter, characterId, out _, out var entity))
        {
            return buffer.Clear();
        }

        var stats = kind == MeterSnapshotKind.Damage
            ? entity.DamageStats
            : entity.HealingStats;

        // 最後の値の時刻は時計より先に読む(後に読むと、その間に届いた値の時刻が読んだ時計の終点より後になる)。
        if (!stats.TryGetLastPerSecondTimestamp(out var lastValueTime))
        {
            return buffer.Clear();
        }

        // 秒の区切りは記録時に戦闘の時計の起点から切ってある。起点が無いのに合計があるなら記録側が壊れている。
        var clock = encounter.ReadCombatClock();
        var endTime = clock.EndUtc
            ?? throw new InvalidOperationException(
                $"Per-second totals exist without FirstDamageTimeStamp (encounter={encounter.EncounterId}).");

        // 記録は時計の終点より前にしか入らないはず(ライブは今、閉じた回は閉じた時刻、計測は窓の終わり)。
        // 後ろにあれば時刻の出所が食い違っているので、伸ばさずに終点まで描き、回ごとに1回だけ残す。
        if (lastValueTime > endTime && !ReferenceEquals(_graphEndErrorEncounter, encounter))
        {
            _graphEndErrorEncounter = encounter;
            Log.Error("Graph value at {LastValue:o} is after the combat clock end {End:o} (encounter={EncounterId}); values after the end are not drawn",
                lastValueTime, endTime, encounter.EncounterId);
        }

        var elapsedSeconds = clock.Elapsed.TotalSeconds;
        var fullBucketCount = (int)Math.Floor(elapsedSeconds / intervalSeconds);
        var partialBucketSeconds = elapsedSeconds - fullBucketCount * (double)intervalSeconds;
        var hasPartialBucket = clock.IsClosed && partialBucketSeconds > 0d;
        var bucketCount = fullBucketCount + (hasPartialBucket ? 1 : 0);
        if (bucketCount == 0)
        {
            return buffer.Clear();
        }

        var bucketTotals = buffer.GetClearedBucketTotals(bucketCount);
        stats.AddPerSecondTotalsTo(bucketTotals, bucketCount, intervalSeconds);

        buffer.EnsureCapacity(bucketCount);
        var changed = false;
        for (var bucket = 0; bucket < fullBucketCount; bucket++)
        {
            changed |= buffer.SetPoint(
                bucket,
                new MetricGraphPoint(
                    bucket * (double)intervalSeconds,
                    (bucket + 1) * (double)intervalSeconds,
                    bucketTotals[bucket] / intervalSeconds));
        }

        if (hasPartialBucket)
        {
            changed |= buffer.SetPoint(
                fullBucketCount,
                new MetricGraphPoint(
                    fullBucketCount * (double)intervalSeconds,
                    elapsedSeconds,
                    bucketTotals[fullBucketCount] / partialBucketSeconds));
        }

        changed |= buffer.SetCount(bucketCount);
        return changed;
    }

    /// <summary>
    /// 推移グラフに出すプレイヤーの技の開始(届いた順)の、<paramref name="startIndex"/> 番目から後の分。回は線と同じ(<see cref="ResolveActiveEncounter"/>)。
    /// 表示中の回か G○ の元の一覧が控え(<paramref name="knownEncounter"/>・<paramref name="knownGradeSource"/>)と違えば先頭から読む。足す分が無ければ何も作らない。
    /// 戦闘の時計の起点がまだ無い回は読み進めない(線も無い)。起点より前の開始は 0 秒に置く
    /// (時計は戦闘していない時間を1点に縮める。自動一時停止の中の開始が止まった時点に重なるのと同じ扱い)。
    /// </summary>
    public static MetricGraphSkillActivationSlice GetPlayerSkillActivations(
        long characterId,
        Encounter? knownEncounter,
        object? knownGradeSource,
        int startIndex)
    {
        var encounter = ResolveActiveEncounter();
        if (encounter is null
            || !TryResolvePlayerEntity(encounter, characterId, out var entityUuid, out var entity))
        {
            return new MetricGraphSkillActivationSlice(null, null, 0, Array.Empty<MetricGraphSkillActivation>());
        }

        var gradeSource = ResolveSkillGradeSource(entityUuid, entity, characterId);
        if (!ReferenceEquals(encounter, knownEncounter) || !ReferenceEquals(gradeSource, knownGradeSource))
        {
            startIndex = 0;
        }

        if (encounter.ToCombatOffset(DateTime.UtcNow) is null)
        {
            return new MetricGraphSkillActivationSlice(encounter, gradeSource, startIndex, Array.Empty<MetricGraphSkillActivation>());
        }

        var records = encounter.GetSkillActivationsFrom(entityUuid, startIndex, out var nextIndex);
        if (records.Length == 0)
        {
            return new MetricGraphSkillActivationSlice(encounter, gradeSource, nextIndex, Array.Empty<MetricGraphSkillActivation>());
        }

        var grades = ResolveSkillGrades(entityUuid, entity, characterId);
        var activations = new MetricGraphSkillActivation[records.Length];
        for (var index = 0; index < records.Length; index++)
        {
            var record = records[index];
            var (name, iconName, frame, usesImagineAsset) = CombatDataCatalog.GetSkillActivationDisplay(record.SkillId);
            activations[index] = new MetricGraphSkillActivation(
                Math.Max(encounter.ToCombatOffset(record.Timestamp)!.Value.TotalSeconds, 0d),
                record.SkillId,
                iconName,
                name,
                frame,
                usesImagineAsset,
                grades.TryGetValue(record.SkillId, out var grade) ? grade : null);
        }

        return new MetricGraphSkillActivationSlice(encounter, gradeSource, nextIndex, activations);
    }

    /// <summary>
    /// G○ の元の一覧(<see cref="ResolvePlayerSkillLevels"/> が読む一覧そのもの)。参照が替わったら G○ を読み直す。
    /// プレイヤーリストの技の枠が一覧の変化を見る目印と同じ参照。
    /// </summary>
    private static object? ResolveSkillGradeSource(long entityUuid, Entity? entity, long characterId)
    {
        var isSelf = IsSelfEntity(entityUuid);
        if (isSelf && PlayerSkillLevelStateStore.TryGetSelfCurrentSkillLevels(out var currentSkillLevels))
        {
            return currentSkillLevels;
        }

        var received = entity?.GetAttrKV("AttrSkillLevelIdList");
        var isEmpty = received switch
        {
            List<DataTypes.Skills.SkillLevelInfo> typedList => typedList.Count == 0,
            JArray jsonArray => jsonArray.Count == 0,
            _ => true
        };
        return isEmpty && !isSelf && PartyMemberCache.Instance.TryGetSkillLevels(characterId, out var cachedSkillLevels)
            ? cachedSkillLevels
            : received;
    }

    /// <summary>
    /// 技ID → G○ の数字。プレイヤーリストの技の枠(<see cref="CreatePlayerImagineRoleSkillLoadout"/>)と同じ技の一覧で、
    /// 数字は同じ <see cref="ResolveSkillGrade"/> で決める。数字の無い技は入れない。
    /// </summary>
    private static Dictionary<int, int> ResolveSkillGrades(long entityUuid, Entity? entity, long characterId)
    {
        var grades = new Dictionary<int, int>();
        foreach (var skillLevel in ResolvePlayerSkillLevels(entityUuid, entity, characterId))
        {
            if (skillLevel.SkillId <= 0 || grades.ContainsKey(skillLevel.SkillId))
            {
                continue;
            }

            var iconName = CombatDataCatalog.GetSkillIconName(skillLevel.SkillId, skillLevel.Icon);
            var isImagine = CombatDataCatalog.IsSkillImagine(skillLevel.SkillId, iconName);
            var showLevel = CombatDataCatalog.IsSkillRole(skillLevel.SkillId)
                && CombatDataCatalog.HasLevelDependentCooldown(skillLevel.SkillId);
            if (ResolveSkillGrade(
                    isImagine,
                    showLevel,
                    skillLevel.Tier,
                    ResolvePlayerSkillCurrentLevel(entityUuid, skillLevel)) is { } grade)
            {
                grades.Add(skillLevel.SkillId, grade);
            }
        }

        return grades;
    }

    public static PlayerMetricSummarySnapshot GetPlayerMetricSummary(PlayerMetricSummaryKind kind, long characterId)
    {
        var encounter = ResolveActiveEncounter();
        if (encounter is null
            || !TryResolvePlayerEntity(encounter, characterId, out _, out var entity))
        {
            return EmptyMetricSummary;
        }

        var stats = kind switch
        {
            PlayerMetricSummaryKind.Damage => entity.DamageStats,
            PlayerMetricSummaryKind.Healing => entity.HealingStats,
            PlayerMetricSummaryKind.DamageTaken => entity.TakenStats,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        return new PlayerMetricSummarySnapshot(
            stats.ValueTotal,
            stats.HitsCount,
            stats.ValueNormalTotal,
            stats.NormalRate,
            stats.NormalCount,
            stats.ValueCritTotal,
            stats.CritRate,
            stats.CritCount,
            stats.ValueLuckyTotal,
            stats.LuckyRate,
            stats.LuckyHitCount,
            [.. stats.GetValueTotalByElementCopy().Where(pair => pair.Value > 0UL).OrderBy(pair => pair.Key)],
            [.. stats.GetValueTotalByModeCopy().Where(pair => pair.Value > 0UL).OrderBy(pair => pair.Key)]);
    }

    private static readonly PlayerMetricSummarySnapshot EmptyMetricSummary = new(
        0UL,
        0UL,
        0UL,
        0d,
        0U,
        0UL,
        0d,
        0U,
        0UL,
        0d,
        0U,
        [],
        []);

    /// <summary>メーターが映している回(<see cref="ResolveActiveEncounter"/>)の、その人の技ごとの内訳。</summary>
    public static MetricSkillTableSnapshot GetPlayerSkillTable(MeterSnapshotKind kind, long characterId)
    {
        return ResolveActiveEncounter() is { } encounter
            ? GetPlayerSkillTable(encounter, kind, characterId)
            : new MetricSkillTableSnapshot(Array.Empty<MetricSkillTableRowSnapshot>());
    }

    /// <summary>渡した回の、その人の技ごとの内訳(閉じた回の結果にも使う)。</summary>
    public static MetricSkillTableSnapshot GetPlayerSkillTable(Encounter encounter, MeterSnapshotKind kind, long characterId)
    {
        if (!TryResolvePlayerEntity(encounter, characterId, out _, out var entity))
        {
            return new MetricSkillTableSnapshot(Array.Empty<MetricSkillTableRowSnapshot>());
        }

        IReadOnlyList<KeyValuePair<long, CombatStats>> skillStats = kind switch
        {
            MeterSnapshotKind.Damage => (IReadOnlyList<KeyValuePair<long, CombatStats>>)entity.SkillMetrics
                .AsValueEnumerable()
                .Where(entry => entry.Value.Damage.ValueTotal > 0UL)
                .OrderByDescending(entry => entry.Value.Damage.ValueTotal)
                .Select(entry => new KeyValuePair<long, CombatStats>(entry.Key, entry.Value.Damage))
                .ToList(),
            MeterSnapshotKind.Healing => (IReadOnlyList<KeyValuePair<long, CombatStats>>)entity.SkillMetrics
                .AsValueEnumerable()
                .Where(entry => entry.Value.Healing.ValueTotal > 0UL)
                .OrderByDescending(entry => entry.Value.Healing.ValueTotal)
                .Select(entry => new KeyValuePair<long, CombatStats>(entry.Key, entry.Value.Healing))
                .ToList(),
            _ => Array.Empty<KeyValuePair<long, CombatStats>>()
        };

        var entityTotalValue = GetPlayerTotalValue(entity, kind);
        if (skillStats.Count == 0 || entityTotalValue == 0UL)
        {
            return new MetricSkillTableSnapshot(Array.Empty<MetricSkillTableRowSnapshot>());
        }

        // 有効は、その人の有効な秒数で割る(行ごとに変わらない)。
        var clock = encounter.ReadCombatClock();
        var elapsedSeconds = clock.Elapsed.TotalSeconds;
        var activeSeconds = entity.GetActiveSeconds(clock, encounter.ToCombatOffset);
        var rows = new MetricSkillTableRowSnapshot[skillStats.Count];
        for (var index = 0; index < skillStats.Count; index++)
        {
            var stat = skillStats[index];
            var value = stat.Value;
            // 小数点以下2位まで持つ。表示側は F2 で固定2桁にする。
            var percentage = value.ValueTotal > 0UL
                ? Math.Round(((double)value.ValueTotal / entityTotalValue) * 100d, 2)
                : 0d;

            // バフとして届いたかどうか。名前には影響しない(見出し表は種別を区別しない)。
            // 内部ID注記の表示区分にだけ効く。
            var hasContainer = entity.SkillMetrics.TryGetValue(stat.Key, out var sourceContainer);
            var isBuffSource = hasContainer && sourceContainer!.IsBuffSource;
            // 見出し表で名前が空の行は、記録時に付与元をたどった着地先の名前を出す。
            var landing = hasContainer ? sourceContainer!.Landing : SourceLanding.None;
            var rowKeyText = CombatDataCatalog.FormatSourceKey(stat.Key);

            rows[index] = new MetricSkillTableRowSnapshot(
                stat.Key,
                rowKeyText,
                // 記録時の名前(英語)ではなく、表示中の言語で引き直す。
                CombatDataCatalog.GetSourceDisplayName(stat.Key, isBuffSource, landing),
                CombatDataCatalog.GetSourceNameWithoutInternalId(stat.Key, isBuffSource, landing),
                value.ValueTotal,
                CombatClockReading.PerSecond(value.ValueTotal, activeSeconds),
                CombatClockReading.PerSecond(value.ValueTotal, elapsedSeconds),
                value.HitsCount,
                value.CritRate,
                value.ValueAverage,
                percentage,
                // 並びは enum の値の順で安定させる。表示の並びは読む側が決める。
                [.. value.GetValueTotalByElementCopy().OrderBy(pair => pair.Key)],
                [.. value.GetValueTotalByModeCopy().OrderBy(pair => pair.Key)]);
        }

        return new MetricSkillTableSnapshot(rows);
    }

    /// <summary>メーターが映している回(<see cref="ResolveActiveEncounter"/>)の全員の集計。回がまだ無ければ空。</summary>
    public static EncounterTotalsSnapshot GetEncounterTotals()
    {
        return ResolveActiveEncounter() is { } encounter
            ? CreateEncounterTotals(encounter, isHistory: AppState.OpenedHistoricalEncounter is not null)
            : new EncounterTotalsSnapshot(false, null, TimeSpan.Zero, false, Array.Empty<EncounterPlayerTotals>());
    }

    /// <summary>渡したライブの回の全員の集計(閉じた回の結果に使う)。</summary>
    public static EncounterTotalsSnapshot GetEncounterTotals(Encounter encounter)
    {
        return CreateEncounterTotals(encounter, isHistory: false);
    }

    private static EncounterTotalsSnapshot CreateEncounterTotals(Encounter encounter, bool isHistory)
    {
        var clock = encounter.ReadCombatClock();
        var elapsedSeconds = clock.Elapsed.TotalSeconds;
        var players = encounter.Entities
            .Where(pair => pair.Value.EntityType == EEntityType.EntChar)
            .Select(pair => pair.Value)
            .Where(entity => entity.TotalDamage > 0UL || entity.TotalHealing > 0UL || entity.TakenStats.ValueTotal > 0UL)
            .Select(entity =>
            {
                var activeSeconds = entity.GetActiveSeconds(clock, encounter.ToCombatOffset);
                return new EncounterPlayerTotals(
                    entity.UID != 0 ? entity.UID : Utils.UuidToEntityId(entity.UUID),
                    CreateCombatTotals(entity.TotalDamage, entity.DamageStats, elapsedSeconds, activeSeconds),
                    CreateCombatTotals(entity.TotalHealing, entity.HealingStats, elapsedSeconds, activeSeconds),
                    CreateCombatTotals(entity.TakenStats.ValueTotal, entity.TakenStats, elapsedSeconds, activeSeconds));
            })
            .ToArray();

        return new EncounterTotalsSnapshot(isHistory, clock.StartUtc, clock.Elapsed, clock.IsClosed, players);
    }

    private static CombatTotalsSnapshot CreateCombatTotals(ulong total, CombatStats stats, double elapsedSeconds, double activeSeconds)
    {
        return new CombatTotalsSnapshot(
            total,
            CombatClockReading.PerSecond(stats.ValueTotal, elapsedSeconds),
            CombatClockReading.PerSecond(stats.ValueTotal, activeSeconds),
            stats.HitsCount,
            stats.ValueCritTotal,
            stats.CritCount,
            stats.ValueLuckyTotal,
            stats.LuckyHitCount);
    }

    /// <summary>
    /// そのプレイヤーに付いているバフを全部(<see cref="GetActiveEntityBuffs"/>)。実体の番号はキャラ番号から作る(自分と同じ形)。
    /// 周りにいない他のプレイヤーは空(<see cref="GetPlayerBuffs"/>と同じ)。
    /// </summary>
    public static IReadOnlyList<ActiveBuffSnapshot> GetActivePlayerBuffs(long characterId)
    {
        if (characterId == 0)
        {
            return Array.Empty<ActiveBuffSnapshot>();
        }

        var entityUuid = Utils.EntityIdToUuid(characterId, (long)EEntityType.EntChar, false, false);
        return IsSelfOrNearbyPlayer(entityUuid)
            ? GetActiveEntityBuffs(entityUuid)
            : Array.Empty<ActiveBuffSnapshot>();
    }

    /// <summary>
    /// その実体に付いているバフを全部、付いた順に。バフ/デバフリストの見せ方の決まり(優先度・手修正・同じバフをまとめる)は通さない。
    /// 履歴を開いても今の値(バフは記録から復元できない)。
    /// </summary>
    public static IReadOnlyList<ActiveBuffSnapshot> GetActiveEntityBuffs(long entityUuid)
    {
        var store = Services.ActiveBuffStore.Instance;
        var buffs = store.GetActive(entityUuid);
        if (buffs.Length == 0)
        {
            return Array.Empty<ActiveBuffSnapshot>();
        }

        var snapshots = new List<ActiveBuffSnapshot>(buffs.Length);
        foreach (var buff in buffs)
        {
            // 読む間に切れたバフは除く。
            if (!store.TryGetRemainingSeconds(
                    entityUuid,
                    (ulong)buff.Uuid,
                    out _,
                    out var isRemainingUnknown,
                    out var durationSeconds,
                    out var endUtc))
            {
                continue;
            }

            var casterUuid = buff.FireUuid;
            var casterIsPlayer = casterUuid != 0 && (EEntityType)Utils.UuidToEntityType(casterUuid) == EEntityType.EntChar;
            snapshots.Add(new ActiveBuffSnapshot(
                buff.Uuid,
                buff.BaseId,
                buff.Level,
                buff.Layer,
                durationSeconds > 0d ? durationSeconds : null,
                endUtc,
                isRemainingUnknown,
                casterUuid,
                casterIsPlayer ? Utils.UuidToEntityId(casterUuid) : null));
        }

        return snapshots;
    }

    /// <summary>
    /// 計測の状態。通知は出さないので、表示側が定期的に読み直す。
    /// 完了は「計測の回で起点があり、今が窓の終わり以降」から毎回計算する(完了の瞬間にすることは無い)。
    /// </summary>
    public static BenchmarkStateSnapshot GetBenchmarkState()
    {
        var current = EncounterManager.Current;
        var isActive = EncounterManager.IsBenchmarkActive;
        var hasBegun = isActive && current?.ExData.FirstDamageTimeStamp != null;
        return new BenchmarkStateSnapshot(
            isActive,
            hasBegun,
            hasBegun && current!.IsBenchmarkWindowElapsed(DateTime.UtcNow));
    }

    /// <summary>
    /// 計測の開始と停止を切り替える。入口(メーターのヘッダー・集計タブ・ホットキー)はここだけを呼ぶ。
    /// 実行はパケットを処理するスレッド(キャプチャが止まっていれば呼んだスレッド)。表示が変わるのは実行の後。
    /// </summary>
    public static void ToggleBenchmark()
    {
        MessageManager.RunOnPacketThread(EncounterManager.ToggleBenchmark);
    }

    /// <summary>今の回を区切り直す。実行するスレッドは <see cref="ToggleBenchmark"/> と同じ。</summary>
    public static void ResetCurrentEncounter()
    {
        MessageManager.RunOnPacketThread(EncounterManager.ResetCurrentEncounter);
    }


    private static bool IsIncludedBuff(PlayerBuffListKind kind, BuffEvent buffEvent)
        => BuffDisplayRules.GetListKind(buffEvent.BuffType) == kind;

    private static string ResolveBuffName(BuffEvent buffEvent)
    {
        return buffEvent.BaseId > 0
            ? CombatDataCatalog.GetBuffName(buffEvent.BaseId)
            : string.Empty;
    }

    private static string ResolveBuffOwnIconName(BuffEvent buffEvent)
    {
        return buffEvent.BaseId > 0
            ? CombatDataCatalog.GetBuffOwnIconName(buffEvent.BaseId)
            : string.Empty;
    }

    /// <summary>
    /// 行が渡すバフの番号(非表示リストとカードが引く)。畳んだバフは畳み先の番号。番号の無い実体は 0 のまま。
    /// </summary>
    private static int ResolveBuffDisplayId(BuffEvent buffEvent)
        => buffEvent.BaseId > 0 ? CombatDataCatalog.GetBuffDisplayId(buffEvent.BaseId) : buffEvent.BaseId;

    /// <summary>
    /// カードとプレイヤーリストの枠のバッジの鍵。バフの番号ごと(番号の無い実体はその実体)。
    /// 畳んだバフは畳み先の番号(<see cref="CombatDataCatalog.GetBuffDisplayId"/>)。番号は <see cref="TryGetBuffIdFromCardKey"/> で読み戻す。
    /// </summary>
    private static string ResolveBuffCardKey(BuffEvent buffEvent)
    {
        if (buffEvent.BaseId > 0)
        {
            return CreateBuffCardKey(CombatDataCatalog.GetBuffDisplayId(buffEvent.BaseId));
        }

        return $"uuid:{buffEvent.Uuid}";
    }

    /// <summary>カードの鍵のうち、バフの番号の鍵の頭(<see cref="CreateBuffCardKey"/> / <see cref="TryGetBuffIdFromCardKey"/>)。</summary>
    private const string BuffCardKeyBasePrefix = "base:";

    /// <summary>
    /// バフ/デバフリストの行の鍵。バフの番号ごと(<paramref name="cardKey"/>)、付与したプレイヤーで分ける行はその実体を足す
    /// (<see cref="BuffRowGrouping"/>)。
    /// </summary>
    private static string ResolveBuffRowKey(string cardKey, long playerCasterUuid)
        => playerCasterUuid == 0 ? cardKey : $"{cardKey}:player:{playerCasterUuid}";

    /// <summary>
    /// 表示用のバフ取得。<b>常に <see cref="Services.ActiveBuffStore"/> だけを見る。</b>
    /// あのストアはエンカウンターに閉じ込められていないので、境界で表示が消えない。
    ///
    /// <para>
    /// ストアが空なら空のまま返す。エンカウンター経路へのフォールバックは意図的に持たない
    /// (故障時に従来経路で正常に見えてしまうと、ストア側の不具合が発見できなくなるため)。
    /// </para>
    /// </summary>
    private static BuffEvent[] ResolveDisplayBuffEvents(long entityUuid)
    {
        return Services.ActiveBuffStore.Instance.GetActive(entityUuid);
    }

    /// <summary>
    /// ライブ表示用の残り時間。基準は <see cref="Services.ActiveBuffStore"/> が持つ観測時刻。
    /// <see cref="BuffEvent.AddDateTime"/> はサーバ由来の時刻が入る経路があってローカル時計とずれるので使えない。
    /// </summary>
    /// <param name="orderingKey">
    /// 切れる順を比べる鍵。切れる時刻(UTC)で、遅いほど大きい。残り秒ではなく時刻で比べる(残り秒はバフごとに読む瞬間が違い、
    /// 切れる時刻のほぼ同じバフどうしで大小が入れ替わる)。
    /// </param>
    /// <param name="durationSeconds">効果時間の総量(秒)。残り時間と同じ値から出す(<see cref="Services.ActiveBuffStore.TryGetRemainingSeconds"/>)。</param>
    private static bool TryResolveLiveBuffTiming(
        long entityUuid,
        BuffEvent buffEvent,
        out DateTime orderingKey,
        out double? remainingSeconds,
        out bool remainingUnknown,
        out double durationSeconds)
    {
        if (!Services.ActiveBuffStore.Instance.TryGetRemainingSeconds(
                entityUuid,
                (ulong)buffEvent.Uuid,
                out remainingSeconds,
                out remainingUnknown,
                out durationSeconds,
                out var endUtc))
        {
            orderingKey = DateTime.MinValue;
            remainingSeconds = null;
            remainingUnknown = false;
            durationSeconds = 0d;
            return false;
        }

        // 持続時間なし(null)のものと経過が分からないものは残り時間を出さないが、
        // 表示は残すので最後尾に並べる。
        orderingKey = endUtc ?? DateTime.MaxValue;
        return true;
    }

    private static void UpdatePlayerMeterState(IReadOnlyList<MeterPlayerSnapshot> players)
    {
        if (AppState.PlayerUUID == 0)
        {
            return;
        }

        for (var index = 0; index < players.Count; index++)
        {
            var player = players[index];
            if (!player.IsSelf)
            {
                continue;
            }

            AppState.PlayerMeterPlacement = index + 1;
            AppState.PlayerTotalMeterValue = player.TotalValue;
            return;
        }
    }

    private static int ResolvePlayerSkillCurrentLevel(
        long entityUuid,
        DataTypes.Skills.SkillLevelInfo skillLevel)
    {
        if (IsSelfEntity(entityUuid)
            && !CombatDataCatalog.IsSkillRole(skillLevel.SkillId)
            && PlayerSkillLevelStateStore.TryGetSelfSkillLevel(
                skillLevel.SkillId,
                out var selfLevel))
        {
            return selfLevel;
        }

        return skillLevel.CurrentLevel;
    }

    private static bool IsSelfEntity(long entityUuid)
    {
        return entityUuid != 0
            && (entityUuid == MessageManager.currentUserUuid
                || entityUuid == AppState.PlayerUUID
                || (AppState.PlayerUID != 0
                    && Utils.UuidToEntityId(entityUuid) == AppState.PlayerUID));
    }

    private static bool IsSelfOrNearbyPlayer(long entityUuid)
    {
        return IsSelfEntity(entityUuid) || PlayerRosterProjection.IsNearbyPlayer(entityUuid);
    }

    private static IReadOnlyList<DataTypes.Skills.SkillLevelInfo> ResolvePlayerSkillLevels(
        long entityUuid,
        Entity? entity,
        long characterId)
    {
        if (IsSelfEntity(entityUuid)
            && PlayerSkillLevelStateStore.TryGetSelfCurrentSkillLevels(
                out var currentSkillLevels))
        {
            return currentSkillLevels;
        }

        var receivedSkillLevels = entity?.GetAttrKV("AttrSkillLevelIdList") switch
        {
            List<DataTypes.Skills.SkillLevelInfo> typedList => typedList,
            JArray jsonArray =>
                (IReadOnlyList<DataTypes.Skills.SkillLevelInfo>?)
                jsonArray.ToObject<List<DataTypes.Skills.SkillLevelInfo>>()
                ?? Array.Empty<DataTypes.Skills.SkillLevelInfo>(),
            _ => Array.Empty<DataTypes.Skills.SkillLevelInfo>()
        };

        // AOI外へ出ると AttrSkillLevelIdList が届かなくなるので、
        // 自分以外のパーティメンバーは保持しておいた分で補完する。
        if (receivedSkillLevels.Count == 0
            && !IsSelfEntity(entityUuid)
            && PartyMemberCache.Instance.TryGetSkillLevels(
                characterId,
                out var cachedSkillLevels))
        {
            receivedSkillLevels = cachedSkillLevels;
        }

        // AOI外だと Entity が無い場合がある。職業はパーティのsocial dataからライブで引ける。
        var professionId = entity?.ProfessionId ?? 0;
        if (professionId <= 0
            && PartyStateStore.Instance.Current.TryGetSupplement(characterId, out var supplement))
        {
            professionId = supplement.ProfessionId;
        }

        // ここでは絞らない。この戻り値はスキルリストウィジェット(全スキル一覧)にも使われ、
        // そこでは習得済みを全部出すのが正しい。
        // 「装備中の4枠」を出すのはプレイヤーリスト側だけなので、
        // ロールスキルの絞り込みは CreatePlayerImagineRoleSkillLoadout で行う。
        return receivedSkillLevels;
    }

    private static bool TryResolvePlayerEntityByUuid(
        Encounter encounter,
        long characterId,
        long entityUuid,
        out Entity entity)
    {
        if (entityUuid == 0
            || !encounter.Entities.TryGetValue(entityUuid, out var resolvedEntity)
            || resolvedEntity.EntityType != EEntityType.EntChar)
        {
            entity = null!;
            return false;
        }

        var playerId = resolvedEntity.UID != 0
            ? resolvedEntity.UID
            : Utils.UuidToEntityId(entityUuid);
        if (playerId != characterId)
        {
            entity = null!;
            return false;
        }

        entity = resolvedEntity;
        return true;
    }

    private static bool TryResolvePlayerEntity(
        Encounter encounter,
        long characterId,
        out long entityUuid,
        out Entity entity)
    {
        if (encounter.Entities.TryGetValue(characterId, out var resolvedEntity)
            && resolvedEntity.EntityType == EEntityType.EntChar)
        {
            entityUuid = characterId;
            entity = resolvedEntity;
            return true;
        }

        foreach (var pair in encounter.Entities)
        {
            if (pair.Value.EntityType != EEntityType.EntChar)
            {
                continue;
            }

            var playerId = pair.Value.UID != 0
                ? pair.Value.UID
                : Utils.UuidToEntityId(pair.Key);
            if (playerId != characterId)
            {
                continue;
            }

            entityUuid = pair.Key;
            entity = pair.Value;
            return true;
        }

        entityUuid = 0;
        entity = null!;
        return false;
    }

    private static ulong GetPlayerTotalValue(Entity entity, MeterSnapshotKind kind)
    {
        return kind == MeterSnapshotKind.Damage
            ? entity.TotalDamage
            : entity.TotalHealing;
    }

    /// <summary>
    /// プレイヤーリストとバフ系が見るエンカウンター。<b>常にライブ。</b>
    ///
    /// <para>
    /// HP・バフ・スキル枠はDBから復元できないので、履歴を開いても固める意味がない。
    /// 履歴に追従するのは <see cref="ResolveActiveEncounter"/> を通る4系統だけ
    /// (メーター2種 / スキル詳細 / ヒール・ダメージ詳細 / グラフ)。
    /// </para>
    /// </summary>
    private static Encounter? ResolvePlayerDetailEncounter()
    {
        return EncounterManager.Current;
    }

    /// <summary>
    /// ライブに居ない人の受け皿。<b>メーターが映しているエンカウンター。</b>
    ///
    /// <para>
    /// 履歴にしか居ない灰色の行でも、イマジン/ロールと特化はDBに残っている
    /// (<c>Entity.Attributes</c> は public プロパティ、<c>SubProfessionId</c> は private setter 付きで
    /// どちらも blob に載る)。ライブで引けなかったときだけここから拾う。
    /// <b>ライブが先。</b> 逆にすると、AOIに居る人まで履歴の値で固まる。
    /// </para>
    /// </summary>
    private static Encounter? ResolveFallbackDetailEncounter()
    {
        return AppState.OpenedHistoricalEncounter;
    }

    /// <summary>
    /// ライブ → メーターのエンカウンター の順で実体を引く。
    /// <paramref name="isFromFallback"/> は「ライブでは見つからず、履歴から拾った」を表す。
    /// </summary>
    private static bool TryResolveDetailEntity(
        long characterId,
        long entityUuid,
        out Entity? entity,
        out bool isFromFallback)
    {
        entity = null;
        isFromFallback = false;

        var live = ResolvePlayerDetailEncounter();
        if (live is not null
            && TryResolvePlayerEntityByUuid(live, characterId, entityUuid, out entity))
        {
            return true;
        }

        var fallback = ResolveFallbackDetailEncounter();
        if (fallback is not null
            && TryResolvePlayerEntity(fallback, characterId, out _, out entity))
        {
            isFromFallback = true;
            return true;
        }

        entity = null;
        return false;
    }

    /// <summary>
    /// メーターが映すエンカウンター。履歴を開いていればその回、無ければ今の回。プレイヤーリストの名簿も同じ回の人を載せる
    /// (<see cref="PlayerRosterProjection.RebuildRoster"/>)。
    ///
    /// <para>
    /// 設定「次のイベントまで結果を保持」がオンで、今の回が前の回(<see cref="Encounter.PastEncounterForMeter"/>)を持っていれば前の回を出す。
    /// 保持してよい区切りかは回を作るときに決まっている(<see cref="EncounterManager.EnterDungeon"/>)。
    /// 前の回を手放すのはパケットの処理の側(<see cref="EncounterManager.ReleaseHeldPastEncounter"/>)で、ここは読むだけ。
    /// </para>
    /// </summary>
    public static (Encounter Encounter, PlayerRosterEntry Player, Entity Entity)? GetBuildPlayer(long characterId, Encounter? specifiedEncounter = null)
    {
        var encounter = specifiedEncounter ?? ResolveActiveEncounter();
        if (encounter is null || !TryResolvePlayerEntity(encounter, characterId, out _, out var entity) || entity is null)
            return null;
        // Build snapshots must never fill historical gaps from the current party.
        var source = PlayerDataSourceResolver.Resolve(characterId, null, entity, IsSelf(entity), recordedOnly: true);
        return (encounter, new PlayerRosterEntry(characterId, source.Name, source.ProfessionId,
            ClassSpec: source.ClassSpec, SubProfessionId: source.SubProfessionId,
            EquipmentData: source.EquipmentData, IsNpc: source.IsNpc,
            SeasonTalentBuffId: source.SeasonTalentBuffId, IsSeasonTalentInactive: source.IsSeasonTalentInactive,
            GameServer: specifiedEncounter is null && encounter == EncounterManager.Current ? GameServerStore.Current : encounter.ExData.GameServer,
            RootBuffData: source.RootBuffData, TalentNodeIds: source.TalentNodeIds,
            LastReplaceableSkills: source.LastReplaceableSkills, IsFromRecord: true), entity);
    }

    internal static Encounter? ResolveActiveEncounter()
    {
        if (AppState.OpenedHistoricalEncounter is { } openedHistoricalEncounter)
        {
            return openedHistoricalEncounter;
        }

        var currentEncounter = EncounterManager.Current;
        return CombatRuntimeSettings.KeepPastEncounterInMeterUntilNextDamage
            && currentEncounter?.ExData.UseQuestClock != true
            && currentEncounter?.PastEncounterForMeter is { } pastEncounter
                ? pastEncounter
                : currentEncounter;
    }

    private static MeterPlayerSnapshot CreatePlayerValue(long characterId, Entity entity, MeterSnapshotKind kind, CombatClockReading clock)
    {
        var totalValue = kind == MeterSnapshotKind.Damage
            ? entity.TotalDamage
            : entity.TotalHealing;
        var stats = kind == MeterSnapshotKind.Damage
            ? entity.DamageStats
            : entity.HealingStats;
        var valuePerSecond = CombatClockReading.PerSecond(stats.ValueTotal, clock.Elapsed.TotalSeconds);
        var isSelf = IsSelf(entity);
        var source = PlayerDataSourceResolver.Resolve(entity, isSelf);
        return new MeterPlayerSnapshot(
            characterId,
            source.CharacterId,
            source.Name,
            source.ProfessionId,
            source.SubProfessionId,
            source.ClassSpec,
            source.CombatPower,
            source.SeasonStrength,
            source.Level,
            source.SeasonLevel,
            isSelf,
            source.IsNpc,
            totalValue,
            valuePerSecond,
            0d,
            0d,
            source.SeasonTalentBuffId,
            source.IsSeasonTalentInactive,
            source.DataGaps);
    }

    private static bool IsSelf(Entity entity)
    {
        var uuid = entity.UUID;
        return uuid != 0
            && (uuid == MessageManager.currentUserUuid
                || uuid == AppState.PlayerUUID
                || (AppState.PlayerUID != 0 && Utils.UuidToEntityId(uuid) == AppState.PlayerUID));
    }

}
