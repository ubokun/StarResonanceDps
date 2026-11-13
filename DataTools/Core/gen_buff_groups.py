# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.
"""
料理・薬剤のバフの名前テーブルを生成する。

  Data/Localization/CuisineBuffs.json
  Data/Localization/PotionBuffs.json

料理・薬剤は使い直すたびに別のIDへ入れ替わるので、アプリは個別のIDではなくまとまりとして扱う。
この2つにあるバフが、それぞれ料理・薬剤のまとまりになる。形はほかの名前テーブルと同じ。

**選ぶ条件は`BuffTable`の2つの項目だけ。名前は見ない。**

- `Tags`に消耗品のタグを持つ
- アイコン(`ShowHUDIcon`、空なら`Icon`)が料理のアイコンなら料理、薬剤のアイコンなら薬剤

アイコンだけでは、料理・薬剤でないバフも同じアイコンを使っていて混ざる。タグを併せると料理・薬剤だけになる。

**名前はそのバフを付けるアイテムの名前。**バフ自身の名前は料理・薬剤の1語だけ。
料理の表(`CookCuisineTable`)・薬剤の表(`ChemistryCuisineTable`)の行の`Id`がアイテムの`Id`で、`BuffPar`の各要素の先頭がそのアイテムが付けるバフ。

- 付けるアイテムが1つなら、その名前
- 複数なら、アイテムの`Id`の順に` / `でつなぐ(どのアイテムで付いたかはバフからは決まらない)
- 無ければ、バフ自身の名前

言語ごとに、つなぐ名前が1つでも空なら、その言語は空にする(表示時にzh-CNにフォールバックする)。

**出力は`Star`で作る。**次のときは書かずに止まる。

- `StarASIA`にだけある料理・薬剤のバフがある、料理と薬剤が入れ替わっている
- 料理・薬剤の表に、`StarASIA`にだけある行か、付けるバフが`Star`と違う行がある
- 料理・薬剤の表の行が、アイテムの表に無い
"""
import sys

# _commonを読み込む前に置く(後ろだと__pycache__が作られる)
sys.dont_write_bytecode = True

from _common import LANGS, SOURCES, TABLES_ENV, name_of, table, table_of_source, write_localized

# 出力を作るデータソース。ほかのデータソースの料理・薬剤を全部含んでいることを実行のたびに確かめる。
PRIMARY_SOURCE = "Star"

# 選ぶ条件を読む言語フォルダ。タグとアイコンは言語で変わらない。
SELECTION_LANG_DIR = "cn"

CONSUMABLE_TAG = 100

# アイコン名に含まれる語 → まとまり。
GROUP_ICONS = (("buff_food_up", "Cuisine"), ("buff_agentia_up", "Potion"))

# アイテムが付けるバフを持つ表。行のIdがアイテムのId。
ITEM_BUFF_TABLES = ("CookCuisineTable", "ChemistryCuisineTable")

# 付けるアイテムが複数あるときの区切り。
NAME_SEPARATOR = " / "

OUTPUTS = (("Cuisine", "CuisineBuffs"), ("Potion", "PotionBuffs"))


def raw_table(source, name):
    found = table_of_source(source, SELECTION_LANG_DIR, name)
    if found is None:
        raise FileNotFoundError(
            "%sの%s/%sが無い。入力の置き場は環境変数%sか、_common.pyのTABLES_DIRで設定する"
            % (source, SELECTION_LANG_DIR, name, TABLES_ENV))
    return found


def icon_of(row):
    return (row.get("ShowHUDIcon") or "").strip() or (row.get("Icon") or "").strip()


def group_of(buff_id, row):
    """料理なら "Cuisine"、薬剤なら "Potion"、どちらでもなければNone。"""
    if CONSUMABLE_TAG not in (row.get("Tags") or []):
        return None
    icon = icon_of(row).lower()
    groups = [group for marker, group in GROUP_ICONS if marker in icon]
    if len(groups) > 1:
        raise ValueError("バフ%dのアイコン%sが料理と薬剤の両方に該当する" % (buff_id, icon))
    return groups[0] if groups else None


def groups_of(source):
    """そのデータソースの料理・薬剤のバフ。バフID → まとまり。"""
    result = {}
    for row in raw_table(source, "BuffTable").values():
        buff_id = int(row["Id"])
        group = group_of(buff_id, row)
        if group is not None:
            result[buff_id] = group
    print("%-8s料理%d / 薬剤%d"
          % (source,
             sum(1 for group in result.values() if group == "Cuisine"),
             sum(1 for group in result.values() if group == "Potion")))
    return result


def item_buffs_of(source):
    """そのデータソースの料理・薬剤の表の、アイテムID → 付けるバフIDの組(表の名前 → {アイテムID: (バフID, ...)})。"""
    return {name: {int(row["Id"]): tuple(par[0] for par in row.get("BuffPar") or [] if par)
                   for row in raw_table(source, name).values()}
            for name in ITEM_BUFF_TABLES}


def items_by_buff(primary_group):
    """
    バフID → そのバフを付けるアイテムIDの一覧(昇順)。`Star`の料理・薬剤の表から作る。
    `StarASIA`の表の行が全部`Star`にあって付けるバフも同じこと、行がアイテムの表にあることを確かめる。
    """
    found = {source: item_buffs_of(source) for source in SOURCES}
    primary = found[PRIMARY_SOURCE]
    for source, tables in found.items():
        if source == PRIMARY_SOURCE:
            continue
        for name, rows in tables.items():
            extra = sorted(item for item in rows if item not in primary[name])
            if extra:
                raise ValueError("%sの%sにだけある行が%d件: %s" % (source, name, len(extra), extra))
            differ = sorted(item for item, buffs in rows.items() if primary[name][item] != buffs)
            if differ:
                raise ValueError("%sと%sの%sで付けるバフが違う行: %s" % (source, PRIMARY_SOURCE, name, differ))
        print("%-8s料理・薬剤の表%sに無い行0 / 付けるバフの違い0" % (source, PRIMARY_SOURCE))

    items = raw_table(PRIMARY_SOURCE, "ItemTable")
    result = {}
    for name, rows in primary.items():
        missing = sorted(item for item in rows if str(item) not in items)
        if missing:
            raise ValueError("%sの%sの行がアイテムの表に無い: %s" % (PRIMARY_SOURCE, name, missing))
        for item, buffs in rows.items():
            for buff_id in buffs:
                if buff_id in primary_group:
                    result.setdefault(buff_id, set()).add(item)
    return {buff_id: sorted(found_items) for buff_id, found_items in result.items()}


def names_of(buff_ids, items_of):
    """言語 → {バフID: 名前}。アイテムの名前をつなぐ。アイテムが無ければバフ自身の名前。"""
    names = {}
    for lang, lang_dir in LANGS.items():
        item_table = table(lang_dir, "ItemTable")
        buff_table = table(lang_dir, "BuffTable")
        by_buff = {}
        for buff_id in buff_ids:
            found_items = items_of.get(buff_id)
            if not found_items:
                by_buff[str(buff_id)] = name_of(buff_table.get(str(buff_id)))
                continue
            parts = [name_of(item_table.get(str(item))) for item in found_items]
            by_buff[str(buff_id)] = NAME_SEPARATOR.join(parts) if all(parts) else ""
        names[lang] = by_buff
    return names


def main():
    found = {PRIMARY_SOURCE: groups_of(PRIMARY_SOURCE)}
    for source in SOURCES:
        if source != PRIMARY_SOURCE:
            found[source] = groups_of(source)

    primary = found[PRIMARY_SOURCE]
    for source, buffs in found.items():
        if source == PRIMARY_SOURCE:
            continue
        extra = sorted(b for b in buffs if b not in primary)
        if extra:
            raise ValueError("%sにだけある料理・薬剤のバフが%d件: %s(%sで作ると取りこぼす)"
                             % (source, len(extra), extra, PRIMARY_SOURCE))
        moved = sorted(b for b, group in buffs.items() if primary[b] != group)
        if moved:
            raise ValueError("%sと%sで料理と薬剤が入れ替わっているバフ: %s" % (source, PRIMARY_SOURCE, moved))
        print("%-8s %sに無いもの0 / 入れ替わり0" % (source, PRIMARY_SOURCE))

    items_of = items_by_buff(primary)
    names = names_of(sorted(primary), items_of)

    print("")
    for group, basename in OUTPUTS:
        buff_ids = [b for b in sorted(primary) if primary[b] == group]
        counts = [len(items_of.get(b, ())) for b in buff_ids]
        print("  %s: アイテム0: %d / 1: %d / 複数: %d"
              % (basename, counts.count(0), counts.count(1), sum(1 for c in counts if c > 1)))
        write_localized(basename, names, [str(b) for b in buff_ids])


if __name__ == "__main__":
    main()
