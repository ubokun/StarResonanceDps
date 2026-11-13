# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.
"""
モジュールツールの画像(`StarResonanceDps.Plugins.ModuleTool/Assets`)のうち、表が使うのに足りないものを表示する。
`--write`でバンドルから取り出して置き、`--compare`で今の画像とゲームの画像を比べる。

必要な画像は`Data/ModuleTables.json`(`gen_modules.py`の出力)から決める(プラグインの読み込みと同じ)。
- 効果のアイコン(`Effects`の`Icon`)とモジュールのアイコン(`Modules`の`Icon`)
- 品質の背景`item_quality_<品質>`(`Modules`の`Quality`に出てくる品質の全部)
ファイルは`Assets/<名前>.png`。

`--before <Dataの写し>`: 差し替える前のプラグインの`Data`の写しでも同じように必要な画像を決め、前から足りなかったものは件数だけ表示する。
  `--write`と一緒に渡すと、新しく足りなくなったものだけを書く。
`--write`: 足りない画像をバンドルから取り出してPNGで置く。**今あるファイルは上書きしない。**
  `Star`、次に`StarASIA`の順に、ファイル名でテクスチャのアドレスの最後の部分とアトラスの中のスプライトの名前を引く。
  候補が複数で中身が違えば書かずに表示する。見つからない画像は「用意が要る」として表示する。
`--compare`: `Assets`の今の画像を、同じ名前のゲームの画像と比べ、同じ大きさで中身が違うものを表示する。
  書き込みはしない。更新したい画像は、そのファイルを消してから`--write`を流す。

`--write`と`--compare`にはPillowが要り、`--compare`にはnumpyも要る。
画像はプラグインのDLLに埋め込まれるので、書いたらプラグインをビルドし直す。
取り出しや読み込みに失敗した画像があれば、全部表示してから終了の値1で終わる。
"""
import argparse
import os
import sys

# _commonを読み込む前に置く(後ろだと__pycache__が作られる)
sys.dont_write_bytecode = True

from _common import ASSETS, DATA, compare_image, extract_images, image_candidates, image_index, load, same_image

SOURCES_ORDER = ("Star", "StarASIA")
QUALITY_IMAGE = "item_quality_%d"
STATUS_ORDER = ("same", "exporter", "edited", "size")
STATUS_NAMES = {"same": "同じ", "exporter": "書き出しの崩れの範囲で違う", "edited": "中身が違う", "size": "大きさが違う",
                "notfound": "ゲームに無い", "error": "読めない"}


def needed(data_dir):
    """`data_dir`(プラグインの`Data`かその写し)の表が使う画像。名前 → 使う所。"""
    path = os.path.join(data_dir, "ModuleTables.json")
    tables = load(path)
    if tables is None:
        raise FileNotFoundError("%sが無い。先にgen_modules.pyを流す" % path)
    need = {}

    def add(name, use):
        name = (name or "").strip()
        if name:
            need.setdefault(name, []).append(use)

    for effect_id, effect in tables["Effects"].items():
        add(effect.get("Icon"), "効果 %s" % effect_id)
    for module_id, module in tables["Modules"].items():
        add(module.get("Icon"), "モジュール %s" % module_id)
    for quality in sorted({module["Quality"] for module in tables["Modules"].values()}):
        add(QUALITY_IMAGE % quality, "品質 %d" % quality)
    return need


def exists(name):
    return os.path.exists(os.path.join(ASSETS, name + ".png"))


def uses_text(uses):
    return " / ".join(uses[:3]) + ("(ほか%d)" % (len(uses) - 3) if len(uses) > 3 else "")


def write_missing(targets, need):
    """足りない画像を取り出して置く。失敗の数を返す。"""
    plans = {}
    not_found = []
    for name in targets:
        source, candidates = image_candidates(name, SOURCES_ORDER)
        if source is None:
            not_found.append(name)
        else:
            plans[name] = [((source, kind, number, inner), address) for kind, address, number, inner in candidates]
    results = extract_images([request for pairs in plans.values() for request, _ in pairs])

    written = []
    failed = []
    for name, pairs in sorted(plans.items()):
        images = [results[request] for request, _ in pairs if not isinstance(results[request], str)]
        if not images:
            failed.append((name, "; ".join(results[request] for request, _ in pairs)))
            continue
        if any(not same_image(images[0], image) for image in images[1:]):
            failed.append((name, "候補が複数で中身が違う: %s" % ", ".join("%s %s" % (r[0], a) for r, a in pairs)))
            continue
        os.makedirs(ASSETS, exist_ok=True)
        images[0].save(os.path.join(ASSETS, name + ".png"))
        written.append((name, pairs[0], images[0].size))

    print("書いた %d件" % len(written))
    for name, (request, address), size in written:
        print("  %s ← %s %s(%dx%d)" % (name, request[0], address, size[0], size[1]))
    print("バンドルに無い(用意が要る) %d件" % len(not_found))
    for name in not_found:
        print("  %s  %s" % (name, uses_text(need[name])))
    if failed:
        print("取り出せなかった %d件" % len(failed))
        for name, reason in failed:
            print("  %s  %s" % (name, reason))
    return len(failed)


def compare_existing():
    """今の画像を同じ名前のゲームの画像と比べる。読めなかった数を返す。"""
    plans = {}
    for file_name in sorted(os.listdir(ASSETS)):
        if not file_name.lower().endswith(".png"):
            continue
        stem = os.path.splitext(file_name)[0]
        pairs = []
        for source in SOURCES_ORDER:
            pairs.extend(((source, kind, number, inner), address)
                         for kind, address, number, inner in image_index(source).get(stem.lower(), []))
        plans[file_name] = pairs
    results = extract_images([request for pairs in plans.values() for request, _ in pairs])

    counts = {}
    listed = []
    unreadable = []
    for file_name, pairs in sorted(plans.items()):
        path = os.path.join(ASSETS, file_name)
        statuses = [(STATUS_ORDER.index(compare_image(path, results[r])), 0 if r[0] == "Star" else 1, r[0], a)
                    for r, a in pairs if not isinstance(results[r], str)]
        if not statuses:
            status = "notfound" if not pairs else "error"
            if pairs:
                unreadable.append((file_name, "; ".join(results[r] for r, _ in pairs)))
        else:
            order, _, source, address = min(statuses)
            status = STATUS_ORDER[order]
            if status in ("exporter", "edited"):
                listed.append((file_name, status, source, address))
        counts[status] = counts.get(status, 0) + 1

    print("  Assets %s" % " / ".join("%s %d" % (STATUS_NAMES[s], n) for s, n in sorted(counts.items())))
    print("同じ大きさで中身が違う %d件(更新するなら、ファイルを消して--writeを流す)" % len(listed))
    for file_name, status, source, address in listed:
        print("  %s  %s ← %s %s" % (file_name, STATUS_NAMES[status], source, address))
    if unreadable:
        print("読めなかった %d件" % len(unreadable))
        for file_name, reason in unreadable:
            print("  %s  %s" % (file_name, reason))
    return len(unreadable)


def main():
    parser = argparse.ArgumentParser(description="モジュールツールの画像の足りないものを表示し、バンドルから取り出す")
    parser.add_argument("--before", help="差し替える前のプラグインのDataの写し。前から足りなかった画像は件数だけ表示する")
    parser.add_argument("--write", action="store_true", help="足りない画像をバンドルから取り出して置く(上書きしない)")
    parser.add_argument("--compare", action="store_true", help="今の画像を同じ名前のゲームの画像と比べる(書き込まない)")
    args = parser.parse_args()

    need = needed(DATA)
    missing = sorted(name for name in need if not exists(name))
    before = needed(args.before) if args.before else {}
    old = [name for name in missing if name in before]
    new = [name for name in missing if name not in before]

    print("必要な画像 %d件" % len(need))
    if args.before:
        print("足りない画像 %d件(新しく足りない %d / 前から足りない %d)" % (len(missing), len(new), len(old)))
    else:
        print("足りない画像 %d件" % len(missing))
    for name in new:
        print("  %s  %s" % (name, uses_text(need[name])))

    failures = 0
    if args.write:
        failures += write_missing(new, need)
    if args.compare:
        failures += compare_existing()
    if failures:
        sys.exit(1)


if __name__ == "__main__":
    main()
