# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.
"""
シーズンの名前と、シーズンランクの名前のテーブルを生成する。

  Data/Localization/SeasonNames.json
  Data/Localization/SeasonRankNames.json

SeasonNames: キーはシーズン番号。値は`AchievementSeasonClassTable`のうち`Type`がそのシーズン番号の行で、
`SortID`が一番小さい行の`ClassName`(シーズン実績の1枠目の分類名)。`Type` 0はシーズンに属さない分類なので入れない。

SeasonRankNames: キーは シーズン番号 × 100＋ 段階(`SeasonRankTable.RankToLevel`)。値は、その段階の行のうち
`StarLevel`が一番小さい行の`Name`。同じ段階の中で名前が分かれていれば一覧を出す(★が一番小さい行を採る)。

**ベースにするデータソースはキーの単位で決める**(cn / enは`Star`、jp / krは`StarASIA`)。ベースにそのキーの名前があればベースだけを使い、
無いときだけもう一方を使う(`gen_season_talents.py`と同じ)。

次のときは一覧を出して止める。
- シーズンの分類で、`SortID`が一番小さい行が2行以上ある
- 段階の行で、`StarLevel`が一番小さい行が2行以上ある
- 段階が整数でないか、キーの桁に収まらない(0未満か100以上)、シーズン番号が1未満
"""
import sys

# _commonを読み込む前に置く(後ろだと__pycache__が作られる)
sys.dont_write_bytecode = True

from _common import LANGS, name_of, tables_by_source, write_localized

CLASS_TABLE = "AchievementSeasonClassTable"
RANK_TABLE = "SeasonRankTable"
# 段階のキーは シーズン番号 × これ ＋ 段階。
RANK_DIGIT = 100


def season_names_of(label, rows, problems):
    """1つのデータソースの表から「シーズン番号 → 1枠目の分類名」を作る。名前が無ければ空。"""
    rows_by_season = {}
    for row in rows.values():
        season = row.get("Type") or 0
        if season <= 0:
            continue
        rows_by_season.setdefault(season, []).append(row)

    found = {}
    for season, season_rows in rows_by_season.items():
        first_sort = min(row.get("SortID") or 0 for row in season_rows)
        firsts = [row for row in season_rows if (row.get("SortID") or 0) == first_sort]
        if len(firsts) != 1:
            problems.append("%sシーズン%dのSortID %dの行が%d行: %s"
                            % (label, season, first_sort, len(firsts), [row.get("Id") for row in firsts]))
            continue
        found[str(season)] = name_of(firsts[0], "ClassName")
    return found


def rank_names_of(label, rows, problems, splits):
    """1つのデータソースの表から「シーズン番号 × 100＋ 段階 → ★が一番小さい行の名前」を作る。名前が無ければ空。"""
    rows_by_tier = {}
    for row in rows.values():
        rows_by_tier.setdefault((row.get("SeasonId") or 0, row.get("RankToLevel")), []).append(row)

    found = {}
    for (season, tier), tier_rows in rows_by_tier.items():
        if season < 1 or not isinstance(tier, int) or tier < 0 or tier >= RANK_DIGIT:
            problems.append("%sシーズン%s段階%sはキーにできない: %s"
                            % (label, season, tier, [row.get("Id") for row in tier_rows]))
            continue
        first_star = min(row.get("StarLevel") or 0 for row in tier_rows)
        firsts = [row for row in tier_rows if (row.get("StarLevel") or 0) == first_star]
        if len(firsts) != 1:
            problems.append("%sシーズン%d段階%dの ★%dの行が%d行: %s"
                            % (label, season, tier, first_star, len(firsts), [row.get("Id") for row in firsts]))
            continue
        key = str(season * RANK_DIGIT + tier)
        if len({name_of(row) for row in tier_rows}) > 1:
            listing = " / ".join("★%s「%s」" % (row.get("StarLevel"), name_of(row))
                                 for row in sorted(tier_rows, key=lambda row: row.get("StarLevel") or 0))
            splits.append("%sキー%s(シーズン%d段階%d): %s → ★%dを採る" % (label, key, season, tier, listing, first_star))
        found[key] = name_of(firsts[0])
    return found


def collect(table_name, build):
    """言語ごとにデータソースをベースから順に読み、キーごとに先のデータソースの名前を採る。"""
    values = {lang: {} for lang in LANGS}
    for lang, lang_dir in LANGS.items():
        counts = []
        for source_name, rows in tables_by_source(lang_dir, table_name):
            found = build("%s %s" % (lang_dir, source_name), rows)
            for key, name in found.items():
                # 先のデータソース(ベース)で名前が決まったキーには、後のデータソースを混ぜない。
                if values[lang].get(key) or not name:
                    continue
                values[lang][key] = name
            counts.append("%s %d" % (source_name, len(found)))
        print("%-6s %s %3d(%s)" % (lang, table_name, len(values[lang]), " / ".join(counts)))
    return values


def main():
    problems = []
    splits = []

    season_values = collect(CLASS_TABLE, lambda label, rows: season_names_of(label, rows, problems))
    rank_values = collect(RANK_TABLE, lambda label, rows: rank_names_of(label, rows, problems, splits))

    if problems:
        for text in problems:
            print("  構造の問題: %s" % text)
        raise ValueError("シーズンの名前を作れない(構造の問題%d件)" % len(problems))

    print("  同じ段階で名前が分かれる%d件" % len(splits))
    for text in splits:
        print("    %s" % text)
    print()

    for basename, values in (("SeasonNames", season_values), ("SeasonRankNames", rank_values)):
        keys = set()
        for lang in LANGS:
            keys |= set(values[lang])
        write_localized(basename, values, keys)


if __name__ == "__main__":
    main()
