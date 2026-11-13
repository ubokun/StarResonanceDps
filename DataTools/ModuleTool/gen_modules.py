# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.
"""
モジュールツールの表と名前を生成する。

  Data/ModuleTables.json   数値とアイコン(言語に依らない)
  Data/ModuleNames.json    名前(4言語)

ModuleTables.json:
  SlotCount            モジュールの枠の数(`ModHoleTable`の行の数)
  Effects              効果の番号 → アイコンのファイル名と段階ごとの「必要なリンク数・能力スコア」(`ModEffectTable`)
  Modules              モジュールのアイテムID → アイコンのファイル名と品質(`ModTable`の行、`ItemTable`の`Icon`と`Quality`)
  LinkFightValues      データソースごとの「リンクの総和 → 能力スコア」の並び(`ModLinkEffectTable`、添字がリンクの総和)

ModuleNames.json:
  Effects / Modules    キーごとの4言語の名前。効果は`ModEffectTable.EffectName`、モジュールは`ItemTable.Name`

名前でベースにするデータソースは言語フォルダごと(`_common.table`)。数値は、両方のデータソースにあるものが同じでなければならない。

次のときは一覧を出して止める。
- 効果の段階の数値(必要なリンク数・能力スコア)かアイコンがデータソースで違う、効果の段階が0から続いていない
- リンクの総和が0から続いていない、両方のデータソースにあるリンクの総和で能力スコアが違う
- 枠の数がデータソースで違う
- モジュールの行がアイテムの表に無い、アイコンが無い、アイコンか品質が言語で違う
"""
import sys

# _commonを読み込む前に置く(後ろだと__pycache__が作られる)
sys.dont_write_bytecode = True

from _common import LANGS, SOURCES, dump, name_of, per_lang, table, table_of_source, tables_by_source

# 数値を比べる言語フォルダ(データソースごとに1つ)。数値は言語に依らない。
NUMBER_LANG_DIRS = {"Star": "cn", "StarASIA": "jp"}


def file_name(path):
    return (path or "").split("/")[-1]


def build_effects(problems):
    """効果の番号 → アイコンと段階の並び。データソースで違えば問題の一覧に足す。"""
    by_source = {}
    for source in SOURCES:
        rows = table_of_source(source, NUMBER_LANG_DIRS[source], "ModEffectTable")
        if rows is None:
            problems.append("%sにModEffectTableが無い" % source)
            continue
        effects = {}
        for row in rows.values():
            effect = effects.setdefault(str(row["EffectID"]), {"Icon": set(), "Levels": {}})
            effect["Icon"].add(file_name(row["EffectConfigIcon"]))
            effect["Levels"][row["Level"]] = {"Links": row["EnhancementNum"], "FightValue": row["FightValue"]}
        by_source[source] = effects

    result = {}
    for source, effects in by_source.items():
        for effect_id, effect in effects.items():
            icons = sorted(name for name in effect["Icon"] if name)
            if len(icons) != 1:
                problems.append("%s効果%sのアイコンが1つでない: %s" % (source, effect_id, icons))
                continue
            levels = [effect["Levels"][level] for level in sorted(effect["Levels"])]
            if sorted(effect["Levels"]) != list(range(len(levels))):
                problems.append("%s効果%sの段階が0から続いていない: %s" % (source, effect_id, sorted(effect["Levels"])))
                continue
            built = {"Icon": icons[0], "Levels": levels}
            if effect_id in result and result[effect_id] != built:
                problems.append("効果%sの数値かアイコンがデータソースで違う" % effect_id)
                continue
            result[effect_id] = built
    return result


def build_link_fight_values(problems):
    """データソースごとの「リンクの総和 → 能力スコア」の並び。"""
    result = {}
    for source in SOURCES:
        rows = table_of_source(source, NUMBER_LANG_DIRS[source], "ModLinkEffectTable")
        if rows is None:
            problems.append("%sにModLinkEffectTableが無い" % source)
            continue
        values = {row["LinkTime"]: row["FightValue"] for row in rows.values()}
        if sorted(values) != list(range(len(values))):
            problems.append("%sのリンクの総和が0から続いていない" % source)
            continue
        result[source] = [values[link] for link in range(len(values))]

    if len(result) == 2:
        star, asia = result["Star"], result["StarASIA"]
        differ = [link for link in range(min(len(star), len(asia))) if star[link] != asia[link]]
        if differ:
            problems.append("両方のデータソースにあるリンクの総和で能力スコアが違う: %s" % differ)
    return result


def build_slot_count(problems):
    counts = {}
    for source in SOURCES:
        rows = table_of_source(source, NUMBER_LANG_DIRS[source], "ModHoleTable")
        if rows is None:
            problems.append("%sにModHoleTableが無い" % source)
            continue
        counts[source] = len(rows)
    if len(set(counts.values())) != 1:
        problems.append("枠の数がデータソースで違う: %s" % counts)
        return 0
    return next(iter(counts.values()))


def build_modules(problems):
    """モジュールのアイテムID → アイコンと品質、4言語の名前。"""
    module_ids = set()
    for source in SOURCES:
        rows = table_of_source(source, NUMBER_LANG_DIRS[source], "ModTable")
        if rows is None:
            problems.append("%sにModTableが無い" % source)
            continue
        module_ids |= set(rows)

    modules = {}
    names = {lang: {} for lang in LANGS}
    for lang, lang_dir in LANGS.items():
        items = table(lang_dir, "ItemTable")
        for module_id in module_ids:
            item = items.get(module_id)
            if item is None:
                problems.append("%sモジュール%sがアイテムの表に無い" % (lang_dir, module_id))
                continue
            names[lang][module_id] = name_of(item)
            icon = file_name(item.get("Icon"))
            if not icon:
                problems.append("%sモジュール%sのアイコンが無い" % (lang_dir, module_id))
                continue
            built = {"Icon": icon, "Quality": item["Quality"]}
            if modules.setdefault(module_id, built) != built:
                problems.append("モジュール%sのアイコンか品質が言語で違う" % module_id)
    return {module_id: modules[module_id] for module_id in sorted(modules, key=int)}, names


def build_effect_names(effect_ids):
    names = {lang: {} for lang in LANGS}
    for lang, lang_dir in LANGS.items():
        for _, rows in reversed(tables_by_source(lang_dir, "ModEffectTable")):
            for row in rows.values():
                name = name_of(row, "EffectName")
                if name:
                    names[lang][str(row["EffectID"])] = name
    return {effect_id: per_lang(names, effect_id) for effect_id in effect_ids}


def main():
    problems = []
    effects = build_effects(problems)
    link_fight_values = build_link_fight_values(problems)
    slot_count = build_slot_count(problems)
    modules, module_names = build_modules(problems)
    effect_names = build_effect_names(sorted(effects, key=int))

    if problems:
        for text in problems:
            print("  問題: %s" % text)
        raise ValueError("モジュールの表を作れない(%d件)" % len(problems))

    dump("ModuleTables", {
        "SlotCount": slot_count,
        "Effects": {effect_id: effects[effect_id] for effect_id in sorted(effects, key=int)},
        "Modules": modules,
        "LinkFightValues": link_fight_values,
    })
    dump("ModuleNames", {
        "Effects": effect_names,
        "Modules": {module_id: per_lang(module_names, module_id) for module_id in modules},
    })

    print("効果%d / モジュール%d / 枠%d / リンクの表%s"
          % (len(effects), len(modules), slot_count,
             " / ".join("%s 0〜%d" % (source, len(values) - 1) for source, values in link_fight_values.items())))
    for title, names in (("効果", effect_names), ("モジュール", module_names)):
        if title == "モジュール":
            counts = {lang: sum(1 for module_id in modules if names[lang].get(module_id)) for lang in LANGS}
        else:
            counts = {lang: sum(1 for value in names.values() if value[lang]) for lang in LANGS}
        print("  %sの名前: %s" % (title, " / ".join("%s %d" % (lang, count) for lang, count in counts.items())))


if __name__ == "__main__":
    main()
