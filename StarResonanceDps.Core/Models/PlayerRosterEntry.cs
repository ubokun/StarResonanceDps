using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.Core.Models;

public sealed record PlayerRosterEntry(
    long CharacterId,
    string Name,
    int ProfessionId,
    int CombatPower = 0,
    int SeasonStrength = 0,
    long CurrentHp = 0,
    long MaxHp = 0,
    PlayerClassSpec ClassSpec = PlayerClassSpec.Unknown,
    bool IsSelf = false,
    PlayerCombatAttributes CombatAttributes = default,
    int SubProfessionId = 0,
    int Level = 0,
    int SeasonLevel = 0,
    PlayerEquipmentData? EquipmentData = null,
    bool IsNpc = false,
    long CurrentShield = 0,
    bool IsPartyMember = false,
    int? PartyNumber = null,
    bool IsLive = true,
    // 自分の実体に届いている属性の全部(番号順)。他人には能力値が届かないので自分だけ入る。
    IReadOnlyList<PlayerAttributeEntry>? Attributes = null,
    // 有効化しているシーズンタレントの型の根ノードのバフID(0 = 不明か無効)。名前は表示時に CombatDataCatalog.GetSeasonTalentName で引く。
    int SeasonTalentBuffId = 0,
    // シーズンタレントの型が無効(どの型も有効化していない)と確定しているか。
    bool IsSeasonTalentInactive = false,
    // シーズンランクの段階(SeasonRankTable.RankToLevel、属性 AttrRankLevel)。段階 0 があるので、属性が届いていなければ null。
    int? SeasonRankLevel = null,
    // いま周りの実体から値が届いているか(自分を含む)。周りに見えている人は自分と同じ場所にいる。
    bool IsNearby = false,
    // パーティ情報のいる場所。マップ番号とチャンネル番号、0 は分からない(パーティ外も 0)。
    int PartySceneId = 0,
    int PartyLineId = 0,
    // この行のシーズン番号。素性が履歴の回から来た行だけその回のシーズン(0 は分からない)。
    // ライブの行は null で、一覧の今のシーズン(PlayerRosterSnapshot.SeasonId)を使う。
    int? SeasonId = null,
    // この行のゲームのサーバー。素性が履歴の回から来た行だけその回のサーバー(保存する前の回は Unknown)。
    // ライブの行は null で、今のサーバー(GameServerStore.Current)を使う。
    GameServer? GameServer = null,
    // この行の顔と名刺の写真のリンク。素性が履歴の回から来た行だけ、その回に保存した値(保存の時点で届いていなければ null)。
    // ライブの行は null で、名刺の店(SocialDataStore)の今の値を使う。
    SocialAvatarState? RecordedAvatar = null,
    // 乗っている根のバフ(タレント・潜在心相晶のノードと上級・因子・モジュール効果)。全部をまだ受けていなければ null。
    // 履歴の行はその回に保存した値。
    PlayerRootBuffData? RootBuffData = null,
    // いま戦闘状態か(実体の属性 AttrCombatState、104)。属性が届くのは自分と周りに見えている人だけで、ほかの行(履歴の行も)は false。
    bool IsInCombat = false,
    // 他のプレイヤーについて、まだ届いていない情報(ウィジェットの状態の印の材料)。自分の行と、記録から来た行(IsFromRecord)はいつも無し。
    PlayerDataGaps DataGaps = PlayerDataGaps.None,
    // 素性が開いている履歴の回の記録から来た行。窓の中身は全部が履歴で、ライブの受信の欠けは表さない。
    bool IsFromRecord = false,
    // 戦闘不能か。HP を取った出所で決める(実体なら最大HPが分かっていて HP 0、パーティの同期なら同期の状態が戦闘不能)。
    // パーティの同期の HP は読み込み中などの生きている人にも 0 で届くので、HP 0 だけでは決めない。
    bool IsIncapacitated = false,
    // 今の職業の取ったタレントの木の節の番号(昇順)。自分にしか届かないので、ほかのプレイヤーの行はいつも null。
    // 自分も全体コンテナかタレントを付け替えた差分を受けるまでは null。履歴の行はその回に保存した値。
    IReadOnlyList<int>? TalentNodeIds = null,
    // バフを付けないアビリティの節の技の置き換えに出てくる技の、元の技 → 最後に使ったほう(元の技か置き換え後の技)。
    // どれもまだ使っていなければ null。履歴の行はその回に保存した値。
    IReadOnlyDictionary<int, int>? LastReplaceableSkills = null,
    RecordedPlayerBuildSnapshot? RecordedBuild = null);
