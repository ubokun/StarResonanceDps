# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.
"""
メーターの行名の表`RecountTable`を、発生源キーで引ける形に変換する。

  Data/Localization/RecountRows.json

**生の`RecountTable`と同じ形にする。**行ごとに`RecountName`(4言語)と、その行に属する発生源キーの一覧。
行の構成はそのまま写す。

**項目名は`SourceId`。`DamageId`にはしない。**中身は`DamageId`そのものではなく
`TypeEnum:枝番`で、生の`DamageId`(`117010101`など)とは別の値だから。
同じ名前にすると、生の表と見比べたときに値が食い違って見える。

```json
{ "3": { "RecountName": { "zh-CN": "…", "en-US": "…", "ja-JP": "…", "ko-KR": "…" }, "SourceId": ["1401:1", "1401:4", "1402:1"] } }
```

こうすると名前をキーごとに繰り返さずに済み、まとめ方の対応表を別に持つ必要もない。
行の所属と行名が同じ1か所に入る。

**発生源キーは`ownerId:枝番`。**ゲーム内メーターは`RecountTable.DamageId`で行を引く。
`DamageId`は通信に乗っていないが、`SyncDamageInfo`の`OwnerId`と`HitEventId`の組が
`DamageId`と1対1で対応する。

  DamageId → DamageAttrTable.TypeEnum = ownerId
  DamageIdの下2桁                    = 枝番(HitEventId)

`TypeEnum`だけをキーにすると粗すぎる。同じ`TypeEnum`の`DamageId`が別の行に入ることがあり、
どちらか一方の行が消える。

**行構成は`Star`。名前はcn / enが`Star`、jp / krが`StarASIA`をベースにする**(`_common.sources_for`)。
`Star`のほうが行構成が新しく収録範囲も広いが、jp/krの`RecountName`列が壊れている(「その他」の行の検算で除く)。

**「その他」の行(其他)は除く。**キーの大半を占め、個別の名前を持たない。除けば表に無いキーと
同じ扱いになり、実行時は名前が空のまま内部ID注記だけが出る。

まとめ方の手修正(`Data/Overrides/RecountRowOverrides.json`)は**手動編集のファイル**で、
このツールは読みも書きもしない。
"""
import collections
import os
import sys

# _commonを読み込む前に置く(後ろだと__pycache__が作られる)
sys.dont_write_bytecode = True

from _common import LANGS, LOCALIZATION, SOURCES, dump, name_of, per_lang, sources_for, table_of_source

# 行構成を決めるデータソース。収録IDが多く、行の統合もこちらが新しい。
STRUCTURE = "Star"

# 「その他」の行。含むキーが突出して多い行として特定し、この名前で検算する。
CATCHALL = {"zh-CN": "其他", "en-US": "Other", "ja-JP": "その他", "ko-KR": "기타"}


def build_rows(source, lang_dir):
    """
    1つのデータソースの表から「発生源キー → 行番号」と「行番号 → キーの一覧」を作る。

    「その他」の行は除かずに含め、その行の番号(キーが一番多い行)と生の`RecountTable`も一緒に返す。
    `RecountTable`か`DamageAttrTable`が無いときはNoneを返す。
    """
    recount = table_of_source(source, lang_dir, "RecountTable")
    damage_attr = table_of_source(source, lang_dir, "DamageAttrTable")
    if recount is None or damage_attr is None:
        return None

    by_row = collections.defaultdict(list)
    row_of = {}
    for row_key in sorted(recount, key=int):
        for damage_id in recount[row_key].get("DamageId") or []:
            attr = damage_attr.get(str(damage_id))
            if not attr:
                continue
            type_enum = str(attr.get("TypeEnum"))
            if not type_enum or type_enum == "None":
                continue
            # 枝番はDamageIdの下2桁。通信のHitEventIdと一致する。
            key = "%s:%d" % (type_enum, int(damage_id) % 100)
            row_of[key] = row_key
            by_row[row_key].append(key)

    catchall = max(by_row, key=lambda k: len(by_row[k]))
    return row_of, by_row, catchall, recount


def name_column(source, lang, lang_dir):
    """
    1つのデータソース×言語の行名を返す。「その他」の行の名前で検算し、失敗したらNone。

    **検算に失敗した列は使わない。**行の中身は正しくても名前列だけがずれていることがあり、
    そのまま採るとIDと名前の対応が丸ごとずれる。
    """
    built = build_rows(source, lang_dir)
    if built is None:
        return None
    _, by_row, catchall, recount = built
    got = name_of(recount[catchall], "RecountName")
    if got != CATCHALL[lang]:
        named = sum(1 for k in recount if name_of(recount[k], "RecountName"))
        print("  ★%-9s %-6s「その他」の行の検算に失敗key=%s name=%r (行%d / 名前あり%d) → この列は使わない"
              % (source, lang, catchall, got, len(recount), named))
        return None
    return {k: name_of(recount[k], "RecountName") for k in recount}


def main():
    base = build_rows(STRUCTURE, LANGS["zh-CN"])
    if base is None:
        raise SystemExit("%sの行構成を組めない" % STRUCTURE)
    row_of, by_row, catchall, _ = base
    row_of = {k: r for k, r in row_of.items() if r != catchall}
    by_row = {r: ks for r, ks in by_row.items() if r != catchall}
    print("行構成%s  キー%d / 行%d (「その他」の行key=%sのキー%d件は除く)"
          % (STRUCTURE, len(row_of), len(by_row), catchall, len(base[1][catchall])))

    # キーの並び順。ownerId → 枝番 の順で数として比べる。
    def as_pair(key):
        owner, branch = key.split(":")
        return int(owner), int(branch)

    # 名前を取るデータソースの行構成。行の対応は共有するキーで取る。
    others = {}
    for source in SOURCES:
        if source == STRUCTURE:
            continue
        built = build_rows(source, LANGS["zh-CN"])
        if built is None:
            continue
        o_row_of, o_by_row, o_catchall, _ = built
        # STRUCTUREの行 → そのデータソースの行。共有するキーが最も多い行に対応づける。
        link = {}
        for r, ks in by_row.items():
            votes = collections.Counter(o_row_of[k] for k in ks if k in o_row_of)
            votes.pop(o_catchall, None)
            if votes:
                link[r] = votes.most_common(1)[0][0]
        others[source] = link
        print("  %-9sの行に対応づいた%sの行%d / %d" % (source, STRUCTURE, len(link), len(by_row)))

    names = {}
    for lang, lang_dir in LANGS.items():
        columns = []
        # 名前を取るデータソース。先にあるほうを優先し、空欄だけ後ろで補う。
        for source in sources_for(lang_dir):
            column = name_column(source, lang, lang_dir)
            if column is None:
                continue
            link = others.get(source)
            columns.append((source, column, link))

        per_row = {}
        for r in by_row:
            text = ""
            for source, column, link in columns:
                # 名前を取るデータソースでは行番号が違う。対応表で読み替える。
                key = r if link is None else link.get(r)
                if key is None:
                    continue
                text = column.get(key) or ""
                if text:
                    break
            per_row[r] = text
        names[lang] = per_row
        print("  %-6s名前あり%3d / %d行" % (lang, sum(1 for v in per_row.values() if v), len(per_row)))

    # 生の`RecountTable`と同じ形で書く。行ごとに4言語の名前と、その行に属する発生源キーの一覧。
    keys_of_row = {r: sorted(ks, key=as_pair) for r, ks in by_row.items()}
    dump(os.path.join(LOCALIZATION, "RecountRows.json"),
         {r: {"RecountName": per_lang(names, r), "SourceId": keys_of_row[r]}
          for r in sorted(by_row, key=int)})

    print("\nrecountsキー%d / 行%d" % (len(row_of), len(by_row)))


if __name__ == "__main__":
    main()
