# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.
"""
ルートバフの名前・種類・並び・アイコンと、モジュール効果の名前・アイコン・段階、タレントの木の形・タレントの名前を生成する。

  Data/Localization/RootBuffNames.json
  Data/Localization/ModuleEffectNames.json
  Data/Localization/TalentNames.json
  Data/Generated/RootBuffs.json

ルートバフは、タレント・心相晶のノード・心相晶の上級・因子・モジュール効果を付けている間、ずっと付くバフ。
それぞれの効果の`[3, バフID, 1]`がそのバフ。アプリはプレイヤーに付いているルートバフから、
アビリティ情報(タレントの木)・潜在心相晶情報(ノード・上級・因子)・モジュール情報(モジュール効果)を出す。

モジュール効果(`RootBuffs.json`の`ModuleEffects`)は、ルートバフの有無にかかわらず効果の番号(`ModEffectTable.EffectID`)ごとに1つ。
ルートバフ(段階5・6が付けるバフ、無ければ0)・アイコン・段階1から順にその段階に要るリンク数(`EnhancementNum`)を持つ。
名前は`ModuleEffectNames.json`(キーは効果の番号)。アプリはモジュール効果のルートバフの名前も、付与元をたどって
モジュールのルートバフにたどり着いたバフの名前も、効果の番号でここから引く。自分のモジュール情報は、装着中のモジュールのリンク数の和で段階を決める。

因子のアイテム(`RootBuffs.json`の`FactorItems`)は、`SeasonTalentFactorItemTable`の行(等級ごとのアイテム)ごとに1つ。
付けるバフ(バフを付けず能力値だけの因子は0)とアイコン(`ItemTable.Icon`)を持つ。シーズンが進んだサーバーでは前のシーズンの因子が
共通の無効の説明のバフに置き換わり、同じアイテムでもサーバーで付けるバフが違うので、データソースごとに作る(タレントの木と同じ)。
アプリは自分の潜在心相晶情報の因子を、付けている因子のアイテムで出す(名前は等級つきのアイテムの名前`ItemNames.json`)。

タレントの木(`RootBuffs.json`の`TalentTrees`)は、アビリティ情報がゲームと同じ形の木を描くのに使う。
サーバーごとに、そのデータソース(`Star` / `StarASIA`)の表だけで作る(データソースでタレントが違う節や、2つのタレントの位置が入れ替わった節がある)。
木は段階の行(`TalentStageTable`の`RootId`が0でない行)ごとに1本で、節はその行と同じ
(`WeaponType`, `TalentStage`, `BdType`)の`TalentTreeTable`の行。位置は`TalentPosition`、線は`NextTalent`。
クラスR1(`TalentStage` 0)の木は特化の番号0、クラスR2の木は段階が職業の`ShowTalentStage`の何番目かで
特化の番号(職業ID × 10000＋ 並び、アプリの特化の番号)を決める。クラスR2のルートは特化型のバフなので入れない。
両方のデータソースで中身の違う節は一覧を出す(止めない)。

木の節は、タレントの番号(`TalentTreeTable.TalentId`)・アビリティの節か(`TalentTable.TalentType`が4。1は能力値の小さい節、
5は特化型のルート)・アイコン(`TalentIcon`)・役割のタグ・技の置き換え(`TalentEffect`の`[6, 元, 先]`の元と先の組の一覧)を持つ。
役割のタグは、その木の職業(`TalentTreeTable.WeaponType`)の`ProfessionSystemTable.Talent`(`TalentTagTable`の番号。1攻撃 / 2支援 / 3防御)。
アプリは取ったアビリティの節を、節のアイコン・役割(アイコンの背景の色)・タレントの名前(`TalentNames.json`、キーはタレントの番号)で描く。
タレントの名前の対象は、木の節に出てくるタレント全部。

| 種類 | 表 | 名前 | アイコン | 並び |
|---|---|---|---|---|
| タレント | `TalentTable.TalentEffect` | なし(木の節のタレントの名前を使う) | なし(木の節のアイコンを使う) | `Id` |
| 心相晶のノード | `SeasonTalentEffectOrdinaryTable.Effect` | `Name` | `IconNormal` | `Id` |
| 心相晶の上級 | `SeasonTalentEffectAdvancedTable.Effect` | `Name` | `Icon`(型の上級の番号ごと) | `Id` |
| 因子 | `SeasonTalentFactorItemTable.FactorItemEffect` | `ItemTable.Name`から等級を外した名前 | `ItemTable.Icon` | バフID |
| モジュール効果 | `ModEffectTable.EffectConfig` | `EffectName`(効果の番号ごと) | `EffectConfigIcon` | `EffectID` |

タレントは、段階のルートのタレント(`TalentStageTable.RootId` → `TalentTreeTable.TalentId`)のバフを入れない。
特化型のバフで、特化はプレイヤーリストが出す。

心相晶のノードは、型のルートノード(`SeasonTalentTemplateTable.NoteRootId`。型はプレイヤーリストが出す)と、
有効なプレイモード(`EffectiveGameplayType`)が決まっている型の木(`SeasonTalentTreeTable.TemplateId`)のノードを入れない。

心相晶の上級は、1つの型の上級(`EffectId`)にしか出てこないバフ(型の専用効果)を入れない。入れるのは全部の型が使う共通の効果。
上級のアイコンは、同じバフでも型ごとに違う(型の`AdvancedEffectId` = 上級の行の`EffectId`)。
`RootBuffs.json`は上級の番号ごとのアイコンと、型のルートバフ → 上級の番号(0の型は上級を持たないので入れない)を持つ。

因子は、等級を外した名前が2つ以上になるルートバフ(シーズンが変わって効果を失った因子の共通の説明のバフ)を入れない。

**名前でベースにするデータソースはバフIDの単位で決める**(cn / enは`Star`、jp / krは`StarASIA`)。ベースにそのバフIDの名前があればベースだけを使い、
無いときだけもう一方を使う。モジュール効果の名前は同じ決め方を効果の番号の単位で行い、1つの効果に名前の違う行があれば段階の一番小さい行を採り、一覧を出す。心相晶のノードで同じデータソースの中の1つのバフIDに複数の行が該当したら、型の`BelongSeasonId`が一番大きい行を採る。
ほかの種類で1つのバフIDに名前の違う行が該当したら、並びの一番小さい行を採り、一覧を出す。
タレントの名前も同じベースで、タレントの番号の単位で決める。

**種類・並び・アイコンは`Star`をベースにし、無いバフだけ`StarASIA`で補う。**データソースでアイコンが違うバフは一覧を出す。
モジュール効果のルートバフ・アイコン・段階も同じく`Star`をベースにし、無い効果だけ`StarASIA`で補う。

次のときは一覧を出して止める。
- 1つのバフIDが2つの種類に該当する
- 段階のルートが木の表に無い、上級の番号が1つしか無い(型の専用効果を見分けられない)
- 心相晶のノードの行がどの型の木にも無い、メインの型とローグライクの型の両方の木に入る
- 一番新しいシーズンの中で、同じノードのバフIDの名前が食い違う
- 型のルートバフに上級の番号が2つ以上対応する、1つのモジュールのルートバフに効果が2つ以上対応する
- 1つのモジュール効果にルートバフが2つ以上ある、アイコンが1つに決まらない、段階が0から順に並ばない・段階0が0でない・
  リンク数が段階とともに増えない、両方のデータソースで段階のリンク数かルートバフが違う、どの言語の名前も無い
- 1つの因子のアイテムにバフが2つ以上ある、アイコンが無い(どれもデータソースごと)
- タレントの木が空、接続先が同じ木の節に無い、クラスR2のルートが木の節に無い、クラスR2の段階が職業の`ShowTalentStage`に無い、
  1つの職業・特化に木が2本ある
- 節のバフが`Talents`に無い、1つのバフが同じ木の2つ以上の節にある、クラスR2の木がある職業にクラスR1の木が無い(どれもデータソースごと)
- 節のタレントが表に無い、`TalentType`が1 / 4 / 5のどれでもない、バフのある節がアビリティの節でない、
  役割のタグが1〜3のどれでもない(どれもデータソースごと)
- アビリティの節のタレントに、どの言語の名前も無い
"""
import os
import re
import sys

# _commonを読み込む前に置く(後ろだと__pycache__が作られる)
sys.dont_write_bytecode = True

from _common import GENERATED, LANGS, dump, name_of, table_of_source, tables_by_source, write_localized

# 効果の要素`[種類, 値, …]`のうち、バフを付ける種類。
EFFECT_ADD_BUFF = 3

# 効果の要素のうち、技を置き換える種類(`[6, 元の技, 置き換え後の技]`)。
EFFECT_REPLACE_SKILL = 6

# タレントの種類(`TalentTable.TalentType`)。1能力値の小さい節 / 4アビリティの節 / 5特化型のルート。
ABILITY_TALENT_TYPE = 4
TALENT_TYPES = (1, ABILITY_TALENT_TYPE, 5)

# 種類・並び・アイコンを読むデータソースと言語フォルダ。先にあるほうがベース。
STRUCTURE_SOURCES = (("Star", "cn"), ("StarASIA", "jp"))

KINDS = ("Talents", "SeasonTalentNodes", "SeasonTalentAdvanced", "Factors")

# モジュール効果の段階の数(段階0〜6)。
MODULE_EFFECT_LEVELS = 6

# タレントの役割のタグ(`TalentTagTable`の番号)。1攻撃 / 2支援 / 3防御。
TALENT_TAGS = (1, 2, 3)

# クラスR1の段階(`TalentStage`)。ほかはクラスR2。
RANK1_STAGE = 0

# 特化の番号は 職業ID × これ ＋ 特化の並び(1始まり)。gen_class_specs.pyと同じ決め方。
PROFESSION_DIGIT = 10000

# 因子のアイテムの名前の末尾の等級(jp / kr「・G1」、cn「·G1」、en「 - G1」)。
FACTOR_GRADE = re.compile(r"\s*(?:・|·|-)\s*G\d+\s*$")


def buff_ids_of(effects):
    """効果の配列のうち、バフを付けるもののバフID。"""
    return [effect[1] for effect in effects or []
            if isinstance(effect, list) and len(effect) >= 2 and effect[0] == EFFECT_ADD_BUFF
            and isinstance(effect[1], int) and effect[1] > 0]


def stage_root_buffs(source, lang_dir, talents, tree, problems, label):
    """段階のルートのタレント(`TalentStageTable.RootId` → `TalentTreeTable.TalentId`)のバフ。特化型のバフ。"""
    buffs = set()
    for stage in (table_of_source(source, lang_dir, "TalentStageTable") or {}).values():
        root_id = stage.get("RootId") or 0
        if root_id <= 0:
            continue
        node = tree.get(str(root_id))
        if node is None:
            problems.append("%s段階%sのルート%sが木の表に無い" % (label, stage.get("Id"), root_id))
            continue
        buffs.update(buff_ids_of(talents.get(str(node.get("TalentId")), {}).get("TalentEffect")))
    return buffs


def skill_replacements_of(effects):
    """効果の配列のうち、技を置き換えるものの`[元の技, 置き換え後の技]`。"""
    return [[effect[1], effect[2]] for effect in effects or []
            if isinstance(effect, list) and len(effect) >= 3 and effect[0] == EFFECT_REPLACE_SKILL
            and isinstance(effect[1], int) and isinstance(effect[2], int) and effect[1] > 0 and effect[2] > 0]


def talent_trees_of(source, lang_dir, problems):
    """
    1つのデータソースの表から、タレントの木を「(職業, 特化の番号) → 木」で作る。
    節は「節の番号 → {X, Y, Next, Buffs, Talent, Ability, Icon, TalentTag, Replace}」。位置は木の左上を0にした表の単位。
    """
    label = "%s %s" % (lang_dir, source)
    talents = table_of_source(source, lang_dir, "TalentTable") or {}
    tree = table_of_source(source, lang_dir, "TalentTreeTable") or {}
    professions = table_of_source(source, lang_dir, "ProfessionSystemTable") or {}
    spec_of_stage = {}
    for profession_id, profession in professions.items():
        for index, stage_id in enumerate(profession.get("ShowTalentStage") or []):
            spec_of_stage[stage_id] = int(profession_id) * PROFESSION_DIGIT + index + 1
    members = {}
    for node in tree.values():
        members.setdefault((node.get("WeaponType"), node.get("TalentStage"), node.get("BdType")), []).append(node)

    trees = {}
    for stage in (table_of_source(source, lang_dir, "TalentStageTable") or {}).values():
        root_id = stage.get("RootId") or 0
        if root_id <= 0:
            continue
        profession_id = stage.get("WeaponType")
        stage_label = "%s段階%s" % (label, stage.get("Id"))
        if stage.get("TalentStage") == RANK1_STAGE:
            spec, root = 0, 0
        else:
            spec = spec_of_stage.get(stage.get("Id"))
            if spec is None:
                problems.append("%sが職業%sのShowTalentStageに無い" % (stage_label, profession_id))
                continue
            root = root_id

        rows = members.get((profession_id, stage.get("TalentStage"), stage.get("BdType")), [])
        if not rows:
            problems.append("%sの木に節が無い" % stage_label)
            continue
        ids = {row["Id"] for row in rows}
        if root and root not in ids:
            problems.append("%sのルート%sが木の節に無い" % (stage_label, root))
            continue
        min_x = min(row["TalentPosition"][0] for row in rows)
        min_y = min(row["TalentPosition"][1] for row in rows)
        tag = professions.get(str(profession_id), {}).get("Talent")
        if tag not in TALENT_TAGS:
            problems.append("%sの役割のタグ%sが1〜3のどれでもない" % (stage_label, tag))
        nodes = {}
        for row in rows:
            outside = [next_id for next_id in row.get("NextTalent") or [] if next_id not in ids]
            if outside:
                problems.append("%sの節%sの接続先%sが同じ木に無い" % (stage_label, row["Id"], outside))
            talent = talents.get(str(row.get("TalentId")))
            if talent is None:
                problems.append("%sの節%sのタレント%sが表に無い" % (stage_label, row["Id"], row.get("TalentId")))
                continue
            talent_type = talent.get("TalentType")
            if talent_type not in TALENT_TYPES:
                problems.append("%sの節%sのタレント%sの種類%sが1 / 4 / 5のどれでもない"
                                % (stage_label, row["Id"], talent["Id"], talent_type))
            buffs = [] if row["Id"] == root else buff_ids_of(talent.get("TalentEffect"))
            if buffs and talent_type != ABILITY_TALENT_TYPE:
                problems.append("%sの節%sはバフを付けるのにアビリティの節でない(種類%s)" % (stage_label, row["Id"], talent_type))
            nodes[row["Id"]] = {
                "X": row["TalentPosition"][0] - min_x,
                "Y": row["TalentPosition"][1] - min_y,
                "Next": list(row.get("NextTalent") or []),
                "Buffs": buffs,
                "Talent": talent["Id"],
                "Ability": talent_type == ABILITY_TALENT_TYPE,
                "Icon": (talent.get("TalentIcon") or "").strip(),
                "TalentTag": tag,
                "Replace": skill_replacements_of(talent.get("TalentEffect")),
            }

        key = (profession_id, spec)
        if key in trees:
            problems.append("%s職業%sの特化%sに木が2本ある" % (label, profession_id, spec))
            continue
        trees[key] = {"Profession": profession_id, "Spec": spec, "Root": root, "Nodes": nodes}
    return trees


def talent_tree_differences(trees_by_source):
    """2つのデータソースの木を比べ、片方にしか無い木・節と、中身(位置・接続先・バフ・ルート)の違う節を一覧にする。"""
    (left_source, left), (right_source, right) = trees_by_source.items()
    differences = []
    for key in sorted(set(left) | set(right)):
        if key not in left or key not in right:
            differences.append("木%s/%sは%sにだけある" % (key[0], key[1], left_source if key in left else right_source))
            continue
        left_nodes, right_nodes = left[key]["Nodes"], right[key]["Nodes"]
        for node_id in sorted(set(left_nodes) | set(right_nodes)):
            if node_id not in left_nodes or node_id not in right_nodes:
                differences.append("木%s/%sの節%sは%sにだけある"
                                   % (key[0], key[1], node_id, left_source if node_id in left_nodes else right_source))
            elif left_nodes[node_id] != right_nodes[node_id]:
                differences.append("木%s/%sの節%s: %s %s / %s %s"
                                   % (key[0], key[1], node_id, left_source, left_nodes[node_id], right_source, right_nodes[node_id]))
        if left[key]["Root"] != right[key]["Root"]:
            differences.append("木%s/%sのルート: %s %s / %s %s"
                               % (key[0], key[1], left_source, left[key]["Root"], right_source, right[key]["Root"]))
    return differences


def season_talent_scope(templates, tree, problems, label):
    """
    心相晶の木の範囲。返すのは
    (入れないノードのグループ, ノードのグループ → シーズン, 型のルートノードのグループ → 型, ノードのグループ → そのグループが入る木の型の番号の集合)。
    """
    rogue = {str(tid) for tid, t in templates.items() if t.get("EffectiveGameplayType")}
    root_groups = {t.get("NoteRootId"): t for t in templates.values()}
    groups_of = {}
    for row in tree.values():
        groups_of.setdefault(row.get("GroupId"), set()).add(str(row.get("TemplateId")))

    excluded = set(root_groups)
    seasons = {}
    for group, template_ids in groups_of.items():
        main_ids = template_ids - rogue
        if main_ids and template_ids & rogue:
            problems.append("%sノードのグループ%sがメインの型とローグライクの型の両方の木に入る(%s)" % (label, group, sorted(template_ids)))
        if not main_ids:
            excluded.add(group)
            continue
        seasons[group] = max(templates[tid].get("BelongSeasonId") or 0 for tid in main_ids if tid in templates)
    return excluded, seasons, root_groups, groups_of


def rows_of_source(source, lang_dir):
    """
    1つのデータソースの表から「種類 → バフID → [(並び, 名前, アイコン, 付記)]」を作る。付記はノードのシーズン、上級の番号。
    タレントは並びだけを持つ(名前とアイコンは木の節のタレントが持つ)。外したバフは「種類 → [バフID]」で返す。ほかに、構造の問題の一覧と、心相晶の型の表(`SeasonTalentTemplateTable`)も返す。
    """
    label = "%s %s" % (lang_dir, source)
    problems = []
    found = {kind: {} for kind in KINDS}

    def add(kind, buff_id, order, name, icon, extra=None):
        found[kind].setdefault(buff_id, []).append((order, name, (icon or "").strip(), extra))

    talents = table_of_source(source, lang_dir, "TalentTable") or {}
    talent_tree = table_of_source(source, lang_dir, "TalentTreeTable") or {}
    spec_buffs = stage_root_buffs(source, lang_dir, talents, talent_tree, problems, label)
    for row in talents.values():
        for buff_id in buff_ids_of(row.get("TalentEffect")):
            if buff_id in spec_buffs:
                continue
            add("Talents", buff_id, row["Id"], "", "")

    templates = table_of_source(source, lang_dir, "SeasonTalentTemplateTable") or {}
    tree = table_of_source(source, lang_dir, "SeasonTalentTreeTable") or {}
    excluded, seasons, _, groups_of = season_talent_scope(templates, tree, problems, label)
    for row in (table_of_source(source, lang_dir, "SeasonTalentEffectOrdinaryTable") or {}).values():
        group = row.get("GroupId")
        if group in excluded:
            continue
        if group not in groups_of:
            problems.append("%sノードの行%sのグループ%sがどの型の木にも無い" % (label, row.get("Id"), group))
            continue
        for buff_id in buff_ids_of(row.get("Effect")):
            add("SeasonTalentNodes", buff_id, row["Id"], name_of(row), row.get("IconNormal"), seasons.get(group, 0))
    for row in (table_of_source(source, lang_dir, "SeasonTalentEffectAdvancedTable") or {}).values():
        for buff_id in buff_ids_of(row.get("Effect")):
            add("SeasonTalentAdvanced", buff_id, row["Id"], name_of(row), row.get("Icon"), row.get("EffectId"))

    items = table_of_source(source, lang_dir, "ItemTable") or {}
    for row in (table_of_source(source, lang_dir, "SeasonTalentFactorItemTable") or {}).values():
        item = items.get(str(row["Id"]), {})
        name = FACTOR_GRADE.sub("", name_of(item))
        for buff_id in buff_ids_of(row.get("FactorItemEffect")):
            add("Factors", buff_id, buff_id, name, item.get("Icon"), row["Id"])

    # 1つの型の上級にしか出てこないバフは、その型の専用効果。
    advanced = found["SeasonTalentAdvanced"]
    advanced_ids = {extra for rows in advanced.values() for _, _, _, extra in rows}
    if advanced and len(advanced_ids) < 2:
        problems.append("%s上級の番号が%sだけで、型の専用効果を見分けられない" % (label, sorted(advanced_ids)))
    dedicated = sorted(b for b, rows in advanced.items() if len({extra for _, _, _, extra in rows}) == 1)
    for buff_id in dedicated:
        del advanced[buff_id]

    # 等級を外した名前が2つ以上になる因子のルートバフは、1つの因子ではない(共通の説明のバフ)。
    shared = sorted(b for b, rows in found["Factors"].items() if len({name for _, name, _, _ in rows if name}) > 1)
    for buff_id in shared:
        del found["Factors"][buff_id]

    excluded = {"Talents": sorted(spec_buffs), "SeasonTalentAdvanced": dedicated, "Factors": shared}
    return found, problems, excluded, templates


def module_effects_of(source, lang_dir, problems):
    """
    1つのデータソースの`ModEffectTable`から「効果の番号 → {Buff, Icon, Links, Name}」を作る。
    `Buff`はルートバフ(無ければ0)、`Links`は段階1から順にその段階に要るリンク数、`Name`は段階の一番小さい行の名前。
    """
    label = "%s %s" % (lang_dir, source)
    rows_by_effect = {}
    for row in (table_of_source(source, lang_dir, "ModEffectTable") or {}).values():
        rows_by_effect.setdefault(row["EffectID"], []).append(row)

    effects = {}
    effect_of_buff = {}
    for effect_id, rows in sorted(rows_by_effect.items()):
        rows.sort(key=lambda row: (row.get("Level") or 0, row["Id"]))
        buffs = sorted({buff_id for row in rows for buff_id in buff_ids_of(row.get("EffectConfig"))})
        icons = sorted({(row.get("EffectConfigIcon") or "").strip() for row in rows} - {""})
        levels = [row.get("Level") or 0 for row in rows]
        links = [row.get("EnhancementNum") for row in rows]
        if len(buffs) > 1:
            problems.append("%sモジュール効果%dにルートバフが%s" % (label, effect_id, buffs))
        if len(icons) != 1:
            problems.append("%sモジュール効果%dのアイコンが%s" % (label, effect_id, icons))
        if levels != list(range(MODULE_EFFECT_LEVELS + 1)):
            problems.append("%sモジュール効果%dの段階が%s" % (label, effect_id, levels))
        elif links[0] != 0 or any(not isinstance(value, int) for value in links) \
                or any(later <= earlier for earlier, later in zip(links, links[1:])):
            problems.append("%sモジュール効果%dの段階のリンク数が%s" % (label, effect_id, links))
        for buff_id in buffs:
            effect_of_buff.setdefault(buff_id, set()).add(effect_id)

        names = [name_of(row, "EffectName") for row in rows]
        named = [name for name in names if name]
        effects[effect_id] = {
            "Buff": buffs[0] if buffs else 0,
            "Icon": icons[0] if icons else "",
            "Links": links[1:],
            "Name": named[0] if named else "",
            "OtherNames": sorted(set(named[1:]) - {named[0]}) if named else [],
        }

    for buff_id, effect_ids in sorted(effect_of_buff.items()):
        if len(effect_ids) > 1:
            problems.append("%sモジュールのルートバフ%dに効果が%s" % (label, buff_id, sorted(effect_ids)))
    return effects


def factor_items_of(source, lang_dir, problems):
    """
    1つのデータソースの`SeasonTalentFactorItemTable`から「因子のアイテム → {Buff, Icon}」を作る。
    `Buff`はアイテムの効果が付けるバフ(付けなければ0)、`Icon`は`ItemTable.Icon`。
    """
    label = "%s %s" % (lang_dir, source)
    items = table_of_source(source, lang_dir, "ItemTable") or {}
    result = {}
    for row in (table_of_source(source, lang_dir, "SeasonTalentFactorItemTable") or {}).values():
        item_id = row["Id"]
        buffs = sorted(set(buff_ids_of(row.get("FactorItemEffect"))))
        icon = ((items.get(str(item_id)) or {}).get("Icon") or "").strip()
        if len(buffs) > 1:
            problems.append("%s因子のアイテム%dにバフが%s" % (label, item_id, buffs))
        if not icon:
            problems.append("%s因子のアイテム%dにアイコンが無い" % (label, item_id))
        result[item_id] = {"Buff": buffs[0] if buffs else 0, "Icon": icon}
    return result


def pick_name(kind, buff_id, rows, conflicts, resolved, lang):
    """1つのデータソースの行から名前を1つ選ぶ。"""
    named = [row for row in rows if row[1]]
    if not named:
        return ""
    if kind == "SeasonTalentNodes":
        newest = max(season for _, _, _, season in named)
        names = sorted({name for _, name, _, season in named if season == newest})
        if len(names) > 1:
            conflicts.append("%s %d %s" % (lang, buff_id, " / ".join(names)))
            return ""
        if len({name for _, name, _, _ in named}) > 1:
            resolved.append("%sノード%d → シーズン%dの「%s」" % (lang, buff_id, newest, names[0]))
        return names[0]

    named.sort(key=lambda row: row[0])
    if len({name for _, name, _, _ in named}) > 1:
        resolved.append("%s %s %d → 並びの一番小さい行の「%s」(ほか%s)"
                        % (lang, kind, buff_id, named[0][1],
                           " / ".join(sorted({name for _, name, _, _ in named[1:] if name != named[0][1]}))))
    return named[0][1]


def main():
    problems = []
    conflicts = []
    resolved = []

    # 名前(言語ごと、ベースにするデータソースはバフIDの単位)
    names = {lang: {} for lang in LANGS}
    for lang, lang_dir in LANGS.items():
        counts = []
        for source, _ in tables_by_source(lang_dir, "TalentTable"):
            found, source_problems, _, _ = rows_of_source(source, lang_dir)
            problems.extend(source_problems)
            for kind in KINDS:
                for buff_id, rows in found[kind].items():
                    key = str(buff_id)
                    if names[lang].get(key):
                        continue
                    name = pick_name(kind, buff_id, rows, conflicts, resolved, lang)
                    if name:
                        names[lang][key] = name
            counts.append("%s %d" % (source, sum(len(found[kind]) for kind in KINDS)))
        print("%-6sルートバフ%4d(%s)" % (lang, len(names[lang]), " / ".join(counts)))

    # モジュール効果の名前(言語ごと、ベースにするデータソースは効果の番号の単位)
    module_names = {lang: {} for lang in LANGS}
    for lang, lang_dir in LANGS.items():
        for source, _ in tables_by_source(lang_dir, "ModEffectTable"):
            for effect_id, effect in module_effects_of(source, lang_dir, problems).items():
                key = str(effect_id)
                if module_names[lang].get(key) or not effect["Name"]:
                    continue
                module_names[lang][key] = effect["Name"]
                if effect["OtherNames"]:
                    resolved.append("%s ModuleEffects %d → 段階の一番小さい行の「%s」(ほか%s)"
                                    % (lang, effect_id, effect["Name"], " / ".join(effect["OtherNames"])))
        print("%-6sモジュール効果%3d" % (lang, len(module_names[lang])))

    # 種類・並び・アイコン(StarをベースにStarASIAで補う)
    structure = {kind: {} for kind in KINDS}
    advanced_icons = {}
    type_advanced = {}
    icon_differences = []
    excluded_by_kind = {}
    talent_trees = {}
    module_structure = {}
    factor_items = {}
    for source, lang_dir in STRUCTURE_SOURCES:
        found, source_problems, excluded, templates = rows_of_source(source, lang_dir)
        problems.extend(source_problems)
        talent_trees[source] = talent_trees_of(source, lang_dir, problems)
        factor_items[source] = factor_items_of(source, lang_dir, problems)
        for effect_id, effect in module_effects_of(source, lang_dir, problems).items():
            base = module_structure.get(effect_id)
            if base is None:
                module_structure[effect_id] = effect
                continue
            if base["Links"] != effect["Links"] or base["Buff"] != effect["Buff"]:
                problems.append("モジュール効果%dの段階のリンク数かルートバフがデータソースで違う(%s %s %d / ベース %s %d)"
                                % (effect_id, source, effect["Links"], effect["Buff"], base["Links"], base["Buff"]))
            if effect["Icon"] and base["Icon"] != effect["Icon"]:
                icon_differences.append("ModuleEffects %d %s「%s」/ ベース「%s」" % (effect_id, source, effect["Icon"], base["Icon"]))
        for kind, buff_ids in excluded.items():
            excluded_by_kind.setdefault(kind, set()).update(buff_ids)
        for kind in KINDS:
            for buff_id, rows in found[kind].items():
                rows = sorted(rows, key=lambda row: row[0])
                order, _, icon, _ = rows[0]
                if buff_id in structure[kind]:
                    if kind != "SeasonTalentAdvanced" and icon and structure[kind][buff_id][1] != icon:
                        icon_differences.append("%s %d %s「%s」/ ベース「%s」" % (kind, buff_id, source, icon, structure[kind][buff_id][1]))
                    continue
                structure[kind][buff_id] = (order, icon)
                if kind == "SeasonTalentAdvanced":
                    for _, _, row_icon, effect_id in rows:
                        advanced_icons.setdefault(buff_id, {}).setdefault(str(effect_id), row_icon)

        ordinary = table_of_source(source, lang_dir, "SeasonTalentEffectOrdinaryTable") or {}
        root_buffs = {}
        for row in ordinary.values():
            root_buffs.setdefault(row.get("GroupId"), []).extend(buff_ids_of(row.get("Effect")))
        for template in templates.values():
            advanced_id = template.get("AdvancedEffectId") or 0
            if advanced_id <= 0 or template.get("EffectiveGameplayType"):
                continue
            for buff_id in root_buffs.get(template.get("NoteRootId"), []):
                type_advanced.setdefault(buff_id, set()).add(advanced_id)

    for buff_id, advanced_ids in sorted(type_advanced.items()):
        if len(advanced_ids) > 1:
            problems.append("型のルートバフ%dに上級の番号が%s" % (buff_id, sorted(advanced_ids)))

    kinds_of = {}
    for kind in KINDS:
        for buff_id in structure[kind]:
            kinds_of.setdefault(buff_id, []).append(kind)
    for effect in module_structure.values():
        if effect["Buff"]:
            kinds_of.setdefault(effect["Buff"], []).append("ModuleEffects")
    for effect_id in sorted(module_structure):
        if not any(module_names[lang].get(str(effect_id)) for lang in LANGS):
            problems.append("モジュール効果%dにどの言語の名前も無い" % effect_id)
    for buff_id, kinds in sorted(kinds_of.items()):
        if len(kinds) > 1:
            problems.append("バフ%dが2つの種類に該当する(%s)" % (buff_id, " / ".join(kinds)))

    for source, trees in talent_trees.items():
        for (profession_id, spec), tree in sorted(trees.items()):
            nodes_of_buff = {}
            for node_id, node in sorted(tree["Nodes"].items()):
                for buff_id in node["Buffs"]:
                    nodes_of_buff.setdefault(buff_id, []).append(node_id)
                    if buff_id not in structure["Talents"]:
                        problems.append("%sタレントの木%s/%sの節%sのバフ%dがTalentsに無い"
                                        % (source, profession_id, spec, node_id, buff_id))
            for buff_id, node_ids in sorted(nodes_of_buff.items()):
                if len(node_ids) > 1:
                    problems.append("%sタレントの木%s/%sのバフ%dが2つ以上の節にある(%s)"
                                    % (source, profession_id, spec, buff_id, node_ids))
        for profession_id in sorted({profession_id for profession_id, spec in trees if spec}):
            if (profession_id, 0) not in trees:
                problems.append("%s職業%sにクラスR1の木が無い" % (source, profession_id))

    # タレントの名前(言語ごと、ベースにするデータソースはタレントの番号の単位)。対象は木の節に出てくるタレント。
    tree_nodes = [node for trees in talent_trees.values() for tree in trees.values() for node in tree["Nodes"].values()]
    talent_ids = {node["Talent"] for node in tree_nodes}
    talent_names = {lang: {} for lang in LANGS}
    for lang, lang_dir in LANGS.items():
        for _, talents in tables_by_source(lang_dir, "TalentTable"):
            for talent_id in talent_ids:
                key = str(talent_id)
                if talent_names[lang].get(key):
                    continue
                name = name_of(talents.get(key), "TalentName")
                if name:
                    talent_names[lang][key] = name
    for talent_id in sorted({node["Talent"] for node in tree_nodes if node["Ability"]}):
        if not any(talent_names[lang].get(str(talent_id)) for lang in LANGS):
            problems.append("アビリティの節のタレント%dにどの言語の名前も無い" % talent_id)

    if problems or conflicts:
        for text in sorted(set(problems)):
            print("  構造の問題: %s" % text)
        for text in conflicts:
            print("  一番新しいシーズンで名前が食い違う: %s" % text)
        raise ValueError("RootBuffsを作れない(構造の問題%d件 / 名前の食い違い%d件)" % (len(set(problems)), len(conflicts)))

    output = {}
    for kind in KINDS:
        entries = []
        for buff_id, (order, icon) in sorted(structure[kind].items(), key=lambda item: (item[1][0], item[0])):
            if kind == "SeasonTalentAdvanced":
                entries.append({"Buff": buff_id, "Icons": dict(sorted(advanced_icons.get(buff_id, {}).items(), key=lambda item: int(item[0])))})
            elif kind == "Talents":
                entries.append({"Buff": buff_id})
            else:
                entries.append({"Buff": buff_id, "Icon": icon})
        output[kind] = entries
    output["ModuleEffects"] = [
        {"Effect": effect_id, "Buff": effect["Buff"], "Icon": effect["Icon"], "Links": effect["Links"]}
        for effect_id, effect in sorted(module_structure.items())]
    output["FactorItems"] = {
        source: [{"Item": item_id, "Buff": item["Buff"], "Icon": item["Icon"]} for item_id, item in sorted(items.items())]
        for source, items in factor_items.items()}
    output["SeasonTalentAdvancedEffects"] = {
        str(buff_id): next(iter(advanced_ids)) for buff_id, advanced_ids in sorted(type_advanced.items())}
    output["TalentTrees"] = {
        source: [
            {"Profession": profession_id, "Spec": spec, "Root": tree["Root"],
             "Nodes": [dict({"Id": node_id}, **node) for node_id, node in sorted(tree["Nodes"].items())]}
            for (profession_id, spec), tree in sorted(trees.items())]
        for source, trees in talent_trees.items()}
    tree_differences = talent_tree_differences(talent_trees)

    print("→ 種類ごとのルートバフ: %s" % " / ".join("%s %d" % (kind, len(output[kind])) for kind in KINDS))
    print("  モジュール効果%d件(ルートバフあり%d件)" % (
        len(output["ModuleEffects"]), sum(1 for entry in output["ModuleEffects"] if entry["Buff"])))
    for source, entries in output["FactorItems"].items():
        print("  因子のアイテム%s %d件(バフあり%d件)" % (source, len(entries), sum(1 for entry in entries if entry["Buff"])))
    for kind, label in (("Talents", "特化型"), ("SeasonTalentAdvanced", "上級の型の専用効果"), ("Factors", "因子の共通の説明")):
        buff_ids = sorted(excluded_by_kind.get(kind, ()))
        print("  外した%sのバフ%d件: %s" % (label, len(buff_ids), ", ".join(map(str, buff_ids))))
    print("  型の上級の番号: %d件" % len(output["SeasonTalentAdvancedEffects"]))
    for source, trees in output["TalentTrees"].items():
        print("  タレントの木%s: クラスR1 %d本 / クラスR2 %d本 / 節%d" % (
            source,
            sum(1 for tree in trees if not tree["Spec"]),
            sum(1 for tree in trees if tree["Spec"]),
            sum(len(tree["Nodes"]) for tree in trees)))
    print("  両方のデータソースで中身の違うタレントの木の節%d件" % len(tree_differences))
    for text in tree_differences:
        print("    %s" % text)
    for source, trees in output["TalentTrees"].items():
        nodes = [node for tree in trees for node in tree["Nodes"]]
        print("  タレントの木%sのアビリティの節%d(バフ無し%d / 技の置き換え%d)" % (
            source,
            sum(1 for node in nodes if node["Ability"]),
            sum(1 for node in nodes if node["Ability"] and not node["Buffs"]),
            sum(1 for node in nodes if node["Replace"])))
    print("  名前を選んだ%d件" % len(resolved))
    for text in resolved:
        print("    %s" % text)
    print("  データソースでアイコンが違う%d件" % len(icon_differences))
    for text in icon_differences:
        print("    %s" % text)
    print()

    keys = set()
    for kind in KINDS:
        if kind != "Talents":
            keys |= {str(buff_id) for buff_id in structure[kind]}
    write_localized("RootBuffNames", names, keys)
    write_localized("ModuleEffectNames", module_names, {str(effect_id) for effect_id in module_structure})
    write_localized("TalentNames", talent_names, {str(talent_id) for talent_id in talent_ids})
    dump(os.path.join(GENERATED, "RootBuffs.json"), output)
    print("  RootBuffs.json %s" % " / ".join("%s %d" % (kind, len(output[kind])) for kind in KINDS + ("ModuleEffects", "FactorItems")))


if __name__ == "__main__":
    main()
