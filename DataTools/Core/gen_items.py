# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.
"""
アイテムの名前テーブルを生成する。

  Data/Localization/ItemNames.json

キーはアイテムID(`ItemTable`の全行、4言語ぶんの和集合)。値は`ItemTable.Name`。名前の空のキーも残す。
装備IDもアイテムIDなので、装備の名前もここから引く。

名前でベースにするデータソースは言語フォルダごと(`_common.table`)。
"""
import sys

# _commonを読み込む前に置く(後ろだと__pycache__が作られる)
sys.dont_write_bytecode = True

from _common import LANGS, name_of, table, write_localized

ITEM_TABLE = "ItemTable"


def main():
    names = {}
    for lang, lang_dir in LANGS.items():
        items = table(lang_dir, ITEM_TABLE)
        names[lang] = {item_id: name_of(row) for item_id, row in items.items()}
    keys = set().union(*[set(values) for values in names.values()])
    write_localized("ItemNames", names, list(keys))


if __name__ == "__main__":
    main()
