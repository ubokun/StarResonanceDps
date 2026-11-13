# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.
# DataTools共通。パスと入出力、データソースのマージ、名前の判定と取り出し、上書きファイルへの名前の書き足し、
# アドレス一覧とバンドル(UnityFS)の読み方、バンドルからの画像の取り出しを持つ。
#
# 各ツールは単体で実行できる。作業ディレクトリはどこでもよい
# (このファイルの位置からリポジトリを求める)。
import glob
import io
import json
import os
import re
import struct

# …/StarResonanceDps/DataTools/Core/_common.py → …/StarResonanceDps
SOLUTION = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# 入力の置き場。環境変数があればそれを使い、無ければリポジトリのフォルダと同じ階層のJSONSを見る。
# 中身は「データソース → Ztable → 言語フォルダ → ZTable → *.json」。
# リポジトリには含まれないので各自で用意する。
TABLES_ENV = "BPSR_TABLES"
TABLES_DIR = os.environ.get(TABLES_ENV) or os.path.join(os.path.dirname(SOLUTION), "JSONS")

# データソース。**言語フォルダごとにベースが違う**。先にあるほうがベースで、後ろは先に無い行を足し、空欄を補うだけ。
# cn / enはStar、jp / krはStarASIAがベース(その言語が入っているビルドの表)。
SOURCES = ("StarASIA", "Star")
SOURCES_BY_LANG_DIR = {
    "cn": ("Star", "StarASIA"),
    "en": ("Star", "StarASIA"),
    "jp": ("StarASIA", "Star"),
    "kr": ("StarASIA", "Star"),
}


def sources_for(lang_dir):
    """その言語フォルダで使うデータソースの順。先にあるほうがベース。"""
    return SOURCES_BY_LANG_DIR[lang_dir]


def unk_dir(source):
    """データソースごとの入力`Unk`。アドレス一覧が入る。"""
    return os.path.join(TABLES_DIR, source, "Unk")


def bundles_dir(source):
    """データソースごとの入力`Bundles`(`<番号>.ab`)。同じデータソースのアドレス一覧と対になる。"""
    return os.path.join(TABLES_DIR, source, "Bundles")

DATA = os.path.join(SOLUTION, "StarResonanceDps.Core", "Data")
LOCALIZATION = os.path.join(DATA, "Localization")
GENERATED = os.path.join(DATA, "Generated")
OVERRIDES = os.path.join(DATA, "Overrides")

# 表示言語 → 入力側のフォルダ名。
# 言語ごとにテーブルの版が違うことがあり、収録IDも一致しない。
LANGS = {"zh-CN": "cn", "en-US": "en", "ja-JP": "jp", "ko-KR": "kr"}

# 未翻訳の行に入っているプレースホルダ。名前として扱わない。
PLACEHOLDERS = {"场地标记01", "气刃突刺计数"}


def load(path):
    """JSONを読む。無ければNone。"""
    if not os.path.exists(path):
        return None
    with io.open(path, encoding="utf-8") as fh:
        return json.load(fh)


def _path(source, lang_dir, name):
    return os.path.join(TABLES_DIR, source, "Ztable", lang_dir, "ZTable", name + ".json")


def table_of_source(source, lang_dir, name):
    """
    データソースを指定して生テーブルを読む。無ければNone。

    **行をIDそのもので引けないテーブル用。** `RecountTable`のキーは行番号で、データソースが違えば
    同じ番号が別の行を指す。マージすると
    無関係な行が混ざるので、データソースごとに組み立ててから行の対応を取る。
    """
    return load(_path(source, lang_dir, name))


def tables_by_source(lang_dir, name):
    """
    データソースごとの生テーブルを`sources_for(lang_dir)`の順で返す。`table`もこれで読む。

    どこにも無ければ、置き場の設定方法を示して止まる。
    """
    order = sources_for(lang_dir)
    found = [(s, load(_path(s, lang_dir, name))) for s in order]
    found = [(s, t) for s, t in found if t is not None]
    if not found:
        raise FileNotFoundError(
            "%sが%sのどこにも無い。入力の置き場は環境変数%sか、_common.pyのTABLES_DIRで設定する"
            % (name, " / ".join(order), TABLES_ENV))
    return found


def table(lang_dir, name):
    """
    データソースをまたいでマージした入力テーブルを読む。

    キーは和集合。**行は先のデータソースを優先し**(順は言語フォルダごとに`sources_for`)、後ろは無い行を足すだけ。
    そのうえで、採った行の**空欄の文字列項目**だけを別のデータソースで補う。

    **キーがIDそのもののテーブル専用。**行番号で引くものは`table_of_source`を使う。
    """
    found = tables_by_source(lang_dir, name)
    merged = {}
    for key in set().union(*[set(t) for _, t in found]):
        rows = [t[key] for _, t in found if key in t]
        row = dict(rows[0])
        for other in rows[1:]:
            for field, value in other.items():
                if isinstance(value, str) and value.strip() and not (row.get(field) or "").strip():
                    row[field] = value
        merged[key] = row
    return merged


def dump(path, obj):
    """JSONを書く。差分が見やすいよう整形して改行で終える。"""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with io.open(path, "w", encoding="utf-8") as fh:
        fh.write(json.dumps(obj, ensure_ascii=False, indent=2) + "\n")


def named(value):
    """名前として使える文字列か。空とプレースホルダは名前ではない。"""
    text = (value or "").strip()
    return bool(text) and text not in PLACEHOLDERS


def name_of(row, field="Name"):
    """入力の1行から名前を取る。プレースホルダは空にする。"""
    text = ((row or {}).get(field) or "").strip()
    return "" if text in PLACEHOLDERS else text


def per_lang(values_by_lang, key):
    """1つのキーの4言語ぶんの名前。`{ "zh-CN": ..., "en-US": ..., "ja-JP": ..., "ko-KR": ... }`の順で、無い言語は空文字。"""
    return {lang: (values_by_lang.get(lang, {}).get(key) or "").strip() for lang in LANGS}


def write_localized(basename, values_by_lang, keys, sort_key=int, dropped_names_overrides=None):
    """
    `{basename}.json`を書く。形は`{ "キー": { "zh-CN": ..., "en-US": ..., "ja-JP": ..., "ko-KR": ... } }`。

    どのキーも4言語をすべて持つ。版によって収録IDが違っても、キーの集合が言語で変わると
    まとめる処理の停止条件が言語で変わってしまうため。足りない言語は空文字で入り、
    表示時にzh-CNにフォールバックするのに任せる。

    **完全な上書き。**出力は入力だけで決まる。入力に名前が無ければ空になる。
    前の出力を読むのは、`dropped_names_overrides`(上書きファイルの名前)を渡したときに、
    空になった名前をその上書きへ書き足すためだけ(→ `carry_dropped_names`)。

    `sort_key`はキーの並べ方。キーが数字だけでないテーブルで渡す。
    """
    keys = sorted(keys, key=sort_key)
    merged = {key: per_lang(values_by_lang, key) for key in keys}
    path = os.path.join(LOCALIZATION, "%s.json" % basename)
    if dropped_names_overrides:
        carry_dropped_names(load(path), merged, dropped_names_overrides, sort_key)
    dump(path, merged)
    print("  %s %5dキー / 名前あり%s"
          % (basename, len(keys),
             " / ".join("%s %d" % (lang, sum(1 for names in merged.values() if names[lang])) for lang in LANGS)))


def carry_dropped_names(previous, merged, override_basename, sort_key=int):
    """
    前の出力`previous`にあって今回の`merged`で空になった名前を、
    上書きファイル`Data/Overrides/{override_basename}.json`へ書き足す。比べるのはキーと言語ごと。

    入力の表の更新で名前が消された行の名前を残すため(名前の上書きファイルの役割と同じ)。

    - 上書きにキーが無ければ、空になった言語だけを持つ項目を足す。空になっていない言語は書かない
      (上書きの空文字は生成値を消す意味になるため)
    - キーがあれば、空になった言語だけ前の名前で上書きする。ほかの言語はそのまま
    - 今回の入力に行ごと無いキーは書き足さない(上書きは行が残って名前だけ消えたものを戻すもの)。キーを表示する

    名前の表より先に呼ぶ。上書きファイルが無い・形が違うときは止まり、名前の表も書かれない。
    前の出力が無ければ何もしない。
    """
    if previous is None:
        print("  %s: 前の出力が無いので書き足さない" % override_basename)
        return

    path = os.path.join(OVERRIDES, "%s.json" % override_basename)
    overrides = load(path)
    if overrides is None:
        raise FileNotFoundError("%sが無い。空になった名前の書き足し先" % path)

    added = []
    replaced = []
    removed_rows = []
    for key in sorted(previous, key=sort_key):
        old_names = previous[key]
        dropped = {lang: old_names[lang].strip() for lang in LANGS
                   if (old_names.get(lang) or "").strip() and not ((merged.get(key) or {}).get(lang) or "").strip()}
        if not dropped:
            continue
        if key not in merged:
            removed_rows.append(key)
            continue

        entry = overrides.get(key)
        if entry is None:
            overrides[key] = {"Name": {lang: dropped[lang] for lang in LANGS if lang in dropped}}
            added.extend((key, lang, dropped[lang]) for lang in LANGS if lang in dropped)
            continue
        names = entry.get("Name")
        if not isinstance(names, dict):
            raise ValueError("%sのキー%sにNameが無い" % (path, key))
        for lang in LANGS:
            if lang in dropped:
                replaced.append((key, lang, names.get(lang), dropped[lang]))
        combined = dict(names)
        combined.update(dropped)
        entry["Name"] = {lang: combined[lang] for lang in LANGS if lang in combined}

    if added or replaced:
        dump(path, {key: overrides[key] for key in sorted(overrides, key=sort_key)})
    print("  %s: 空になった名前を書き足した キー%d(新しく足した%d / 上書きした%d)"
          % (override_basename, len({key for key, *_ in added + replaced}),
             len({key for key, *_ in added}), len({key for key, *_ in replaced})))
    for key, lang, name in added:
        print("    %s %-6s %s" % (key, lang, name))
    for key, lang, before, name in replaced:
        print("    %s %-6s %s → %s(上書き)" % (key, lang, before if before is not None else "(無し)", name))
    if removed_rows:
        print("  %s: 行ごと消えたキー(書き足さない) %d: %s"
              % (override_basename, len(removed_rows), ", ".join(removed_rows)))


# ---------------------------------------------------------------------------
# アドレス一覧
#
# データソースの`Unk/*.bin`のうち`->>>> bundleHash:`を含む1本。アドレス(資源の場所)→ バンドルの番号を持つ。

ADDRESS_LIST_MARKER = b"->>>> bundleHash:"


def find_address_list(source):
    """データソースの`Unk/*.bin`から、中身でアドレス一覧を1本だけ探す。"""
    directory = unk_dir(source)
    paths = sorted(glob.glob(os.path.join(directory, "*.bin")))
    if not paths:
        raise FileNotFoundError("%sに*.binが無い" % directory)
    found = []
    for path in paths:
        with open(path, "rb") as fh:
            data = fh.read()
        if ADDRESS_LIST_MARKER in data:
            found.append((path, data))
    if len(found) != 1:
        raise ValueError("%sのアドレス一覧が1本に決まらない: %s" % (directory, [p for p, _ in found]))
    return found[0]


def header_count(path, data, name):
    counts = re.findall(rb"^(?:\xef\xbb\xbf)?" + re.escape(name.encode()) + rb":(\d+) ", data, re.MULTILINE)
    if len(counts) != 1:
        raise ValueError("%sに%sのヘッダー行が%d件ある" % (path, name, len(counts)))
    return int(counts[0])


def read_address_list(path, data):
    """
    アドレス一覧から、アドレス → バンドル番号 と、バンドルの一覧(番号の集合)を読む。

    アドレス行は`address:<アドレス> ->>>> hash:<数字> ->>>> bundleHash:<数字>`(アドレスは空白を含むことがある)で、
    件数はヘッダー行`AddressCount`。バンドルの一覧はヘッダー行`DepsDictCount`のあとの`bundleHash:<数字>`の行。
    どちらもヘッダー行の件数と行数が合わなければ止まる。
    """
    rows = re.findall(rb"^address:(.*) ->>>> hash:\d+ ->>>> bundleHash:(\d+)\r?$", data, re.MULTILINE)
    address_count = header_count(path, data, "AddressCount")
    if len(rows) != address_count:
        raise ValueError("%sのアドレス行が%d行で、AddressCountは%d" % (path, len(rows), address_count))
    addresses = {}
    for address, number in rows:
        address = address.decode("utf-8")
        if address in addresses:
            raise ValueError("%sにアドレス%sが重複" % (path, address))
        addresses[address] = int(number)

    bundles = [int(n) for n in re.findall(rb"^bundleHash:(\d+)\r?$", data, re.MULTILINE)]
    bundle_count = header_count(path, data, "DepsDictCount")
    if len(bundles) != bundle_count or len(set(bundles)) != bundle_count:
        raise ValueError("%sのバンドル一覧が%d行(異なる番号%d)で、DepsDictCountは%d"
                         % (path, len(bundles), len(set(bundles)), bundle_count))
    return addresses, set(bundles)


def present_bundles(source):
    """そのデータソースの`Bundles`にある`<番号>.ab`の番号。"""
    directory = bundles_dir(source)
    if not os.path.isdir(directory):
        raise FileNotFoundError("%sが無い" % directory)
    return {int(name[:-3]) for name in os.listdir(directory) if re.fullmatch(r"\d+\.ab", name)}


_ADDRESSES = {}


def addresses_of(source):
    """そのデータソースのアドレス → バンドルの番号。1回の実行で1回だけ読む。"""
    if source not in _ADDRESSES:
        _ADDRESSES[source] = read_address_list(*find_address_list(source))[0]
    return _ADDRESSES[source]


def bundle_path(source, number):
    """そのデータソースのバンドル`<番号>.ab`のパス。"""
    return os.path.join(bundles_dir(source), "%d.ab" % number)


# ---------------------------------------------------------------------------
# UnityFS


def lz4_block(src, size):
    dst = bytearray()
    i = 0
    n = len(src)
    while i < n:
        token = src[i]
        i += 1
        literal = token >> 4
        if literal == 15:
            while True:
                b = src[i]
                i += 1
                literal += b
                if b != 255:
                    break
        dst += src[i:i + literal]
        i += literal
        if i >= n:
            break
        offset = src[i] | (src[i + 1] << 8)
        i += 2
        match = token & 15
        if match == 15:
            while True:
                b = src[i]
                i += 1
                match += b
                if b != 255:
                    break
        match += 4
        start = len(dst) - offset
        if offset == 0 or start < 0:
            raise ValueError("LZ4の参照先が不正")
        if offset >= match:
            dst += dst[start:start + match]
        else:
            for k in range(match):
                dst.append(dst[start + k])
    if len(dst) != size:
        raise ValueError("LZ4の展開後の長さが%dで、期待は%d" % (len(dst), size))
    return bytes(dst)


def decompress(data, flags, size):
    kind = flags & 0x3F
    if kind == 0:
        return data
    if kind in (2, 3):
        return lz4_block(data, size)
    raise ValueError("未対応の圧縮形式%d" % kind)


def cstring(data, i):
    end = data.index(0, i)
    return data[i:end].decode("utf-8"), end + 1


def read_unityfs(path, wanted=None):
    """
    UnityFSのバンドルから、ノードの名前 → 中身 を返す。

    `wanted`(ノードの名前を受けて真偽を返す)を渡すと、そのノードだけを返し、展開するのもそのノードに掛かるブロックだけにする
    (アトラスのスプライトの名前だけを読むときに、画像の本体を展開しないため)。
    """
    with open(path, "rb") as fh:
        ab = fh.read()
    signature, i = cstring(ab, 0)
    if signature != "UnityFS":
        raise ValueError("%sはUnityFSではない(%s)" % (path, signature))
    version = struct.unpack_from(">I", ab, i)[0]
    i += 4
    _, i = cstring(ab, i)
    _, i = cstring(ab, i)
    _, compressed_info_size, info_size, flags = struct.unpack_from(">qIII", ab, i)
    i += 20
    if version >= 7:
        i = (i + 15) // 16 * 16
    if flags & 0x80:
        raise ValueError("ブロック情報が末尾にある形は未対応")
    info = decompress(ab[i:i + compressed_info_size], flags, info_size)
    i += compressed_info_size
    if flags & 0x200:
        i = (i + 15) // 16 * 16

    j = 16
    block_count = struct.unpack_from(">i", info, j)[0]
    j += 4
    spans = []
    start = 0
    for _ in range(block_count):
        size, compressed_size, block_flags = struct.unpack_from(">IIH", info, j)
        j += 10
        spans.append((start, size, i, compressed_size, block_flags))
        start += size
        i += compressed_size
    node_count = struct.unpack_from(">i", info, j)[0]
    j += 4
    nodes = []
    for _ in range(node_count):
        offset, size, _ = struct.unpack_from(">qqI", info, j)
        j += 20
        name, j = cstring(info, j)
        nodes.append((offset, size, name))

    blocks = {}

    def block(index):
        if index not in blocks:
            _, size, position, compressed_size, block_flags = spans[index]
            blocks[index] = decompress(ab[position:position + compressed_size], block_flags, size)
        return blocks[index]

    result = {}
    for offset, size, name in nodes:
        if wanted is not None and not wanted(name):
            continue
        parts = []
        for index, (block_start, block_size, _, _, _) in enumerate(spans):
            if block_start + block_size <= offset or block_start >= offset + size:
                continue
            data = block(index)
            parts.append(data[max(offset - block_start, 0):min(offset + size - block_start, block_size)])
        result[name] = b"".join(parts)
    return result


def is_resource_node(name):
    """画像などの本体が入るノード(SerializedFileではないノード)か。"""
    return name.endswith(".resS") or name.endswith(".resource")


def serialized_objects(serialized):
    """
    SerializedFile(版22以上)のオブジェクトを、バイト順と`(パスID, クラスID, 中身)`の一覧で返す。
    """
    version = struct.unpack_from(">I", serialized, 8)[0]
    if version < 22:
        raise ValueError("SerializedFileの版%dは未対応" % version)
    endian = ">" if serialized[16] else "<"
    data_offset = struct.unpack_from(">q", serialized, 32)[0]
    k = 48
    _, k = cstring(serialized, k)
    k += 4
    has_type_tree = serialized[k]
    k += 1
    type_count = struct.unpack_from(endian + "i", serialized, k)[0]
    k += 4
    class_ids = []
    for _ in range(type_count):
        class_id = struct.unpack_from(endian + "i", serialized, k)[0]
        k += 4 + 1 + 2
        if class_id == 114:
            k += 16
        k += 16
        if has_type_tree:
            node_count, string_size = struct.unpack_from(endian + "ii", serialized, k)
            k += 8 + node_count * 32 + string_size
            dependency_count = struct.unpack_from(endian + "i", serialized, k)[0]
            k += 4 + dependency_count * 4
        class_ids.append(class_id)
    object_count = struct.unpack_from(endian + "i", serialized, k)[0]
    k += 4
    objects = []
    for _ in range(object_count):
        k = (k + 3) // 4 * 4
        path_id, start, size, type_index = struct.unpack_from(endian + "qqIi", serialized, k)
        k += 24
        objects.append((path_id, class_ids[type_index], serialized[data_offset + start:data_offset + start + size]))
    return endian, objects


def object_name(body, endian, position=0):
    """オブジェクトの中身の、長さつきの文字列(先頭なら名前)と、その次の位置(4バイトにそろえる)。"""
    length = struct.unpack_from(endian + "i", body, position)[0]
    text = body[position + 4:position + 4 + length].decode("utf-8")
    return text, (position + 4 + length + 3) // 4 * 4


# ---------------------------------------------------------------------------
# バンドルの画像(Texture2D・スプライトアトラス)
#
# 並びはUnity 2022.3の形に合わせて読む。画像にするのにPillowが要る(使うときだけ読み込む)。

TEXTURE_CLASS_ID = 28
SPRITE_CLASS_ID = 213
SPRITE_ATLAS_CLASS_ID = 687078895

# Texture2Dの形式。圧縮形式は4×4画素のブロックのバイト数、非圧縮はPillowの形と1画素のバイト数。
TEXTURE_BLOCK_BYTES = {10: 8, 12: 16, 25: 16, 26: 8, 27: 16}
TEXTURE_DDS_FOURCC = {10: b"DXT1", 12: b"DXT5"}
TEXTURE_DXGI = {25: 98, 26: 80, 27: 83}
TEXTURE_RAW_MODES = {1: ("L", 1), 3: ("RGB", 3), 4: ("RGBA", 4), 5: ("RGBA", 4)}

# スプライトの回転(settingsRawのビット2〜5)→ 戻すためのPillowの変換
SPRITE_ROTATIONS = {1: "FLIP_LEFT_RIGHT", 2: "FLIP_TOP_BOTTOM", 3: "ROTATE_180", 4: "ROTATE_270"}


def pillow_image():
    """PillowのImage。入っていなければ入れ方を示して止まる。"""
    try:
        from PIL import Image
    except ImportError:
        raise SystemExit("画像を取り出すにはPillowが要る(pip install pillow)")
    return Image


def read_texture(body, endian):
    """Texture2Dの中身から`(名前, 幅, 高さ, 形式, 埋め込みのデータ, (外部の位置, 大きさ, パス))`を返す。"""
    name, p = object_name(body, endian)
    p += 4  # m_ForcedFallbackFormat
    p += 2  # m_DownscaleFallback, m_IsAlphaChannelOptional
    p = (p + 3) // 4 * 4
    width, height, _, _, fmt, _ = struct.unpack_from(endian + "6i", body, p)
    # 外部の位置(m_StreamData)は末尾: 位置(u64)・大きさ(u32)・パスの文字列
    for path_length in range(0, 256):
        start = len(body) - ((path_length + 3) // 4 * 4) - 4
        if start < 0:
            break
        length = struct.unpack_from(endian + "i", body, start)[0]
        if length == path_length:
            path = body[start + 4:start + 4 + length].decode("utf-8", "replace")
            if length == 0 or path.startswith("archive:"):
                offset, size = struct.unpack_from(endian + "QI", body, start - 12)
                break
    else:
        raise ValueError("Texture2D %sの m_StreamData が見つからない" % name)
    inline = b""
    if size == 0:
        # 埋め込み: m_StreamDataの直前の配列(大きさのint＋バイト、4バイトにそろえて終わる)
        end = start - 12
        need = texture_level_size(width, height, fmt)
        for q in range(p, end - 4):
            n = struct.unpack_from(endian + "i", body, q)[0]
            if n >= need and q + 4 + n <= end and (q + 4 + n + 3) // 4 * 4 == end:
                inline = body[q + 4:q + 4 + n]
                break
        if not inline:
            raise ValueError("Texture2D %sの埋め込みのデータが見つからない" % name)
    return name, width, height, fmt, inline, (offset, size, path)


def texture_level_size(width, height, fmt):
    """一番大きい段(ミップマップの0段目)のバイト数。"""
    if fmt in TEXTURE_BLOCK_BYTES:
        return ((width + 3) // 4) * ((height + 3) // 4) * TEXTURE_BLOCK_BYTES[fmt]
    if fmt in TEXTURE_RAW_MODES:
        return width * height * TEXTURE_RAW_MODES[fmt][1]
    raise ValueError("未対応のTexture2Dの形式%d" % fmt)


def decode_texture(width, height, fmt, data):
    """Texture2Dのデータを、上下を直したRGBAの画像にする。"""
    Image = pillow_image()
    level = data[:texture_level_size(width, height, fmt)]
    if fmt in TEXTURE_RAW_MODES:
        image = Image.frombytes(TEXTURE_RAW_MODES[fmt][0], (width, height), level)
        if fmt == 5:
            a, r, g, b = image.split()
            image = Image.merge("RGBA", (r, g, b, a))
    else:
        pitch = len(level)
        if fmt in TEXTURE_DDS_FOURCC:
            header = struct.pack("<7I44x8I5I", 124, 0x81007, height, width, pitch, 0, 1,
                                 32, 0x4, struct.unpack("<I", TEXTURE_DDS_FOURCC[fmt])[0], 0, 0, 0, 0, 0, 0x1000, 0, 0, 0, 0)
            dds = b"DDS " + header + level
        else:
            header = struct.pack("<7I44x8I5I", 124, 0x81007, height, width, pitch, 0, 1,
                                 32, 0x4, 0x30315844, 0, 0, 0, 0, 0, 0x1000, 0, 0, 0, 0)
            dds = b"DDS " + header + struct.pack("<5I", TEXTURE_DXGI[fmt], 3, 0, 1, 0) + level
        image = Image.open(io.BytesIO(dds))
        image.load()
    return image.convert("RGBA").transpose(Image.FLIP_TOP_BOTTOM)


def _texture_data(nodes, info, inline):
    offset, size, path = info
    if not size:
        return inline
    node = path.split("/")[-1]
    if node not in nodes:
        raise ValueError("画像の本体のノード%sがバンドルに無い" % node)
    return nodes[node][offset:offset + size]


def bundle_textures(path):
    """バンドルの中のTexture2Dを 名前 → 画像 で返す。"""
    nodes = read_unityfs(path)
    textures = {}
    for name, serialized in nodes.items():
        if is_resource_node(name):
            continue
        endian, objects = serialized_objects(serialized)
        for _, class_id, body in objects:
            if class_id != TEXTURE_CLASS_ID:
                continue
            texture_name, width, height, fmt, inline, info = read_texture(body, endian)
            textures[texture_name] = decode_texture(width, height, fmt, _texture_data(nodes, info, inline))
    return textures


def sprite_names(path):
    """バンドルの中のSpriteの名前。画像の本体は展開しない。"""
    names = []
    for _, serialized in read_unityfs(path, lambda name: not is_resource_node(name)).items():
        endian, objects = serialized_objects(serialized)
        names.extend(object_name(body, endian)[0] for _, class_id, body in objects if class_id == SPRITE_CLASS_ID)
    return names


def atlas_sprites(path, wanted):
    """
    スプライトアトラスのバンドルから、名前が`wanted`のスプライトを 名前 → 画像 で返す。アトラスに無い名前は返さない。

    Spriteのm_RenderDataKeyとSpriteAtlasのm_RenderDataMapを突き合わせ、描く画像と四角を決めて切り出す。回転は戻す。
    """
    Image = pillow_image()
    nodes = read_unityfs(path)
    serialized_name = next(name for name in nodes if not is_resource_node(name))
    endian, objects = serialized_objects(nodes[serialized_name])

    atlases = [body for _, class_id, body in objects if class_id == SPRITE_ATLAS_CLASS_ID]
    if len(atlases) != 1:
        raise ValueError("%sのSpriteAtlasが%d個" % (path, len(atlases)))
    atlas = atlases[0]
    _, p = object_name(atlas, endian)
    count = struct.unpack_from(endian + "i", atlas, p)[0]
    p += 4 + 12 * count  # m_PackedSprites
    count = struct.unpack_from(endian + "i", atlas, p)[0]
    p += 4
    for _ in range(count):  # m_PackedSpriteNamesToIndex
        _, p = object_name(atlas, endian, p)
    count = struct.unpack_from(endian + "i", atlas, p)[0]
    p += 4
    render_data = {}
    for _ in range(count):
        key = atlas[p:p + 24]
        p += 24
        _, texture_path_id = struct.unpack_from(endian + "iq", atlas, p)
        p += 12 + 12  # texture, alphaTexture
        rect = struct.unpack_from(endian + "4f", atlas, p)
        p += 16 + 8 + 8 + 16 + 4  # textureRect, textureRectOffset, atlasRectOffset, uvTransform, downscaleMultiplier
        settings = struct.unpack_from(endian + "I", atlas, p)[0]
        p += 4
        secondary = struct.unpack_from(endian + "i", atlas, p)[0]
        p += 4
        for _ in range(secondary):
            p += 12
            _, p = object_name(atlas, endian, p)
        render_data[key] = (texture_path_id, rect, settings)

    texture_bodies = {path_id: body for path_id, class_id, body in objects if class_id == TEXTURE_CLASS_ID}
    decoded = {}

    def texture(path_id):
        if path_id not in decoded:
            _, width, height, fmt, inline, info = read_texture(texture_bodies[path_id], endian)
            decoded[path_id] = decode_texture(width, height, fmt, _texture_data(nodes, info, inline))
        return decoded[path_id]

    sprites = {}
    for _, class_id, body in objects:
        if class_id != SPRITE_CLASS_ID:
            continue
        name, p = object_name(body, endian)
        if name not in wanted:
            continue
        p += 16 + 8 + 16 + 4 + 8 + 4 + 4  # m_Rect, m_Offset, m_Border, m_PixelsToUnits, m_Pivot, m_Extrude, m_IsPolygon
        entry = render_data.get(body[p:p + 24])
        if entry is None:
            continue
        texture_path_id, (x, y, width, height), settings = entry
        image = texture(texture_path_id)
        crop = image.crop((int(round(x)), int(round(image.size[1] - y - height)),
                           int(round(x + width)), int(round(image.size[1] - y))))
        # settingsRawのビット2〜5が回転(1は左右反転、2は上下反転、3は180度、4は90度)
        rotation = (settings >> 2) & 0xF
        if rotation in SPRITE_ROTATIONS:
            crop = crop.transpose(getattr(Image, SPRITE_ROTATIONS[rotation]))
        sprites[name] = crop
    return sprites


# ---------------------------------------------------------------------------
# 画像の索引と取り出し


def is_atlas_address(address):
    """スプライトアトラスのバンドルのアドレスか(`ui/atlas/<名前>`)。"""
    return address.startswith("ui/atlas/") and address.count("/") == 2


_IMAGE_INDEXES = {}


def image_index(source):
    """
    そのデータソースの画像の索引。ファイル名(拡張子なし・小文字)→ [(種類, アドレス, バンドルの番号, 中の名前)]。
    種類は`texture`(アドレスの最後の部分で引く)か`atlas`(スプライトアトラスの中のスプライトの名前で引く)。
    アトラスのバンドルが`Bundles`に無ければ止まる。1回の実行で1回だけ作る。
    """
    if source in _IMAGE_INDEXES:
        return _IMAGE_INDEXES[source]
    index = {}
    atlas_count = 0
    for address, number in addresses_of(source).items():
        if is_atlas_address(address):
            path = bundle_path(source, number)
            if not os.path.exists(path):
                raise FileNotFoundError("%sのアトラス%sのバンドル%sが無い" % (source, address, path))
            for name in sprite_names(path):
                index.setdefault(name.lower(), []).append(("atlas", address, number, name))
            atlas_count += 1
        else:
            last = address.rsplit("/", 1)[-1]
            index.setdefault(last.lower(), []).append(("texture", address, number, last))
    print("  %s 画像の索引: アトラス%d / 名前%d" % (source, atlas_count, len(index)))
    _IMAGE_INDEXES[source] = index
    return index


def image_stem(icon):
    """画像の名前(全体のパスかファイル名)から、拡張子なしのファイル名。"""
    return os.path.splitext(icon.strip().replace("\\", "/").rsplit("/", 1)[-1])[0]


def image_candidates(icon, sources):
    """
    画像の名前(全体のパスかファイル名)から、取り出す元の候補を`(データソース, [(種類, アドレス, 番号, 中の名前)])`で返す。

    `sources`の順に探し、最初に見つかったデータソースの候補を返す。どこにも無ければ`(None, [])`。
    全体のパスがアドレスそのものならそれだけ。そうでなければファイル名で索引を引き、
    パスがあれば、アドレスがパスに近いもの(`/`を`_`に替えて比べ、頭の一致が一番長いもの)だけに絞る。
    """
    path = icon.strip().replace("\\", "/")
    stem = image_stem(icon)
    for source in sources:
        addresses = addresses_of(source)
        if "/" in path and path in addresses:
            return source, [("texture", path, addresses[path], path.rsplit("/", 1)[-1])]
        found = image_index(source).get(stem.lower(), [])
        if not found:
            continue
        if "/" not in path:
            return source, found
        target = path.rsplit("/", 1)[0].replace("/", "_")

        def score(candidate):
            address = candidate[1].replace("/", "_")
            n = 0
            while n < min(len(address), len(target)) and address[n] == target[n]:
                n += 1
            return n

        best = max(score(c) for c in found)
        return source, [c for c in found if score(c) == best]
    return None, []


def extract_images(requests):
    """
    `(データソース, 種類, バンドルの番号, 中の名前)`の集まりを取り出し、組→画像を返す。取り出せなかった組は理由の文字列。
    同じバンドルは1回だけ開く。Texture2Dは名前で探し、名前が合わなくてもバンドルの中に1枚だけならそれを使う
    (アドレスの最後の部分とTexture2Dの名前が違うことがある)。
    """
    results = {}
    by_bundle = {}
    for request in set(requests):
        by_bundle.setdefault(request[:3], set()).add(request[3])
    for (source, kind, number), names in sorted(by_bundle.items()):
        path = bundle_path(source, number)
        if not os.path.exists(path):
            for name in names:
                results[(source, kind, number, name)] = "バンドル%sが無い" % path
            continue
        try:
            if kind == "atlas":
                found = atlas_sprites(path, names)
                for name in names:
                    results[(source, kind, number, name)] = found.get(name, "アトラスにスプライト%sが無い" % name)
            else:
                textures = bundle_textures(path)
                for name in names:
                    if name in textures:
                        results[(source, kind, number, name)] = textures[name]
                    elif len(textures) == 1:
                        results[(source, kind, number, name)] = next(iter(textures.values()))
                    else:
                        results[(source, kind, number, name)] = "Texture2D %sが無い(バンドルの中に%d枚)" % (name, len(textures))
        except ValueError as ex:
            for name in names:
                results[(source, kind, number, name)] = "%sを読めない: %s" % (path, ex)
    return results


def same_image(a, b):
    """2つの画像の大きさと画素がすべて同じか。"""
    return a.size == b.size and a.tobytes() == b.tobytes()


def compare_image(current_path, image):
    """
    今のファイルと取り出した画像を比べる。

    `same`(透明な画素の色を除いて全部一致)/ `exporter`(透明度が8より大きく違う画素が0.5%以下、両方とも不透明に近い画素で
    色が40より大きく違う画素が1%以下。書き出しの崩れの範囲)/ `edited`(それより大きく違う)/ `size`(大きさが違う)。
    numpyが要る。
    """
    try:
        import numpy
    except ImportError:
        raise SystemExit("画像を比べるにはnumpyが要る(pip install numpy)")
    Image = pillow_image()
    a = numpy.asarray(Image.open(current_path).convert("RGBA")).astype(numpy.int16)
    b = numpy.asarray(image.convert("RGBA")).astype(numpy.int16)
    if a.shape != b.shape:
        return "size"
    visible = (a[..., 3] > 0) | (b[..., 3] > 0)
    difference = numpy.abs(a - b)
    color = difference[..., :3].max(axis=2)
    alpha = difference[..., 3]
    if not numpy.any((color > 0) & visible) and not numpy.any(alpha > 0):
        return "same"
    total = a.shape[0] * a.shape[1]
    solid = (a[..., 3] >= 128) & (b[..., 3] >= 128)
    if numpy.count_nonzero(alpha > 8) <= total * 0.005 and numpy.count_nonzero((color > 40) & solid) <= total * 0.01:
        return "exporter"
    return "edited"
