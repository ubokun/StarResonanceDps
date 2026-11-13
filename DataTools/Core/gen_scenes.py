# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.
"""
シーン名テーブルと、ダンジョン名に付ける難易度名のテーブルを生成する。

  Data/Localization/SceneNames.json
  Data/Localization/DungeonTypeNames.json

SceneNamesのキーは`SceneTable`と`DungeonsTable`の番号の和集合。アプリはマップの番号(`LevelMapId`)で引く。
ダンジョンではこの番号がダンジョンの番号で、ダンジョンの番号はシーン表にも行があるので、
番号ごとに次の順で名前を選ぶ。

  両方の表にある   `SceneTable.SceneSubType`がダンジョンなら`DungeonsTable.Name`、それ以外は`SceneTable.Name`
  片方だけにある   その表の`Name`

DungeonTypeNamesは、SceneNamesでダンジョン名を選んだ番号にだけ難易度名を持つ。キーは2つの形で、どの言語にも名前が無いキーは書かない。

  番号          `DungeonsTable.DungeonTypeName`。マスター難易度のダンジョン(`PlayType`がMasterMode)は持たない
  番号:段階     マスター難易度のダンジョンの段階ごとの名前。`MasterChallengeDungeonTable`の`DungeonId`と`Difficulty`から作る

どちらもキーは4言語の和集合。言語によって収録数に差があるので、無い行は空文字で入り、表示時にzh-CNにフォールバックする。
"""
import sys

# _commonを読み込む前に置く(後ろだと__pycache__が作られる)
sys.dont_write_bytecode = True

from _common import LANGS, name_of, table, write_localized

# ESceneSubType.SceneSubTypeDungeon
SCENE_SUB_TYPE_DUNGEON = 5
# EDungeonPlayType.MasterMode
PLAY_TYPE_MASTER_MODE = 17


def dungeon_type_sort_key(key):
    dungeon_id, _, difficulty = key.partition(":")
    return int(dungeon_id), int(difficulty or 0)


def main():
    scene_tables = {lang: table(lang_dir, "SceneTable") for lang, lang_dir in LANGS.items()}
    dungeon_tables = {lang: table(lang_dir, "DungeonsTable") for lang, lang_dir in LANGS.items()}
    stage_tables = {lang: table(lang_dir, "MasterChallengeDungeonTable") for lang, lang_dir in LANGS.items()}

    keys = set()
    for lang in LANGS:
        keys |= set(scene_tables[lang]) | set(dungeon_tables[lang])

    values = {}
    type_values = {}
    type_keys = set()
    for lang in LANGS:
        scenes, dungeons = scene_tables[lang], dungeon_tables[lang]
        names = {}
        type_names = {}
        dungeon_name_keys = set()
        picked = {"シーン表": 0, "ダンジョン表(シーンの種類)": 0, "ダンジョン表(ダンジョン表だけ)": 0, "どちらにも無い": 0}
        for key in keys:
            scene, dungeon = scenes.get(key), dungeons.get(key)
            if scene is not None and dungeon is not None:
                if scene.get("SceneSubType") == SCENE_SUB_TYPE_DUNGEON:
                    names[key] = name_of(dungeon)
                    dungeon_name_keys.add(key)
                    picked["ダンジョン表(シーンの種類)"] += 1
                else:
                    names[key] = name_of(scene)
                    picked["シーン表"] += 1
            elif scene is not None:
                names[key] = name_of(scene)
                picked["シーン表"] += 1
            elif dungeon is not None:
                names[key] = name_of(dungeon)
                dungeon_name_keys.add(key)
                picked["ダンジョン表(ダンジョン表だけ)"] += 1
            else:
                names[key] = ""
                picked["どちらにも無い"] += 1
        values[lang] = names
        print("%-6s %s" % (lang, " / ".join("%s %d" % kv for kv in picked.items())))

        for key in dungeon_name_keys:
            if dungeons[key].get("PlayType") == PLAY_TYPE_MASTER_MODE:
                continue
            type_names[key] = name_of(dungeons[key], "DungeonTypeName")
            if type_names[key]:
                type_keys.add(key)

        for stage_key, stage in stage_tables[lang].items():
            dungeon_key = str(stage["DungeonId"])
            difficulty = stage["Difficulty"]
            if dungeon_key not in dungeon_name_keys:
                raise ValueError("%s: MasterChallengeDungeonTable %sのダンジョン%sはダンジョン名を選んだ番号にない"
                                 % (lang, stage_key, dungeon_key))
            if dungeons[dungeon_key].get("PlayType") != PLAY_TYPE_MASTER_MODE:
                raise ValueError("%s: MasterChallengeDungeonTable %sのダンジョン%sはマスター難易度のダンジョンでない"
                                 % (lang, stage_key, dungeon_key))
            if not isinstance(difficulty, int) or difficulty <= 0:
                raise ValueError("%s: MasterChallengeDungeonTable %sのDifficultyが正の整数でない: %r"
                                 % (lang, stage_key, difficulty))
            key = "%s:%d" % (dungeon_key, difficulty)
            if key in type_names:
                raise ValueError("%s: 段階のキー%sが重複する(MasterChallengeDungeonTable %s)" % (lang, key, stage_key))
            type_names[key] = name_of(stage, "DungeonTypeName")
            if type_names[key]:
                type_keys.add(key)
        type_values[lang] = type_names
    print("→ 4言語の和集合%d件\n" % len(keys))

    write_localized("SceneNames", values, keys)
    write_localized("DungeonTypeNames", type_values, type_keys, sort_key=dungeon_type_sort_key)


if __name__ == "__main__":
    main()
