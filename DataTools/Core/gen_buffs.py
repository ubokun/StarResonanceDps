# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.
"""
バフ名テーブルを生成する。

  Data/Localization/BuffNames.json

**`BuffTable`の全行を収録する。**使い道を決めて絞らない。
バフ/デバフリストに出るバフに加えて、バフ由来の被ダメなどバフ欄に出ないバフも名前を引ける。

キーは4言語の和集合。言語によって収録数に差がある。

未翻訳の行はプレースホルダ(`_common.PLACEHOLDERS`)が入っているので空にする。
プレースホルダ`气刃突刺计数`は先頭行`201`の名前だが、`201`自身も4言語とも中国語のままなので同じく空にする。

前の出力にあって今回空になった名前は`Data/Overrides/BuffNameOverrides.json`へ書き足す(`_common.carry_dropped_names`)。

> **表示条件そのものはこのファイルで判定しない。**
>同梱`Data/Raw/BuffTable.json`の`BuffPriority`で判定する。言語別テーブルで判定すると、
>片方にしか無いバフが特定の表示言語でだけ出なくなる。
"""
import sys

# _commonを読み込む前に置く(後ろだと__pycache__が作られる)
sys.dont_write_bytecode = True

from _common import LANGS, name_of, table, write_localized


def main():
    tables = {lang: table(lang_dir, "BuffTable") for lang, lang_dir in LANGS.items()}

    keys = set()
    for lang, buff_table in tables.items():
        keys |= set(buff_table)
        print("%-6s BuffTable %5d" % (lang, len(buff_table)))
    print("→ 和集合%d" % len(keys))

    values = {lang: {i: name_of(t.get(i)) for i in keys} for lang, t in tables.items()}
    print("")
    write_localized("BuffNames", values, keys, dropped_names_overrides="BuffNameOverrides")


if __name__ == "__main__":
    main()
