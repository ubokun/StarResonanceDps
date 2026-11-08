using System.Collections.Frozen;
using Newtonsoft.Json;
using Serilog;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.Core.CombatRuntime;

public static class CombatDataCatalog
{
    private static readonly string[] SupportedCultures = ["en-US", "ja-JP", "ko-KR", "zh-CN"];
    private static readonly object Sync = new();

    private static FrozenDictionary<int, Skill> _skills =
        new Dictionary<int, Skill>().ToFrozenDictionary();
    private static FrozenDictionary<int, Buff> _buffs =
        new Dictionary<int, Buff>().ToFrozenDictionary();
    private static FrozenDictionary<int, FrozenDictionary<int, float>> _skillCooldownsByLevel =
        new Dictionary<int, FrozenDictionary<int, float>>().ToFrozenDictionary();
    /// <summary>技ID → 技のアイコンの背景の枠(<see cref="BuildSkillIconFrames"/>)。技の表の全部の技。</summary>
    private static FrozenDictionary<int, SkillIconFrame> _skillIconFrames = FrozenDictionary<int, SkillIconFrame>.Empty;
    /// <summary>技の名前。技の表の名前(<c>SkillNames</c>)に古い版の名前(<c>SkillNameOverrides</c>)と <c>SkillTalk</c> を重ねたもの。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _skillNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    /// <summary>技の表の名前に古い版の名前を重ねたもの(<c>SkillTalk</c> を重ねる前)。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _skillTableNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    /// <summary>技の <c>SkillTalk</c>(<c>SkillTalkNames.json</c>)だけ。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _skillTalkNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _buffNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _monsterNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    /// <summary><see cref="_monsterNames"/> の鍵(<c>MonsterTable</c> の行)全部。名前が空の行も含む。</summary>
    private static FrozenSet<int> _monsterIds = FrozenSet<int>.Empty;
    /// <summary>技ID → ボス大技の予告(<c>DbmTable</c>)の名前。<c>Data/Localization/DbmNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _dbmNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// <c>SkillTable.EffectIDs</c> の要素 → その要素を持つ技ID。予告の通知が技IDではなくエフェクトIDを運ぶときに引く。
    /// 複数の技が同じ要素を持つものは持ち主が決まらないので入れない。
    /// </summary>
    private static FrozenDictionary<int, int> _skillIdByEffectId = FrozenDictionary<int, int>.Empty;
    /// <summary>技ID → その技を <c>MonsterTable.SkillIds</c> に持つモンスターの種別ID。</summary>
    private static FrozenDictionary<int, FrozenSet<int>> _monsterIdsBySkillId = FrozenDictionary<int, FrozenSet<int>>.Empty;
    /// <summary>
    /// 戦闘画面の警告バーを出す技の技レベルID(技ID×100＋レベル)。<c>Data/Generated/SkillWarnings.json</c>。
    /// <c>DataTools/Core/gen_skill_warnings.py</c> が生成する。
    /// </summary>
    private static FrozenSet<int> _warningSkillLevelIds = FrozenSet<int>.Empty;
    /// <summary><see cref="_warningSkillLevelIds"/> のどれかのレベルを持つ技ID。</summary>
    private static FrozenSet<int> _warningSkillIds = FrozenSet<int>.Empty;
    /// <summary>
    /// ボス大技の予告の番号(<c>DbmTable.Id</c>) → 予告のバーの既定の秒数(<c>CountCDTime</c>)。<c>Data/Raw/DbmTable.json</c>。
    /// 予告の通知の持続が 0 のとき、ゲームはこの秒数でバーを出す。
    /// </summary>
    private static FrozenDictionary<int, int> _dbmCountCdTimes = FrozenDictionary<int, int>.Empty;
    /// <summary>
    /// 料理のバフ → 名前(そのバフを付けるアイテムの名前)。<c>Data/Localization/CuisineBuffs.json</c>。
    /// <c>DataTools/Core/gen_buff_groups.py</c> が生成する。バフ自身の名前は「料理」の1語に丸められているので、こちらを先に引く。
    /// </summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _cuisineBuffNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    /// <summary><see cref="_cuisineBuffNames"/> の鍵(料理のバフ)全部。</summary>
    private static FrozenSet<int> _cuisineBuffIds = FrozenSet<int>.Empty;
    /// <summary>薬剤のバフ → 名前。<c>Data/Localization/PotionBuffs.json</c>。形と引き方は料理と同じ。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _potionBuffNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    /// <summary><see cref="_potionBuffNames"/> の鍵(薬剤のバフ)全部。</summary>
    private static FrozenSet<int> _potionBuffIds = FrozenSet<int>.Empty;
    /// <summary>行代表キー → ゲーム内メーターの行名。<c>Data/Localization/RecountRows.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<long, string>> _recountNames =
        new Dictionary<string, FrozenDictionary<long, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>発生源キー → 行代表キー。<c>RecountRows.json</c> の行構成に手修正を重ねたもの。</summary>
    private static FrozenDictionary<long, long> _recountRows = FrozenDictionary<long, long>.Empty;

    /// <summary>
    /// ownerId → その ownerId の鍵の枝番と行代表キー(枝番の昇順)。<see cref="_recountRows"/> を ownerId で引き直したもの。
    /// 押した技の表示を見出し表の行で畳むのに使う(<see cref="GetSkillActivationDisplay"/>)。
    /// </summary>
    private static FrozenDictionary<int, (int Branch, long RowKey)[]> _recountKeysByOwner =
        FrozenDictionary<int, (int Branch, long RowKey)[]>.Empty;

    /// <summary>
    /// 押した技の技ID → 推移グラフの技のアイコンの名前とアイコンの手修正。<c>Data/Overrides/SkillActivationOverrides.json</c>。
    /// 表に名前もアイコンも無い技(回避・共通受身スキルなど)、通常攻撃の2段目以降、
    /// 押した技の番号とダメージの発生源キーが違って見出し表の名前に届かない技に使う(<see cref="GetSkillActivationDisplay"/>)。
    /// </summary>
    private static FrozenDictionary<int, SkillActivationOverride> _skillActivationOverrides =
        FrozenDictionary<int, SkillActivationOverride>.Empty;

    /// <summary>オプションのバフID → オプション名。<c>Data/Localization/RogueEntryNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _rogueEntryNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary><see cref="_rogueEntryNames"/> の鍵(オプションのバフID)全部。</summary>
    private static FrozenSet<int> _rogueEntryBuffIds = FrozenSet<int>.Empty;

    /// <summary>
    /// 根のバフ(心相晶のノードと上級・因子。タレントとモジュール効果は入らない)→ 名前。<c>Data/Localization/RootBuffNames.json</c>。
    /// 潜在心相晶情報の行に使う。
    /// </summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _rootBuffNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// モジュール効果の番号 → 名前。<c>Data/Localization/ModuleEffectNames.json</c>。
    /// モジュール情報の行(根のバフの行も効果の番号で引く)と、付与元をたどってモジュールの根に着いたバフの名前に使う。
    /// </summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _moduleEffectNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>モジュール効果の番号 → 根のバフ・絵・段階に要るリンク数。<c>Data/Generated/RootBuffs.json</c> の <c>ModuleEffects</c>。</summary>
    private static FrozenDictionary<int, ModuleEffectDefinition> _moduleEffects = FrozenDictionary<int, ModuleEffectDefinition>.Empty;

    /// <summary>
    /// 因子のアイテム → 付けるバフ・絵。<c>Data/Generated/RootBuffs.json</c> の <c>FactorItems</c>。
    /// 同じアイテムでもサーバーで付けるバフが違う(前のシーズンの因子は無効の説明のバフに置き換わる)ので、サーバーの表ごとに持つ。
    /// </summary>
    private static FrozenDictionary<int, FactorItemDefinition> _asiaFactorItems = FrozenDictionary<int, FactorItemDefinition>.Empty;

    private static FrozenDictionary<int, FactorItemDefinition> _chinaFactorItems = FrozenDictionary<int, FactorItemDefinition>.Empty;

    /// <summary>根のバフ → 種類・並び・絵。<c>Data/Generated/RootBuffs.json</c>。</summary>
    private static FrozenDictionary<int, RootBuffDefinition> _rootBuffs = FrozenDictionary<int, RootBuffDefinition>.Empty;

    /// <summary>潜在心相晶の型の根のバフ → 型の上級の番号(上級の絵を選ぶのに使う)。<c>Data/Generated/RootBuffs.json</c>。</summary>
    private static FrozenDictionary<int, int> _seasonTalentAdvancedEffectIds = FrozenDictionary<int, int>.Empty;

    /// <summary>
    /// アジアサーバーの表(StarASIA)のタレントの木。(職業ID, 特化の番号。クラスR1 は 0)→ 木の形。<c>Data/Generated/RootBuffs.json</c>。
    /// </summary>
    private static FrozenDictionary<(int ProfessionId, int SubProfessionId), TalentTreeDefinition> _asiaTalentTrees =
        FrozenDictionary<(int ProfessionId, int SubProfessionId), TalentTreeDefinition>.Empty;

    /// <summary>中国サーバーの表(Star)のタレントの木。形は <see cref="_asiaTalentTrees"/> と同じ。</summary>
    private static FrozenDictionary<(int ProfessionId, int SubProfessionId), TalentTreeDefinition> _chinaTalentTrees =
        FrozenDictionary<(int ProfessionId, int SubProfessionId), TalentTreeDefinition>.Empty;

    /// <summary>
    /// バフを付けないアビリティの節の技の置き換えに出てくる技(元の技と置き換え後の技)→ 元の技。両方のサーバーの木から作る。
    /// ほかのプレイヤーがその節を取っているかを、最後に使った技で決めるのに使う。
    /// </summary>
    private static FrozenDictionary<int, int> _replaceableSkillOriginals = FrozenDictionary<int, int>.Empty;

    /// <summary>タレント → 名前。<c>Data/Localization/TalentNames.json</c>。アビリティ情報の節の TIPS に使う。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _talentNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// モジュールの根のバフ(<c>ModEffectTable.EffectConfig</c> の <c>[3, バフID, 1]</c>)→ モジュール効果の番号。<see cref="_moduleEffects"/> から作る。
    /// バフの由来をたどるときの止まり先(<see cref="Services.SourceLandingResolver.ResolveBuffOrigin"/>)と、根のバフの名前に使う。
    /// </summary>
    private static FrozenDictionary<int, int> _modEffectIdByRootBuff = FrozenDictionary<int, int>.Empty;

    /// <summary>
    /// イマジンのパッシブの根のバフ(<c>SkillAoyiTable</c> / <c>SkillAoyiStarTable</c> の <c>TransformationType</c> の <c>[3, バフID, 1]</c>)→ イマジンの技ID。
    /// バフの由来をたどるときの止まり先。
    /// </summary>
    private static FrozenDictionary<int, int> _imagineSkillIdByPassiveRoot = FrozenDictionary<int, int>.Empty;

    /// <summary>シーズンタレントの型の根ノードのバフID → 根ノードの名前。<c>Data/Localization/SeasonTalentNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _seasonTalentNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// <see cref="_seasonTalentNames"/> の鍵(根ノードのバフID)全部。型の判定に使う。
    /// 名前の辞書は空の値を落とすので、判定は名前ではなくこの鍵で行う。
    /// </summary>
    private static FrozenSet<int> _seasonTalentRootBuffIds = FrozenSet<int>.Empty;

    /// <summary>特化の番号(<c>SubProfessionId</c>)→ 特化の名前。<c>Data/Localization/ClassSpecNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _classSpecNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>パーティの目的(<c>TeamTargetTable</c> の行)→ 名前。パーティのマッチングのマッチング先。<c>Data/Localization/TeamTargetNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _teamTargetNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>活動(<c>SeasonActTable</c> の行)→ 名前。活動のマッチングのマッチング先。<c>Data/Localization/SeasonActNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _seasonActNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>シーズン番号 → シーズンの名前(シーズン実績の1枠目の分類名)。<c>Data/Localization/SeasonNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _seasonNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>シーズン番号 × 100 ＋ 段階 → シーズンランクの名前。<c>Data/Localization/SeasonRankNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _seasonRankNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>アイテムID → アイテム名(<c>ItemTable.Name</c>)。装備IDもアイテムID。<c>Data/Localization/ItemNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _itemNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>チャットのリンクの番号 → 文の型(<c>ChatHyperlink.Content</c>)。<c>Data/Localization/ChatHyperlinks.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _chatHyperlinkTemplates =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>ギルドの建物の番号 → 名前(<c>UnionBuildingTable.BuildingName</c>)。<c>Data/Localization/UnionBuildingNames.json</c>。</summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _unionBuildingNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 進化・改鋳・レアの効果の番号(一時属性・バフ)→ 説明文。値の差し込みは <c>{0}</c>(そのまま)と <c>{1}</c>(%)。
    /// <c>Data/Localization/EquipEffectTexts.json</c>。
    /// </summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> _equipEffectTexts =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>装備ID → 装備の定義。<c>Data/Generated/Equips.json</c> の <c>Equips</c>。</summary>
    private static FrozenDictionary<int, EquipmentDefinition> _equipments =
        FrozenDictionary<int, EquipmentDefinition>.Empty;

    /// <summary>
    /// <see cref="MakeEquipmentKey"/>(属性庫の型, 庫ID)→ 庫の行(表の並び順)。<c>Equips.json</c> の <c>AttrLibs</c>。
    /// 型は 1 = <c>EquipAttrLibTable</c>、2 = <c>EquipAttrSchoolLibTable</c>。
    /// </summary>
    private static FrozenDictionary<long, IReadOnlyList<EquipmentAttrLibRow>> _equipmentAttrLibs =
        FrozenDictionary<long, IReadOnlyList<EquipmentAttrLibRow>>.Empty;

    /// <summary><see cref="MakeEquipmentKey"/>(属性庫の型, 行ID)→ 行。<c>AttrLibs</c> の全行の索引。</summary>
    private static FrozenDictionary<long, EquipmentAttrLibRow> _equipmentAttrRows =
        FrozenDictionary<long, EquipmentAttrLibRow>.Empty;

    /// <summary>
    /// <see cref="MakeEquipmentKey"/>(効果の種類, 番号)→ シーズン強度の項目のシーズン。
    /// <c>Equips.json</c> の <c>StrengthSeasons</c>。
    /// </summary>
    private static FrozenDictionary<long, int> _equipmentStrengthSeasons = FrozenDictionary<long, int>.Empty;

    /// <summary>
    /// 特化の番号(<c>SubProfessionId</c>)→ 型2 の属性庫の特化(<c>TalentSchoolTable.Id</c>)。
    /// <c>Equips.json</c> の <c>SpecSchools</c>。
    /// </summary>
    private static FrozenDictionary<int, int> _equipmentSpecSchools = FrozenDictionary<int, int>.Empty;

    /// <summary>職業ID → クラスR1 の人の型2 の属性庫の特化。<c>Equips.json</c> の <c>Rank1Schools</c>。</summary>
    private static FrozenDictionary<int, int> _equipmentRank1Schools = FrozenDictionary<int, int>.Empty;

    private static FrozenDictionary<string, FrozenDictionary<int, string>> _sceneNames =
        new Dictionary<string, FrozenDictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);


    /// <summary>
    /// ダンジョン名に付ける難易度名。<c>Data/Localization/DungeonTypeNames.json</c>。
    /// 鍵は <see cref="MakeDungeonTypeKey"/>(ファイルでは <c>番号</c> と <c>番号:段階</c>)。
    /// </summary>
    private static FrozenDictionary<string, FrozenDictionary<long, string>> _dungeonTypeNames =
        new Dictionary<string, FrozenDictionary<long, string>>(StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>ダンジョン名と難易度名の区切り。ゲームの表示と同じで、全言語で同じ文字。</summary>
    private const string DungeonTypeNameSeparator = "-";

    /// <summary>
    /// チャットの文に混ぜる顔文字を、本文の <c>&lt;sprite=N&gt;</c> の N の順(0始まり)に並べた絵文字の文字。
    /// 並びはゲームの顔文字の資源と同じ(ゲームの顔文字の窓が出すのは 1〜63)。
    /// </summary>
    private static readonly string[] ChatEmojis =
    [
        "😀", "😁", "😂", "😃", "😄", "😅", "😆", "😇",
        "😈", "😉", "😐", "😑", "😒", "😓", "😔", "😕",
        "😖", "😗", "😘", "😙", "😠", "😡", "😢", "😣",
        "😤", "😥", "😦", "😧", "😨", "😩", "😰", "😱",
        "😲", "😳", "😴", "😵", "😶", "😷", "🙁", "🙂",
        "🙃", "🙄", "😊", "😋", "😌", "😍", "😎", "😏",
        "😚", "😛", "😜", "😝", "😞", "😟", "😪", "😫",
        "😬", "😭", "😮", "😯", "😺", "😼", "😽", "😾"
    ];
    private static string _cultureName = "en-US";

    /// <summary>
    /// 参照した内部IDを名前に添えるか。<c>int</c> で持つのは <see cref="Volatile"/> で読み書きするため。
    /// </summary>
    private static int _internalIdDisplayMode = (int)InternalIdDisplayMode.Hidden;

    // 名前の無い技とバフに内部名を出すか(全体設定。既定はオン)。Volatile で読み書きする。
    private static bool _showsUnnamedSkillInternalNames = true;

    private static int _displayNameRevision;

    /// <summary>
    /// 名前の決め方(言語・内部IDの表示・名前の無い技の内部名)が変わるたびに増える番号。引いた名前を控えて持つ所が読み直す目印にする。
    /// </summary>
    public static int DisplayNameRevision => Volatile.Read(ref _displayNameRevision);

    public static void SetCulture(string? cultureName)
    {
        var normalized = NormalizeCultureName(cultureName);
        if (normalized == Volatile.Read(ref _cultureName))
        {
            return;
        }

        Volatile.Write(ref _cultureName, normalized);
        Interlocked.Increment(ref _displayNameRevision);
    }

    public static void SetInternalIdDisplay(InternalIdDisplayMode mode)
    {
        if ((int)mode == Volatile.Read(ref _internalIdDisplayMode))
        {
            return;
        }

        Volatile.Write(ref _internalIdDisplayMode, (int)mode);
        Interlocked.Increment(ref _displayNameRevision);
    }

    /// <summary>
    /// 名前の無い技とバフに内部名を出すか。出すのは画面の技名(タイムライン・スキル詳細の行・推移グラフ・プレイヤーリストの技の枠)と、
    /// タイムラインのバフの名前(バフ由来の行・敵が付けたバフ)だけで、空欄の検知(<see cref="GetSourceNameBeforeLanding"/>)・予兆技の通知・保存する名前には効かない。
    /// </summary>
    public static void SetUnnamedSkillInternalNameDisplay(bool shows)
    {
        if (shows == Volatile.Read(ref _showsUnnamedSkillInternalNames))
        {
            return;
        }

        Volatile.Write(ref _showsUnnamedSkillInternalNames, shows);
        Interlocked.Increment(ref _displayNameRevision);
    }

    /// <summary><see cref="SetUnnamedSkillInternalNameDisplay"/> の今の値。</summary>
    public static bool ShowsUnnamedSkillInternalNames => Volatile.Read(ref _showsUnnamedSkillInternalNames);

    /// <summary>
    /// 参照した内部IDを名前に添える。<b>書式は全言語で半角括弧。</b>
    ///
    /// <para>
    /// テーブルに無くて生テーブル名へ落ちた場合も付ける。「どのIDを引いたか」を見るためのもので、
    /// 引けなかったときこそIDが要る(欠けているエントリを特定できる)。
    /// </para>
    ///
    /// <para>
    /// <b>名前が空でもIDだけ出す。</b> 空のままだとどのIDの行か分からず、
    /// 「テーブルに無いID」と「テーブルにあるが名前が空のID」も見分けが付かない。
    /// </para>
    ///
    /// <para>
    /// <b>この注記が付いた文字列を記憶・保存しない。</b> 注記は表示設定なので、
    /// 保存すると設定を切ったあとも残る。控える値は注記を付けない getter から取る。
    /// </para>
    /// </summary>
    private static string AppendInternalId(string name, InternalIdDisplayMode kind, long id)
    {
        if (id <= 0)
        {
            return name;
        }

        var mode = (InternalIdDisplayMode)Volatile.Read(ref _internalIdDisplayMode);
        if (mode != InternalIdDisplayMode.All && mode != kind)
        {
            return name;
        }

        return string.IsNullOrEmpty(name) ? $"({id})" : $"{name}({id})";
    }

    /// <summary>
    /// メーターの行に内部IDを添える。書式は <c>ownerId:枝番</c> で、生成物・手修正の鍵と同じ。
    ///
    /// <para>
    /// <b>枝番まで出す。</b> 同じ <c>ownerId</c> が別の行に分かれることがあり、
    /// <c>ownerId</c> だけだと 臣鷹出撃 と 臣鷹の雷撃衝撃 がどちらも <c>(2203291)</c> になって
    /// 見分けが付かない。
    /// </para>
    /// </summary>
    private static string AppendSourceInternalId(string name, InternalIdDisplayMode kind, long rowKey)
    {
        if (rowKey == 0)
        {
            return name;
        }

        var mode = (InternalIdDisplayMode)Volatile.Read(ref _internalIdDisplayMode);
        if (mode != InternalIdDisplayMode.All && mode != kind)
        {
            return name;
        }

        var text = FormatSourceKey(rowKey);
        return string.IsNullOrEmpty(name) ? $"({text})" : $"{name}({text})";
    }

    /// <summary><see cref="ReadLocalizedFile"/> で読む名前テーブル(<c>Data/Localization/{名前}.json</c>)。<see cref="StartReads"/> が先に解き始める。</summary>
    private static readonly string[] LocalizedFileNames =
    [
        "SkillNames", "SkillTalkNames", "BuffNames", "MonsterNames", "DbmNames", "RogueEntryNames", "SeasonTalentNames", "ClassSpecNames",
        "SeasonNames", "SeasonRankNames", "TeamTargetNames", "SeasonActNames", "ItemNames", "EquipEffectTexts",
        "CuisineBuffs", "PotionBuffs", "SceneNames", "DungeonTypeNames", "RootBuffNames", "ModuleEffectNames", "TalentNames", "ChatHyperlinks",
        "UnionBuildingNames",
    ];

    /// <summary><see cref="ApplyNameOverrides"/> で重ねる名前の上書き(<c>Data/Overrides/</c>)。<see cref="StartReads"/> が先に解き始める。</summary>
    private static readonly string[] NameOverrideFileNames =
    [
        "SkillNameOverrides.json", "BuffNameOverrides.json", "BuffNameAssignedOverrides.json", "MonsterNameOverrides.json",
    ];

    private static string LocalizedFilePath(string fileName) => Path.Combine(CombatRuntimePaths.LocalizationDirectory, $"{fileName}.json");

    private static string OverrideFilePath(string fileName) => Path.Combine(CombatRuntimePaths.OverridesDirectory, fileName);

    private static string RecountRowsPath => LocalizedFilePath("RecountRows");

    private static string RecountRowOverridesPath => OverrideFilePath("RecountRowOverrides.json");

    private static string SkillActivationOverridesPath => OverrideFilePath("SkillActivationOverrides.json");

    private static string SkillWarningsPath => Path.Combine(CombatRuntimePaths.GeneratedDirectory, "SkillWarnings.json");

    private static string DbmTablePath => Path.Combine(CombatRuntimePaths.RawTableDirectory, "DbmTable.json");

    private static string EquipmentsPath => Path.Combine(CombatRuntimePaths.GeneratedDirectory, "Equips.json");

    private static string RootBuffsPath => Path.Combine(CombatRuntimePaths.GeneratedDirectory, "RootBuffs.json");

    /// <summary>
    /// <see cref="Load"/> が読むファイルを、先に解き始める。読む側と同じパスと型で始める
    /// (食い違うと、受け取る側がエラーログを出してその場で読む)。
    /// </summary>
    internal static void StartReads(JsonDataFileReads reads)
    {
        foreach (var fileName in LocalizedFileNames)
        {
            reads.Start<Dictionary<string, Dictionary<string, string>?>>(LocalizedFilePath(fileName));
        }

        reads.Start<Dictionary<string, RecountRow>>(RecountRowsPath);
        foreach (var fileName in NameOverrideFileNames)
        {
            reads.Start<Dictionary<string, NameOverrideEntry?>>(OverrideFilePath(fileName));
        }

        reads.Start<Dictionary<string, Newtonsoft.Json.Linq.JObject>>(RecountRowOverridesPath);
        reads.Start<Dictionary<string, SkillActivationOverrideEntry>>(SkillActivationOverridesPath);
        reads.Start<int[]>(SkillWarningsPath);
        reads.Start<Dictionary<string, DbmTableRow>>(DbmTablePath);
        reads.Start<EquipmentFile>(EquipmentsPath);
        reads.Start<RootBuffFile>(RootBuffsPath);
    }

    /// <summary>
    /// 技・バフの表(<c>HelperMethods.DataTables</c>)が入った後に呼ぶ。ファイルは <see cref="StartReads"/> で始めたものを受け取る。
    /// </summary>
    internal static void Load(JsonDataFileReads reads)
    {
        lock (Sync)
        {
            _skills = LoadNumericCatalog(HelperMethods.DataTables.Skills.Data);
            _buffs = LoadNumericCatalog(HelperMethods.DataTables.Buffs.Data);
            _skillCooldownsByLevel = LoadSkillCooldowns();
            _skillIconFrames = BuildSkillIconFrames();
            var skillNames = LoadLocalizedText(reads, "SkillNames", out var skillNameIds);
            (_skillTableNames, _) = ApplyNameOverrides(reads, skillNames, skillNameIds, "SkillNameOverrides.json");
            _skillTalkNames = LoadLocalizedText(reads, "SkillTalkNames", out _);
            // 公式の技名(SkillTalk)。技の表の名前と、表の更新で落ちた名前を戻したもの(SkillNameOverrides)の上に重ねる。
            _skillNames = ApplySkillTalkNames(_skillTableNames, _skillTalkNames);
            var buffNames = LoadLocalizedText(reads, "BuffNames", out var buffNameIds);
            var (restoredBuffNames, restoredBuffNameIds) = ApplyNameOverrides(reads, buffNames, buffNameIds, "BuffNameOverrides.json");
            // 名前の無いバフと名前を変えるバフに割り当てた名前。表の更新で落ちた名前を戻したもの(BuffNameOverrides)の上に重ねる。
            var (assignedBuffNames, _) = ApplyNameOverrides(reads, restoredBuffNames, restoredBuffNameIds, "BuffNameAssignedOverrides.json");
            _buffNames = ApplyBuffFoldNames(assignedBuffNames);
            var monsterNames = LoadLocalizedText(reads, "MonsterNames", out var monsterIds);
            (_monsterNames, _monsterIds) = ApplyNameOverrides(reads, monsterNames, monsterIds, "MonsterNameOverrides.json");
            _dbmNames = LoadLocalizedText(reads, "DbmNames", out _);
            _rogueEntryNames = LoadLocalizedText(reads, "RogueEntryNames", out var rogueEntryBuffIds);
            _rogueEntryBuffIds = rogueEntryBuffIds;
            _rootBuffNames = LoadLocalizedText(reads, "RootBuffNames", out _);
            _moduleEffectNames = LoadLocalizedText(reads, "ModuleEffectNames", out _);
            _talentNames = LoadLocalizedText(reads, "TalentNames", out _);
            LoadRootBuffs(reads);
            _imagineSkillIdByPassiveRoot = BuildImagineSkillIdByPassiveRoot();
            _seasonTalentNames = LoadLocalizedText(reads, "SeasonTalentNames", out var seasonTalentRootBuffIds);
            _seasonTalentRootBuffIds = seasonTalentRootBuffIds;
            _classSpecNames = LoadLocalizedText(reads, "ClassSpecNames", out _);
            _seasonNames = LoadLocalizedText(reads, "SeasonNames", out _);
            _seasonRankNames = LoadLocalizedText(reads, "SeasonRankNames", out _);
            _teamTargetNames = LoadLocalizedText(reads, "TeamTargetNames", out _);
            _seasonActNames = LoadLocalizedText(reads, "SeasonActNames", out _);
            _itemNames = LoadLocalizedText(reads, "ItemNames", out _);
            _equipEffectTexts = LoadLocalizedText(reads, "EquipEffectTexts", out _);
            _skillIdByEffectId = BuildSkillIdByEffectId(_skills);
            _monsterIdsBySkillId = BuildMonsterIdsBySkillId();
            _warningSkillLevelIds = LoadWarningSkillLevels(reads);
            _warningSkillIds = _warningSkillLevelIds.Select(skillLevelId => skillLevelId / 100).ToFrozenSet();
            _dbmCountCdTimes = LoadDbmCountCdTimes(reads);
            LoadEquipments(reads);
            LoadBuffGroups(reads);
            _sceneNames = LoadLocalizedText(reads, "SceneNames", out _);
            _dungeonTypeNames = LoadDungeonTypeNames(reads);
            _chatHyperlinkTemplates = LoadLocalizedText(reads, "ChatHyperlinks", out _);
            _unionBuildingNames = LoadLocalizedText(reads, "UnionBuildingNames", out _);
            LoadRecounts(reads);
            _skillActivationOverrides = LoadSkillActivationOverrides(reads);
        }
    }

    /// <summary>
    /// 発生源キーを作る。<b>ゲーム内メーターの行はこの粒度で決まる。</b>
    ///
    /// <para>
    /// ゲームは <c>RecountTable.DamageId</c> で行を引く。<c>DamageId</c> はワイヤに乗っていないが、
    /// <c>SyncDamageInfo</c> の <c>OwnerId</c> と <c>HitEventId</c> の組が <c>DamageId</c> と
    /// 1対1で対応する(<c>DamageId</c> の <c>TypeEnum</c> が <c>OwnerId</c>、下2桁が <c>HitEventId</c>)。
    /// </para>
    ///
    /// <para>
    /// <b><c>OwnerId</c> だけでは粗すぎる。</b> 同じ <c>OwnerId</c> の枝が別の行に入る例が26〜28種あり、
    /// 枝番を見ないとどちらか一方の行が消える(<c>2203291</c> が 臣鷹出撃 と 臣鷹の雷撃衝撃 にまたがる)。
    /// </para>
    ///
    /// <para>
    /// 詰め方は32bitシフト。<b>桁を食い合わないので、枝番がどんな値でも別の鍵と衝突しない。</b>
    /// </para>
    /// </summary>
    public static long MakeSourceKey(int ownerId, int branch)
        => ((long)ownerId << 32) | (uint)branch;

    /// <summary>発生源キーの <c>OwnerId</c> 側。</summary>
    public static int SourceKeyOwnerId(long key) => (int)(key >> 32);

    /// <summary>発生源キーの枝番(<c>HitEventId</c>)側。</summary>
    public static int SourceKeyBranch(long key) => unchecked((int)(uint)key);

    /// <summary>発生源キーの表示形。ファイルの鍵と同じ <c>ownerId:枝番</c>。</summary>
    public static string FormatSourceKey(long key)
        => $"{SourceKeyOwnerId(key)}:{SourceKeyBranch(key)}";

    private static bool TryParseSourceKey(string text, out long key)
    {
        key = 0;
        var separator = text.IndexOf(':');
        if (separator <= 0
            || !int.TryParse(text.AsSpan(0, separator), out var ownerId)
            || !int.TryParse(text.AsSpan(separator + 1), out var branch))
        {
            return false;
        }

        key = MakeSourceKey(ownerId, branch);
        return true;
    }

    /// <summary>
    /// 内部ID注記を付けないメーターの行名。<b>記録・保存する値にはこちらを使う。</b>
    ///
    /// <para>
    /// 注記は表示設定なので、付いたまま保存すると設定を切ったあとも残る。
    /// </para>
    /// </summary>
    public static string GetSourceName(long rowKey)
        => ResolveText(_recountNames, Volatile.Read(ref _cultureName), rowKey);

    /// <summary>
    /// メーターの行に出す名前。<b>ゲーム内メーターの見出し表(手修正込み)が先に決める。</b>
    ///
    /// <para>
    /// 見出し表で名前が空の行は、バフから来た行で鍵のバフが料理なら料理の名前(<c>CuisineBuffs.json</c>)。
    /// それも無ければ、記録時に付与元をたどって着いた先(<paramref name="landing"/>)の名前を出す。
    /// オプションのバフなら <c>RogueEntryNames</c>、プレイヤーが使った技なら技の名前(<c>SkillTalkNames</c> → <c>SkillNames</c>)。
    /// それも空なら、全体設定で内部名を出すときは内部名(<see cref="GetSourceInternalName"/>)、出さなければ空のまま(内部ID注記だけ)。
    /// 見出し表の名前をこれらで上書きすることはない。
    /// </para>
    ///
    /// <para>
    /// 引数は<b>畳んだあとの行代表キー</b>。注記もそのキーで付けるので、
    /// 「どの行か」と「注記のID」が必ず一致する。
    /// </para>
    /// </summary>
    public static string GetSourceDisplayName(long rowKey, bool isBuffSource, SourceLanding landing)
    {
        var name = GetSourceNameWithoutInternalId(rowKey, isBuffSource, landing);
        if (string.IsNullOrEmpty(name) && ShowsUnnamedSkillInternalNames)
        {
            name = GetSourceInternalName(rowKey, isBuffSource, landing);
        }

        return AppendSourceInternalId(
            name,
            isBuffSource ? InternalIdDisplayMode.BuffOnly : InternalIdDisplayMode.SkillOnly,
            rowKey);
    }

    /// <summary>
    /// メーターの行名から、内部名と内部ID注記を除いたもの(見出し表と料理 → 着地先の名前)。どちらも無ければ空。
    /// 表示(<see cref="GetSourceDisplayName"/>)と、プラグインへ渡す行名がこれを使う。
    /// </summary>
    public static string GetSourceNameWithoutInternalId(long rowKey, bool isBuffSource, SourceLanding landing)
    {
        var name = GetSourceNameBeforeLanding(rowKey, isBuffSource);
        return string.IsNullOrEmpty(name)
            ? GetLandingName(landing)
            : name;
    }

    /// <summary>
    /// メーターの行の内部名。付与元をたどって技に着いていればその技の内部名、空なら行の鍵の番号で引く
    /// (バフの行はバフの内部名、それ以外は技の内部名。弾の番号は親の技へたどる)。どれも無ければ空。
    /// </summary>
    private static string GetSourceInternalName(long rowKey, bool isBuffSource, SourceLanding landing)
    {
        var name = landing.Kind == SourceLandingKind.Skill
            ? GetSkillInternalName(landing.Id)
            : string.Empty;
        if (name.Length != 0)
        {
            return name;
        }

        var ownerId = SourceKeyOwnerId(rowKey);
        if (isBuffSource)
        {
            return GetBuffInternalName(ownerId);
        }

        return TryResolveBulletParentSkillId(ownerId, out var skillId)
            ? GetSkillInternalName(skillId)
            : string.Empty;
    }

    /// <summary>
    /// 着地先より前に決まるメーターの行名(内部ID注記なし)。見出し表(手修正込み)、空ならバフから来た行で鍵のバフが料理なら料理の名前。
    /// どちらも無ければ空。
    ///
    /// <para>
    /// 表示(<see cref="GetSourceDisplayName"/>)と空欄の検知(着地先を決めるかどうか)の両方がこれで判定する。
    /// 片方だけ変えると、名前が出る行を空欄として報告するか、空欄の行を報告しなくなる。
    /// </para>
    /// </summary>
    public static string GetSourceNameBeforeLanding(long rowKey, bool isBuffSource)
    {
        var cultureName = Volatile.Read(ref _cultureName);
        var name = ResolveText(_recountNames, cultureName, rowKey);
        if (string.IsNullOrEmpty(name) && isBuffSource && _cuisineBuffIds.Contains(SourceKeyOwnerId(rowKey)))
        {
            name = ResolveText(_cuisineBuffNames, cultureName, SourceKeyOwnerId(rowKey));
        }

        return name;
    }

    /// <summary>着地先の名前。着いていないか名前が無ければ空。</summary>
    public static string GetLandingName(SourceLanding landing)
    {
        var cultureName = Volatile.Read(ref _cultureName);
        return landing.Kind switch
        {
            SourceLandingKind.RogueEntry => ResolveText(_rogueEntryNames, cultureName, landing.Id),
            SourceLandingKind.Skill => ResolveText(_skillNames, cultureName, landing.Id),
            SourceLandingKind.Module => ResolveRootBuffName(cultureName, landing.Id),
            _ => string.Empty
        };
    }

    /// <summary>
    /// バフ/デバフリストに出すバフの名前(<see cref="GetBuffDisplayNameWithoutInternalId"/>)に、内部ID注記をバフの番号で付けたもの。
    /// </summary>
    public static string GetBuffDisplayName(int buffId, SourceLanding origin)
        => AppendInternalId(GetBuffDisplayNameWithoutInternalId(buffId, origin), InternalIdDisplayMode.BuffOnly, buffId);

    /// <summary>
    /// バフ/デバフリストに出すバフの名前(注記なし)。バフの番号で引く名前(<c>BuffNames</c> と手修正、料理・薬剤はその表)が空なら、
    /// そのバフの付与元をたどった由来(<paramref name="origin"/>)の名前。どちらも無ければ空。カードの見出しと通知もこの名前を使う。
    /// </summary>
    public static string GetBuffDisplayNameWithoutInternalId(int buffId, SourceLanding origin)
    {
        var name = ResolveBuffName(Volatile.Read(ref _cultureName), buffId);
        return string.IsNullOrEmpty(name) ? GetLandingName(origin) : name;
    }

    /// <summary>オプションの表(<c>RogueEntryTable.BuffId</c>)にあるバフか。</summary>
    public static bool IsRogueEntryBuff(int buffId) => _rogueEntryBuffIds.Contains(buffId);

    /// <summary>モジュールの根のバフなら、そのモジュール効果の番号を返す。</summary>
    public static bool TryGetModEffectIdByRootBuff(int buffId, out int effectId)
        => _modEffectIdByRootBuff.TryGetValue(buffId, out effectId);

    /// <summary>
    /// 根のバフ(タレント・心相晶のノードと上級・因子・モジュール効果)か。表 <c>RootBuffs.json</c> にあるかで決める。
    /// タレントの特化型、心相晶の型の根・上級の型の専用効果・ローグライク専用の型の木のノードは表に無いので、根のバフとして扱わない。
    /// </summary>
    public static bool IsRootBuff(int buffId) => _rootBuffs.ContainsKey(buffId);
    public static bool IsSilenceBuff(int buffId) => buffId is 55426 or 510541
        || (_buffs.TryGetValue(buffId, out var buff) && buff.BuffAbilitySubType == (int)Zproto.EBuffAbnormalSubType.EabnormalTypeSilence);

    /// <summary>根のバフの種類と、その種類の中の並び(表の順)。根のバフでなければ false。</summary>
    public static bool TryGetRootBuffKind(int buffId, out RootBuffKind kind, out int order)
    {
        if (_rootBuffs.TryGetValue(buffId, out var definition))
        {
            kind = definition.Kind;
            order = definition.Order;
            return true;
        }

        kind = default;
        order = 0;
        return false;
    }

    /// <summary>根のバフの名前。内部ID注記はバフと同じ設定で付く。名前が無ければ空(注記だけ)。</summary>
    public static string GetRootBuffName(int buffId)
        => AppendInternalId(ResolveRootBuffName(Volatile.Read(ref _cultureName), buffId), InternalIdDisplayMode.BuffOnly, buffId);

    /// <summary>根のバフの名前(注記なし)。モジュールの根のバフはその効果の名前、ほかは根のバフの名前。</summary>
    private static string ResolveRootBuffName(string cultureName, int buffId)
        => _modEffectIdByRootBuff.TryGetValue(buffId, out var effectId)
            ? ResolveText(_moduleEffectNames, cultureName, effectId)
            : ResolveText(_rootBuffNames, cultureName, buffId);

    /// <summary>
    /// モジュール効果の名前。内部ID注記は、根のバフのある効果ならその根のバフの番号で、バフと同じ設定で付く(根のバフの行と同じ見た目)。
    /// 名前が無ければ空(注記だけ)。
    /// </summary>
    public static string GetModuleEffectName(int effectId)
    {
        var name = ResolveText(_moduleEffectNames, Volatile.Read(ref _cultureName), effectId);
        return _moduleEffects.TryGetValue(effectId, out var definition) && definition.RootBuffId > 0
            ? AppendInternalId(name, InternalIdDisplayMode.BuffOnly, definition.RootBuffId)
            : name;
    }

    /// <summary>モジュール効果の絵の全体のパス(表の値のまま)。表に無い効果は空。</summary>
    public static string GetModuleEffectIconName(int effectId)
        => _moduleEffects.TryGetValue(effectId, out var definition) ? definition.Icon : string.Empty;

    /// <summary>
    /// モジュール効果のリンク数の和から段階(0〜6)を決める。段階ごとに要るリンク数は表の値で、届いている一番上の段階。
    /// 表に無い効果は null。
    /// </summary>
    public static int? GetModuleEffectLevel(int effectId, int linkCount)
    {
        if (!_moduleEffects.TryGetValue(effectId, out var definition))
        {
            return null;
        }

        var level = 0;
        while (level < definition.LevelLinks.Length && linkCount >= definition.LevelLinks[level])
        {
            level++;
        }

        return level;
    }

    /// <summary>
    /// 根のバフの絵の全体のパス(表の値のまま)。心相晶の上級は型ごとに絵が違うので、その人の型の根のバフ
    /// (<paramref name="seasonTalentRootBuffId"/>、0 は分からない)で選ぶ。型が分からない・型が上級を持たないときは空。
    /// </summary>
    public static string GetRootBuffIconName(int buffId, int seasonTalentRootBuffId)
    {
        if (!_rootBuffs.TryGetValue(buffId, out var definition))
        {
            return string.Empty;
        }

        if (definition.Kind != RootBuffKind.SeasonTalentAdvanced)
        {
            return definition.Icon;
        }

        return _seasonTalentAdvancedEffectIds.TryGetValue(seasonTalentRootBuffId, out var advancedEffectId)
            && definition.AdvancedIcons.TryGetValue(advancedEffectId, out var icon)
            ? icon
            : string.Empty;
    }

    /// <summary>タレントの名前(<c>TalentNames.json</c>)。名前が無ければ空。</summary>
    public static string GetTalentName(int talentId)
        => ResolveText(_talentNames, Volatile.Read(ref _cultureName), talentId);

    /// <summary>
    /// バフを付けないアビリティの節の技の置き換えに出てくる技(元の技か置き換え後の技)なら、その元の技。ほかの技なら false。
    /// </summary>
    public static bool TryGetReplaceableSkillOriginal(int skillId, out int originalSkillId)
        => _replaceableSkillOriginals.TryGetValue(skillId, out originalSkillId);

    /// <summary>
    /// タレントの木の形。<paramref name="subProfessionId"/> は特化の番号(クラスR2 の木)、クラスR1 の木は 0。
    /// サーバーで表の版が違い、2つのタレントの位置が入れ替わった節などがあるので、アジアサーバーならアジアサーバーの表の木、
    /// それ以外(中国・グローバル・東南アジア・分からない)は中国サーバーの表の木を返す。表に無い組(変身クラスなど)なら <c>null</c>。
    /// </summary>
    public static TalentTreeDefinition? GetTalentTree(GameServer server, int professionId, int subProfessionId)
    {
        var trees = server == GameServer.Asia ? _asiaTalentTrees : _chinaTalentTrees;
        return trees.TryGetValue((professionId, subProfessionId), out var tree) ? tree : null;
    }

    /// <summary>
    /// 因子のアイテムの名前(等級つきのアイテム名 <c>ItemNames.json</c>)。内部ID注記は、そのサーバーでアイテムがバフを付けるなら
    /// そのバフの番号で、バフと同じ設定で付く(根のバフの行と同じ見た目)。名前が無ければ空(注記だけ)。
    /// サーバーの表はタレントの木と同じ決め方(アジアサーバーだけアジアの表、それ以外は中国の表)。
    /// </summary>
    public static string GetFactorItemName(GameServer server, int itemId)
    {
        var name = GetItemName(itemId);
        var items = server == GameServer.Asia ? _asiaFactorItems : _chinaFactorItems;
        return items.TryGetValue(itemId, out var item) && item.BuffId > 0
            ? AppendInternalId(name, InternalIdDisplayMode.BuffOnly, item.BuffId)
            : name;
    }

    /// <summary>因子のアイテムの絵の全体のパス(表の値のまま)。そのサーバーの表に無いアイテムは空。</summary>
    public static string GetFactorItemIconName(GameServer server, int itemId)
    {
        var items = server == GameServer.Asia ? _asiaFactorItems : _chinaFactorItems;
        return items.TryGetValue(itemId, out var item) ? item.Icon : string.Empty;
    }

    /// <summary>
    /// バフ/デバフリストで使うバフの番号。手修正 <c>BuffOverrides.json</c> の <c>FoldInto</c> で畳んだバフは畳み先の番号、ほかは自分の番号。
    /// 行とカードの鍵、行が渡す番号(非表示リスト・カードが引く)に使う。名前と絵は畳む側の番号でも畳み先のものが引ける
    /// (読み込みが写す)。
    /// </summary>
    public static int GetBuffDisplayId(int buffId)
        => _buffs.TryGetValue(buffId, out var buff) && buff.FoldInto is { } targetId ? targetId : buffId;

    /// <summary>
    /// モジュール効果の絵の置き場所(全体のパスの先頭)。モジュール効果のバフの絵は、手修正 <c>BuffOverrides.json</c> の <c>Icon</c> に
    /// 表 <c>ModEffectTable</c> の <c>EffectConfigIcon</c> の値を書く。
    /// </summary>
    private const string ModEffectIconFolder = "ui/atlas/mod_effect/";

    /// <summary>
    /// アイコンがモジュール効果の絵か。引数は絵の全体のパス。アプリはこの判定でモジュールの絵のフォルダを探す。
    /// </summary>
    public static bool IsModEffectIcon(string? iconName)
        => iconName?.StartsWith(ModEffectIconFolder, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>イマジンのパッシブの根のバフなら、そのイマジンの技IDを返す。</summary>
    public static bool TryGetImagineSkillIdByPassiveRoot(int buffId, out int skillId)
        => _imagineSkillIdByPassiveRoot.TryGetValue(buffId, out skillId);

    /// <summary>シーズンタレントの型の根ノードのバフか(<c>SeasonTalentNames.json</c> の鍵にあるか)。</summary>
    public static bool IsSeasonTalentRootBuff(int buffId) => _seasonTalentRootBuffIds.Contains(buffId);

    /// <summary>
    /// シーズンタレントの型の根ノードの名前。鍵は根ノードのバフID。表示中の言語で引き、無ければ空。
    /// 内部IDの注記はバフと同じ設定で付く。
    /// </summary>
    public static string GetSeasonTalentName(int rootBuffId)
    {
        return AppendInternalId(
            ResolveText(_seasonTalentNames, Volatile.Read(ref _cultureName), rootBuffId),
            InternalIdDisplayMode.BuffOnly,
            rootBuffId);
    }

    /// <summary>特化の名前。鍵は特化の番号(<c>SubProfessionId</c>)。表示中の言語で引き、無ければ空。</summary>
    public static string GetClassSpecName(int subProfessionId)
    {
        return ResolveText(_classSpecNames, Volatile.Read(ref _cultureName), subProfessionId);
    }

    /// <summary>シーズンの名前(シーズン実績の1枠目の分類名)。表示中の言語で引き、無ければ空。</summary>
    public static string GetSeasonName(int seasonId)
    {
        return ResolveText(_seasonNames, Volatile.Read(ref _cultureName), seasonId);
    }

    /// <summary>
    /// マッチング先の名前(マッチングの通知の <c>MatchKeyInfo</c>)。番号の意味は種類で決まる:
    /// パーティのマッチングは <c>TeamTargetTable</c>、活動のマッチングは <c>SeasonActTable</c> の行。表示中の言語で引き、無ければ空。
    /// </summary>
    public static string GetMatchTargetName(Zproto.EMatchType matchType, long matchTypeUuid)
    {
        if (matchTypeUuid is <= 0 or > int.MaxValue)
        {
            return string.Empty;
        }

        var names = matchType switch
        {
            Zproto.EMatchType.Team => _teamTargetNames,
            Zproto.EMatchType.Activity => _seasonActNames,
            _ => null
        };
        return names is null ? string.Empty : ResolveText(names, Volatile.Read(ref _cultureName), (int)matchTypeUuid);
    }

    /// <summary>
    /// シーズンランクの名前。段階(<c>SeasonRankTable.RankToLevel</c>)の中で★が一番小さい行の名前。
    /// 表示中の言語で引き、無ければ空。
    /// </summary>
    public static string GetSeasonRankName(int seasonId, int rankLevel)
    {
        return ResolveText(_seasonRankNames, Volatile.Read(ref _cultureName), seasonId * 100 + rankLevel);
    }

    /// <summary>アイテム名(<c>ItemTable.Name</c>)。鍵はアイテムID(装備IDも同じ)。表示中の言語で引き、無ければ空。</summary>
    public static string GetItemName(int itemId)
    {
        return ResolveText(_itemNames, Volatile.Read(ref _cultureName), itemId);
    }

    /// <summary>
    /// 進化・改鋳・レアの効果(一時属性・バフ)の説明文。値の差し込みは <c>{0}</c>(そのままの値)と
    /// <c>{1}</c>(% の値。値 ÷ 100)。言語でどちらが入るかが違うことがある。
    /// 鍵は効果の番号。表示中の言語で引き、無ければ空。
    /// </summary>
    public static string GetEquipEffectText(int effectId)
    {
        return ResolveText(_equipEffectTexts, Volatile.Read(ref _cultureName), effectId);
    }

    /// <summary>装備の定義。表に無い装備IDなら <c>null</c>。</summary>
    public static EquipmentDefinition? GetEquipment(int equipId)
    {
        return _equipments.TryGetValue(equipId, out var equipment) ? equipment : null;
    }

    /// <summary>
    /// 属性庫の行を表の並び順で。<paramref name="libType"/> は 1 = <c>EquipAttrLibTable</c>、
    /// 2 = <c>EquipAttrSchoolLibTable</c>。表に無い庫なら空。
    /// </summary>
    public static IReadOnlyList<EquipmentAttrLibRow> GetEquipmentAttrLibRows(int libType, int libId)
    {
        return _equipmentAttrLibs.TryGetValue(MakeEquipmentKey(libType, libId), out var rows) ? rows : [];
    }

    /// <summary>
    /// 属性庫の行を行ID で。<paramref name="libType"/> は <see cref="GetEquipmentAttrLibRows"/> と同じ。
    /// 行ID は型ごとの番号なので、型を取り違えると別の行になる。表に無ければ <c>null</c>。
    /// </summary>
    public static EquipmentAttrLibRow? GetEquipmentAttrRow(int libType, int rowId)
    {
        return _equipmentAttrRows.TryGetValue(MakeEquipmentKey(libType, rowId), out var row) ? row : null;
    }

    /// <summary>
    /// 効果(種類と番号)がシーズン強度の項目ならそのシーズン、でなければ 0。
    /// 項目のシーズンは、その効果を基礎に持つ装備の <c>EquipTable.SeasonId</c> の積集合で決めてある。
    /// </summary>
    public static int GetEquipmentStrengthSeason(int kind, int effectId)
    {
        return _equipmentStrengthSeasons.TryGetValue(MakeEquipmentKey(kind, effectId), out var season) ? season : 0;
    }

    /// <summary>特化の番号(<c>SubProfessionId</c>)に対応する型2 の属性庫の特化。無ければ 0。</summary>
    public static int GetEquipmentSpecSchool(int subProfessionId)
    {
        return _equipmentSpecSchools.TryGetValue(subProfessionId, out var school) ? school : 0;
    }

    /// <summary>クラスR1 の人(職業ID)に対応する型2 の属性庫の特化。無ければ 0。</summary>
    public static int GetEquipmentRank1School(int professionId)
    {
        return _equipmentRank1Schools.TryGetValue(professionId, out var school) ? school : 0;
    }

    /// <summary>
    /// 発生源キー → 行代表キー。<c>RecountRows.json</c> の行構成に手修正を重ねたもの。
    ///
    /// <para>
    /// ゲーム内メーターは <c>RecountTable</c> の1行に複数の <c>DamageId</c> をまとめる。
    /// 生成物はその所属をそのまま写したもので、<b>手書きの判断は入っていない</b>。
    /// 判断が要るぶんは <c>Data/Overrides/RecountRowOverrides.json</c> にある。
    /// </para>
    /// </summary>
    public static bool TryResolveRecountRow(long key, out long rowKey)
        => _recountRows.TryGetValue(key, out rowKey);

    /// <summary>
    /// 押した技(推移グラフの技のアイコン)の名前・アイコン名・背景の枠・イマジンの絵か。<b>名前とアイコンはメーターと同じ見出し表の行で畳む</b>
    /// (通常攻撃の2段目以降のように、技の表に名前もアイコンも無い技を根の技の行で出すため)。
    ///
    /// <para>
    /// 行は <see cref="TryResolveSkillActivationRow"/>。名前は行の名前、アイコンは行代表の技のアイコン。
    /// 行が無い、または行から決まらなければ技自身の名前・アイコン。どちらも無ければ空。
    /// 内部IDの注記は技の番号で付け、技名と同じ設定に従う。
    /// </para>
    ///
    /// <para>
    /// 最後に手修正(<see cref="_skillActivationOverrides"/>、押した技の技ID)で、書いてある言語の名前とアイコンだけを差し替える。
    /// 書いていない言語とアイコンは上の決め方のまま。それでも名前が空なら、全体設定で内部名を出すときは押した技の内部名。
    /// </para>
    ///
    /// <para>
    /// 枠(<see cref="GetSkillIconFrame"/>)は押した技で決める(押した技が属する、枠の番号を持つ技の番号。畳んだ行では決めない)。
    /// イマジンの絵か(<see cref="IsImagineArtIcon"/>)は出す絵(手修正の後)で決める。
    /// </para>
    /// </summary>
    public static (string Name, string IconName, SkillIconFrame Frame, bool UsesImagineAsset) GetSkillActivationDisplay(int skillId)
    {
        var cultureName = Volatile.Read(ref _cultureName);
        var name = string.Empty;
        var iconName = string.Empty;
        if (TryResolveSkillActivationRow(skillId, out var rowKey))
        {
            name = ResolveText(_recountNames, cultureName, rowKey);
            iconName = GetSkillIconName(SourceKeyOwnerId(rowKey));
        }

        if (string.IsNullOrEmpty(name))
        {
            name = ResolveText(_skillNames, cultureName, skillId);
        }

        if (string.IsNullOrEmpty(iconName))
        {
            iconName = GetSkillIconName(skillId);
        }

        if (_skillActivationOverrides.TryGetValue(skillId, out var activationOverride))
        {
            if (activationOverride.Names.TryGetValue(cultureName, out var overrideName))
            {
                name = overrideName;
            }

            if (activationOverride.Icon is { } overrideIcon)
            {
                iconName = overrideIcon;
            }
        }

        if (string.IsNullOrEmpty(name) && ShowsUnnamedSkillInternalNames)
        {
            name = GetSkillInternalName(skillId);
        }

        return (
            AppendInternalId(name, InternalIdDisplayMode.SkillOnly, skillId),
            iconName,
            GetSkillIconFrame(skillId),
            IsImagineArtIcon(iconName));
    }

    /// <summary>
    /// 押した技を畳む見出し表の行。技の番号を ownerId とする鍵の行で、鍵が2つ以上の行に分かれていれば、
    /// 枝番が 0 でない最小の枝番の鍵の行(枝番 0 しか無ければ枝番 0 の鍵の行)。
    /// </summary>
    public static long GetSkillActivationSourceKey(int skillId)
        => TryResolveSkillActivationRow(skillId, out var rowKey) ? rowKey : MakeSourceKey(skillId, 0);

    private static bool TryResolveSkillActivationRow(int skillId, out long rowKey)
    {
        rowKey = 0;
        if (!_recountKeysByOwner.TryGetValue(skillId, out var keys) || keys.Length == 0)
        {
            return false;
        }

        var chosen = keys[0];
        foreach (var key in keys)
        {
            if (key.Branch != 0)
            {
                chosen = key;
                break;
            }
        }

        rowKey = chosen.RowKey;
        return true;
    }

    /// <param name="Row">
    /// 行の出入り。別の鍵ならその鍵の行へ入れる(追加)、<c>null</c> なら行から外して単独にする(削除)。
    /// 省略したら生成物のまま。
    /// </param>
    /// <param name="RowSpecified">
    /// <c>Row</c> が書かれていたか。<c>null</c> は「外す」という指示なので、
    /// 「書かなかった」と区別しないと省略が全部「外す」になる。
    /// </param>
    /// <param name="Name">言語 → 名前。書いた言語だけ差し替え、空文字は生成値を消す。</param>
    private sealed class RecountOverrideEntry
    {
        public string? Row { get; set; }

        public bool RowSpecified { get; set; }

        public Dictionary<string, string>? Name { get; set; }
    }

    /// <summary>
    /// 行構成・行名・手修正をまとめて読む。
    ///
    /// <list type="number">
    ///   <item>生成物 <c>RecountRows.json</c> の行から、行の集まりを作る</item>
    ///   <item>手修正の <c>Row</c> を当てる。<b>外すほうを先に</b>当てないと、外した鍵へ寄せられない</item>
    ///   <item>手修正に <c>Name</c> だけ書いた、どの行にも無い鍵を、その鍵だけの行にする</item>
    ///   <item>行代表を取り直す。外した鍵が代表だった行は代表が変わる</item>
    ///   <item>生成物の名前を行代表へ配り、手修正の <c>Name</c> を重ねる</item>
    /// </list>
    /// </summary>
    private static void LoadRecounts(JsonDataFileReads reads)
    {
        // 生成物は生の見出し表と同じ形。行ごとに RecountName(4言語)と、その行に属する発生源キーの一覧。
        // 項目名は SourceId。中身は TypeEnum:枝番 で、生の DamageId とは別の値。
        var layout = new Dictionary<string, RecountRow>();
        var path = RecountRowsPath;
        if (reads.TryTake<Dictionary<string, RecountRow>>(path, out var loadedLayout))
        {
            layout = loadedLayout
                ?? throw new InvalidDataException($"{path} is empty.");
            var missingNames = 0;
            foreach (var row in layout)
            {
                row.Value.RecountName = CheckLocalizedNames("RecountRows", row.Key, row.Value.RecountName, ref missingNames);
            }

            LogMissingLocalizedNames("RecountRows", missingNames);
            Log.Information("Loaded localization {FileName}", "RecountRows");
        }
        else
        {
            Log.Error("Missing localization {FileName}", "RecountRows");
        }

        var groups = new Dictionary<long, HashSet<long>>();
        var groupOf = new Dictionary<long, long>();
        // 鍵 → 元の行。手修正で行から外した鍵が、外れたあとも元の行名を土台にできる。
        var originRow = new Dictionary<long, string>();

        foreach (var row in layout)
        {
            long? head = null;
            foreach (var text in row.Value.SourceId ?? [])
            {
                if (!TryParseSourceKey(text, out var key))
                {
                    Log.Error("recounts: row {Row} has \"{Key}\" which is not in ownerId:branch form", row.Key, text);
                    continue;
                }

                originRow[key] = row.Key;
                if (head is null)
                {
                    groups[key] = [key];
                    groupOf[key] = key;
                    head = key;
                }
                else
                {
                    Join(groups, groupOf, key, head.Value);
                }
            }
        }

        var overrides = LoadRecountOverrides(reads);

        // 外すほうを先に。寄せ先が「外したばかりの鍵」であることがある。
        foreach (var entry in overrides)
        {
            if (entry.Value.RowSpecified && entry.Value.Row is null)
            {
                Detach(groups, groupOf, entry.Key);
            }
        }

        foreach (var entry in overrides)
        {
            if (!entry.Value.RowSpecified || entry.Value.Row is null)
            {
                continue;
            }

            if (!TryParseSourceKey(entry.Value.Row, out var target))
            {
                Log.Error("RecountRowOverrides: {Key} has Row \"{Row}\" which is not in ownerId:branch form",
                    FormatSourceKey(entry.Key), entry.Value.Row);
                continue;
            }

            Join(groups, groupOf, entry.Key, target);
        }

        // 名前だけ書いた、どの行にも無い鍵は、その鍵だけの行にする(生成物に無い鍵を足す仕様)。
        // 行に入れないと、押した技の名前(GetSkillActivationDisplay)がこの鍵の行を引けない。
        foreach (var entry in overrides)
        {
            if (!entry.Value.RowSpecified && entry.Value.Name is not null && !groupOf.ContainsKey(entry.Key))
            {
                groups[entry.Key] = [entry.Key];
                groupOf[entry.Key] = entry.Key;
            }
        }

        // 行代表は行の最小キー。ownerId → 枝番 の順で比べる。
        var repOf = new Dictionary<long, long>();
        var membersOfRep = new Dictionary<long, List<long>>();
        foreach (var group in groups)
        {
            var members = group.Value.OrderBy(k => SourceKeyOwnerId(k)).ThenBy(SourceKeyBranch).ToList();
            var rep = members[0];
            membersOfRep[rep] = members;
            foreach (var key in members)
            {
                repOf[key] = rep;
            }
        }

        _recountRows = repOf.ToFrozenDictionary();
        _recountKeysByOwner = repOf
            .GroupBy(pair => SourceKeyOwnerId(pair.Key))
            .ToFrozenDictionary(
                group => group.Key,
                group => group
                    .Select(pair => (Branch: SourceKeyBranch(pair.Key), RowKey: pair.Value))
                    .OrderBy(entry => entry.Branch)
                    .ToArray());
        _recountNames = BuildRecountNames(membersOfRep, originRow, layout, repOf, overrides);
        Log.Information("Loaded {Keys} recount keys / {Rows} rows / {Overrides} overrides",
            repOf.Count, groups.Count, overrides.Count);
    }

    /// <param name="RecountName">行名。</param>
    /// <param name="SourceId">
    /// その行に属する発生源キー(<c>ownerId:枝番</c>)。
    /// <b><c>DamageId</c> とは別の値</b>なので、生の見出し表と同じ名前は使わない。
    /// </param>
    private sealed class RecountRow
    {
        /// <summary>言語 → 行名。</summary>
        public Dictionary<string, string>? RecountName { get; set; }

        public List<string>? SourceId { get; set; }
    }

    private static void Join(
        Dictionary<long, HashSet<long>> groups,
        Dictionary<long, long> groupOf,
        long key,
        long target)
    {
        // 先に今いる行から抜く。抜き忘れると空になった行が残り、
        // 代表を取り直すときにその行が「鍵ひとつだけの行」として復活してしまう。
        Remove(groups, groupOf, key);

        // 寄せ先がまだどの行にも属していなければ、その鍵だけの行を先に作る。
        if (!groupOf.TryGetValue(target, out var groupId))
        {
            groupId = target;
            groups[groupId] = [target];
            groupOf[target] = groupId;
        }

        groups[groupId].Add(key);
        groupOf[key] = groupId;
    }

    private static void Detach(
        Dictionary<long, HashSet<long>> groups,
        Dictionary<long, long> groupOf,
        long key)
    {
        Remove(groups, groupOf, key);
        groups[key] = [key];
        groupOf[key] = key;
    }

    private static void Remove(
        Dictionary<long, HashSet<long>> groups,
        Dictionary<long, long> groupOf,
        long key)
    {
        if (!groupOf.TryGetValue(key, out var groupId) || !groups.TryGetValue(groupId, out var members))
        {
            return;
        }

        members.Remove(key);
        groupOf.Remove(key);

        if (members.Count == 0)
        {
            groups.Remove(groupId);
            return;
        }

        // 行の識別子はメンバーの鍵そのもの。その鍵を抜いたら残りのメンバーで付け直す。
        // 付け直さないと、抜いた鍵で行を作り直したときに残りのメンバーごと消える。
        if (groupId == key)
        {
            groups.Remove(groupId);
            var next = members.First();
            groups[next] = members;
            foreach (var member in members)
            {
                groupOf[member] = next;
            }
        }
    }

    /// <summary>
    /// <c>Data/Overrides/</c> の名前の上書きを、<c>Data/Localization/</c> の名前の表に重ねる。形は
    /// <c>RecountRowOverrides</c> の名前と同じで、書いた言語だけ差し替わる(空文字はその言語の名前を消し、以後は zh-CN へ落ちる)。
    ///
    /// <para>
    /// 生成物に無い番号も書け、名前の表に行がある扱いになる。<c>Name</c> の無い項目は行を作らずに飛ばす。
    /// 鍵が番号でない・言語名の打ち間違いはエラーログを出して飛ばす(静かに効かないので)。
    /// </para>
    /// </summary>
    private static (FrozenDictionary<string, FrozenDictionary<int, string>> Names, FrozenSet<int> Ids) ApplyNameOverrides(
        JsonDataFileReads reads,
        FrozenDictionary<string, FrozenDictionary<int, string>> names,
        FrozenSet<int> ids,
        string fileName)
    {
        var relativePath = $"Overrides/{fileName}";
        var path = OverrideFilePath(fileName);
        if (!reads.TryTake<Dictionary<string, NameOverrideEntry?>>(path, out var loadedOverrides))
        {
            Log.Error("Failed to load {OverridePath}", relativePath);
            return (names, ids);
        }

        var overrides = loadedOverrides
            ?? throw new InvalidDataException($"{relativePath} が空です。");
        var merged = names.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToDictionary(),
            StringComparer.OrdinalIgnoreCase);
        var mergedIds = ids.ToHashSet();

        foreach (var (key, entry) in overrides)
        {
            if (!int.TryParse(key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var id)
                || id <= 0)
            {
                Log.Error("{OverridePath}: skipping \"{Key}\" because the key is not numeric", relativePath, key);
                continue;
            }

            if (entry?.Name is null)
            {
                Log.Error("{OverridePath}: skipping {Key} because it has no Name", relativePath, key);
                continue;
            }

            mergedIds.Add(id);
            foreach (var (culture, value) in entry.Name)
            {
                if (!merged.TryGetValue(culture, out var namesById))
                {
                    Log.Error(
                        "{OverridePath}: {Key} に未知の言語 \"{Culture}\"。言語は {Cultures}",
                        relativePath, key, culture, string.Join(" / ", SupportedCultures));
                    continue;
                }

                var text = value?.Trim() ?? string.Empty;
                if (text.Length == 0)
                {
                    namesById.Remove(id);
                }
                else
                {
                    namesById[id] = text;
                }
            }
        }

        Log.Information("Loaded {OverridePath}", relativePath);
        return (
            merged.ToFrozenDictionary(
                pair => pair.Key,
                pair => pair.Value.ToFrozenDictionary(),
                StringComparer.OrdinalIgnoreCase),
            mergedIds.ToFrozenSet());
    }

    /// <summary>
    /// 手修正 <c>BuffOverrides.json</c> の <c>FoldInto</c> で畳んだバフに、畳み先の名前を言語ごとに写す
    /// (畳み先に名前の無い言語は畳む側の名前も消し、zh-CN へ落ちる)。畳めなかった行は読み込みが印を消している。
    /// </summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> ApplyBuffFoldNames(
        FrozenDictionary<string, FrozenDictionary<int, string>> names)
    {
        var folds = _buffs
            .Where(pair => pair.Value.FoldInto.HasValue)
            .Select(pair => (BuffId: pair.Key, TargetId: pair.Value.FoldInto!.Value))
            .ToList();
        if (folds.Count == 0)
        {
            return names;
        }

        return names.ToFrozenDictionary(
            pair => pair.Key,
            pair =>
            {
                var namesById = pair.Value.ToDictionary();
                foreach (var (buffId, targetId) in folds)
                {
                    if (namesById.TryGetValue(targetId, out var targetName))
                    {
                        namesById[buffId] = targetName;
                    }
                    else
                    {
                        namesById.Remove(buffId);
                    }
                }

                return namesById.ToFrozenDictionary();
            },
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 技の名前に公式の技名(<c>SkillTable.SkillTalk</c>、<c>SkillTalkNames.json</c>)を言語ごとに重ねる。
    /// その言語に <c>SkillTalk</c> の名前がある技だけ差し替わり、無い技は技の表の名前(と <c>SkillNameOverrides</c>)のまま。
    /// </summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> ApplySkillTalkNames(
        FrozenDictionary<string, FrozenDictionary<int, string>> names,
        FrozenDictionary<string, FrozenDictionary<int, string>> talkNames)
    {
        return names.ToFrozenDictionary(
            pair => pair.Key,
            pair =>
            {
                if (!talkNames.TryGetValue(pair.Key, out var talks) || talks.Count == 0)
                {
                    return pair.Value;
                }

                var namesById = pair.Value.ToDictionary();
                foreach (var (skillId, talk) in talks)
                {
                    namesById[skillId] = talk;
                }

                return namesById.ToFrozenDictionary();
            },
            StringComparer.OrdinalIgnoreCase);
    }

    private sealed class NameOverrideEntry
    {
        public Dictionary<string, string?>? Name { get; set; }
    }

    private static Dictionary<long, RecountOverrideEntry> LoadRecountOverrides(JsonDataFileReads reads)
    {
        var result = new Dictionary<long, RecountOverrideEntry>();

        // Row に null が書かれたのか、そもそも書かれなかったのかを見分けるため、
        // 一度 JObject で受けてからキーの有無を見る。
        if (!reads.TryTake<Dictionary<string, Newtonsoft.Json.Linq.JObject>>(RecountRowOverridesPath, out var raw))
        {
            Log.Error("Failed to load {OverridePath}", "Overrides/RecountRowOverrides.json");
            return result;
        }

        foreach (var pair in raw ?? [])
        {
            if (!TryParseSourceKey(pair.Key, out var key))
            {
                Log.Error("RecountRowOverrides: skipping \"{Key}\" because the key is not in ownerId:branch form", pair.Key);
                continue;
            }

            var rowToken = pair.Value["Row"];
            var entry = new RecountOverrideEntry
            {
                RowSpecified = rowToken is not null,
                Row = rowToken?.Type == Newtonsoft.Json.Linq.JTokenType.String
                    ? (string?)rowToken
                    : null,
            };

            if (pair.Value["Name"] is Newtonsoft.Json.Linq.JObject names)
            {
                entry.Name = [];
                foreach (var name in names)
                {
                    // 言語名の打ち間違いは静かに効かないので、必ず出す。
                    if (!SupportedCultures.Contains(name.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        Log.Error(
                            "RecountRowOverrides: {Key} に未知の言語 \"{Culture}\"。言語は {Cultures}",
                            pair.Key, name.Key, string.Join(" / ", SupportedCultures));
                        continue;
                    }

                    entry.Name[name.Key] = ((string?)name.Value)?.Trim() ?? string.Empty;
                }
            }

            result[key] = entry;
        }

        return result;
    }

    /// <summary>推移グラフの技のアイコンの手修正1件(<see cref="_skillActivationOverrides"/>)。</summary>
    /// <param name="Names">言語 → 名前。書いてある言語だけ。</param>
    /// <param name="Icon">アイコン(技の表の <c>Icon</c> と同じゲームのパスの形)。書いていなければ null。</param>
    private sealed record SkillActivationOverride(FrozenDictionary<string, string> Names, string? Icon);

    private sealed class SkillActivationOverrideEntry
    {
        public Dictionary<string, string?>? Name { get; set; }

        public string? Icon { get; set; }
    }

    /// <summary>
    /// <c>Data/Overrides/SkillActivationOverrides.json</c> を読む。鍵は押した技の技ID、中身は4言語の名前と、アイコン。
    /// 空の値は書かなかったのと同じに扱う。鍵が技IDの形でない行と、知らない言語の名前は、記録して読まない。
    /// </summary>
    private static FrozenDictionary<int, SkillActivationOverride> LoadSkillActivationOverrides(JsonDataFileReads reads)
    {
        const string relativePath = "Overrides/SkillActivationOverrides.json";
        if (!reads.TryTake<Dictionary<string, SkillActivationOverrideEntry>>(SkillActivationOverridesPath, out var raw))
        {
            Log.Error("Failed to load {OverridePath}", relativePath);
            return FrozenDictionary<int, SkillActivationOverride>.Empty;
        }

        var result = new Dictionary<int, SkillActivationOverride>();
        foreach (var pair in raw ?? [])
        {
            if (!int.TryParse(pair.Key, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var skillId))
            {
                Log.Error("SkillActivationOverrides: skipping \"{Key}\" because the key is not a skill ID", pair.Key);
                continue;
            }

            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (cultureName, text) in pair.Value?.Name ?? [])
            {
                // 言語名の打ち間違いは静かに効かないので、必ず出す。
                if (!SupportedCultures.Contains(cultureName, StringComparer.OrdinalIgnoreCase))
                {
                    Log.Error(
                        "SkillActivationOverrides: {Key} has unknown culture \"{Culture}\"; cultures are {Cultures}",
                        pair.Key, cultureName, string.Join(" / ", SupportedCultures));
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    names[cultureName] = text.Trim();
                }
            }

            var iconText = pair.Value?.Icon;
            var icon = string.IsNullOrWhiteSpace(iconText) ? null : iconText.Trim();
            result[skillId] = new SkillActivationOverride(
                names.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                icon);
        }

        Log.Information("Loaded {OverridePath}", relativePath);
        return result.ToFrozenDictionary();
    }

    /// <summary>
    /// 行代表キー → 表示名を、言語ごとに組む。
    ///
    /// <para>
    /// 生成物は行ごとに名前を持つので、<b>行代表がどの行から来たか</b>で引く。
    /// 手修正で行が組み替わると、代表が生成物に無い鍵になることがある(総括行から戻した鍵)。
    /// そのときは同じ行の別のメンバーの元の行名で埋める。
    /// </para>
    /// </summary>
    private static FrozenDictionary<string, FrozenDictionary<long, string>> BuildRecountNames(
        Dictionary<long, List<long>> membersOfRep,
        Dictionary<long, string> originRow,
        Dictionary<string, RecountRow> rows,
        Dictionary<long, long> repOf,
        Dictionary<long, RecountOverrideEntry> overrides)
    {
        var result = new Dictionary<string, FrozenDictionary<long, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var cultureName in SupportedCultures)
        {
            var names = new Dictionary<long, string>();

            foreach (var group in membersOfRep)
            {
                foreach (var member in group.Value)
                {
                    if (originRow.TryGetValue(member, out var rowId)
                        && rows.TryGetValue(rowId, out var row)
                        && row.RecountName is not null
                        && row.RecountName.TryGetValue(cultureName, out var rowName)
                        && !string.IsNullOrWhiteSpace(rowName))
                    {
                        names[group.Key] = rowName.Trim();
                        break;
                    }
                }
            }

            foreach (var entry in overrides)
            {
                if (entry.Value.Name is null
                    || !entry.Value.Name.TryGetValue(cultureName, out var text))
                {
                    continue;
                }

                // 名前は行のもの。畳まれる鍵に書いても行代表へ届く。
                var rep = repOf.TryGetValue(entry.Key, out var known) ? known : entry.Key;
                if (string.IsNullOrEmpty(text))
                {
                    names.Remove(rep);
                }
                else
                {
                    names[rep] = text;
                }
            }

            result[cultureName] = names.ToFrozenDictionary();
        }

        return result.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Available source-table description, kept as original text rather than guessed game effects.</summary>
    public static string GetActionDescription(int id, bool isBuffSource)
        => isBuffSource ? _buffs.GetValueOrDefault(id)?.Desc ?? string.Empty
            : _skills.GetValueOrDefault(id)?.Desc ?? string.Empty;

    public static string GetSkillName(int skillId)
    {
        return AppendInternalId(
            ResolveText(_skillNames, Volatile.Read(ref _cultureName), skillId),
            InternalIdDisplayMode.SkillOnly,
            skillId);
    }

    /// <summary>内部ID注記を付けない技名。名前が取れていなければ空を返す。</summary>
    public static string GetSkillNameWithoutInternalId(int skillId)
    {
        return ResolveText(_skillNames, Volatile.Read(ref _cultureName), skillId);
    }

    /// <summary>技の表の名前(古い版の名前を含む、<c>SkillTalk</c> は見ない)。内部ID注記は付けない。名前が取れていなければ空。</summary>
    public static string GetSkillTableNameWithoutInternalId(int skillId)
    {
        return ResolveText(_skillTableNames, Volatile.Read(ref _cultureName), skillId);
    }

    /// <summary>技の <c>SkillTalk</c> だけ。内部ID注記は付けない。無ければ空。</summary>
    public static string GetSkillTalkNameWithoutInternalId(int skillId)
    {
        return ResolveText(_skillTalkNames, Volatile.Read(ref _cultureName), skillId);
    }

    /// <summary>
    /// 技の内部名(<c>SkillTable.NameDesign</c>。開発用の中国語の名前で、言語で変わらない)。表に行が無いか空なら空を返す。
    /// 表示名(<see cref="GetSkillNameWithoutInternalId"/>)には混ぜない。名前の無い技に出すのは画面の技名を引く所だけ(<see cref="ShowsUnnamedSkillInternalNames"/>)。
    /// </summary>
    public static string GetSkillInternalName(int skillId)
    {
        return _skills.TryGetValue(skillId, out var skill) && !string.IsNullOrWhiteSpace(skill.NameDesign)
            ? skill.NameDesign.Trim()
            : string.Empty;
    }

    /// <summary>バフの内部名(<c>BuffTable.NameDesign</c>)。表に行が無いか空なら空を返す。使い方は <see cref="GetSkillInternalName"/> と同じ。</summary>
    public static string GetBuffInternalName(int buffId)
    {
        return _buffs.TryGetValue(buffId, out var buff) && !string.IsNullOrWhiteSpace(buff.NameDesign)
            ? buff.NameDesign.Trim()
            : string.Empty;
    }

    /// <summary>
    /// 画面に出す技名(内部ID注記つき)。名前が無ければ、全体設定で内部名を出すときは技の内部名。
    /// プレイヤーリストの技の枠が使う。保存する名前や判定には <see cref="GetSkillName"/> を使う。
    /// </summary>
    public static string GetSkillDisplayName(int skillId)
    {
        var name = ResolveText(_skillNames, Volatile.Read(ref _cultureName), skillId);
        if (string.IsNullOrEmpty(name) && ShowsUnnamedSkillInternalNames)
        {
            name = GetSkillInternalName(skillId);
        }

        return AppendInternalId(name, InternalIdDisplayMode.SkillOnly, skillId);
    }

    /// <summary>
    /// ボス大技の予告(<c>DbmTable</c>)に載っている技の名前。内部ID注記は付けない。載っていなければ空。
    ///
    /// <para>
    /// ゲームが予告に出す正式な技名で、<c>SkillTable.Name</c> が埋め草の技にも名前がある。
    /// 鍵は技ID(生成時に <c>EffectIDs</c> の要素から持ち主の技へ寄せてある)。
    /// </para>
    /// </summary>
    public static string GetDbmNameWithoutInternalId(int skillId)
    {
        return ResolveText(_dbmNames, Volatile.Read(ref _cultureName), skillId);
    }

    /// <summary>
    /// 弾(<c>Bullet</c> / <c>FakeBullet</c> 由来のダメージの <c>OwnerId</c>)から親の技IDを引く。
    ///
    /// <para>
    /// <c>OwnerId</c> は <c>BulletTable</c> の番号で、<c>BulletTable</c> には技を指す項目が無い。
    /// <c>SkillTable</c> にその番号があればその技、無ければ <c>SkillFightLevelTable</c> の同じ番号の行の
    /// <c>SkillId</c>(<c>SkillTable</c> にあるもの)を親とする。どちらにも無ければ辿れない。
    /// </para>
    /// </summary>
    public static bool TryResolveBulletParentSkillId(int bulletId, out int skillId)
    {
        if (_skills.ContainsKey(bulletId))
        {
            skillId = bulletId;
            return true;
        }

        if (HelperMethods.DataTables.SkillFightLevels.Data.TryGetValue(
                bulletId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                out var fightLevel)
            && _skills.ContainsKey(fightLevel.SkillId))
        {
            skillId = fightLevel.SkillId;
            return true;
        }

        skillId = 0;
        return false;
    }

    /// <summary>
    /// 被ダメ1件が当たった技。<b>記録時(レベルを引く対象)と表示時(ギミック技の判定)で同じ決め方にするため、ここだけで決める。</b>
    /// 技由来はその技、弾由来は親の技(<see cref="TryResolveBulletParentSkillId"/>)、バフ由来は付与元の技(<paramref name="buffSourceSkillId"/>)。
    /// それ以外(落下など)と、辿れないものは 0。
    /// </summary>
    public static int ResolveHitSkillId(Zproto.EDamageSource damageSource, int ownerId, int buffSourceSkillId)
    {
        return damageSource switch
        {
            Zproto.EDamageSource.Skill => ownerId,
            Zproto.EDamageSource.Bullet or Zproto.EDamageSource.FakeBullet =>
                TryResolveBulletParentSkillId(ownerId, out var parentSkillId) ? parentSkillId : 0,
            Zproto.EDamageSource.Buff => buffSourceSkillId,
            _ => 0
        };
    }

    /// <summary>呼び出し側で決めた名前(空のときの代わりの名前など)に、技の内部ID注記を添える。</summary>
    public static string AppendSkillInternalId(string name, int skillId)
    {
        return AppendInternalId(name, InternalIdDisplayMode.SkillOnly, skillId);
    }

    /// <summary>呼び出し側で決めた名前に、バフの内部ID注記を添える。</summary>
    public static string AppendBuffInternalId(string name, int buffId)
    {
        return AppendInternalId(name, InternalIdDisplayMode.BuffOnly, buffId);
    }

    /// <summary>
    /// 詠唱(または誘導)のバーを持つ技か。<c>SkillTable.SingOrGuideTime</c> の先頭要素の全体秒数が 0 より大きい。
    ///
    /// <para>
    /// サーバは「詠唱中」を送らない。ゲームは技の開始とこの設定から詠唱バーを出す。
    /// </para>
    /// </summary>
    public static bool HasSingOrGuideTime(int skillId)
    {
        return TryGetSingOrGuideSeconds(skillId, out _);
    }

    /// <summary>
    /// 詠唱(または誘導)のバーの全体秒数(<c>SingOrGuideTime</c> の先頭要素の先頭)。バーを持たない技なら false。
    /// 表の値は float なので、書かれた10進の値(9.4 など)に直して返す。
    /// </summary>
    public static bool TryGetSingOrGuideSeconds(int skillId, out double seconds)
    {
        if (_skills.TryGetValue(skillId, out var skill)
            && skill.SingOrGuideTime is [[> 0f and var totalSeconds, ..], ..])
        {
            seconds = (double)(decimal)totalSeconds;
            return true;
        }

        seconds = 0d;
        return false;
    }

    /// <summary>
    /// 戦闘画面の警告バーを出す技か(<c>Data/Generated/SkillWarnings.json</c>)。
    /// <b>技レベルで決まる</b>(同じ技でもレベルによって持たないことがある)ので、技IDとレベルの両方で引く。
    /// </summary>
    public static bool IsWarningSkill(int skillId, int skillLevel)
    {
        var skillLevelId = (long)skillId * 100 + skillLevel;
        return skillLevelId <= int.MaxValue && _warningSkillLevelIds.Contains((int)skillLevelId);
    }

    /// <summary>どれかのレベルで戦闘画面の警告バーを出す技か。レベルが分からないときの判定に使う。</summary>
    public static bool IsWarningSkillId(int skillId)
    {
        return _warningSkillIds.Contains(skillId);
    }

    /// <summary>予告の番号が予告の表にあれば、バーの既定の秒数(<c>CountCDTime</c>)を返す。表に無い番号のバーはゲームも出さない。</summary>
    public static bool TryGetDbmCountCdTime(int dbmId, out int seconds)
    {
        return _dbmCountCdTimes.TryGetValue(dbmId, out seconds);
    }

    /// <summary>
    /// ボス大技の予告の通知(<c>DbmTable.Id</c>)から技IDを引く。
    /// <c>SkillTable</c> にその番号があればその技、無ければその番号を <c>EffectIDs</c> に持つ技。
    /// </summary>
    public static bool TryResolveDbmSkillId(int dbmId, out int skillId)
    {
        if (_skills.ContainsKey(dbmId))
        {
            skillId = dbmId;
            return true;
        }

        return _skillIdByEffectId.TryGetValue(dbmId, out skillId);
    }

    /// <summary>モンスター(種別ID)が <c>MonsterTable.SkillIds</c> にその技を持つか。</summary>
    public static bool MonsterHasSkill(int monsterId, int skillId)
    {
        return _monsterIdsBySkillId.TryGetValue(skillId, out var monsterIds)
            && monsterIds.Contains(monsterId);
    }

    /// <summary>
    /// 内部ID注記を付けないバフ名。<b>記憶・保存する値にはこちらを使う。</b>
    ///
    /// <para>
    /// 注記は表示設定なので、付いたまま控えると設定を切ったあとも残る。
    /// 名前が取れていなければ空を返すので、「まだ分かっていない」の判定にも使える。
    /// </para>
    /// </summary>
    public static string GetBuffNameWithoutInternalId(int buffId)
    {
        return ResolveBuffName(Volatile.Read(ref _cultureName), buffId);
    }

    public static string GetBuffName(int buffId)
    {
        return AppendInternalId(
            ResolveBuffName(Volatile.Read(ref _cultureName), buffId),
            InternalIdDisplayMode.BuffOnly,
            buffId);
    }

    /// <summary>
    /// バフの名前。料理・薬剤のバフはその表の名前(付けるアイテムの名前)、それ以外は <c>BuffNames</c>(手修正込み)。
    /// 料理・薬剤のバフでも、その表の名前が全言語で空なら <c>BuffNames</c> へ落とさず空を返す。
    /// </summary>
    private static string ResolveBuffName(string cultureName, int buffId)
    {
        return GetBuffGroup(buffId) switch
        {
            BuffGroup.Cuisine => ResolveText(_cuisineBuffNames, cultureName, buffId),
            BuffGroup.Potion => ResolveText(_potionBuffNames, cultureName, buffId),
            _ => ResolveText(_buffNames, cultureName, buffId)
        };
    }

    /// <summary>
    /// モンスター名。<b>表示中の言語で引く。</b>
    ///
    /// <para>
    /// 引数は <c>AttrId</c>(<c>MonsterTable</c> のキー＝種別ID)。エンティティ側が持っている
    /// 名前は起動時に英語で焼き付くので、言語切替に追従させるにはここを通す。
    /// </para>
    /// </summary>
    /// <summary>表示名が空でないか。内部ID注記は数えない(注記は表示設定で付くので、名前の有無の判定に使えない)。</summary>
    public static bool HasMonsterName(long monsterId)
    {
        return monsterId is > 0 and <= int.MaxValue
            && !string.IsNullOrEmpty(ResolveText(_monsterNames, Volatile.Read(ref _cultureName), (int)monsterId));
    }

    public static string GetMonsterName(long monsterId)
    {
        return monsterId is > 0 and <= int.MaxValue
            ? AppendInternalId(
                ResolveText(_monsterNames, Volatile.Read(ref _cultureName), (int)monsterId),
                InternalIdDisplayMode.EntityOnly,
                monsterId)
            : string.Empty;
    }

    /// <summary>
    /// エンティティリストの実体の名前。<b>表示中の言語で引く。</b>
    ///
    /// <para>
    /// 引数は実体の種類と <c>AttrId</c>(種別ID)。引く表は種類で決まる(いまはモンスターだけ)。
    /// 同じ番号が別の表で別のものを指すので、種類を取り違えると別の実体の名前になる。
    /// 表を持たない種類は空。
    /// </para>
    ///
    /// <para>
    /// <paramref name="unnamedLabel"/> は、表に行があって名前が空のときに代わりに出す文字列(「敵」「味方」)。
    /// <b>表に行が無い番号には当てない。</b> 表が古いことを、名前の無い雑魚と見分けられるように空欄のまま(内部ID注記だけ)にする。
    /// </para>
    /// </summary>
    public static string GetEntityName(Zproto.EEntityType entityType, long entityId, string? unnamedLabel = null)
    {
        if (GetEntityNameTable(entityType) is not { } table || entityId is not (> 0 and <= int.MaxValue))
        {
            return string.Empty;
        }

        var name = ResolveText(table.Names, Volatile.Read(ref _cultureName), (int)entityId);
        if (string.IsNullOrEmpty(name) && unnamedLabel is not null && table.Ids.Contains((int)entityId))
        {
            name = unnamedLabel;
        }

        return AppendInternalId(name, InternalIdDisplayMode.EntityOnly, entityId);
    }

    /// <summary>
    /// <see cref="GetEntityName"/> の表の名前を、内部ID注記と名前の無い実体の代わりの名前なしで返す(表示中の言語)。
    /// 名前が無ければ空。通知の名前の照合と文に使う。
    /// </summary>
    public static string GetEntityNameWithoutInternalId(Zproto.EEntityType entityType, long entityId)
    {
        return GetEntityNameTable(entityType) is { } table && entityId is > 0 and <= int.MaxValue
            ? ResolveText(table.Names, Volatile.Read(ref _cultureName), (int)entityId)
            : string.Empty;
    }

    /// <summary><see cref="GetEntityName"/> の名前(内部ID注記を除く)が空でないか。</summary>
    public static bool HasEntityName(Zproto.EEntityType entityType, long entityId)
    {
        return GetEntityNameTable(entityType) is { } table
            && entityId is > 0 and <= int.MaxValue
            && !string.IsNullOrEmpty(ResolveText(table.Names, Volatile.Read(ref _cultureName), (int)entityId));
    }

    /// <summary>
    /// 種類の表にその番号の行があるか。名前が空の行もある扱い。
    /// 「行があって名前が空」と「表に行が無い(表が古い)」を見分けるのに使う。
    /// </summary>
    public static bool HasEntityRow(Zproto.EEntityType entityType, long entityId)
    {
        return GetEntityNameTable(entityType) is { } table
            && entityId is > 0 and <= int.MaxValue
            && table.Ids.Contains((int)entityId);
    }

    /// <summary>
    /// そのモンスターがゲーム内でプレイヤーに見える HP バーを持つか。エンティティリストとタイムライン・被ダメログが同じ判定を使う。
    ///
    /// <para>
    /// <c>MonsterTable.HudShowParam</c> の3番目が HP バーを出すか。値が1つの形は全部の位置がその値。
    /// 先頭は HP バーではない(名前はあって HP バーの無い実体、画面に名前が無くて HP バーのある実体がいる)。
    /// 先頭は画面に名前を出すかと見ているが推定(表に名前があっても画面に出ない実体がいる)。
    /// </para>
    ///
    /// <para>
    /// 表に無い番号・<c>HudShowParam</c> が空か2つのときは判定できないので、持つ側に倒す。
    /// </para>
    /// </summary>
    public static bool HasMonsterHpBar(long monsterId)
    {
        if (monsterId is not (> 0 and <= int.MaxValue)
            || !HelperMethods.DataTables.Monsters.Data.TryGetValue(
                monsterId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                out var monster)
            || monster.HudShowParam is not { } hudShowParam)
        {
            return true;
        }

        return hudShowParam.Count switch
        {
            1 => hudShowParam[0] != 0,
            >= 3 => hudShowParam[2] != 0,
            _ => true
        };
    }

    /// <summary>
    /// 倒すとダンジョンの得点(空間調査)に入る点数(<c>MonsterTable.Score</c>)。表に無い番号は 0。
    /// 場所ごとの倍率と実体ごとの加算は掛けない(掛け方がサーバー側にしか無い)。
    /// </summary>
    public static int GetMonsterScore(long monsterId)
    {
        return monsterId is > 0 and <= int.MaxValue
            && HelperMethods.DataTables.Monsters.Data.TryGetValue(
                monsterId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                out var monster)
                ? monster.Score
                : 0;
    }

    /// <summary>目標の表の種類が得点の目標(「空間調査を完了してボス戦を開始する」など)か。表に無い番号は false。</summary>
    public static bool IsDungeonScoreTarget(int targetId)
    {
        return HelperMethods.DataTables.Targets.Data.TryGetValue(
                targetId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                out var target)
            && target.TargetType == (int)Zproto.ETargetType.TargetDungeonScore;
    }

    /// <summary>チャットの本文の <c>&lt;sprite=N&gt;</c> の N から、顔文字(絵文字の文字)を引く。並びの外の番号は null。</summary>
    public static string? GetChatEmoji(int number)
    {
        return number >= 0 && number < ChatEmojis.Length ? ChatEmojis[number] : null;
    }

    /// <summary>
    /// ゲームの用意した顔のアイコン(名刺照会の <c>avatarId</c>)の絵の全体のパス。プロフィールの絵の表の種類1(アイコン)の行の <c>Image</c>。
    /// 表に無い番号・種類1でない行は null。
    /// </summary>
    public static string? GetAvatarPresetImage(int avatarId)
    {
        return HelperMethods.DataTables.ProfileImages.Data.TryGetValue(
                avatarId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                out var image)
            && image.Type == AvatarProfileImageType
                ? image.Image
                : null;
    }

    /// <summary>プロフィールの絵の表で、顔のアイコンの行の種類。</summary>
    private const int AvatarProfileImageType = 1;

    /// <summary>チャットのスタンプの番号(configId)から、絵の名前を引く。定型文(絵なし)は空、表に無い番号は null。</summary>
    public static string? GetChatStickerResource(uint configId)
    {
        return HelperMethods.DataTables.ChatStickers.Data.TryGetValue(
                configId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                out var sticker)
            ? sticker.Res
            : null;
    }

    /// <summary>
    /// チャットのリンクの発言の文の型(表 <c>ChatHyperlink</c> の <c>Content</c>)。差し込みの値の <c>{*…*}</c> とゲームの書式のタグを含む。
    /// 表示中の言語で引き、表に無い番号と型が空の行は空。
    /// </summary>
    public static string GetChatHyperlinkTemplate(uint configId)
    {
        return configId <= int.MaxValue
            ? ResolveText(_chatHyperlinkTemplates, Volatile.Read(ref _cultureName), (int)configId)
            : string.Empty;
    }

    /// <summary>ギルドの建物の名前(<c>UnionBuildingTable.BuildingName</c>)。表示中の言語で引き、無ければ空。</summary>
    public static string GetUnionBuildingName(int buildingId)
    {
        return ResolveText(_unionBuildingNames, Volatile.Read(ref _cultureName), buildingId);
    }

    private static (FrozenDictionary<string, FrozenDictionary<int, string>> Names, FrozenSet<int> Ids)? GetEntityNameTable(
        Zproto.EEntityType entityType)
    {
        return entityType switch
        {
            Zproto.EEntityType.EntMonster => (_monsterNames, _monsterIds),
            _ => null
        };
    }

    /// <summary>
    /// シーン/ダンジョン名。<b>表示中の言語で引く。</b>
    ///
    /// <para>
    /// 引数は <c>LevelMapId</c> と、ダンジョン同期で届いた難易度(<c>EncounterExData.DungeonDifficulty</c>)。
    /// 履歴は名前ではなくIDと難易度を保持し、表示時にここで引き直す。
    /// </para>
    ///
    /// <para>
    /// 難易度名があれば <c>ダンジョン名-難易度名</c> にする。マスターの段階の名前(<c>番号:段階</c>)を先に引き、
    /// 無ければダンジョンごとの名前(<c>番号</c>)。マスターのダンジョンは後者を持たないので、
    /// 段階が分からなければ難易度名を付けない。
    /// </para>
    /// </summary>
    /// <summary>
    /// その職業が出す攻撃力の属性番号(物理 11330 か魔法 11340)。
    /// 表に行が無ければ 0。<b>直書きの分岐で持たない</b> — ゲームの表とずれる。
    /// </summary>
    public static int GetProfessionAttackAttrId(int professionId)
        => GetProfessionShownAttrId(professionId, static row => row.AttackShow);

    /// <summary>
    /// その職業が出す主ステータスの属性番号(筋力 11010 / 知力 11020 / 敏捷 11030)。
    /// 表に行が無ければ 0。
    /// </summary>
    public static int GetProfessionPrimaryStatAttrId(int professionId)
        => GetProfessionShownAttrId(professionId, static row => row.StrOrIntOrDexShow);

    /// <summary>
    /// 職業の役割(職業の表の役割のタグ。ゲームの呼び方は 攻撃 / 支援 / 防御)。職業が分からない・表に行が無ければ false。
    /// </summary>
    public static bool TryGetProfessionRole(int professionId, out DataTypes.Enums.Professions.ERoleType role)
    {
        if (professionId <= 0
            || !HelperMethods.DataTables.ProfessionSystems.Data.TryGetValue(professionId.ToString(), out var row))
        {
            role = default;
            return false;
        }

        role = ToTalentRole(row.Talent, $"ProfessionSystemTable[{professionId}]");
        return true;
    }

    /// <summary>
    /// 表の <c>[[種別, 番号], …]</c> から番号を1つ取る。形が想定と違えば 0。
    /// </summary>
    private static int GetProfessionShownAttrId(
        int professionId,
        Func<DataTypes.ProfessionSystem, List<List<int>>?> select)
    {
        if (professionId <= 0
            || !HelperMethods.DataTables.ProfessionSystems.Data.TryGetValue(
                professionId.ToString(), out var row))
        {
            return 0;
        }

        var pairs = select(row);
        if (pairs is null)
        {
            return 0;
        }

        foreach (var pair in pairs)
        {
            if (pair is { Count: >= 2 } && pair[1] > 0)
            {
                return pair[1];
            }
        }

        return 0;
    }

    public static string GetSceneName(long levelMapId, int dungeonDifficulty)
    {
        return levelMapId is > 0 and <= int.MaxValue
            ? AppendInternalId(GetSceneNameWithoutInternalId(levelMapId, dungeonDifficulty), InternalIdDisplayMode.MapOnly, levelMapId)
            : string.Empty;
    }

    /// <summary>
    /// 場所の名前(注記なし)。難易度名があれば「名前-難易度名」(マスターの段階の名前、無ければダンジョンごとの名前)。
    /// 通知の入場確認もこの名前を使う。
    /// </summary>
    public static string GetSceneNameWithoutInternalId(long levelMapId, int dungeonDifficulty)
    {
        if (levelMapId is not (> 0 and <= int.MaxValue))
        {
            return string.Empty;
        }

        var cultureName = Volatile.Read(ref _cultureName);
        var name = ResolveText(_sceneNames, cultureName, (int)levelMapId);
        var typeName = dungeonDifficulty > 0
            ? ResolveText(_dungeonTypeNames, cultureName, MakeDungeonTypeKey((int)levelMapId, dungeonDifficulty))
            : string.Empty;
        if (string.IsNullOrEmpty(typeName))
        {
            typeName = ResolveText(_dungeonTypeNames, cultureName, MakeDungeonTypeKey((int)levelMapId, 0));
        }

        if (!string.IsNullOrEmpty(typeName))
        {
            name = $"{name}{DungeonTypeNameSeparator}{typeName}";
        }

        return name;
    }

    /// <summary>難易度名の鍵。段階 0 がダンジョンごとの名前、正の段階がマスターの段階の名前。</summary>
    private static long MakeDungeonTypeKey(int dungeonId, int difficulty)
        => ((long)dungeonId << 32) | (uint)difficulty;

    public static string GetSkillIconName(int skillId, string? fallbackIcon = null)
    {
        if (_skills.TryGetValue(skillId, out var skill))
        {
            return FirstNonEmpty(skill.Icon, fallbackIcon);
        }

        return fallbackIcon?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// バフの絵。表の <c>ShowHUDIcon</c>、空なら <c>Icon</c>(どちらも手修正 <c>BuffOverrides.json</c> を重ねた値)。
    /// 無ければ空で、代わりの絵は出さない(バフ/デバフリストではゲームの条件で出なくなる)。
    /// </summary>
    public static string GetBuffOwnIconName(int buffId)
    {
        if (_buffs.TryGetValue(buffId, out var buff))
        {
            return FirstNonEmpty(buff.ShowHUDIcon, buff.Icon);
        }

        return string.Empty;
    }

    /// <summary>
    /// バフの表の絵(<c>Icon</c>)。HUD 用の絵は見ない(装備の TIPS の効果の行はこの絵を使う)。無ければ空。
    /// </summary>
    public static string GetBuffTableIconName(int buffId)
    {
        return _buffs.TryGetValue(buffId, out var buff)
            ? buff.Icon?.Trim() ?? string.Empty
            : string.Empty;
    }

    /// <summary>一時属性の表の絵(<c>AttrIcon</c>)。無ければ空。</summary>
    public static string GetTempAttrIconName(int tempAttrId)
    {
        return HelperMethods.DataTables.TempAttrs.Data.TryGetValue(
                tempAttrId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                out var tempAttr)
            ? tempAttr.AttrIcon?.Trim() ?? string.Empty
            : string.Empty;
    }

    /// <summary>
    /// 個別に扱わず1つのまとまりとして見るバフか。<c>Data/Localization/CuisineBuffs.json</c> にあれば料理、
    /// <c>Data/Localization/PotionBuffs.json</c> にあれば薬剤。選ぶ条件は <c>DataTools/Core/gen_buff_groups.py</c> が持つ。
    /// </summary>
    public static BuffGroup GetBuffGroup(int buffId)
    {
        if (_cuisineBuffIds.Contains(buffId))
        {
            return BuffGroup.Cuisine;
        }

        return _potionBuffIds.Contains(buffId)
            ? BuffGroup.Potion
            : BuffGroup.None;
    }

    /// <summary>
    /// 料理・薬剤のまとまりの絵。まとまりのバフはどれも同じ絵を使うので、代表(<see cref="TryGetBuffGroupRepresentativeId"/>)の絵。
    /// まとまりにバフが無ければ空。
    /// </summary>
    public static string GetBuffGroupIconName(BuffGroup group)
    {
        return TryGetBuffGroupRepresentativeId(group, out var buffId)
            ? GetBuffOwnIconName(buffId)
            : string.Empty;
    }

    /// <summary>
    /// バフがバフリストとデバフリストのどちらに出るか(<see cref="BuffDisplayRules.GetListKind"/>、表の種別は手修正込み)。
    /// 一覧の振り分けと同じ決め方。どちらにも出ない種別と表に無い番号は null。
    /// </summary>
    public static PlayerBuffListKind? GetBuffListKind(int buffId)
    {
        return _buffs.TryGetValue(buffId, out var buff)
            ? BuffDisplayRules.GetListKind(buff.BuffType)
            : null;
    }

    /// <summary>
    /// 料理・薬剤のまとまりがバフリストとデバフリストのどちらに出るか。まとまりのバフはどれも同じ種別なので、代表の種別。
    /// まとまりにバフが無ければ null。
    /// </summary>
    public static PlayerBuffListKind? GetBuffGroupListKind(BuffGroup group)
    {
        return TryGetBuffGroupRepresentativeId(group, out var buffId)
            ? GetBuffListKind(buffId)
            : null;
    }

    /// <summary>まとまりの代表のバフ(番号の一番小さいバフ)。まとまりにバフが無ければ偽。</summary>
    private static bool TryGetBuffGroupRepresentativeId(BuffGroup group, out int buffId)
    {
        var buffIds = group switch
        {
            BuffGroup.Cuisine => _cuisineBuffIds,
            BuffGroup.Potion => _potionBuffIds,
            _ => FrozenSet<int>.Empty
        };

        buffId = buffIds.Count == 0 ? 0 : buffIds.Min();
        return buffIds.Count > 0;
    }

    public static string GetBuffIconName(int buffId, int sourceSkillId, string? fallbackIcon = null)
    {
        var buffIcon = GetBuffOwnIconName(buffId);
        if (!string.IsNullOrEmpty(buffIcon))
        {
            return buffIcon;
        }

        if (sourceSkillId > 0
            && _skills.TryGetValue(sourceSkillId, out var skill)
            && !string.IsNullOrWhiteSpace(skill.Icon))
        {
            return skill.Icon;
        }

        return fallbackIcon?.Trim() ?? string.Empty;
    }

    public static bool IsSkillImagine(int skillId, string? fallbackIcon = null)
    {
        if (_skills.TryGetValue(skillId, out var skill))
        {
            return skill.IsImagineSlot();
        }

        return fallbackIcon?.Contains("skill_aoyi", StringComparison.OrdinalIgnoreCase) == true;
    }

    public static bool IsSkillRole(int skillId)
    {
        return _skills.TryGetValue(skillId, out var skill)
            && skill.IsRoleSlot();
    }

    public static bool HasLevelDependentCooldown(int skillId)
    {
        return _skillCooldownsByLevel.TryGetValue(skillId, out var cooldownsByLevel)
            && cooldownsByLevel.Count > 1
            && cooldownsByLevel.Values.Distinct().Skip(1).Any();
    }

    /// <summary>
    /// 技のアイコンの背景の枠(<see cref="BuildSkillIconFrames"/>)。表に無い技は <see cref="SkillIconFrame.Standard"/>。
    /// </summary>
    public static SkillIconFrame GetSkillIconFrame(int skillId)
    {
        return _skillIconFrames.TryGetValue(skillId, out var frame)
            ? frame
            : SkillIconFrame.Standard;
    }

    /// <summary>イマジンの絵の置き場所(技の表の <c>Icon</c> の全体のパスの先頭)。</summary>
    private const string ImagineArtIconFolder = "ui/textures/skill_aoyi/";

    /// <summary>
    /// アイコンがイマジンの絵か(イマジンの絵は丸い絵で、ほかの技の絵と大きさと位置の調整が違う)。
    /// 引数は技の表の <c>Icon</c> の全体のパス(<see cref="GetSkillIconName"/> の値)。ファイル名だけでは判定できない。
    /// 見た目の調整のための判定で、イマジンの技か(<see cref="IsSkillImagine"/>)とは別。
    /// </summary>
    public static bool IsImagineArtIcon(string? iconName)
    {
        return iconName?.StartsWith(ImagineArtIconFolder, StringComparison.OrdinalIgnoreCase) == true;
    }

    public static int GetSkillMaxCharges(int skillId)
    {
        return _skills.TryGetValue(skillId, out var skill)
            && skill.MaxEnergyChargeNum > 1
                ? skill.MaxEnergyChargeNum
                : 0;
    }

    public static double GetSkillChargeCooldownSeconds(int skillId, int tier)
    {
        if (!_skills.TryGetValue(skillId, out var skill)
            || skill.MaxEnergyChargeNum <= 1
            || skill.EnergyChargeTime <= 0)
        {
            return 0;
        }

        return ApplyImagineTierCooldownReduction(
            skill,
            skill.EnergyChargeTime / 1000d,
            tier);
    }

    public static double GetSkillPveCooldownSeconds(int skillId, int currentLevel, int tier)
    {
        if (!_skills.TryGetValue(skillId, out var skill))
        {
            return 0;
        }

        return ApplyImagineTierCooldownReduction(
            skill,
            ResolveSkillPveCooldownSeconds(skill, currentLevel),
            tier);
    }

    private static double ApplyImagineTierCooldownReduction(
        Skill skill,
        double cooldownSeconds,
        int tier)
    {
        if (cooldownSeconds <= 60 || !skill.IsImagineSlot())
        {
            return cooldownSeconds;
        }

        return tier switch
        {
            >= 3 and <= 4 => Math.Ceiling(cooldownSeconds * 0.8333d),
            >= 5 and <= 6 => Math.Ceiling(cooldownSeconds * 0.6666d),
            _ => cooldownSeconds
        };
    }

    private static FrozenDictionary<int, FrozenDictionary<int, float>> LoadSkillCooldowns()
    {
        var cooldownsBySkill = new Dictionary<int, Dictionary<int, float>>();

        foreach (var skillFightLevel in HelperMethods.DataTables.SkillFightLevels.Data.Values)
        {
            if (skillFightLevel.SkillId <= 0 || skillFightLevel.Level <= 0)
            {
                continue;
            }

            if (!cooldownsBySkill.TryGetValue(skillFightLevel.SkillId, out var cooldownsByLevel))
            {
                cooldownsByLevel = new Dictionary<int, float>();
                cooldownsBySkill.Add(skillFightLevel.SkillId, cooldownsByLevel);
            }

            cooldownsByLevel.TryAdd(skillFightLevel.Level, skillFightLevel.PVECoolTime);
        }

        return cooldownsBySkill
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToFrozenDictionary())
            .ToFrozenDictionary();
    }

    /// <summary>
    /// 技の表の全部の技のアイコンの背景の枠(<see cref="_skillIconFrames"/>)。<b>技が属する、枠の番号を持つ技の番号で決める。</b>
    /// 判定はプレイヤーリストのイマジンの枠と同じ形(<c>Skill.IsImagineSlot</c>)にそろえる。
    /// <list type="bullet">
    ///   <item>
    ///     番号は技の表の <c>SlotPositionId</c>(0 以外)。変身クラスの技は持たないので、職業の表の技の4列(通常攻撃・特殊攻撃・マスタリー・究極)を番号の代わりにする。
    ///     通常攻撃・特殊攻撃・マスタリーの列は、たどるのを止める所として要る(止めないと、手修正で寄せた変身中の技がイマジンまでたどって <see cref="SkillIconFrame.Imagine"/> になる)
    ///   </item>
    ///   <item>
    ///     番号を持たない技(通常攻撃の2段目以降・特化で置き換わった技など)は、番号を持つ技に着くまでたどる。
    ///     <c>SkillLevelGroup</c> の根、それが無ければ自分を切り替え先(<c>SwitchSkillId</c>)か空中版(<c>SkySkillId</c>)に持つ技(番号の小さい順)。
    ///     たどった技を控えて輪を止める。<c>SkillLevelGroup</c> は手修正(<c>SkillOverrides.json</c>)を重ねた値
    ///   </item>
    ///   <item>
    ///     着いた技が 7・8 → <see cref="SkillIconFrame.Imagine"/>、6(職業の表の究極)→ <see cref="SkillIconFrame.Ultimate"/>。
    ///     ほかの番号と、着かなかった技は <see cref="SkillIconFrame.Standard"/>
    ///   </item>
    /// </list>
    /// </summary>
    private static FrozenDictionary<int, SkillIconFrame> BuildSkillIconFrames()
    {
        var professionClassSkillIds = new HashSet<int>();
        var professionUltimateSkillIds = new HashSet<int>();
        foreach (var profession in HelperMethods.DataTables.ProfessionSystems.Data.Values)
        {
            professionClassSkillIds.UnionWith(profession.NormalAttackSkill ?? []);
            professionClassSkillIds.UnionWith(profession.SpecialSkill ?? []);
            professionClassSkillIds.UnionWith(profession.NormalSkill ?? []);
            professionUltimateSkillIds.UnionWith(profession.UltimateSkill ?? []);
        }

        // 技 → 自分を切り替え先・空中版に持つ技(番号の小さい順)。
        var linkParents = new Dictionary<int, List<int>>();
        foreach (var skill in _skills.Values)
        {
            AddLinkParent(skill.SwitchSkillId, skill.Id);
            AddLinkParent(skill.SkySkillId, skill.Id);
        }

        foreach (var parents in linkParents.Values)
        {
            parents.Sort();
        }

        var frames = new Dictionary<int, SkillIconFrame>(_skills.Count);
        foreach (var skillId in _skills.Keys)
        {
            frames[skillId] = ResolveFrame(skillId);
        }

        return frames.ToFrozenDictionary();

        void AddLinkParent(int linkedSkillId, int parentSkillId)
        {
            if (linkedSkillId <= 0 || linkedSkillId == parentSkillId)
            {
                return;
            }

            if (!linkParents.TryGetValue(linkedSkillId, out var parents))
            {
                parents = [];
                linkParents.Add(linkedSkillId, parents);
            }

            parents.Add(parentSkillId);
        }

        bool HasSlotNumber(Skill skill)
        {
            return skill.HasSlotPosition()
                || professionClassSkillIds.Contains(skill.Id)
                || professionUltimateSkillIds.Contains(skill.Id);
        }

        SkillIconFrame ResolveFrame(int skillId)
        {
            var visited = new HashSet<int>();
            var currentId = skillId;
            while (visited.Add(currentId) && _skills.TryGetValue(currentId, out var current))
            {
                if (HasSlotNumber(current))
                {
                    return ToFrame(current);
                }

                if (current.SkillLevelGroup > 0
                    && current.SkillLevelGroup != currentId
                    && _skills.ContainsKey(current.SkillLevelGroup))
                {
                    currentId = current.SkillLevelGroup;
                    continue;
                }

                if (!linkParents.TryGetValue(currentId, out var parents))
                {
                    break;
                }

                currentId = parents[0];
            }

            return SkillIconFrame.Standard;
        }

        SkillIconFrame ToFrame(Skill skill)
        {
            if (skill.IsImagineSlot())
            {
                return SkillIconFrame.Imagine;
            }

            if (skill.IsUltimateSlot()
                || professionUltimateSkillIds.Contains(skill.Id))
            {
                return SkillIconFrame.Ultimate;
            }

            return SkillIconFrame.Standard;
        }
    }

    private static float ResolveSkillPveCooldownSeconds(Skill skill, int currentLevel)
    {
        if (_skillCooldownsByLevel.TryGetValue(skill.Id, out var cooldownsByLevel)
            && cooldownsByLevel.Count > 0)
        {
            var normalizedLevel = Math.Max(currentLevel, 1);
            if (cooldownsByLevel.TryGetValue(normalizedLevel, out var exactCooldown))
            {
                return exactCooldown;
            }

            var lowerLevel = cooldownsByLevel.Keys
                .Where(level => level <= normalizedLevel)
                .DefaultIfEmpty(0)
                .Max();
            if (lowerLevel > 0)
            {
                return cooldownsByLevel[lowerLevel];
            }

            return cooldownsByLevel.OrderBy(pair => pair.Key).First().Value;
        }

        if (skill.EffectIDs is { Count: > 0 }
            && HelperMethods.DataTables.SkillFightLevels.Data.TryGetValue(
                skill.EffectIDs[0].ToString(),
                out var firstSkillFightLevel))
        {
            return firstSkillFightLevel.PVECoolTime;
        }

        return 0;
    }

    private static FrozenDictionary<int, T> LoadNumericCatalog<T>(IReadOnlyDictionary<string, T> source)
    {
        var result = new Dictionary<int, T>();
        foreach (var pair in source)
        {
            if (int.TryParse(pair.Key, out var id))
            {
                result[id] = pair.Value;
            }
        }

        return result.ToFrozenDictionary();
    }

    private static FrozenDictionary<int, int> BuildSkillIdByEffectId(FrozenDictionary<int, Skill> skills)
    {
        var owners = new Dictionary<int, HashSet<int>>();
        foreach (var (skillId, skill) in skills)
        {
            foreach (var effectId in skill.EffectIDs ?? [])
            {
                if (!owners.TryGetValue(effectId, out var skillIds))
                {
                    skillIds = [];
                    owners[effectId] = skillIds;
                }

                skillIds.Add(skillId);
            }
        }

        return owners
            .Where(pair => pair.Value.Count == 1)
            .ToFrozenDictionary(pair => pair.Key, pair => pair.Value.First());
    }

    private static FrozenDictionary<int, FrozenSet<int>> BuildMonsterIdsBySkillId()
    {
        var result = new Dictionary<int, HashSet<int>>();
        foreach (var (key, monster) in HelperMethods.DataTables.Monsters.Data)
        {
            if (!int.TryParse(key, out var monsterId))
            {
                continue;
            }

            foreach (var skillId in monster.SkillIds ?? [])
            {
                if (!result.TryGetValue(skillId, out var monsterIds))
                {
                    monsterIds = [];
                    result[skillId] = monsterIds;
                }

                monsterIds.Add(monsterId);
            }
        }

        return result.ToFrozenDictionary(pair => pair.Key, pair => pair.Value.ToFrozenSet());
    }

    /// <summary>効果の並び(<c>[種類, 番号, …]</c>)のうち、種類 3(バフを付ける)の番号。</summary>
    private const int GrantBuffEffectKind = 3;

    private static FrozenDictionary<int, int> BuildImagineSkillIdByPassiveRoot()
    {
        var targets = new Dictionary<int, HashSet<int>>();
        foreach (var aoyi in HelperMethods.DataTables.SkillAoyis.Data.Values)
        {
            foreach (var effect in aoyi.TransformationType ?? [])
            {
                AddGrantedBuffTarget(targets, effect, aoyi.Id);
            }
        }

        foreach (var star in HelperMethods.DataTables.SkillAoyiStars.Data.Values)
        {
            foreach (var effect in star.TransformationType ?? [])
            {
                AddGrantedBuffTarget(targets, effect, star.SkillId);
            }
        }

        return ToUniqueTargets(targets, "SkillAoyiTable / SkillAoyiStarTable", "imagine skill");
    }

    private static void AddGrantedBuffTarget(Dictionary<int, HashSet<int>> targets, List<int>? effect, int target)
    {
        if (effect is not { Count: >= 2 } || effect[0] != GrantBuffEffectKind || effect[1] <= 0 || target <= 0)
        {
            return;
        }

        if (!targets.TryGetValue(effect[1], out var set))
        {
            set = [];
            targets[effect[1]] = set;
        }

        set.Add(target);
    }

    /// <summary>根のバフ → 行き先。行き先が2つ以上に割れた根は入れず、エラーログを出す(そのバフの由来は名前を出さない)。</summary>
    private static FrozenDictionary<int, int> ToUniqueTargets(Dictionary<int, HashSet<int>> targets, string tableName, string targetKind)
    {
        foreach (var (buffId, set) in targets.Where(pair => pair.Value.Count > 1))
        {
            Log.Error("{TableName}: root buff {BuffId} points to more than one {TargetKind} ({Targets}); its origin name is not shown",
                tableName, buffId, targetKind, string.Join(", ", set.Order()));
        }

        return targets
            .Where(pair => pair.Value.Count == 1)
            .ToFrozenDictionary(pair => pair.Key, pair => pair.Value.First());
    }

    /// <summary>形は技レベルIDの配列。ファイルが無ければ空(警告の技は0件)。</summary>
    private static FrozenSet<int> LoadWarningSkillLevels(JsonDataFileReads reads)
    {
        var path = SkillWarningsPath;
        if (!reads.TryTake<int[]>(path, out var loadedSkillLevelIds))
        {
            return FrozenSet<int>.Empty;
        }

        var skillLevelIds = loadedSkillLevelIds
            ?? throw new InvalidDataException($"{path} is empty.");
        return skillLevelIds.ToFrozenSet();
    }

    /// <summary>
    /// 予告の表の番号と既定の秒数。ファイルが無ければエラーを出して空(予告のバーの終わりが決まらないので、予兆技の予告は通知されない)。
    /// </summary>
    private static FrozenDictionary<int, int> LoadDbmCountCdTimes(JsonDataFileReads reads)
    {
        var path = DbmTablePath;
        if (!reads.TryTake<Dictionary<string, DbmTableRow>>(path, out var loadedRows))
        {
            Log.Error("DbmTable.json is missing. Boss announcement bars cannot be timed path={Path}", path);
            return FrozenDictionary<int, int>.Empty;
        }

        var rows = loadedRows
            ?? throw new InvalidDataException($"{path} is empty.");
        return rows.Values.ToFrozenDictionary(row => row.Id, row => row.CountCDTime);
    }

    private sealed class DbmTableRow
    {
        public int Id { get; set; }

        public int CountCDTime { get; set; }
    }

    /// <summary>装備の表の2つ組の鍵(属性庫の型と庫ID、効果の種類と番号)。詰め方は <see cref="MakeSourceKey"/> と同じ。</summary>
    private static long MakeEquipmentKey(int first, int second)
        => ((long)first << 32) | (uint)second;

    /// <summary>
    /// 装備の定義 <c>Data/Generated/Equips.json</c> を読む。<c>DataTools/Core/gen_equips.py</c> が生成する。
    ///
    /// <para>
    /// ファイルが無い・形が合わないときはエラーログを出して全部空のまま続ける(装備詳細は定義を出せない)。
    /// 読めたところまでで続けることはしない。
    /// </para>
    /// </summary>
    private static void LoadEquipments(JsonDataFileReads reads)
    {
        const string relativePath = "Generated/Equips.json";
        var path = EquipmentsPath;

        FrozenDictionary<int, EquipmentDefinition> equipments;
        FrozenDictionary<long, IReadOnlyList<EquipmentAttrLibRow>> attrLibs;
        FrozenDictionary<long, EquipmentAttrLibRow> attrRows;
        FrozenDictionary<long, int> strengthSeasons;
        FrozenDictionary<int, int> specSchools;
        FrozenDictionary<int, int> rank1Schools;
        try
        {
            // 解けなかったときの例外は受け取るときに出るので、受け取りもこの try の中。
            if (!reads.TryTake<EquipmentFile>(path, out var loadedFile))
            {
                Log.Error("{FileName} is missing. Equipment details cannot be shown path={Path}", relativePath, path);
                return;
            }

            var file = loadedFile
                ?? throw new InvalidDataException("the file is empty");
            equipments = ReadEquipmentDefinitions(file.Equips);
            attrLibs = ReadEquipmentAttrLibs(file.AttrLibs, out attrRows);
            strengthSeasons = ReadEquipmentStrengthSeasons(file.StrengthSeasons);
            specSchools = ReadEquipmentSchools(file.SpecSchools, "SpecSchools");
            rank1Schools = ReadEquipmentSchools(file.Rank1Schools, "Rank1Schools");
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Log.Error(ex, "{FileName} could not be read. Equipment details cannot be shown path={Path}", relativePath, path);
            return;
        }

        _equipments = equipments;
        _equipmentAttrLibs = attrLibs;
        _equipmentAttrRows = attrRows;
        _equipmentStrengthSeasons = strengthSeasons;
        _equipmentSpecSchools = specSchools;
        _equipmentRank1Schools = rank1Schools;
        Log.Information(
            "Loaded {FileName}: {Equips} equips / {AttrLibs} attribute libraries / {StrengthSeasons} strength seasons / {SpecSchools} spec schools / {Rank1Schools} rank-1 schools",
            relativePath, equipments.Count, attrLibs.Count, strengthSeasons.Count, specSchools.Count, rank1Schools.Count);
    }

    /// <summary>
    /// 根のバフの表 <c>Data/Generated/RootBuffs.json</c> を読む。<c>DataTools/Core/gen_root_buffs.py</c> が生成する。
    ///
    /// <para>
    /// ファイルが無い・形が合わないときはエラーログを出して全部空のまま続ける(アビリティ情報・潜在心相晶情報・モジュール情報は何も出さない)。
    /// 読めたところまでで続けることはしない。
    /// </para>
    /// </summary>
    private static void LoadRootBuffs(JsonDataFileReads reads)
    {
        const string relativePath = "Generated/RootBuffs.json";
        var path = RootBuffsPath;

        FrozenDictionary<int, RootBuffDefinition> rootBuffs;
        FrozenDictionary<int, ModuleEffectDefinition> moduleEffects;
        FrozenDictionary<int, int> modEffectIdByRootBuff;
        FrozenDictionary<int, FactorItemDefinition> asiaFactorItems;
        FrozenDictionary<int, FactorItemDefinition> chinaFactorItems;
        FrozenDictionary<int, int> advancedEffectIds;
        FrozenDictionary<(int ProfessionId, int SubProfessionId), TalentTreeDefinition> asiaTalentTrees;
        FrozenDictionary<(int ProfessionId, int SubProfessionId), TalentTreeDefinition> chinaTalentTrees;
        FrozenDictionary<int, int> replaceableSkillOriginals;
        try
        {
            // 解けなかったときの例外は受け取るときに出るので、受け取りもこの try の中。
            if (!reads.TryTake<RootBuffFile>(path, out var loadedFile))
            {
                Log.Error("{FileName} is missing. Talent, psychoscope and module info cannot be shown path={Path}", relativePath, path);
                return;
            }

            var file = loadedFile
                ?? throw new InvalidDataException("the file is empty");
            var definitions = new Dictionary<int, RootBuffDefinition>();
            AddRootBuffs(definitions, RootBuffKind.Talent, file.Talents, nameof(RootBuffFile.Talents));
            AddRootBuffs(definitions, RootBuffKind.SeasonTalentNode, file.SeasonTalentNodes, nameof(RootBuffFile.SeasonTalentNodes));
            AddRootBuffs(definitions, RootBuffKind.SeasonTalentAdvanced, file.SeasonTalentAdvanced, nameof(RootBuffFile.SeasonTalentAdvanced));
            AddRootBuffs(definitions, RootBuffKind.Factor, file.Factors, nameof(RootBuffFile.Factors));
            moduleEffects = ReadModuleEffects(definitions, file.ModuleEffects, nameof(RootBuffFile.ModuleEffects));
            modEffectIdByRootBuff = moduleEffects.Values
                .Where(definition => definition.RootBuffId > 0)
                .ToFrozenDictionary(definition => definition.RootBuffId, definition => definition.EffectId);
            asiaFactorItems = ReadFactorItems(file.FactorItems.Asia, $"{nameof(RootBuffFile.FactorItems)}.StarASIA");
            chinaFactorItems = ReadFactorItems(file.FactorItems.China, $"{nameof(RootBuffFile.FactorItems)}.Star");
            rootBuffs = definitions.ToFrozenDictionary();
            advancedEffectIds = file.SeasonTalentAdvancedEffects.ToFrozenDictionary(
                pair => ParseRootBuffKey(pair.Key, nameof(RootBuffFile.SeasonTalentAdvancedEffects)),
                pair => pair.Value);
            asiaTalentTrees = ReadTalentTrees(file.TalentTrees.Asia, rootBuffs, $"{nameof(RootBuffFile.TalentTrees)}.StarASIA");
            chinaTalentTrees = ReadTalentTrees(file.TalentTrees.China, rootBuffs, $"{nameof(RootBuffFile.TalentTrees)}.Star");
            replaceableSkillOriginals = BuildReplaceableSkillOriginals(asiaTalentTrees.Values.Concat(chinaTalentTrees.Values));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Log.Error(ex, "{FileName} could not be read. Talent, psychoscope and module info cannot be shown path={Path}", relativePath, path);
            return;
        }

        _rootBuffs = rootBuffs;
        _moduleEffects = moduleEffects;
        _modEffectIdByRootBuff = modEffectIdByRootBuff;
        _asiaFactorItems = asiaFactorItems;
        _chinaFactorItems = chinaFactorItems;
        _seasonTalentAdvancedEffectIds = advancedEffectIds;
        _asiaTalentTrees = asiaTalentTrees;
        _chinaTalentTrees = chinaTalentTrees;
        _replaceableSkillOriginals = replaceableSkillOriginals;
        Log.Information(
            "Loaded {FileName}: {RootBuffs} root buffs / {ModuleEffects} module effects / {AsiaFactorItems} Asia and {ChinaFactorItems} China factor items / {AdvancedEffects} psychoscope types with advanced effects / {AsiaTalentTrees} Asia and {ChinaTalentTrees} China talent trees",
            relativePath, rootBuffs.Count, moduleEffects.Count, asiaFactorItems.Count, chinaFactorItems.Count, advancedEffectIds.Count, asiaTalentTrees.Count, chinaTalentTrees.Count);
    }

    /// <summary>
    /// タレントの木を読む。線は節のつなぎ(<c>Next</c>)から作る。節のバフはタレントの根のバフで、バフのある節はアビリティの節でなければならず、
    /// つなぎの先は同じ木の節、クラスR2 の木(特化の番号が 0 でない)だけが根の節を持ち、役割のタグは 1〜3、技の置き換えは2つの正の技の番号。
    /// 合わなければ形の誤りとして止める。
    /// </summary>
    private static FrozenDictionary<(int ProfessionId, int SubProfessionId), TalentTreeDefinition> ReadTalentTrees(
        List<TalentTreeEntry?> entries,
        FrozenDictionary<int, RootBuffDefinition> rootBuffs,
        string listPath)
    {
        var trees = new Dictionary<(int ProfessionId, int SubProfessionId), TalentTreeDefinition>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            var itemPath = $"{listPath}[{index}]";
            var entry = entries[index] ?? throw new InvalidDataException($"{itemPath} is null");
            if (entry.Profession <= 0 || entry.Spec < 0 || (entry.Spec == 0) != (entry.Root == 0))
            {
                throw new InvalidDataException(
                    $"{itemPath} has an invalid profession {entry.Profession} / spec {entry.Spec} / root {entry.Root}");
            }

            if (entry.Nodes.Count == 0)
            {
                throw new InvalidDataException($"{itemPath} has no nodes");
            }

            var nodeEntries = new List<TalentTreeNodeEntry>(entry.Nodes.Count);
            var nodeEntriesById = new Dictionary<int, TalentTreeNodeEntry>(entry.Nodes.Count);
            for (var nodeIndex = 0; nodeIndex < entry.Nodes.Count; nodeIndex++)
            {
                var node = entry.Nodes[nodeIndex] ?? throw new InvalidDataException($"{itemPath}.Nodes[{nodeIndex}] is null");
                if (!nodeEntriesById.TryAdd(node.Id, node))
                {
                    throw new InvalidDataException($"{itemPath} lists node {node.Id} twice");
                }

                nodeEntries.Add(node);
            }

            if (entry.Root != 0 && !nodeEntriesById.ContainsKey(entry.Root))
            {
                throw new InvalidDataException($"{itemPath} root {entry.Root} is not one of its nodes");
            }

            var nodes = new List<TalentTreeNode>(nodeEntries.Count);
            var lines = new List<TalentTreeLine>();
            foreach (var node in nodeEntries)
            {
                foreach (var buffId in node.Buffs)
                {
                    if (!rootBuffs.TryGetValue(buffId, out var definition) || definition.Kind != RootBuffKind.Talent)
                    {
                        throw new InvalidDataException($"{itemPath} node {node.Id} has buff {buffId} that is not a talent root buff");
                    }
                }

                if (node.Buffs.Count > 0 && !node.Ability)
                {
                    throw new InvalidDataException($"{itemPath} node {node.Id} has buffs but is not an ability node");
                }

                var replacements = new List<TalentSkillReplacement>(node.Replace.Count);
                foreach (var pair in node.Replace)
                {
                    if (pair is not [> 0 and var originalSkillId, > 0 and var replacedSkillId])
                    {
                        throw new InvalidDataException($"{itemPath} node {node.Id} has a skill replacement that is not two positive skill ids");
                    }

                    replacements.Add(new TalentSkillReplacement(originalSkillId, replacedSkillId));
                }

                nodes.Add(new TalentTreeNode(
                    node.Id,
                    node.X,
                    node.Y,
                    [.. node.Buffs],
                    node.Id == entry.Root,
                    node.Talent,
                    node.Ability,
                    node.Icon,
                    ToTalentRole(node.TalentTag, $"{itemPath} node {node.Id}"),
                    replacements));
                foreach (var nextId in node.Next)
                {
                    if (!nodeEntriesById.TryGetValue(nextId, out var next))
                    {
                        throw new InvalidDataException($"{itemPath} node {node.Id} links to {nextId} outside the tree");
                    }

                    lines.Add(new TalentTreeLine(node.X, node.Y, next.X, next.Y));
                }
            }

            var tree = new TalentTreeDefinition(
                entry.Profession,
                entry.Spec,
                nodes.Max(node => node.X),
                nodes.Max(node => node.Y),
                nodes,
                lines);
            if (!trees.TryAdd((entry.Profession, entry.Spec), tree))
            {
                throw new InvalidDataException($"{itemPath} repeats profession {entry.Profession} / spec {entry.Spec}");
            }
        }

        return trees.ToFrozenDictionary();
    }

    /// <summary>
    /// バフを付けないアビリティの節の技の置き換えに出てくる技 → 元の技。1つの技が2つの元の技に当たったら形の誤りとして止める。
    /// バフを付ける節は根のバフで取っているかが分かるので入れない(ふだん使う技を数えないため)。
    /// </summary>
    private static FrozenDictionary<int, int> BuildReplaceableSkillOriginals(IEnumerable<TalentTreeDefinition> trees)
    {
        var originals = new Dictionary<int, int>();
        foreach (var tree in trees)
        {
            foreach (var node in tree.Nodes)
            {
                if (!node.IsAbility || node.Buffs.Count > 0)
                {
                    continue;
                }

                foreach (var replacement in node.SkillReplacements)
                {
                    AddReplaceableSkill(originals, replacement.OriginalSkillId, replacement.OriginalSkillId);
                    AddReplaceableSkill(originals, replacement.ReplacedSkillId, replacement.OriginalSkillId);
                }
            }
        }

        return originals.ToFrozenDictionary();
    }

    private static void AddReplaceableSkill(Dictionary<int, int> originals, int skillId, int originalSkillId)
    {
        if (originals.TryGetValue(skillId, out var known) && known != originalSkillId)
        {
            throw new InvalidDataException($"Skill {skillId} is replaced from two skills ({known} and {originalSkillId})");
        }

        originals[skillId] = originalSkillId;
    }

    /// <summary>種類の配列を、並び(配列の順)を付けて表へ足す。1つのバフが2つの種類に当たったら形の誤りとして止める。</summary>
    private static void AddRootBuffs(
        Dictionary<int, RootBuffDefinition> definitions,
        RootBuffKind kind,
        List<RootBuffEntry?> entries,
        string itemPath)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index] ?? throw new InvalidDataException($"{itemPath}[{index}] is null");
            if (entry.Buff <= 0)
            {
                throw new InvalidDataException($"{itemPath}[{index}].Buff is not positive");
            }

            var advancedIcons = entry.Icons is null
                ? FrozenDictionary<int, string>.Empty
                : entry.Icons.ToFrozenDictionary(
                    pair => ParseRootBuffKey(pair.Key, $"{itemPath}[{index}].Icons"),
                    pair => pair.Value ?? string.Empty);
            if (!definitions.TryAdd(entry.Buff, new RootBuffDefinition(kind, index, entry.Icon ?? string.Empty, advancedIcons)))
            {
                throw new InvalidDataException($"{itemPath}[{index}].Buff {entry.Buff} is listed in two kinds");
            }
        }
    }

    /// <summary>
    /// モジュール効果を読む。根のバフのある効果は、根のバフ(種類はモジュール効果、並びは配列の順)として表へも足す。
    /// 効果の番号は正で重ならず、根のバフは 0 か正、段階ごとのリンク数は段階1〜6 の6つで、正で段階とともに増える。合わなければ形の誤りとして止める。
    /// </summary>
    private static FrozenDictionary<int, ModuleEffectDefinition> ReadModuleEffects(
        Dictionary<int, RootBuffDefinition> definitions,
        List<ModuleEffectEntry?> entries,
        string itemPath)
    {
        var result = new Dictionary<int, ModuleEffectDefinition>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            var entryPath = $"{itemPath}[{index}]";
            var entry = entries[index] ?? throw new InvalidDataException($"{entryPath} is null");
            if (entry.Effect <= 0 || entry.Buff < 0)
            {
                throw new InvalidDataException($"{entryPath} has an invalid effect {entry.Effect} / buff {entry.Buff}");
            }

            var links = entry.Links.ToArray();
            if (links.Length != ModuleEffectLevelCount
                || links[0] <= 0
                || links.Zip(links.Skip(1)).Any(pair => pair.Second <= pair.First))
            {
                throw new InvalidDataException($"{entryPath}.Links [{string.Join(", ", links)}] is not {ModuleEffectLevelCount} increasing positive numbers");
            }

            if (!result.TryAdd(entry.Effect, new ModuleEffectDefinition(entry.Effect, entry.Buff, entry.Icon, links)))
            {
                throw new InvalidDataException($"{entryPath}.Effect {entry.Effect} is listed twice");
            }

            if (entry.Buff > 0
                && !definitions.TryAdd(entry.Buff, new RootBuffDefinition(RootBuffKind.ModuleEffect, index, entry.Icon, FrozenDictionary<int, string>.Empty)))
            {
                throw new InvalidDataException($"{entryPath}.Buff {entry.Buff} is listed in two kinds");
            }
        }

        return result.ToFrozenDictionary();
    }

    /// <summary>
    /// 因子のアイテムを読む。アイテムは正で重ならず、バフは 0 か正、絵は空でない。合わなければ形の誤りとして止める。
    /// </summary>
    private static FrozenDictionary<int, FactorItemDefinition> ReadFactorItems(List<FactorItemEntry?> entries, string itemPath)
    {
        var result = new Dictionary<int, FactorItemDefinition>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            var entryPath = $"{itemPath}[{index}]";
            var entry = entries[index] ?? throw new InvalidDataException($"{entryPath} is null");
            if (entry.Item <= 0 || entry.Buff < 0 || string.IsNullOrWhiteSpace(entry.Icon))
            {
                throw new InvalidDataException($"{entryPath} has an invalid item {entry.Item} / buff {entry.Buff} / icon \"{entry.Icon}\"");
            }

            if (!result.TryAdd(entry.Item, new FactorItemDefinition(entry.Buff, entry.Icon)))
            {
                throw new InvalidDataException($"{entryPath}.Item {entry.Item} is listed twice");
            }
        }

        return result.ToFrozenDictionary();
    }

    /// <summary>タレントの役割のタグ(<c>TalentTagTable</c> の番号。1 攻撃 / 2 支援 / 3 防御)→ 役割。</summary>
    private static DataTypes.Enums.Professions.ERoleType ToTalentRole(int? talentTag, string itemPath)
        => talentTag switch
        {
            1 => DataTypes.Enums.Professions.ERoleType.DPS,
            2 => DataTypes.Enums.Professions.ERoleType.Healer,
            3 => DataTypes.Enums.Professions.ERoleType.Tank,
            _ => throw new InvalidDataException($"{itemPath}.TalentTag {talentTag?.ToString() ?? "null"} is not 1, 2 or 3")
        };

    private static int ParseRootBuffKey(string key, string itemPath)
        => int.TryParse(key, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : throw new InvalidDataException($"{itemPath} has a key that is not a positive number: {key}");

    /// <summary>
    /// 根のバフ1つの種類・並び(種類の中の順)・絵。心相晶の上級だけ、型の上級の番号ごとの絵を持つ。
    /// タレントは絵を持たない(アビリティ情報は木の節の絵で描く)。
    /// </summary>
    private sealed record RootBuffDefinition(
        RootBuffKind Kind,
        int Order,
        string Icon,
        FrozenDictionary<int, string> AdvancedIcons);

    /// <summary>モジュール効果の段階の数(段階1〜6)。</summary>
    private const int ModuleEffectLevelCount = 6;

    /// <summary>
    /// モジュール効果1つ。根のバフ(段階5・6 が付けるバフ、無ければ 0)・絵・段階1 から順にその段階に要るリンク数。
    /// </summary>
    private sealed record ModuleEffectDefinition(
        int EffectId,
        int RootBuffId,
        string Icon,
        int[] LevelLinks);

    /// <summary>因子のアイテム1つ。付けるバフ(付けなければ 0)と絵。</summary>
    private sealed record FactorItemDefinition(int BuffId, string Icon);

    /// <summary><c>RootBuffs.json</c> の形。項目はどれも省略できない。</summary>
    [JsonObject(ItemRequired = Required.Always)]
    private sealed class RootBuffFile
    {
        public List<RootBuffEntry?> Talents { get; set; } = null!;

        public List<RootBuffEntry?> SeasonTalentNodes { get; set; } = null!;

        public List<RootBuffEntry?> SeasonTalentAdvanced { get; set; } = null!;

        public List<RootBuffEntry?> Factors { get; set; } = null!;

        public List<ModuleEffectEntry?> ModuleEffects { get; set; } = null!;

        public FactorItemFile FactorItems { get; set; } = null!;

        /// <summary>型の根のバフ → 型の上級の番号。</summary>
        public Dictionary<string, int> SeasonTalentAdvancedEffects { get; set; } = null!;

        public TalentTreeFile TalentTrees { get; set; } = null!;
    }

    /// <summary>因子のアイテムを出所(サーバーの表)ごとに。項目はどれも省略できない。</summary>
    [JsonObject(ItemRequired = Required.Always)]
    private sealed class FactorItemFile
    {
        [JsonProperty("StarASIA")]
        public List<FactorItemEntry?> Asia { get; set; } = null!;

        [JsonProperty("Star")]
        public List<FactorItemEntry?> China { get; set; } = null!;
    }

    /// <summary>因子のアイテム1つ。<c>Buff</c> はアイテムが付けるバフ(付けなければ 0)。項目はどれも省略できない。</summary>
    [JsonObject(ItemRequired = Required.Always)]
    private sealed class FactorItemEntry
    {
        public int Item { get; set; }

        public int Buff { get; set; }

        public string Icon { get; set; } = null!;
    }

    /// <summary>タレントの木を出所(サーバーの表)ごとに。項目はどれも省略できない。</summary>
    [JsonObject(ItemRequired = Required.Always)]
    private sealed class TalentTreeFile
    {
        [JsonProperty("StarASIA")]
        public List<TalentTreeEntry?> Asia { get; set; } = null!;

        [JsonProperty("Star")]
        public List<TalentTreeEntry?> China { get; set; } = null!;
    }

    /// <summary>タレントの木1本。<c>Root</c> は根の節(クラスR1 の木は 0)。</summary>
    [JsonObject(ItemRequired = Required.Always)]
    private sealed class TalentTreeEntry
    {
        public int Profession { get; set; }

        public int Spec { get; set; }

        public int Root { get; set; }

        public List<TalentTreeNodeEntry?> Nodes { get; set; } = null!;
    }

    /// <summary>
    /// 木の節1つ。<c>Id</c> は木の表の番号、<c>Next</c> はつなぐ先の節、<c>Talent</c> は節のタレント、
    /// <c>Ability</c> はアビリティの節か、<c>TalentTag</c> は役割のタグ、<c>Replace</c> は技の置き換え(元の技, 置き換え後の技)の組。
    /// </summary>
    [JsonObject(ItemRequired = Required.Always)]
    private sealed class TalentTreeNodeEntry
    {
        public int Id { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public List<int> Next { get; set; } = null!;

        public List<int> Buffs { get; set; } = null!;

        public int Talent { get; set; }

        public bool Ability { get; set; }

        public string Icon { get; set; } = null!;

        public int TalentTag { get; set; }

        public List<List<int>> Replace { get; set; } = null!;
    }

    /// <summary>
    /// 根のバフ1つ。上級は <see cref="Icons"/>(型の上級の番号 → 絵)、タレントは番号だけ、ほかは <see cref="Icon"/> を持つ。
    /// </summary>
    private sealed class RootBuffEntry
    {
        [JsonProperty(Required = Required.Always)]
        public int Buff { get; set; }

        public string? Icon { get; set; }

        public Dictionary<string, string?>? Icons { get; set; }
    }

    /// <summary>モジュール効果1つ。<c>Buff</c> は根のバフ(無ければ 0)、<c>Links</c> は段階1 から順にその段階に要るリンク数。項目はどれも省略できない。</summary>
    [JsonObject(ItemRequired = Required.Always)]
    private sealed class ModuleEffectEntry
    {
        public int Effect { get; set; }

        public int Buff { get; set; }

        public string Icon { get; set; } = null!;

        public List<int> Links { get; set; } = null!;
    }

    private static FrozenDictionary<int, EquipmentDefinition> ReadEquipmentDefinitions(
        Dictionary<string, EquipmentEntry?> entries)
    {
        var result = new Dictionary<int, EquipmentDefinition>(entries.Count);
        foreach (var (key, entry) in entries)
        {
            var equipId = ParseEquipmentKey(key, "Equips");
            var equip = RequireEquipmentItem(entry, $"Equips.{key}");
            if (equip.Stages.Count == 0)
            {
                throw new InvalidDataException($"Equips.{key} has no stages");
            }

            var stages = new EquipmentStage[equip.Stages.Count];
            for (var index = 0; index < stages.Length; index++)
            {
                var stage = RequireEquipmentItem(equip.Stages[index], $"Equips.{key}.Stages[{index}]");
                stages[index] = new EquipmentStage(stage.Gs, stage.Basic.ToArray(), stage.Advanced.ToArray());
            }

            result[equipId] = new EquipmentDefinition(
                equipId,
                equip.Part,
                equip.Quality,
                equip.PerfectUpperLimit,
                equip.MainStat,
                stages,
                equip.Recast.ToArray(),
                equip.Rare.ToArray());
        }

        return result.ToFrozenDictionary();
    }

    /// <param name="rowsById">
    /// 読んだ行を <see cref="MakeEquipmentKey"/>(型, 行ID)で引く索引。同じ型に同じ行ID が2回出たら形の誤り。
    /// </param>
    private static FrozenDictionary<long, IReadOnlyList<EquipmentAttrLibRow>> ReadEquipmentAttrLibs(
        Dictionary<string, Dictionary<string, List<EquipmentAttrLibRowEntry?>?>?> entries,
        out FrozenDictionary<long, EquipmentAttrLibRow> rowsById)
    {
        var result = new Dictionary<long, IReadOnlyList<EquipmentAttrLibRow>>();
        var byId = new Dictionary<long, EquipmentAttrLibRow>();
        foreach (var (typeKey, libs) in entries)
        {
            var libType = ParseEquipmentKey(typeKey, "AttrLibs");
            foreach (var (libKey, rows) in RequireEquipmentItem(libs, $"AttrLibs.{typeKey}"))
            {
                var libId = ParseEquipmentKey(libKey, $"AttrLibs.{typeKey}");
                var libPath = $"AttrLibs.{typeKey}.{libKey}";
                var rowEntries = RequireEquipmentItem(rows, libPath);
                var libRows = new EquipmentAttrLibRow[rowEntries.Count];
                for (var rowIndex = 0; rowIndex < libRows.Length; rowIndex++)
                {
                    var rowPath = $"{libPath}[{rowIndex}]";
                    var row = RequireEquipmentItem(rowEntries[rowIndex], rowPath);
                    var effects = new EquipmentAttrEffect[row.Effects.Count];
                    for (var effectIndex = 0; effectIndex < effects.Length; effectIndex++)
                    {
                        var effect = RequireEquipmentItem(row.Effects[effectIndex], $"{rowPath}.Effects[{effectIndex}]");
                        effects[effectIndex] = new EquipmentAttrEffect(effect.Kind, effect.Id, effect.Min, effect.Max, effect.Format);
                    }

                    libRows[rowIndex] = new EquipmentAttrLibRow(row.Id, row.Parts.ToArray(), row.Specs.ToArray(), effects);
                    if (!byId.TryAdd(MakeEquipmentKey(libType, row.Id), libRows[rowIndex]))
                    {
                        throw new InvalidDataException($"AttrLibs.{typeKey} has row {row.Id} more than once");
                    }
                }

                result[MakeEquipmentKey(libType, libId)] = libRows;
            }
        }

        rowsById = byId.ToFrozenDictionary();
        return result.ToFrozenDictionary();
    }

    private static FrozenDictionary<long, int> ReadEquipmentStrengthSeasons(List<EquipmentStrengthSeasonEntry?> entries)
    {
        var result = new Dictionary<long, int>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = RequireEquipmentItem(entries[index], $"StrengthSeasons[{index}]");
            if (!result.TryAdd(MakeEquipmentKey(entry.Kind, entry.Id), entry.Season))
            {
                throw new InvalidDataException($"StrengthSeasons has kind {entry.Kind} id {entry.Id} more than once");
            }
        }

        return result.ToFrozenDictionary();
    }

    private static FrozenDictionary<int, int> ReadEquipmentSchools(Dictionary<string, int> entries, string section)
    {
        var result = new Dictionary<int, int>(entries.Count);
        foreach (var (key, school) in entries)
        {
            result[ParseEquipmentKey(key, section)] = school;
        }

        return result.ToFrozenDictionary();
    }

    private static int ParseEquipmentKey(string key, string section)
    {
        return int.TryParse(key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var id)
            ? id
            : throw new InvalidDataException($"{section} has \"{key}\" which is not numeric");
    }

    /// <summary><c>Equips.json</c> の中の <c>null</c> を形の誤りにする。</summary>
    private static T RequireEquipmentItem<T>(T? item, string itemPath)
        where T : class
    {
        return item ?? throw new InvalidDataException($"{itemPath} is null");
    }

    /// <summary><c>Equips.json</c> の形。項目の意味は <see cref="EquipmentDefinition"/> ほか公開の型と同じ。項目はどれも省略できない。</summary>
    [JsonObject(ItemRequired = Required.Always)]
    private sealed class EquipmentFile
    {
        public Dictionary<string, EquipmentEntry?> Equips { get; set; } = null!;

        /// <summary>属性庫の型 → 庫ID → 庫の行。</summary>
        public Dictionary<string, Dictionary<string, List<EquipmentAttrLibRowEntry?>?>?> AttrLibs { get; set; } = null!;

        public List<EquipmentStrengthSeasonEntry?> StrengthSeasons { get; set; } = null!;

        /// <summary>特化の番号(<c>SubProfessionId</c>)→ 型2 の属性庫の特化。</summary>
        public Dictionary<string, int> SpecSchools { get; set; } = null!;

        /// <summary>職業ID → クラスR1 の人の型2 の属性庫の特化。</summary>
        public Dictionary<string, int> Rank1Schools { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.Always)]
    private sealed class EquipmentEntry
    {
        public int Part { get; set; }

        public int Quality { get; set; }

        public int PerfectUpperLimit { get; set; }

        public int MainStat { get; set; }

        public List<EquipmentStageEntry?> Stages { get; set; } = null!;

        public List<int> Recast { get; set; } = null!;

        public List<int> Rare { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.Always)]
    private sealed class EquipmentStageEntry
    {
        public int Gs { get; set; }

        public List<int> Basic { get; set; } = null!;

        public List<int> Advanced { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.Always)]
    private sealed class EquipmentAttrLibRowEntry
    {
        public int Id { get; set; }

        public List<int> Parts { get; set; } = null!;

        public List<int> Specs { get; set; } = null!;

        public List<EquipmentAttrEffectEntry?> Effects { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.Always)]
    private sealed class EquipmentAttrEffectEntry
    {
        public int Kind { get; set; }

        public int Id { get; set; }

        public long Min { get; set; }

        public long Max { get; set; }

        public int Format { get; set; }
    }

    /// <summary>シーズン強度の項目(効果の種類と番号)とそのシーズン。</summary>
    [JsonObject(ItemRequired = Required.Always)]
    private sealed class EquipmentStrengthSeasonEntry
    {
        public int Kind { get; set; }

        public int Id { get; set; }

        public int Season { get; set; }
    }

    /// <summary>
    /// 料理・薬剤の名前テーブル(<c>Data/Localization/CuisineBuffs.json</c> / <c>PotionBuffs.json</c>)を読む。
    /// 形はほかの名前テーブルと同じで、表にある鍵がそのまとまりのバフ。ファイルが無ければエラーログを出して空(そのまとまりが無い)。
    /// 同じバフが両方にあれば止まる。
    /// </summary>
    private static void LoadBuffGroups(JsonDataFileReads reads)
    {
        var cuisineBuffNames = LoadLocalizedText(reads, "CuisineBuffs", out var cuisineBuffIds);
        var potionBuffNames = LoadLocalizedText(reads, "PotionBuffs", out var potionBuffIds);

        var both = cuisineBuffIds.Intersect(potionBuffIds).ToArray();
        if (both.Length > 0)
        {
            throw new InvalidDataException($"CuisineBuffs and PotionBuffs share buffs: {string.Join(", ", both)}");
        }

        _cuisineBuffNames = cuisineBuffNames;
        _cuisineBuffIds = cuisineBuffIds;
        _potionBuffNames = potionBuffNames;
        _potionBuffIds = potionBuffIds;
    }

    /// <summary>
    /// 4言語をまとめた名前テーブル <c>Data/Localization/{fileName}.json</c> を読む。
    /// 形は <c>{ "鍵": { "zh-CN": "名前", "en-US": "名前", "ja-JP": "名前", "ko-KR": "名前" } }</c>。
    ///
    /// <para>
    /// ファイルが無ければエラーログを出して <c>null</c>(呼び出し側は全言語を空で続ける)。
    /// 言語名は <see cref="CheckLocalizedNames"/> で検める。
    /// </para>
    /// </summary>
    private static Dictionary<string, Dictionary<string, string>>? ReadLocalizedFile(JsonDataFileReads reads, string fileName)
    {
        var path = LocalizedFilePath(fileName);
        if (!reads.TryTake<Dictionary<string, Dictionary<string, string>?>>(path, out var loadedEntries))
        {
            Log.Error("Missing localization {FileName}", fileName);
            return null;
        }

        var entries = loadedEntries
            ?? throw new InvalidDataException($"{path} is empty.");
        var result = new Dictionary<string, Dictionary<string, string>>(entries.Count);
        var missingNames = 0;
        foreach (var entry in entries)
        {
            result[entry.Key] = CheckLocalizedNames(fileName, entry.Key, entry.Value, ref missingNames);
        }

        LogMissingLocalizedNames(fileName, missingNames);
        Log.Information("Loaded localization {FileName}", fileName);
        return result;
    }

    /// <summary>
    /// 1つの鍵の「言語 → 名前」を検める。<b>知らない言語名はエラーログを出して外す。</b>
    /// 欠けた言語は空欄と同じ扱い(zh-CN へ落ちる)にし、数を <paramref name="missingNames"/> に足す。
    /// </summary>
    private static Dictionary<string, string> CheckLocalizedNames(
        string fileName,
        string key,
        Dictionary<string, string>? names,
        ref int missingNames)
    {
        var checkedNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names ?? [])
        {
            if (!SupportedCultures.Contains(name.Key, StringComparer.OrdinalIgnoreCase))
            {
                Log.Error("{FileName}: key {Key} has unknown culture \"{Culture}\". Available cultures are {Cultures}",
                    fileName, key, name.Key, string.Join(" / ", SupportedCultures));
                continue;
            }

            checkedNames[name.Key] = name.Value ?? string.Empty;
        }

        missingNames += SupportedCultures.Count(cultureName => !checkedNames.ContainsKey(cultureName));
        return checkedNames;
    }

    /// <summary>4言語のどれかが欠けていた数を、ファイルごとに1回だけエラーログに出す。</summary>
    private static void LogMissingLocalizedNames(string fileName, int missingNames)
    {
        if (missingNames > 0)
        {
            Log.Error("{FileName}: {Count} names are missing one or more cultures (treated as blank)", fileName, missingNames);
        }
    }

    /// <summary>
    /// 鍵が番号の名前テーブルを、言語ごとの「番号 → 名前」にして返す。
    /// <paramref name="keys"/> は名前の有無を問わない全部の鍵。番号でない鍵はエラーログを出して飛ばす。
    /// </summary>
    private static FrozenDictionary<string, FrozenDictionary<int, string>> LoadLocalizedText(
        JsonDataFileReads reads,
        string fileName,
        out FrozenSet<int> keys)
    {
        var namesByCulture = SupportedCultures.ToDictionary(
            cultureName => cultureName,
            _ => new Dictionary<int, string>(),
            StringComparer.OrdinalIgnoreCase);
        var allKeys = new HashSet<int>();

        foreach (var entry in ReadLocalizedFile(reads, fileName) ?? [])
        {
            if (!int.TryParse(entry.Key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var id))
            {
                Log.Error("{FileName}: skipping \"{Key}\" because the key is not numeric", fileName, entry.Key);
                continue;
            }

            allKeys.Add(id);
            foreach (var name in entry.Value)
            {
                if (!string.IsNullOrWhiteSpace(name.Value))
                {
                    namesByCulture[name.Key][id] = name.Value.Trim();
                }
            }
        }

        keys = allKeys.ToFrozenSet();
        return namesByCulture.ToFrozenDictionary(
            pair => pair.Key,
            pair => pair.Value.ToFrozenDictionary(),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <c>DungeonTypeNames.json</c> を、言語ごとの「<see cref="MakeDungeonTypeKey"/> → 名前」にして返す。
    /// 鍵は <c>番号</c> か <c>番号:段階</c>。形の合わない鍵はエラーログを出して飛ばす。
    /// </summary>
    private static FrozenDictionary<string, FrozenDictionary<long, string>> LoadDungeonTypeNames(JsonDataFileReads reads)
    {
        var namesByCulture = SupportedCultures.ToDictionary(
            cultureName => cultureName,
            _ => new Dictionary<long, string>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in ReadLocalizedFile(reads, "DungeonTypeNames") ?? [])
        {
            var separator = entry.Key.IndexOf(':');
            var idText = separator < 0 ? entry.Key.AsSpan() : entry.Key.AsSpan(0, separator);
            var difficulty = 0;
            if (!int.TryParse(idText, out var dungeonId)
                || dungeonId <= 0
                || (separator >= 0 && (!int.TryParse(entry.Key.AsSpan(separator + 1), out difficulty) || difficulty <= 0)))
            {
                Log.Error("DungeonTypeNames: skipping \"{Key}\" because the key is not in id or id:difficulty form", entry.Key);
                continue;
            }

            foreach (var name in entry.Value)
            {
                if (!string.IsNullOrWhiteSpace(name.Value))
                {
                    namesByCulture[name.Key][MakeDungeonTypeKey(dungeonId, difficulty)] = name.Value.Trim();
                }
            }
        }

        return namesByCulture.ToFrozenDictionary(
            pair => pair.Key,
            pair => pair.Value.ToFrozenDictionary(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveText<TKey>(
        IReadOnlyDictionary<string, FrozenDictionary<TKey, string>> localizedNames,
        string cultureName,
        TKey id)
        where TKey : notnull
    {
        // 「キーが無い」ではなく「空欄」で落とす。テーブルはIDを全言語ぶん持ち、
        // 訳が用意できていない言語だけ空文字にしてある。
        var normalizedCulture = NormalizeCultureName(cultureName);
        if (localizedNames.TryGetValue(normalizedCulture, out var currentNames)
            && currentNames.TryGetValue(id, out var currentName)
            && !string.IsNullOrWhiteSpace(currentName))
        {
            return currentName;
        }

        // 受け皿は zh-CN ただ1つ。en-US を挟まないのは、言語ごとに違う受け皿へ落ちると
        // 「どの言語のテーブルが欠けているのか」が分からなくなるため。
        //
        // 記録時の名前(CombatStats.Name)も使わない。あれは同梱 SkillTable/BuffTable 由来で、
        // 英語と中国語が混ざっており表示言語に追従しない。名前は翻訳テーブルだけが決める。
        if (!string.Equals(normalizedCulture, "zh-CN", StringComparison.OrdinalIgnoreCase)
            && localizedNames.TryGetValue("zh-CN", out var chineseNames)
            && chineseNames.TryGetValue(id, out var chineseName)
            && !string.IsNullOrWhiteSpace(chineseName))
        {
            return chineseName;
        }

        return string.Empty;
    }

    private static string NormalizeCultureName(string? cultureName)
    {
        if (cultureName?.StartsWith("ja", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "ja-JP";
        }

        if (cultureName?.StartsWith("ko", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "ko-KR";
        }

        if (cultureName?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "zh-CN";
        }

        return "en-US";
    }

    private static string FirstNonEmpty(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first.Trim();
        }

        return string.IsNullOrWhiteSpace(second) ? string.Empty : second.Trim();
    }
}
