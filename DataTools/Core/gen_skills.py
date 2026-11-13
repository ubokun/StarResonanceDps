# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.
"""
スキル名テーブルを生成する。

  Data/Localization/SkillNames.json      (`SkillTable.Name`)
  Data/Localization/SkillTalkNames.json  (`SkillTable.SkillTalk`)

**`SkillTable`の全行を収録する。**使い道を決めて絞らない。
スキル枠(イマジン・ロール)に加えて、モンスターの技など枠に置かれないスキルも名前を引ける。

`SkillTalk`は公式の技名で、`Name`より優先する。適用する順はアプリが読み込むときに決める
(`SkillNames`に`SkillNameOverrides`を適用し、その上に`SkillTalkNames`を適用する)。

キーはデータソースと言語の和集合。版によってどれか1つにしか無い行でも除かない。

未翻訳の行はプレースホルダ(`_common.PLACEHOLDERS`)が入っているので空にする。
ただしプレースホルダと同じ文字列を本物の名前として持つ行がある(`GENUINE_PLACEHOLDER_NAME_IDS`。`Name`だけ)。

`SkillNames`で前の出力にあって今回空になった名前は`Data/Overrides/SkillNameOverrides.json`へ書き足す
(`_common.carry_dropped_names`)。`SkillTalkNames`には上書きファイルが無いので書き足さない。
"""
import sys

# _commonを読み込む前に置く(後ろだと__pycache__が作られる)
sys.dont_write_bytecode = True

from _common import LANGS, name_of, table, write_localized

# プレースホルダと同じ文字列が本物の名前になっている行。
# 1101は連番の「フィールドマーク」01〜06の先頭で、他言語には訳語が入っている。
# プレースホルダとして空にするとzh-CNだけ名前が消え、表示時のフォールバック先も失う。
GENUINE_PLACEHOLDER_NAME_IDS = {"1101"}


def skill_name(skill_id, row):
    if skill_id in GENUINE_PLACEHOLDER_NAME_IDS:
        return ((row or {}).get("Name") or "").strip()
    return name_of(row)


def main():
    tables = {lang: table(lang_dir, "SkillTable") for lang, lang_dir in LANGS.items()}

    keys = set()
    for lang, skill_table in tables.items():
        keys |= set(skill_table)
        print("%-6s行%d" % (lang, len(skill_table)))
    print("→ 和集合%d" % len(keys))
    print("")

    values = {lang: {i: skill_name(i, t.get(i)) for i in keys} for lang, t in tables.items()}
    write_localized("SkillNames", values, keys, dropped_names_overrides="SkillNameOverrides")

    talks = {lang: {i: name_of(t.get(i), "SkillTalk") for i in keys} for lang, t in tables.items()}
    write_localized("SkillTalkNames", talks, keys)


if __name__ == "__main__":
    main()
