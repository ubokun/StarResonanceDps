# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.
"""
手動編集ファイル(`Data/Overrides`)のうち、元のテーブルや生成物と同じ値になっていて要らない項目を表示する。書き込みはしない。

入力はリポジトリの`Data`だけ(`Raw/BuffTable.json`・`Raw/SkillTable.json`・`Localization/RecountRows.json`・`Overrides`)。
`Raw`を差し替えて`gen_recounts.py`を流した後に使う。

比べる項目と、要らないと判断する条件:
- `BuffOverrides.json`
  - `BuffType`: テーブルと同じ値
  - `BuffPriority`: テーブルと「0か0以外か」が同じ(アプリは0かどうかだけを見る)
  - `Icon` / `ShowHUDIcon`: 種類とファイル名がテーブルと同じ。アプリはファイルをパスの最後の部分で探し、
    頭は種類(`Icon.` = アプリのアイコンのキー、`Factors/`、`ui/atlas/mod_effect/`)の判定にだけ使う
- `SkillOverrides.json`: `SkillLevelGroup`がテーブルと同じ値
- `RecountRowOverrides.json`の`Row`
  - 行へ入れる`Row`: 全部の手修正を当てたときの行の分け方と、その項目だけ外したときの行の分け方が同じ
    (同じ行のほかのキーへの影響も含む)。当て方はアプリと同じで、生成物の行から始め、`"Row": null`を先に当て、
    次に`"Row": "<キー>"`を1キーずつ当て、`Name`だけでどの行にも無いキーをそのキーだけの行にする
  - `"Row": null`: 生成物でそのキーが1つだけの行にある(どの行にも無い)。同じ行のキーを全部外す組は、
    どれか1つを外さなくても結果が同じになるが、意図した組として要るに数える

比べないもの:
- 名前(4言語で1組として書いているため)
- `SkillActivationOverrides.json`(通常攻撃の2段目以降などに、名前とアイコンを意図して全部書いているため)
- `BuffOverrides.json`の`FoldInto`(テーブルに無い項目なので常に要る)

アプリのアイコンの探し方・行の当て方を写しているので、アプリ側を変えたらこちらも合わせる。
読むファイルが無ければ止まる。見つかっても終了の値は0。
"""
import os
import sys

# _commonを読み込む前に置く(後ろだと__pycache__が作られる)
sys.dont_write_bytecode = True

from _common import DATA, LOCALIZATION, OVERRIDES, load

RAW = os.path.join(DATA, "Raw")


def read(path):
    data = load(path)
    if data is None:
        raise FileNotFoundError("%sが無い" % path)
    return data


def icon_key(path):
    """アイコンの値を、種類とファイル名の組にする。空と`"-"`は空の組。"""
    text = (path or "").strip().replace("\\", "/")
    if not text or text == "-":
        return ("", "")
    kind = ("key" if text.startswith("Icon.") else "factor" if text.startswith("Factors/")
            else "module" if text.lower().startswith("ui/atlas/mod_effect/") else "file")
    return (kind, text.rsplit("/", 1)[-1])


def key_order(key):
    owner, branch = key.split(":")
    return int(owner), int(branch)


def remove(groups, group_of, key):
    group = group_of.pop(key, None)
    if group is None:
        return
    members = groups[group]
    members.discard(key)
    if not members:
        del groups[group]
    elif group == key:
        renamed = min(members, key=key_order)
        groups[renamed] = groups.pop(group)
        for member in members:
            group_of[member] = renamed


def join(groups, group_of, key, target):
    remove(groups, group_of, key)
    if target not in group_of:
        groups[target] = {target}
        group_of[target] = target
    group = group_of[target]
    groups[group].add(key)
    group_of[key] = group


def row_groups(rows, overrides):
    """生成物の行に手修正の`Row`を当てた行の分け方(行の識別子 → キーの集合)。"""
    groups = {}
    group_of = {}
    for row in rows.values():
        head = None
        for key in row.get("SourceId") or []:
            if head is None:
                head = key
                groups[key] = {key}
                group_of[key] = key
            else:
                join(groups, group_of, key, head)
    for key, entry in overrides.items():
        if "Row" in entry and entry["Row"] is None:
            remove(groups, group_of, key)
            groups[key] = {key}
            group_of[key] = key
    for key, entry in overrides.items():
        if "Row" in entry and entry["Row"] is not None:
            join(groups, group_of, key, entry["Row"])
    for key, entry in overrides.items():
        if "Row" not in entry and entry.get("Name") and key not in group_of:
            groups[key] = {key}
            group_of[key] = key
    return {frozenset(members) for members in groups.values()}


def check_buffs(found):
    buffs = read(os.path.join(RAW, "BuffTable.json"))
    for key, entry in read(os.path.join(OVERRIDES, "BuffOverrides.json")).items():
        row = buffs.get(key, {})
        if "BuffType" in entry and row.get("BuffType") == entry["BuffType"]:
            found.append(("BuffOverrides", key, "BuffType", entry["BuffType"], row.get("BuffType")))
        if ("BuffPriority" in entry and row.get("BuffPriority") is not None
                and (row["BuffPriority"] == 0) == (entry["BuffPriority"] == 0)):
            found.append(("BuffOverrides", key, "BuffPriority", entry["BuffPriority"], row["BuffPriority"]))
        for field in ("Icon", "ShowHUDIcon"):
            if field in entry and icon_key(row.get(field))[1] and icon_key(row.get(field)) == icon_key(entry[field]):
                found.append(("BuffOverrides", key, field, entry[field], row.get(field)))


def check_skills(found):
    skills = read(os.path.join(RAW, "SkillTable.json"))
    for key, entry in read(os.path.join(OVERRIDES, "SkillOverrides.json")).items():
        group = skills.get(key, {}).get("SkillLevelGroup")
        if "SkillLevelGroup" in entry and group == entry["SkillLevelGroup"]:
            found.append(("SkillOverrides", key, "SkillLevelGroup", entry["SkillLevelGroup"], group))


def check_recount_rows(found):
    rows = read(os.path.join(LOCALIZATION, "RecountRows.json"))
    overrides = read(os.path.join(OVERRIDES, "RecountRowOverrides.json"))
    members_of = {key: row.get("SourceId") or [] for row in rows.values() for key in row.get("SourceId") or []}
    everything = row_groups(rows, overrides)
    for key, entry in overrides.items():
        if "Row" not in entry:
            continue
        if entry["Row"] is None:
            if len(members_of.get(key, [key])) == 1:
                found.append(("RecountRowOverrides", key, "Row", None, "生成物で1つだけの行"))
            continue
        without = {k: dict(v) for k, v in overrides.items()}
        del without[key]["Row"]
        if row_groups(rows, without) == everything:
            found.append(("RecountRowOverrides", key, "Row", entry["Row"], "外しても行の分け方が同じ"))


def main():
    found = []
    check_buffs(found)
    check_skills(found)
    check_recount_rows(found)
    print("要らない項目 %d件" % len(found))
    for file_name, key, field, value, original in found:
        print("  %-20s %-14s %-16s 手修正 %s / 元 %s" % (file_name, key, field, value, original))


if __name__ == "__main__":
    main()
