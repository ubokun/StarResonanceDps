# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.
"""
装備の数値・効果の文言のテーブルを生成する。装備の名前は`gen_items.py`のアイテムの名前(装備IDもアイテムID)。

  Data/Generated/Equips.json
  Data/Localization/EquipEffectTexts.json

Equips: **数値は`Star`の表で作る。**形は次のとおり。
- `Equips`: 装備ID → 部位(`EquipPart`)・品質(`ItemTable.Quality`)・完成度の上限(`PerfectUpperLimit`の2要素目)・
  主能力値の系統・突破の段階ごとの装備Lvと庫の配列・改鋳の庫の配列・レアの庫の配列。
  段階0は`EquipTable`、段階1〜 は`EquipBreakThroughTable`の行を段階の順に
- `AttrLibs`: 型(1 = `EquipAttrLibTable`、2 = `EquipAttrSchoolLibTable`)→ 庫ID → その庫の全行(行の`Id`つき)。
  装備から参照される庫だけ(全段階の基礎・進化、改鋳、装備と突破の行のレア、改造`EquipTransformTable`の改鋳・レア)。
  `Recast` / `Rare`の配列は装備の行の欄のまま(そのままの項目で届いた行を引く型を決めるのに使う。ゲームと同じく改造の行は見ない)
- `StrengthSeasons`: シーズン強度の項目と、そのシーズン。基礎に出る一時属性と11440系の属性を項目にし、
  それを基礎に持つ装備の`SeasonId`の積集合で決める
- `SpecSchools` / `Rank1Schools`: アプリの特化の番号 / 職業ID → 型2の庫の行を選ぶ特化(`TalentSchoolTable.Id`)。
  特化の段階(`ProfessionSystemTable.ShowTalentStage`の並び / クラスR1の`TalentStageTable`の行)を
  `TalentSchoolTable.TalentStage`で引く。特化に該当しない段階は出さない。
  段階の欄が空の特化は、ほかの特化と同じ[同じ職業のクラスR1の段階, 自分の段階]の形として扱い、
  型2の庫で特化の欄が空の行はその特化の行とする

効果は行の`AttrEffect`を順に読み、`AttrEffectConfig`の添字を、属性と一時属性で1つ、バフで要素の3要素目(無ければ1)進める。
値の下限・上限はその効果の最初の設定。

EquipEffectTexts: キーは効果の番号。進化・改鋳・レアの側に出る一時属性(`TempAttrTable.AttrDesc`)と
バフ(`AttrDescription[BuffTable.TipsDescription].Description`)の文言。値の差し込みは、そのままの値を`{0}`、
%の値(`{*tempAttr.up*}`)を`{1}`に置き換える。

文言でベースにするデータソースは言語フォルダごと(`_common.table`)。

次のときは書かずに止まる。
- 装備の行がアイテムの表に無い、`PerfectUpperLimit`に2要素目が無い
- 突破の段階が1からの連番でない、突破の行の装備が装備の表に無い
- 参照される庫が表に無い、庫の型が1・2以外、装備の改造の行が表に無い
- 効果の種類が1・3・5以外、設定の数が効果と合わない、設定が`[min, max]`でない、属性の系統が`FightAttrTable`に無い、属性の書式が決まらない
- 主能力値が2系統出る
- シーズン強度の項目のシーズンが1つに決まらない、基礎に出る一時属性が進化にも出る
- 1つの段階に特化が2つ該当する、職業のクラスR1の段階が2行ある
- 段階の欄が空の特化が2つ以上ある、それがどの職業のBdType0の段階でもない、その職業のクラスR1の段階が1行でない、
  特化の欄が空の行があるのに段階の欄が空の特化が無い
- 文言に決まった差し込み以外の差し込みか波括弧がある、一時属性とバフで番号が重複する
"""
import os
import re
import sys

# _commonを読み込む前に置く(後ろだと__pycache__が作られる)
sys.dont_write_bytecode = True

from _common import GENERATED, LANGS, TABLES_ENV, dump, name_of, table, table_of_source, write_localized

# 数値を作るデータソースと言語フォルダ。数値の表は言語で変わらない。
PRIMARY_SOURCE = "Star"
VALUE_LANG_DIR = "cn"

# 庫の配列の1要素目(型)→ 庫の表。
LIB_TABLES = {1: "EquipAttrLibTable", 2: "EquipAttrSchoolLibTable"}
SCHOOL_LIB_TYPE = 2

# 効果の種類。
KIND_ATTR = 1
KIND_BUFF = 3
KIND_TEMP_ATTR = 5

# 値の書式。0 = そのままの数、1 = %(値 ÷ 100)。
FORMAT_NUMBER = 0
FORMAT_PERCENT = 1

# 属性の番号の末尾(番号% 10)で、そのままの数になるもの(加算)と%になるもの(割合)。
NUMBER_TAILS = (2, 3)
PERCENT_TAIL = 4
# FightAttrTable.AttrNumType
NUM_TYPE_NUMBER = 0
NUM_TYPE_PERCENT = 1

# 主能力値の系統(筋力 / 知力 / 敏捷)。
MAIN_STAT_BASES = (11010, 11020, 11030)
# シーズン強度の系統。
SEASON_STRENGTH_BASE = 11440

# 特化の番号は 職業ID × これ ＋ 特化の並び(1始まり)。gen_class_specs.pyと同じ決め方。
PROFESSION_DIGIT = 10000

# 文言の値の差し込み → アプリの差し込み。{0}はそのままの値、{1}は%の値(値 ÷ 100、末尾の0を取り除いて%)。
INJECTIONS = {
    "{*tempAttr.un*}": "{0}",
    "{*Decision.unmarkpercent(1)*}": "{0}",
    "{*tempAttr.up*}": "{1}",
}
VALUE_SLOTS = ("{0}", "{1}")
INJECTION_PATTERN = re.compile(r"\{\*.*?\*\}")


def raw_table(name):
    found = table_of_source(PRIMARY_SOURCE, VALUE_LANG_DIR, name)
    if found is None:
        raise FileNotFoundError(
            "%sの%s/%sが無い。入力の置き場は環境変数%sか、_common.pyのTABLES_DIRで設定する"
            % (PRIMARY_SOURCE, VALUE_LANG_DIR, name, TABLES_ENV))
    return found


def base_of(attr_id):
    return attr_id - attr_id % 10


def attr_format(attr_id, fight_attrs, label, problems):
    """属性の値の書式。決まらなければ問題の一覧に足してNone。"""
    row = fight_attrs.get(str(base_of(attr_id)))
    if row is None:
        problems.append("%s属性%dの系統%dがFightAttrTableに無い" % (label, attr_id, base_of(attr_id)))
        return None
    num_type = row.get("AttrNumType")
    tail = attr_id % 10
    if num_type == NUM_TYPE_PERCENT or (num_type == NUM_TYPE_NUMBER and tail == PERCENT_TAIL):
        return FORMAT_PERCENT
    if num_type == NUM_TYPE_NUMBER and tail in NUMBER_TAILS:
        return FORMAT_NUMBER
    problems.append("%s属性%dの書式が決まらない(AttrNumType %s、末尾%d)" % (label, attr_id, num_type, tail))
    return None


def effects_of(row, fight_attrs, label, problems):
    """庫の1行の効果の一覧。"""
    configs = row.get("AttrEffectConfig") or []
    effects = []
    index = 0
    for effect in row.get("AttrEffect") or []:
        kind, effect_id = effect[0], effect[1]
        if kind in (KIND_ATTR, KIND_TEMP_ATTR):
            step = 1
        elif kind == KIND_BUFF:
            step = effect[2] if len(effect) > 2 else 1
        else:
            problems.append("%s効果%sの種類%dは扱えない" % (label, effect, kind))
            return effects
        if step < 1 or index + step > len(configs):
            problems.append("%s効果%sの設定が足りない(設定%d個、%d個目から%d個)"
                            % (label, effect, len(configs), index + 1, step))
            return effects
        config = configs[index]
        index += step
        if len(config) != 2:
            problems.append("%s効果%sの設定%sが[min, max]でない" % (label, effect, config))
            continue
        if kind == KIND_ATTR:
            value_format = attr_format(effect_id, fight_attrs, label, problems)
        elif kind == KIND_BUFF:
            value_format = FORMAT_PERCENT
        else:
            value_format = FORMAT_NUMBER
        effects.append({"Kind": kind, "Id": effect_id, "Min": config[0], "Max": config[1], "Format": value_format})
    if index != len(configs):
        problems.append("%s設定が効果より多い(設定%d個、使ったのは%d個)" % (label, len(configs), index))
    return effects


def rows_by_lib(lib_type):
    """庫ID → その庫の行(表の並び順)。"""
    result = {}
    for row in raw_table(LIB_TABLES[lib_type]).values():
        result.setdefault(row["AttrLibId"], []).append(row)
    return result


def lib_refs(lib_array, label, problems):
    """庫の配列`[型, 庫ID…]` → [(型, 庫ID)]。"""
    if not lib_array:
        return []
    lib_type = lib_array[0]
    if lib_type not in LIB_TABLES:
        problems.append("%s庫の配列%sの型%sは扱えない" % (label, lib_array, lib_type))
        return []
    return [(lib_type, lib_id) for lib_id in lib_array[1:]]


def stages_of(equips, breakthroughs, problems):
    """装備ID → 段階の一覧。段階0は装備の行、段階1〜 は突破の行。"""
    by_equip = {}
    for row in breakthroughs.values():
        by_equip.setdefault(row["EquipId"], []).append(row)
    unknown = sorted(equip_id for equip_id in by_equip if str(equip_id) not in equips)
    if unknown:
        problems.append("突破の行の装備が装備の表に無い: %s" % unknown)

    result = {}
    for equip_id, row in equips.items():
        rows = sorted(by_equip.get(int(equip_id), []), key=lambda r: r["BreakThroughTime"])
        times = [r["BreakThroughTime"] for r in rows]
        if times != list(range(1, len(rows) + 1)):
            problems.append("装備%sの突破の段階が1からの連番でない: %s" % (equip_id, times))
            continue
        result[int(equip_id)] = [
            {"Gs": r["EquipGs"], "Basic": r.get("BasicAttrLibId") or [], "Advanced": r.get("AdvancedAttrLibId") or []}
            for r in [row] + rows]
    return result


def allowed_rows(rows, part):
    """部位か0を`AllowPart`に含む行。"""
    return [row for row in rows if part in row["Parts"] or 0 in row["Parts"]]


def main_stat_of(equip_id, part, stage, attr_libs, problems):
    """段階0の基礎に出る主能力値の系統。無ければ0。"""
    found = set()
    for lib_type, lib_id in lib_refs(stage["Basic"], "装備%d段階0基礎" % equip_id, problems):
        for row in allowed_rows(attr_libs.get(lib_type, {}).get(lib_id, []), part):
            for effect in row["Effects"]:
                if effect["Kind"] == KIND_ATTR and base_of(effect["Id"]) in MAIN_STAT_BASES:
                    found.add(base_of(effect["Id"]))
    if len(found) > 1:
        problems.append("装備%dの主能力値が%d系統: %s" % (equip_id, len(found), sorted(found)))
        return 0
    return found.pop() if found else 0


def strength_seasons_of(equips, stages, attr_libs, advanced_temp_ids, problems):
    """シーズン強度の項目(種類, 番号) → シーズン。"""
    holders = {}
    for equip_id, equip_stages in stages.items():
        part = equips[str(equip_id)]["EquipPart"]
        for index, stage in enumerate(equip_stages):
            for lib_type, lib_id in lib_refs(stage["Basic"], "装備%d段階%d基礎" % (equip_id, index), problems):
                for row in allowed_rows(attr_libs[lib_type][lib_id], part):
                    for effect in row["Effects"]:
                        if effect["Kind"] == KIND_TEMP_ATTR or (
                                effect["Kind"] == KIND_ATTR and base_of(effect["Id"]) == SEASON_STRENGTH_BASE):
                            holders.setdefault((effect["Kind"], effect["Id"]), set()).add(equip_id)

    result = {}
    for (kind, effect_id), equip_ids in sorted(holders.items()):
        if kind == KIND_TEMP_ATTR and effect_id in advanced_temp_ids:
            problems.append("基礎に出る一時属性%dが進化にも出る(シーズン強度か決まらない)" % effect_id)
            continue
        seasons = None
        for equip_id in equip_ids:
            own = set(equips[str(equip_id)].get("SeasonId") or [])
            seasons = own if seasons is None else seasons & own
        if len(seasons) != 1:
            problems.append("シーズン強度の項目%d:%dのシーズンが1つに決まらない: %s(装備%d件)"
                            % (kind, effect_id, sorted(seasons), len(equip_ids)))
            continue
        result[(kind, effect_id)] = seasons.pop()
    return result


def stageless_school_of(schools, stages, problems):
    """
    段階の欄(`TalentSchoolTable.TalentStage`)が空の特化と、それに補う段階。無ければNone。
    ほかの特化の段階の欄は[同じ職業のクラスR1の段階, 自分の段階]の形なので、空の特化もその形として扱う。
    属性庫で特化の欄が空の行は、この特化の行として扱う。
    """
    empty = sorted(int(school_id) for school_id, row in schools.items() if not (row.get("TalentStage") or []))
    if not empty:
        return None
    if len(empty) > 1:
        problems.append("段階の欄が空の特化が%d個ある: %s" % (len(empty), empty))
        return None

    school_id = empty[0]
    stage = stages.get(str(school_id))
    if not stage or stage.get("TalentStage") == 0 or stage.get("BdType") != 0:
        problems.append("段階の欄が空の特化%dが、どの職業のBdType0の段階でもない" % school_id)
        return None

    profession_id = stage.get("WeaponType")
    rank1 = [int(stage_id) for stage_id, row in stages.items()
             if row.get("WeaponType") == profession_id and row.get("TalentStage") == 0 and row.get("BdType") == 0]
    if len(rank1) != 1:
        problems.append("段階の欄が空の特化%dの職業%sのクラスR1の段階が%d行" % (school_id, profession_id, len(rank1)))
        return None
    return school_id, [rank1[0], school_id]


def school_by_stage(schools, stageless, problems):
    """特化の段階ID → 特化(`TalentSchoolTable.Id`)。段階の欄が空の特化は補った段階で引く。"""
    result = {}
    for school_id, row in schools.items():
        stage_ids = row.get("TalentStage") or []
        if stageless and int(school_id) == stageless[0]:
            stage_ids = stageless[1]
        for stage_id in stage_ids:
            if stage_id in result:
                problems.append("段階%dに特化%dと%sが該当する" % (stage_id, result[stage_id], school_id))
                continue
            result[stage_id] = int(school_id)
    return result


def specs_of(row, lib_type, stageless, label, problems):
    """行の特化。型1は空。型2で特化の欄が空の行は、段階の欄が空の特化の行とする。"""
    if lib_type != SCHOOL_LIB_TYPE:
        return []
    specs = row.get("TalentSchoolId") or []
    if specs:
        return specs
    if not stageless:
        problems.append("%sは特化の欄が空だが、段階の欄が空の特化が無い" % label)
        return []
    return [stageless[0]]


def spec_schools_of(professions, stage_schools):
    """アプリの特化の番号 → 特化。段階に特化が無ければ出さない。"""
    result = {}
    for profession_id, profession in professions.items():
        for index, stage_id in enumerate(profession.get("ShowTalentStage") or []):
            if stage_id in stage_schools:
                result[int(profession_id) * PROFESSION_DIGIT + index + 1] = stage_schools[stage_id]
    return result


def rank1_schools_of(professions, stages, stage_schools, problems):
    """職業ID → クラスR1の段階の特化。段階か特化が無ければ出さない。"""
    result = {}
    for profession_id in professions:
        rank1 = [int(stage_id) for stage_id, row in stages.items()
                 if row.get("WeaponType") == int(profession_id) and row.get("TalentStage") == 0 and row.get("BdType") == 0]
        if len(rank1) > 1:
            problems.append("職業%sのクラスR1の段階が%d行: %s" % (profession_id, len(rank1), sorted(rank1)))
            continue
        if rank1 and rank1[0] in stage_schools:
            result[int(profession_id)] = stage_schools[rank1[0]]
    return result


def effect_text(text, label, problems):
    """文言の値の差し込みをアプリの差し込み({0} / {1})に置き換える。"""
    for injection, slot in INJECTIONS.items():
        text = text.replace(injection, slot)
    others = INJECTION_PATTERN.findall(text)
    rest = text
    for slot in VALUE_SLOTS:
        rest = rest.replace(slot, "")
    if others:
        problems.append("%sの文言に扱えない差し込みがある: %s" % (label, others))
    elif rest.count("{") or rest.count("}"):
        problems.append("%sの文言に{0} / {1}以外の波括弧がある: %s" % (label, text))
    return text


def effect_texts_of(temp_ids, buff_ids, problems):
    """言語 → {効果の番号: 文言}。"""
    values = {}
    for lang, lang_dir in LANGS.items():
        temp_attrs = table(lang_dir, "TempAttrTable")
        buffs = table(lang_dir, "BuffTable")
        descriptions = table(lang_dir, "AttrDescription")
        texts = {}
        for temp_id in temp_ids:
            text = name_of(temp_attrs.get(str(temp_id)), "AttrDesc")
            texts[str(temp_id)] = effect_text(text, "%s一時属性%d" % (lang_dir, temp_id), problems)
        for buff_id in buff_ids:
            tips = (buffs.get(str(buff_id)) or {}).get("TipsDescription") or 0
            text = name_of(descriptions.get(str(tips)), "Description") if tips else ""
            texts[str(buff_id)] = effect_text(text, "%sバフ%d" % (lang_dir, buff_id), problems)
        values[lang] = texts
    return values


def main():
    problems = []

    equips = raw_table("EquipTable")
    items = raw_table("ItemTable")
    missing = sorted(int(equip_id) for equip_id in equips if equip_id not in items)
    if missing:
        for equip_id in missing:
            print("  アイテムの表に無い装備: %d" % equip_id)
        raise ValueError("%sの装備%d件がアイテムの表に無い" % (PRIMARY_SOURCE, len(missing)))

    fight_attrs = raw_table("FightAttrTable")
    breakthroughs = raw_table("EquipBreakThroughTable")
    stages = stages_of(equips, breakthroughs, problems)

    # 装備の全段階から参照される庫。
    referenced = {}
    advanced_refs = set()
    for equip_id, equip_stages in stages.items():
        for index, stage in enumerate(equip_stages):
            for side in ("Basic", "Advanced"):
                label = "装備%d段階%d %s" % (equip_id, index, side)
                for ref in lib_refs(stage[side], label, problems):
                    referenced.setdefault(ref, label)
                    if side == "Advanced":
                        advanced_refs.add(ref)

    # 改鋳とレアの庫。改鋳は装備の行と改造の行、レアは装備の行・突破の行・改造の行から。
    # 改造の行の庫で付いた行も、自分の装備の行IDとして届く(品質5の武器のレアは改造の行の型2の庫にしか無い)。
    side_refs = set()
    side_columns = [(row, "装備%s" % equip_id, ("RecastingAttrLibId", "QualityChildAttrLibId"))
                    for equip_id, row in equips.items()]
    side_columns += [(row, "突破の行%s" % row_id, ("QualityChildAttrLibId",))
                     for row_id, row in breakthroughs.items()]
    transforms = raw_table("EquipTransformTable")
    for equip_id, row in equips.items():
        transform_id = row.get("TransformId") or 0
        if not transform_id:
            continue
        transform = transforms.get(str(transform_id))
        if transform is None:
            problems.append("装備%sの改造%dがEquipTransformTableに無い" % (equip_id, transform_id))
            continue
        side_columns.append((transform, "装備%sの改造%d" % (equip_id, transform_id), ("RecastAttrLibId", "QualityAttrLibId")))
    for row, owner, columns in side_columns:
        for column in columns:
            label = "%s %s" % (owner, column)
            for ref in lib_refs(row.get(column) or [], label, problems):
                referenced.setdefault(ref, label)
                side_refs.add(ref)

    schools = raw_table("TalentSchoolTable")
    talent_stages = raw_table("TalentStageTable")
    stageless = stageless_school_of(schools, talent_stages, problems)

    rows_of_type = {lib_type: rows_by_lib(lib_type) for lib_type in LIB_TABLES}
    attr_libs = {lib_type: {} for lib_type in LIB_TABLES}
    for (lib_type, lib_id), label in sorted(referenced.items()):
        rows = rows_of_type[lib_type].get(lib_id)
        if not rows:
            problems.append("%sの庫%dが%sに無い" % (label, lib_id, LIB_TABLES[lib_type]))
            continue
        attr_libs[lib_type][lib_id] = [
            {"Id": row["Id"],
             "Parts": row.get("AllowPart") or [],
             "Specs": specs_of(row, lib_type, stageless, "%s行%d" % (LIB_TABLES[lib_type], row["Id"]), problems),
             "Effects": effects_of(row, fight_attrs, "%s行%d" % (LIB_TABLES[lib_type], row["Id"]), problems)}
            for row in rows]

    if problems:
        for text in problems:
            print("  問題: %s" % text)
        raise ValueError("装備のテーブルを作れない(問題%d件)" % len(problems))

    # 進化の側に出る一時属性(シーズン強度の判定に使う)と、文言を作る一時属性とバフ(進化・改鋳・レアの側)。
    advanced_temp_ids = set()
    temp_ids = set()
    buff_ids = set()
    for ref in advanced_refs | side_refs:
        lib_type, lib_id = ref
        for row in attr_libs[lib_type][lib_id]:
            for effect in row["Effects"]:
                if effect["Kind"] == KIND_TEMP_ATTR:
                    temp_ids.add(effect["Id"])
                    if ref in advanced_refs:
                        advanced_temp_ids.add(effect["Id"])
                elif effect["Kind"] == KIND_BUFF:
                    buff_ids.add(effect["Id"])
    overlap = sorted(temp_ids & buff_ids)
    if overlap:
        problems.append("一時属性とバフで番号が重複する: %s" % overlap)

    strength_seasons = strength_seasons_of(equips, stages, attr_libs, advanced_temp_ids, problems)

    equip_entries = {}
    for equip_id in sorted(stages):
        row = equips[str(equip_id)]
        limit = row.get("PerfectUpperLimit") or []
        if len(limit) < 2:
            problems.append("装備%dのPerfectUpperLimitに2要素目が無い: %s" % (equip_id, limit))
            continue
        equip_entries[str(equip_id)] = {
            "Part": row["EquipPart"],
            "Quality": items[str(equip_id)]["Quality"],
            "PerfectUpperLimit": limit[1],
            "MainStat": main_stat_of(equip_id, row["EquipPart"], stages[equip_id][0], attr_libs, problems),
            "Stages": stages[equip_id],
            "Recast": row.get("RecastingAttrLibId") or [],
            "Rare": row.get("QualityChildAttrLibId") or [],
        }

    professions = raw_table("ProfessionSystemTable")
    stage_schools = school_by_stage(schools, stageless, problems)
    spec_schools = spec_schools_of(professions, stage_schools)
    rank1_schools = rank1_schools_of(professions, talent_stages, stage_schools, problems)

    texts = effect_texts_of(sorted(temp_ids), sorted(buff_ids), problems)

    if problems:
        for text in problems:
            print("  問題: %s" % text)
        raise ValueError("装備のテーブルを作れない(問題%d件)" % len(problems))

    main_stats = [entry["MainStat"] for entry in equip_entries.values()]
    print("%-8s装備%d / 突破あり%d / 主能力値%s / なし%d"
          % (PRIMARY_SOURCE, len(equip_entries),
             sum(1 for entry in equip_entries.values() if len(entry["Stages"]) > 1),
             " / ".join("%d %d" % (base, main_stats.count(base)) for base in MAIN_STAT_BASES), main_stats.count(0)))
    print("         庫%s"
          % " / ".join("型%d %d(行%d)" % (lib_type, len(libs), sum(len(rows) for rows in libs.values()))
                       for lib_type, libs in attr_libs.items()))
    print("         シーズン強度%s"
          % " / ".join("%d:%d → %d" % (kind, effect_id, season)
                       for (kind, effect_id), season in sorted(strength_seasons.items())))
    print("         特化%d / クラスR1 %d / 改鋳・レアの庫%d / 文言の一時属性%d / 文言のバフ%d"
          % (len(spec_schools), len(rank1_schools), len(side_refs), len(temp_ids), len(buff_ids)))
    print()

    path = os.path.join(GENERATED, "Equips.json")
    dump(path, {
        "Equips": equip_entries,
        "AttrLibs": {str(lib_type): {str(lib_id): libs[lib_id] for lib_id in sorted(libs)}
                     for lib_type, libs in attr_libs.items()},
        "StrengthSeasons": [{"Kind": kind, "Id": effect_id, "Season": season}
                            for (kind, effect_id), season in sorted(strength_seasons.items())],
        "SpecSchools": {str(key): spec_schools[key] for key in sorted(spec_schools)},
        "Rank1Schools": {str(key): rank1_schools[key] for key in sorted(rank1_schools)},
    })
    print("  %s %d KB" % (os.path.relpath(path, GENERATED), os.path.getsize(path) // 1024))

    write_localized("EquipEffectTexts", texts, [str(i) for i in sorted(temp_ids | buff_ids)])


if __name__ == "__main__":
    main()
