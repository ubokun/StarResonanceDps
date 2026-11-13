# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.
"""
モンスターの名前テーブルを生成する。

  Data/Localization/MonsterNames.json

エンティティリストや被ダメログで、モンスターの実体の`AttrId`から表示名を引くのに使う。

キーは**`MonsterTable`**の行(4言語ぶんの和集合)。
`DummyTable`と`NpcTable`は混ぜない。同じ番号が別の表で別のものを指すことがあり、
どの表の名前かは実体の種類でしか決まらない。

`MonsterTable`は行があっても`Name`が空のことがあり、その数は言語で大きく違う。

前の出力にあって今回空になった名前は`Data/Overrides/MonsterNameOverrides.json`へ書き足す(`_common.carry_dropped_names`)。
"""
import sys

# _commonを読み込む前に置く(後ろだと__pycache__が作られる)
sys.dont_write_bytecode = True

from _common import LANGS, name_of, table, write_localized

SOURCE_TABLE = "MonsterTable"


def main():
    tables = {lang: table(lang_dir, SOURCE_TABLE) for lang, lang_dir in LANGS.items()}

    keys = set()
    for lang, source in tables.items():
        keys |= set(source)
        print("%-6s %s %5d件" % (lang, SOURCE_TABLE, len(source)))
    print("→ 4言語の和集合%d件\n" % len(keys))

    values = {}
    for lang, source in tables.items():
        values[lang] = {key: name_of(source.get(key)) for key in keys}

    write_localized("MonsterNames", values, keys, dropped_names_overrides="MonsterNameOverrides")


if __name__ == "__main__":
    main()
