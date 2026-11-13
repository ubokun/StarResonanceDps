# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.
"""
アプリの画像(`StarResonanceDps.App/Data/Images`)のうち、テーブルが使うのに足りないものを表示する。
`--write`でバンドルから取り出して置き、`--compare`で今の画像とゲームの画像を比べる。

必要な画像の決め方はアプリ(`CombatIconResolver`)と同じ。アプリ側を変えたらこちらも合わせる。
- バフ(`Raw/BuffTable.json`に`Overrides/BuffOverrides.json`を当てた後): 優先度が0以外で、まとめる側(`FoldInto`)でないもの。
  `ShowHUDIcon`があればそれ、無ければ`Icon`。`Icon.`で始まる値はアプリのアイコンのキーなので対象外。
  頭が`ui/atlas/mod_effect/`なら`Modules`、`Factors/`なら`Factors`、それ以外は`Buffs`
- `Generated/RootBuffs.json`: 心相晶のノード・上級・タレントの木の節(特化型の根とアビリティの節)は`Talents`、
  因子と因子のアイテムは`Factors`、モジュール効果は`Modules`。知らない項目があれば止まる(画像を持つ項目が増えたときに取りこぼさないため)
- 技(`Skills`): イマジンの技(`Raw/SkillAoyiTable.json`のキー)、メーターの行の技(`Localization/RecountRows.json`と
  `Overrides/RecountRowOverrides.json`のキーの持ち主)の`Raw/SkillTable.json`の`Icon`、`Overrides/SkillActivationOverrides.json`の`Icon`
- スタンプ(`Stickers`): `Raw/ChatStickersTable.json`の種類3・4の`Res`。ゲームの資源`ui/textures/chat_emoji/<Res>`が無い行は対象外。
  言語別の資源`ui/localizetextures/<japanese|korean|english>/chat_emoji/<Res>`がある言語は`Stickers/<ja-JP|ko-KR|en-US>`も要る
- 顔のアイコン(`Avatars`): `Raw/ProfileImageTable.json`の種類1で、ゲームで選べないもの(`IsHide`)と番号0・1(写真の人)を除いたもの

ファイルはフォルダの中をパスの最後の部分で探し、拡張子が無ければ`.png` / `.jpg` / `.jpeg` / `.webp`を試す(アプリと同じ)。

`--before <Dataの写し>`: 差し替える前の`Data`の写しでも同じように必要な画像を決め、前から足りなかったもの(同じフォルダと名前)は件数だけ表示する。
  `--write`と一緒に渡すと、新しく足りなくなったものだけを書く。
`--write`: 足りない画像をバンドルから取り出してPNGで置く。**今あるファイルは上書きしない。**
  取り出す元はデータソースの順に探す(言語の無い画像は`Star`、次に`StarASIA`。スタンプのja-JP / ko-KRは`StarASIA`が先)。
  全体のパスがアドレスならそのテクスチャ、そうでなければファイル名で、テクスチャのアドレスの最後の部分とアトラスの中のスプライトの名前を引く。
  候補が複数で中身が違えば書かずに表示する。見つからない画像は「用意が要る」として表示する。
`--compare`: `Data/Images`と`Assets/Images`の今の画像を、同じ名前のゲームの画像と比べ、同じ大きさで中身が違うものを表示する。
  書き込みはしない。更新したい画像は、そのファイルを消してから`--write`を流す。

`--write`と`--compare`にはPillowが要り、`--compare`にはnumpyも要る。
取り出しや読み込みに失敗した画像があれば、全部表示してから終了の値1で終わる。
"""
import argparse
import os
import sys

# _commonを読み込む前に置く(後ろだと__pycache__が作られる)
sys.dont_write_bytecode = True

from _common import (DATA, SOLUTION, addresses_of, compare_image, extract_images, image_candidates, image_index, load,
                     same_image)

APP = os.path.join(SOLUTION, "StarResonanceDps.App")
IMAGES = os.path.join(APP, "Data", "Images")
ASSET_IMAGES = os.path.join(APP, "Assets", "Images")
IMAGE_EXTENSIONS = (".png", ".jpg", ".jpeg", ".webp")

DEFAULT_SOURCES = ("Star", "StarASIA")
STICKER_TYPES = (3, 4)
STICKER_LANGUAGE_FOLDERS = {"ja-JP": "japanese", "ko-KR": "korean", "en-US": "english"}
STICKER_SOURCES = {"ja-JP": ("StarASIA", "Star"), "ko-KR": ("StarASIA", "Star"), "en-US": ("Star", "StarASIA")}
AVATAR_TYPE = 1
PHOTO_AVATAR_IDS = (0, 1)

# RootBuffs.jsonの項目のうち、画像を持たないもの
ROOT_BUFF_KEYS_WITHOUT_IMAGES = {"Talents", "SeasonTalentAdvancedEffects"}

STATUS_ORDER = ("same", "exporter", "edited", "size")
STATUS_NAMES = {"same": "同じ", "exporter": "書き出しの崩れの範囲で違う", "edited": "中身が違う", "size": "大きさが違う"}


def read(path):
    data = load(path)
    if data is None:
        raise FileNotFoundError("%sが無い" % path)
    return data


def first(*values):
    for value in values:
        if value and value.strip():
            return value.strip()
    return ""


def buff_folder(icon):
    if icon.lower().startswith("ui/atlas/mod_effect/"):
        return "Modules"
    if icon.startswith("Factors/"):
        return "Factors"
    return "Buffs"


def needed(data_dir):
    """`data_dir`(`Data`かその写し)のテーブルが使う画像。(フォルダ, 画像の名前) → {"uses": 使う所, "sources": 探す順}。"""
    raw = os.path.join(data_dir, "Raw")
    need = {}

    def add(folder, icon, use, sources=DEFAULT_SOURCES):
        icon = (icon or "").strip()
        if icon:
            need.setdefault((folder, icon), {"uses": [], "sources": sources})["uses"].append(use)

    rows = {key: dict(row) for key, row in read(os.path.join(raw, "BuffTable.json")).items()}
    for key, entry in read(os.path.join(data_dir, "Overrides", "BuffOverrides.json")).items():
        row = rows.setdefault(key, {})
        for field in ("Icon", "ShowHUDIcon"):
            if (entry.get(field) or "").strip():
                row[field] = "" if entry[field] == "-" else entry[field]
        for field in ("BuffPriority", "FoldInto"):
            if field in entry:
                row[field] = entry[field]
    for key, row in rows.items():
        if row.get("FoldInto") is not None or not row.get("BuffPriority"):
            continue
        icon = first(row.get("ShowHUDIcon"), row.get("Icon"))
        if icon and not icon.startswith("Icon."):
            add(buff_folder(icon), icon, "バフ %s" % key)

    root = read(os.path.join(data_dir, "Generated", "RootBuffs.json"))
    unknown = sorted(set(root) - ROOT_BUFF_KEYS_WITHOUT_IMAGES
                     - {"SeasonTalentNodes", "SeasonTalentAdvanced", "Factors", "FactorItems", "ModuleEffects", "TalentTrees"})
    if unknown:
        raise ValueError("RootBuffs.jsonに知らない項目がある: %s(画像を持つならこのツールに足す)" % unknown)
    for entry in root.get("SeasonTalentNodes", []):
        add("Talents", entry.get("Icon"), "心相晶のノード %s" % entry["Buff"])
    for entry in root.get("SeasonTalentAdvanced", []):
        for template, icon in sorted((entry.get("Icons") or {}).items()):
            add("Talents", icon, "心相晶の上級 %s(型%s)" % (entry["Buff"], template))
    for entry in root.get("Factors", []):
        add("Factors", entry.get("Icon"), "因子 %s" % entry["Buff"])
    for source, items in sorted(root.get("FactorItems", {}).items()):
        for entry in items:
            add("Factors", entry.get("Icon"), "因子のアイテム %s(%s)" % (entry["Item"], source))
    for entry in root.get("ModuleEffects", []):
        add("Modules", entry.get("Icon"), "モジュール効果 %s" % entry["Effect"])
    for source, trees in sorted(root.get("TalentTrees", {}).items()):
        for tree in trees:
            for node in tree["Nodes"]:
                if node["Id"] == tree["Root"] or node.get("Ability"):
                    add("Talents", node.get("Icon"), "タレントの木の節 %s(%s)" % (node["Id"], source))

    skills = read(os.path.join(raw, "SkillTable.json"))

    def skill_icon(skill_id):
        return ((skills.get(str(skill_id)) or {}).get("Icon") or "").strip()

    for skill_id in read(os.path.join(raw, "SkillAoyiTable.json")):
        add("Skills", skill_icon(skill_id), "イマジンの技 %s" % skill_id)
    owners = set()
    for row in read(os.path.join(data_dir, "Localization", "RecountRows.json")).values():
        owners.update(key.split(":")[0] for key in row.get("SourceId") or [])
    owners.update(key.split(":")[0] for key in read(os.path.join(data_dir, "Overrides", "RecountRowOverrides.json")))
    for owner in sorted(owners, key=int):
        if owner in skills:
            add("Skills", skill_icon(owner), "メーターの行の技 %s" % owner)
    for skill_id, entry in read(os.path.join(data_dir, "Overrides", "SkillActivationOverrides.json")).items():
        add("Skills", entry.get("Icon"), "推移グラフの手修正 %s" % skill_id)

    for row in sorted(read(os.path.join(raw, "ChatStickersTable.json")).values(), key=lambda r: r["Id"]):
        if row.get("Type") not in STICKER_TYPES or not (row.get("Res") or "").strip():
            continue
        resource = row["Res"].strip()
        base = "ui/textures/chat_emoji/%s" % resource
        if not any(base in addresses_of(source) for source in DEFAULT_SOURCES):
            continue
        add("Stickers", base, "スタンプ %s" % row["Id"])
        for culture, folder in STICKER_LANGUAGE_FOLDERS.items():
            localized = "ui/localizetextures/%s/chat_emoji/%s" % (folder, resource)
            if any(localized in addresses_of(source) for source in STICKER_SOURCES[culture]):
                add("Stickers/" + culture, localized, "スタンプ %s(%s)" % (row["Id"], culture), STICKER_SOURCES[culture])

    for row in read(os.path.join(raw, "ProfileImageTable.json")).values():
        if row.get("Type") == AVATAR_TYPE and not row.get("IsHide") and row.get("Id") not in PHOTO_AVATAR_IDS:
            add("Avatars", row.get("Image"), "顔のアイコン %s" % row["Id"])
    return need


def file_name(icon):
    return icon.strip().replace("\\", "/").rsplit("/", 1)[-1]


def exists(folder, icon):
    name = file_name(icon)
    names = [name] if os.path.splitext(name)[1] else [name + extension for extension in IMAGE_EXTENSIONS]
    return any(os.path.exists(os.path.join(IMAGES, folder, n)) for n in names)


def uses_text(uses):
    return " / ".join(uses[:3]) + ("(ほか%d)" % (len(uses) - 3) if len(uses) > 3 else "")


def write_missing(targets, need):
    """足りない画像を取り出して置く。失敗の数を返す。"""
    plans = {}
    not_found = []
    for key in targets:
        folder, icon = key
        source, candidates = image_candidates(icon, need[key]["sources"])
        if source is None:
            not_found.append(key)
        else:
            plans[key] = [((source, kind, number, name), address) for kind, address, number, name in candidates]
    results = extract_images([request for pairs in plans.values() for request, _ in pairs])

    written = []
    failed = []
    for key, pairs in sorted(plans.items()):
        folder, icon = key
        images = [results[request] for request, _ in pairs if not isinstance(results[request], str)]
        if not images:
            failed.append((key, "; ".join(results[request] for request, _ in pairs)))
            continue
        if any(not same_image(images[0], image) for image in images[1:]):
            failed.append((key, "候補が複数で中身が違う: %s" % ", ".join("%s %s" % (r[0], a) for r, a in pairs)))
            continue
        path = os.path.join(IMAGES, folder, file_name(icon) + ("" if os.path.splitext(file_name(icon))[1] else ".png"))
        os.makedirs(os.path.dirname(path), exist_ok=True)
        images[0].save(path)
        written.append((key, pairs[0], images[0].size))

    print("書いた %d件" % len(written))
    for (folder, icon), (request, address), size in written:
        print("  %-14s %s ← %s %s(%dx%d)" % (folder, file_name(icon), request[0], address, size[0], size[1]))
    print("バンドルに無い(用意が要る) %d件" % len(not_found))
    for folder, icon in not_found:
        print("  %-14s %s  %s" % (folder, icon, uses_text(need[(folder, icon)]["uses"])))
    if failed:
        print("取り出せなかった %d件" % len(failed))
        for (folder, icon), reason in failed:
            print("  %-14s %s  %s" % (folder, icon, reason))
    return len(failed)


def compare_existing():
    """今の画像を同じ名前のゲームの画像と比べる。読めなかった数を返す。"""
    files = []
    for label, root in (("Data/Images", IMAGES), ("Assets/Images", ASSET_IMAGES)):
        for directory, _, names in os.walk(root):
            for name in sorted(names):
                if name.lower().endswith(IMAGE_EXTENSIONS):
                    files.append((label, os.path.relpath(os.path.join(directory, name), root), os.path.join(directory, name)))

    plans = {}
    for label, relative, path in files:
        stem = os.path.splitext(os.path.basename(relative))[0]
        lookup = stem
        if not any(stem.lower() in image_index(source) for source in DEFAULT_SOURCES):
            # 同じ名前を書き出したときの連番(末尾の「_数字」)を外して引く
            base, _, suffix = stem.rpartition("_")
            if base and suffix.isdigit():
                lookup = base
        pairs = []
        for source in DEFAULT_SOURCES:
            pairs.extend(((source, kind, number, name), address)
                         for kind, address, number, name in image_index(source).get(lookup.lower(), []))
        plans[(label, relative, path)] = pairs
    results = extract_images([request for pairs in plans.values() for request, _ in pairs])

    counts = {}
    listed = []
    unreadable = []
    for (label, relative, path), pairs in sorted(plans.items()):
        top = relative.split(os.sep)[0] if os.sep in relative else "(直下)"
        statuses = []
        for request, address in pairs:
            image = results[request]
            if isinstance(image, str):
                continue
            statuses.append((STATUS_ORDER.index(compare_image(path, image)), 0 if request[0] == "Star" else 1,
                             request[0], address))
        if not statuses:
            status = "notfound" if not pairs else "error"
            if pairs:
                unreadable.append((label, relative, "; ".join(results[r] for r, _ in pairs)))
        else:
            order, _, source, address = min(statuses)
            status = STATUS_ORDER[order]
            if status in ("exporter", "edited"):
                listed.append((label, relative, status, source, address))
        counts.setdefault("%s/%s" % (label, top), {}).setdefault(status, 0)
        counts["%s/%s" % (label, top)][status] += 1

    names = dict(STATUS_NAMES, notfound="ゲームに無い", error="読めない")
    for folder in sorted(counts):
        print("  %-24s %s" % (folder, " / ".join("%s %d" % (names[s], n) for s, n in sorted(counts[folder].items()))))
    print("同じ大きさで中身が違う %d件(更新するなら、ファイルを消して--writeを流す)" % len(listed))
    for label, relative, status, source, address in listed:
        print("  %-13s %-50s %s ← %s %s" % (label, relative, names[status], source, address))
    if unreadable:
        print("読めなかった %d件" % len(unreadable))
        for label, relative, reason in unreadable:
            print("  %-13s %s  %s" % (label, relative, reason))
    return len(unreadable)


def main():
    parser = argparse.ArgumentParser(description="アプリの画像の足りないものを表示し、バンドルから取り出す")
    parser.add_argument("--before", help="差し替える前のDataの写し。前から足りなかった画像は件数だけ表示する")
    parser.add_argument("--write", action="store_true", help="足りない画像をバンドルから取り出して置く(上書きしない)")
    parser.add_argument("--compare", action="store_true", help="今の画像を同じ名前のゲームの画像と比べる(書き込まない)")
    args = parser.parse_args()

    need = needed(DATA)
    missing = sorted(key for key in need if not exists(*key))
    before = needed(args.before) if args.before else {}
    old = [key for key in missing if key in before]
    new = [key for key in missing if key not in before]

    folders = sorted({folder for folder, _ in need})
    print("必要な画像 %d件(%s)" % (len(need), " / ".join("%s %d" % (f, sum(1 for k in need if k[0] == f)) for f in folders)))
    if args.before:
        print("足りない画像 %d件(新しく足りない %d / 前から足りない %d)" % (len(missing), len(new), len(old)))
    else:
        print("足りない画像 %d件" % len(missing))
    for folder, icon in new:
        print("  %-14s %s  %s" % (folder, icon, uses_text(need[(folder, icon)]["uses"])))

    failures = 0
    if args.write:
        failures += write_missing(new, need)
    if args.compare:
        failures += compare_existing()
    if failures:
        sys.exit(1)


if __name__ == "__main__":
    main()
