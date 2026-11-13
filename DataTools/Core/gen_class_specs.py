# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.
"""
職業の特化の名前テーブルを生成する。

  Data/Localization/ClassSpecNames.json

キーはアプリの特化の番号(職業ID × 10000＋ 特化の並び)。値は特化の名前から、全特化の名前に共通する語尾を取り除いたもの。

職業ごとの特化は`ProfessionSystemTable.ShowTalentStage`が並びつきで持つ(1番目が末尾01、2番目が末尾02の番号)。
その段階の行`TalentStageTable`の`Name`は[段階の名前, 特化の名前]で、2番目を使う。

**ベースにするデータソースはキーの単位で決める**(cn / enは`Star`、jp / krは`StarASIA`)。ベースにそのキーの名前があればベースだけを使い、
無いときだけもう一方を使う(`gen_season_talents.py`と同じ)。

次のときは一覧を出して止める。
- 職業の段階の行が段階の表に無い
- 段階の行の名前が2要素でない
- 使う名前が、その言語の共通の語尾で終わっていない
- データソースに段階の表が無い
"""
import sys

# _commonを読み込む前に置く(後ろだと__pycache__が作られる)
sys.dont_write_bytecode = True

from _common import LANGS, named, tables_by_source, write_localized

PROFESSION_TABLE = "ProfessionSystemTable"
STAGE_TABLE = "TalentStageTable"
# 特化の番号は 職業ID × これ ＋ 特化の並び(1始まり)。アプリの特化の番号と同じ決め方。
PROFESSION_DIGIT = 10000
# 言語フォルダごとの、全特化の名前に共通する語尾。名前から取り除く。
# アプリのリソースClassSpec_NameSuffixと同じ値(アプリが語尾を付け直す表示がある)。変えるときは両方を直す。
COMMON_SUFFIX = {"cn": "流", "en": " Spec", "jp": "型", "kr": " 계열"}


def names_of(label, professions, stages, problems):
    """1つのデータソースの表から「特化の番号 → 特化の名前(語尾つきのまま)」を作る。名前が無ければ空。"""
    found = {}
    for profession_id, profession in professions.items():
        for index, stage_id in enumerate(profession.get("ShowTalentStage") or []):
            stage = stages.get(str(stage_id))
            if stage is None:
                problems.append("%s職業%sの段階%sが%sに無い" % (label, profession_id, stage_id, STAGE_TABLE))
                continue
            names = stage.get("Name") or []
            if len(names) != 2:
                problems.append("%s職業%sの段階%sの名前が2要素でない: %s" % (label, profession_id, stage_id, names))
                continue
            name = (names[1] or "").strip()
            found[str(int(profession_id) * PROFESSION_DIGIT + index + 1)] = name if named(name) else ""
    return found


def main():
    values = {lang: {} for lang in LANGS}
    problems = []
    for lang, lang_dir in LANGS.items():
        suffix = COMMON_SUFFIX[lang_dir]
        stages_by_source = dict(tables_by_source(lang_dir, STAGE_TABLE))
        # 名前を採るデータソースが決まったキー。語尾の検査を通らなくても、後のデータソースで埋めない。
        decided = set()
        counts = []
        for source_name, professions in tables_by_source(lang_dir, PROFESSION_TABLE):
            label = "%s %s" % (lang_dir, source_name)
            stages = stages_by_source.get(source_name)
            if stages is None:
                problems.append("%sに%sが無い" % (label, STAGE_TABLE))
                continue
            found = names_of(label, professions, stages, problems)
            for key, name in found.items():
                # 先のデータソース(ベース)で名前が決まったキーには、後のデータソースを混ぜない。
                if key in decided or not name:
                    continue
                decided.add(key)
                if not name.endswith(suffix):
                    problems.append("%s特化%sの名前「%s」が共通の語尾「%s」で終わらない" % (label, key, name, suffix))
                    continue
                values[lang][key] = name[:-len(suffix)].strip()
            counts.append("%s %d" % (source_name, len(found)))
        print("%-6s特化%3d(%s)" % (lang, len(values[lang]), " / ".join(counts)))

    if problems:
        for text in problems:
            print("  問題: %s" % text)
        raise ValueError("%sから特化の名前を作れない(%d件)" % (STAGE_TABLE, len(problems)))

    keys = set()
    for lang in LANGS:
        keys |= set(values[lang])

    print("→ 4言語の和集合%d件" % len(keys))
    print()

    write_localized("ClassSpecNames", values, keys)


if __name__ == "__main__":
    main()
