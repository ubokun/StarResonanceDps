# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.
"""
シーンの地図の表を生成する。

  Data/Generated/Maps.json

`Star`をベースにし、`Star`に無いシーン・地図・エリアだけ`StarASIA`で補う。データソースで値が違う項目は`Star`を採り、一覧を表示する
(アプリは同じ名前の地図の画像を1枚しか持たないので、画像と範囲を同じデータソースのものにそろえる)。
テーブルは言語で変わらない項目だけを使うので、データソースごとに1つの言語フォルダ(`Star`はcn、`StarASIA`はjp)から読む。

  Scenes      シーンの番号 → 地図のキー。キーは`SceneTable.SceneResourceId` → `SceneResourceTable.SceneFile`の最後の部分。
              下の`Maps`にキーがあるシーンだけ
  Maps        地図のキー → 範囲と画像。範囲と基本の画像の両方がアドレス一覧にあるキーだけ
                X0 / Z0         地図の左端のワールド座標のxと、下端のz
                Width / Depth   ワールド座標のx・zの幅
                Image           基本の画像
                Layers          階層ごとの画像(`Layer`が階層)。無ければ空
              画像の項目は、アプリの画像のファイル名`File`(基本は`<キー>`、階層は`<キー>_layer<階層>`)と、取り出す元のアドレス`Address`
  AreaLayers  エリアの番号 → 階層。`SceneAreaTable.Hierarchy`が0でない行だけ

ワールド座標から画像の位置へは、横の割合 = (x - X0) / Width、縦の割合 = 1 - (z - Z0) / Depth(画像の上がzの大きい側)。

範囲はアドレス`ui/textures/scenemaps/<キー>/<キー>_region_data`のバンドルから読む。次のときは止まる。

  - シーンが指す地図の範囲のバンドルが`Bundles`に無い
  - 範囲の先頭の4個が 1, 1, 0, 0 でない(上の式で写せるのは、この形の地図だけ)
  - 幅が0以下

画像はこのツールでは取り出さない(`gen_images.py`が取り出す)。
"""
import os
import re
import sys

# _commonを読み込む前に置く(後ろだと__pycache__が作られる)
sys.dont_write_bytecode = True

from _common import (GENERATED, MAP_ADDRESS_PREFIX, addresses_of, bundle_path, dump, map_image_address,
                     map_region_address, read_map_region, table_of_source)

# データソース → テーブルを読む言語フォルダ
SOURCE_LANG_DIRS = {"Star": "cn", "StarASIA": "jp"}
BASE_SOURCE = "Star"
PLAIN_REGION_HEAD = [1.0, 1.0, 0.0, 0.0]


def read_table(source, name):
    rows = table_of_source(source, SOURCE_LANG_DIRS[source], name)
    if rows is None:
        raise FileNotFoundError("%sの%sが無い" % (source, name))
    return rows


def map_key(scene, resources):
    resource = resources.get(str(scene.get("SceneResourceId")))
    scene_file = ((resource or {}).get("SceneFile") or "").strip()
    return scene_file.replace("\\", "/").rsplit("/", 1)[-1]


def layer_addresses(key, addresses):
    """そのキーの階層の画像。階層 → アドレス。"""
    pattern = re.compile(re.escape(MAP_ADDRESS_PREFIX + key) + r"/layer_(-?\d+)/" + re.escape("%s_%s" % (key, key)) + r"$")
    layers = {}
    for address in addresses:
        match = pattern.match(address)
        if match:
            layers[int(match.group(1))] = address
    return layers


def build_map(source, key, addresses):
    region_address = map_region_address(key)
    image_address = map_image_address(key)
    if region_address not in addresses or image_address not in addresses:
        return None

    path = bundle_path(source, addresses[region_address])
    if not os.path.exists(path):
        raise FileNotFoundError("%sの地図%sの範囲のバンドル%sが無い" % (source, key, path))
    region_key, values = read_map_region(path)
    if region_key != key:
        raise ValueError("%sの地図%sの範囲のキーが%s" % (source, key, region_key))
    if values[:4] != PLAIN_REGION_HEAD:
        raise ValueError("%sの地図%sの範囲の先頭が%s(1, 1, 0, 0 ではない)" % (source, key, values[:4]))
    x0, z0, width, depth = values[4], values[6], values[7], values[8]
    if width <= 0 or depth <= 0:
        raise ValueError("%sの地図%sの幅が%s x %s" % (source, key, width, depth))

    layers = layer_addresses(key, addresses)
    return {
        "X0": round(x0, 4),
        "Z0": round(z0, 4),
        "Width": round(width, 4),
        "Depth": round(depth, 4),
        "Image": {"File": key, "Address": image_address},
        "Layers": [
            {"Layer": layer, "File": "%s_layer%d" % (key, layer), "Address": layers[layer]}
            for layer in sorted(layers)
        ],
    }


def build(source):
    scenes = read_table(source, "SceneTable")
    resources = read_table(source, "SceneResourceTable")
    areas = read_table(source, "SceneAreaTable")
    addresses = addresses_of(source)

    maps = {}
    scene_keys = {}
    without_map = 0
    for scene_id in sorted(scenes, key=int):
        key = map_key(scenes[scene_id], resources)
        if not key:
            without_map += 1
            continue
        if key not in maps:
            maps[key] = build_map(source, key, addresses)
        if maps[key] is None:
            without_map += 1
            continue
        scene_keys[scene_id] = key

    area_layers = {}
    for area_id in sorted(areas, key=int):
        layer = areas[area_id].get("Hierarchy") or 0
        if layer:
            area_layers[area_id] = layer

    listed = {key: value for key, value in sorted(maps.items()) if value is not None}
    print("  %s: シーン%d(地図あり%d / 地図なし%d) / 地図%d(階層の画像%d) / 階層のあるエリア%d"
          % (source, len(scenes), len(scene_keys), without_map, len(listed),
             sum(len(value["Layers"]) for value in listed.values()), len(area_layers)))
    return {"Scenes": scene_keys, "Maps": listed, "AreaLayers": area_layers}


def merge(tables):
    """
    データソースごとの表を1つにする。ベースの項目を全部採り、ベースに無いキーだけほかのデータソースで補う。
    (1つにした表, 補ったキーの(キー, データソース), 値がベースと違うキーの(キー, データソース))を返す。
    """
    merged = dict(tables[BASE_SOURCE])
    supplemented = []
    different = []
    for source, table in tables.items():
        if source == BASE_SOURCE:
            continue
        for key, value in table.items():
            if key not in merged:
                merged[key] = value
                supplemented.append((key, source))
            elif merged[key] != value:
                different.append((key, source))
    return merged, supplemented, different


def main():
    built = {source: build(source) for source in SOURCE_LANG_DIRS}
    result = {}
    for name, label, order in (("Scenes", "シーン", int), ("Maps", "地図", None), ("AreaLayers", "エリア", int)):
        merged, supplemented, different = merge({source: built[source][name] for source in SOURCE_LANG_DIRS})
        result[name] = {key: merged[key] for key in sorted(merged, key=order)}
        print("  %s %d(%s以外で補った%d)" % (label, len(merged), BASE_SOURCE, len(supplemented)))
        if different:
            print("    値が%sと違う(%sを採る) %d件: %s"
                  % (BASE_SOURCE, BASE_SOURCE, len(different), ", ".join("%s(%s)" % item for item in different)))
    path = os.path.join(GENERATED, "Maps.json")
    dump(path, result)
    print("wrote %s" % path)


if __name__ == "__main__":
    main()
