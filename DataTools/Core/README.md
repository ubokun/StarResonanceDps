# DataTools

`StarResonanceDps.Core/Data/`に置く名前テーブルなどを、言語別のJSONテーブルから生成するツール。
モジュールツール用は別のツール(`../ModuleTool`)。

Python 3で動く。`gen_images.py`の`--write`と`--compare`だけPillowが要る(`--compare`はnumpyも要る)。

```bash
pip install pillow numpy    # gen_images.pyの--write / --compareを使うときだけ
```

```bash
cd StarResonanceDps/DataTools/Core
python gen_recounts.py        # メーターの行名
python gen_buffs.py           # バフ名
python gen_skills.py          # スキル名と公式の技名
python gen_dbms.py            # ボス大技の予告の技名
python gen_scenes.py          # シーン名(ダンジョンではダンジョン名)と難易度名
python gen_monsters.py        # モンスターの名前
python gen_skill_warnings.py  # 戦闘画面の警告バーを出す技
python gen_rogue_entries.py   # オプション(ローグ系モード)の名前
python gen_buff_groups.py     # 料理・薬剤のバフの名前
python gen_season_talents.py  # シーズンタレントの型のルートノードの名前
python gen_class_specs.py     # 職業の特化の名前
python gen_seasons.py         # シーズンの名前とシーズンランクの名前
python gen_equips.py          # 装備の数値と効果の文言
python gen_match_targets.py   # マッチング先(パーティ・活動)の名前
python gen_root_buffs.py      # ルートバフ(タレント・心相晶・因子・モジュール効果)の名前・アイコン、モジュール効果の段階、因子のアイテムと、タレントの木
python gen_items.py           # アイテムの名前(装備の名前も含む)
python gen_chat_hyperlinks.py # チャットのリンクの発言の文のテンプレートと、ギルドの建物の名前
python gen_maps.py            # シーンの地図(画像の名前と座標の範囲)と、エリアの階層

python check_overrides.py     # 手動編集ファイルのうち、要らなくなった項目を表示する
python gen_images.py          # アプリの画像の足りないものを表示する(--writeで取り出して置く、--compareで今の画像と比べる)
```

作業ディレクトリはどこでもよい。`_common.py`は共有モジュールなので、単体では実行しない。

## 入力

**言語別のJSONテーブル一式が要る。リポジトリには含まれないので、各自で用意する。**

[StarResonanceTool](https://github.com/PotRooms/StarResonanceTool)の内部コードに手を入れると、言語別のZtableを取り出せる。

`Star`と`StarASIA`の両方を、次の形で置く。`gen_skill_warnings.py`・`gen_maps.py`・`gen_images.py`は同じツールの出力の`Unk`と`Bundles`も読む。

```
JSONS/
  StarASIA/Ztable/{cn,en,jp,kr}/ZTable/*.json
  StarASIA/Unk/*.bin
  StarASIA/Bundles/*.ab
  Star/Ztable/{cn,en,jp,kr}/ZTable/*.json
  Star/Unk/*.bin
  Star/Bundles/*.ab
```

既定ではリポジトリのフォルダと同じ階層の`JSONS`を使う。別の場所に置くなら、環境変数`BPSR_TABLES`で指定する。

```bash
set BPSR_TABLES=<置き場所>        # Windows
export BPSR_TABLES=<置き場所>     # bash
```

- 片方しか無いと、名前が欠けるか、エラーで終了するツールがある
- 読むテーブルが見つからないときは、置き場の設定方法を出してエラーで終了する

## 出力

| ツール | 出力 |
|---|---|
| `gen_recounts.py` | `Data/Localization/RecountRows.json` |
| `gen_buffs.py` | `Data/Localization/BuffNames.json`<br>`Data/Overrides/BuffNameOverrides.json`(空になった名前を書き足す) |
| `gen_skills.py` | `Data/Localization/SkillNames.json`<br>`Data/Localization/SkillTalkNames.json`<br>`Data/Overrides/SkillNameOverrides.json`(空になった名前を書き足す) |
| `gen_dbms.py` | `Data/Localization/DbmNames.json` |
| `gen_scenes.py` | `Data/Localization/SceneNames.json`<br>`Data/Localization/DungeonTypeNames.json` |
| `gen_monsters.py` | `Data/Localization/MonsterNames.json`<br>`Data/Overrides/MonsterNameOverrides.json`(空になった名前を書き足す) |
| `gen_skill_warnings.py` | `Data/Generated/SkillWarnings.json` |
| `gen_rogue_entries.py` | `Data/Localization/RogueEntryNames.json` |
| `gen_buff_groups.py` | `Data/Localization/CuisineBuffs.json`<br>`Data/Localization/PotionBuffs.json` |
| `gen_season_talents.py` | `Data/Localization/SeasonTalentNames.json` |
| `gen_class_specs.py` | `Data/Localization/ClassSpecNames.json` |
| `gen_seasons.py` | `Data/Localization/SeasonNames.json`<br>`Data/Localization/SeasonRankNames.json` |
| `gen_equips.py` | `Data/Generated/Equips.json`<br>`Data/Localization/EquipEffectTexts.json` |
| `gen_match_targets.py` | `Data/Localization/TeamTargetNames.json`<br>`Data/Localization/SeasonActNames.json` |
| `gen_root_buffs.py` | `Data/Localization/RootBuffNames.json`<br>`Data/Localization/ModuleEffectNames.json`<br>`Data/Localization/TalentNames.json`<br>`Data/Generated/RootBuffs.json` |
| `gen_items.py` | `Data/Localization/ItemNames.json` |
| `gen_chat_hyperlinks.py` | `Data/Localization/ChatHyperlinks.json`<br>`Data/Localization/UnionBuildingNames.json` |
| `gen_maps.py` | `Data/Generated/Maps.json` |
| `check_overrides.py` | 書かない(表示だけ) |
| `gen_images.py` | `--write`のときだけ`StarResonanceDps.App/Data/Images/`(無いファイルだけ) |

- **出力は毎回、入力だけで作り直す。** 生成物を手で直しても、次に実行すると消える。残したい修正は「手動編集ファイル」に書く
- `gen_buffs.py` / `gen_skills.py` / `gen_monsters.py`は、前の出力にあって今回空になった名前を、名前の上書きファイルへ書き足す(→「空になった名前の書き足し」)。上書きファイルが無いとエラーで終了する

## 入力を新しくしたときの順番

1. `StarResonanceDps.Core/Data`を別の場所へ写しておく(差し替える前の状態。4で使う)
2. `Data/Raw`を新しい`Star`の`cn`のテーブルで差し替え、`gen_*.py`を流す
3. `check_overrides.py`で、手動編集ファイルのうち要らなくなった項目を見る。見つかったら手で外す
4. `gen_images.py --before <1の写し>`で、新しく足りなくなった画像を見る。`--write`も付けると取り出して置く。
   「バンドルに無い(用意が要る)」と出た画像は手で用意する

- `gen_images.py`の`--write`は、今あるファイルを上書きしない。ゲームの画像が変わったかは`--compare`で見て、
  更新する画像はファイルを消してから`--write`を流す
- `--before`を付けずに`--write`を流すと、前から足りなかった画像も取り出して置く
- `Data/Images/Buffs`には、ゲームの画像と大きさが違う(手で整えた)画像がある。`--write`で足す画像はゲームの大きさのまま
- 地図の画像(`Data/Images/Maps`)は`Maps.json`から決めるので、`gen_maps.py`を先に流す。ゲームの大きさのまま置くので、ファイルが大きい
- `check_overrides.py`と`gen_images.py`は、アプリのアイコンの探し方と行のまとめ方に合わせて作ってある。アプリ側を変えたら合わせる

## 手動編集ファイル

**生成物ではないファイル。** ツールは作り直さないので、再生成しても消えない(上の3つのツールが書き足すことだけはある)。

名前の無い言語は、アプリがzh-CNの名前で表示する。zh-CNにも名前が無いときは、名前の上書きファイルに書く。

| ファイル | 中身 |
|---|---|
| `Data/Overrides/RecountRowOverrides.json` | メーターの行の出入りと名前 |
| `Data/Overrides/BuffOverrides.json` | 同梱`Data/BuffTable.json`の項目の差し替え |
| `Data/Overrides/SkillOverrides.json` | 同梱`SkillTable`の`SkillLevelGroup`の補い |
| `Data/Overrides/SkillActivationOverrides.json` | 推移グラフの技のアイコンの名前とアイコン |
| `Data/Overrides/MapOverrides.json` | マップの地図の範囲(`Generated/Maps.json`)の差し替え |
| `Data/Overrides/MonsterNameOverrides.json` | モンスターの名前(`MonsterNames.json`)の差し替え |
| `Data/Overrides/SkillNameOverrides.json` | 技の名前(`SkillNames.json`)の差し替え |
| `Data/Overrides/BuffNameOverrides.json` | バフの名前(`BuffNames.json`)の差し替え |
| `Data/Overrides/BuffNameAssignedOverrides.json` | 名前の無いバフと、名前を変えるバフに割り当てる名前 |

`MonsterNameOverrides.json` / `SkillNameOverrides.json` / `BuffNameOverrides.json`には、**入力のテーブルの更新で名前が消された行**の名前を入れる。
以前の版の入力にはあり、今の版では空になっている名前。行そのものは今の入力にも残っている。

**ゲーム側が名前を入れ直しても、上書きのほうが優先される。** 名前が戻ったキーは手で外す。

### 空になった名前の書き足し

`gen_monsters.py` / `gen_skills.py` / `gen_buffs.py`は、前の出力と比べて、**前に名前があって今回空になった言語**を
`MonsterNameOverrides.json` / `SkillNameOverrides.json` / `BuffNameOverrides.json`へ書き足す。比べるのはキーと言語ごと。

| | |
|---|---|
| 上書きにキーが無い | 空になった言語だけを持つ項目を足す |
| 上書きにキーがある | 空になった言語だけ前の名前で上書きする。他の言語はそのまま |
| 今回の入力に行ごと無いキー | 書き足さず、キーを表示する |

書き足したキー・言語・名前は全部表示する。

### MonsterNameOverrides.json

`MonsterNames.json`の後に適用する。入力のテーブルで名前が空になっているモンスターの名前を補う。

```json
{ "<モンスターの種別ID>": { "Name": { "zh-CN": "<名前>", "en-US": "<名前>", "ja-JP": "<名前>", "ko-KR": "<名前>" } } }
```

| | |
|---|---|
| `Name` | 書いた言語だけ差し替わる。生成物に名前があっても、こちらが優先される |
| `Name`に空文字 | **生成された名前を消す。** その言語はzh-CNの名前で表示される |
| 生成物に無いキー | 書ける。名前のテーブルに行がある扱いになる |

**`Name`の無い項目は置かない。** その項目は読まれず、エラーログが出る。
キーが番号の形でない・言語名の打ち間違いも、エラーログが出て読まれない。ファイルが無ければエラーログを出し、上書き無しのまま続ける。

### SkillNameOverrides.json

`SkillNames.json`の後に適用する。入力のテーブルで名前が空になっている技の名前を補う。
**形も振る舞いも`MonsterNameOverrides.json`と同じ**(適用する先とキーだけが違う)。

```json
{ "<技ID>": { "Name": { "zh-CN": "<名前>", "en-US": "<名前>", "ja-JP": "<名前>", "ko-KR": "<名前>" } } }
```

**キーは技ID。** 技レベルID(技ID×100＋レベル)ではない。

`SkillTalkNames.json`(公式の技名)に名前がある言語では、そちらが優先される。

### BuffNameOverrides.json

`BuffNames.json`の後に適用する。入力のテーブルで名前が空になっているバフの名前を補う。
**形も振る舞いも`MonsterNameOverrides.json`と同じ**(適用する先とキーだけが違う)。

```json
{ "<バフID>": { "Name": { "zh-CN": "<名前>", "en-US": "<名前>", "ja-JP": "<名前>", "ko-KR": "<名前>" } } }
```

同じバフIDを`BuffOverrides.json`にも書ける。あちらは生のテーブルの項目(`BuffType` / `BuffPriority` / `Icon`など)で、名前には触らない
(`FoldInto`でまとめたバフだけは、まとめ先の名前で表示される)。

### BuffNameAssignedOverrides.json

`BuffNameOverrides.json`の後に適用する。**どの版のテーブルにも名前の無いバフ**と、**テーブルや古い版の名前とは別の名前で出すバフ**に割り当てる名前を書く。
形も振る舞いも`MonsterNameOverrides.json`と同じ。最後に適用されるので、同じキーならこちらが優先される。
書いた言語だけ差し替わるので、ある言語をzh-CNの名前で出すときは、その言語を書かない。
`BuffOverrides.json`の`FoldInto`でまとめたバフには書かない(まとめ先の名前で表示される)。

```json
{ "<バフID>": { "Name": { "zh-CN": "<名前>", "en-US": "<名前>", "ja-JP": "<名前>", "ko-KR": "<名前>" } } }
```

### SkillOverrides.json

同梱の`SkillTable`を読んだ後に適用する。**書くのは`SkillLevelGroup`だけ。**
スキル枠のバッジが、付与元の技から装備中のイマジン・ロールスキルをたどるのに使う。
推移グラフの技のアイコンの枠もこれで決まる(枠の番号を持たない技は、`SkillLevelGroup`の先の技の枠になる)。

```json
{
  "<技ID>": {
    "SkillLevelGroup": <まとめ先の技ID>
  }
}
```

キーが`SkillTable`に無いときは、その技の行を作ってから適用する(IDと`SkillLevelGroup`だけを持つ行)。
`SkillLevelGroup`が書かれていない項目は適用されない。ファイルが無ければエラーログを出し、上書き無しのまま続ける。

### SkillActivationOverrides.json

推移グラフの横軸の下に出す、押した技のアイコンの名前とアイコンの手修正。テーブルに名前もアイコンも無い技に使う。
他に次の2つにも使う。

- 通常攻撃の2段目以降。名前は1段目のメーターの行名、アイコンは1段目のアイコン
- 押した技の番号とダメージの発生源キーが違い、メーターの行名に届かない技。名前はメーターの行名(その手修正を含む)を写す

キーは押した技の技ID。**書いてある言語の名前とアイコンだけ**が差し替わる。

```json
{
  "<技ID>": {
    "Name": { "zh-CN": "…", "en-US": "…", "ja-JP": "…", "ko-KR": "…" },
    "Icon": "<アイコンのパス>"
  }
}
```

- `Name`と`Icon`はどちらも省略できる。空の値は書かなかったのと同じ
- `Icon`はファイル名だけで書く。**イマジンのアイコンだけは頭に`ui/textures/skill_aoyi/`を付ける**(イマジンのアイコンかどうかをこの頭で決める)
- キーが技IDの形でない行と知らない言語の名前は、エラーログが出て読まれない。ファイルが無ければエラーログを出し、上書き無しのまま続ける

### MapOverrides.json

マップの地図の範囲の差し替え。`Generated/Maps.json`の範囲で地図を出すと、実際の位置と地図の画像がずれる地図に使う。
キーは`Maps.json`の`Maps`のキー(地図の画像の名前)。**書いた項目だけ**が差し替わり、同じ地図を使うシーンは全部変わる。

```json
{
  "<地図のキー>": {
    "X0": <地図の左端の x>,
    "Z0": <地図の下端の z>,
    "Width": <x の幅>,
    "Depth": <z の幅>
  }
}
```

- 4つの項目はどれも省略できる
- `Maps.json`に無いキーと中身の無い行は、エラーログが出て当てられない。ファイルが無い・読めないときはエラーログを出し、上書き無しのまま続ける

### BuffOverrides.json

同梱の`Data/BuffTable.json`を読むときに適用する。名前ではなく**項目**の差し替え。

```json
{
  "<バフID>": {
    "BuffType": <値>,
    "BuffPriority": <値>,
    "Icon": "<アイコン名>",
    "FoldInto": <まとめ先のバフID>
  }
}
```

キーがバフID。テーブルに無いIDなら、そのIDの行を作ってから適用する。

| 項目 | |
|---|---|
| `BuffType` | バフ側かデバフ側か(0デバフ / 1バフ)。バフとデバフの振り分けは、実質このファイルで決まる |
| `BuffPriority` | ゲーム内のバフ表示に出すか(0は出さない)。**バフ/デバフリストもこの値とアイコンで出すかが決まる**(0なら出さない、0以外ならアイコンがあるときだけ出す) |
| `FoldInto` | まとめ先のバフID。まとめたバフは、バフ/デバフリストでまとめ先と同じものとして出る(名前・アイコン・バフ側かデバフ側か・優先度・非表示リストの番号)。**まとめる側には他の項目を書かない**(まとめ先のものが使われる)。まとめ先がテーブルに無い・自分自身・さらにまとめられているときは、エラーログが出てまとめない |
| `Icon` / `ShowHUDIcon` | アイコンのファイル名。`"-"`は空にする。**ファイル名だけで書き、頭は次のものだけ付ける**: モジュール効果のアイコンは`ui/atlas/mod_effect/`、因子のアイコンは`Factors/`。**`Icon.`で始まる値はアプリの`Styles/Icons.xaml`のアイコンのキー**で、白い形のアイコンとして描く(装備効果の`Icon.EquipSlot.202` / `Icon.EquipSlot.200`など) |

必要な項目だけ書く。**中身の無い項目を置かない。** 差し替える中身が無くても、そのIDが登録されている扱いになる。

- テーブルの優先度が0のバフは出ない。出すなら`BuffPriority`に0以外の値を書く。テーブルの優先度が0以外で、出したくないものは`BuffPriority`に0を書く
- `BuffPriority`を0以外にしても、アイコンが無いバフは出ない。出すなら`Icon`も書く
- アイコンを書いたのにアプリのアイコンのフォルダにファイルが無いバフは、はてなのアイコンで出る
- モジュール効果のバフは、`Icon`にモジュール効果のアイコンのパス(`ModEffectTable`の`EffectConfigIcon`)を書く。名前は`BuffNameAssignedOverrides.json`に書く
- 装備効果(セット効果・武器効果)のバフは、`Icon`にIcons.xamlのキーを書く(セット効果`Icon.EquipSlot.202`、武器効果`Icon.EquipSlot.200`)。キーがIcons.xamlに無ければ、はてなのアイコンで出る
- 因子のバフは、`Icon`に`Factors/`と因子のアイテムのアイコンのファイル名(`ItemTable`の`Icon`)を書く
- テーブルに頭上のアイコン(`ShowHUDIcon`)があるバフは、アプリがそちらを先に使う。別のアイコンを出すなら`ShowHUDIcon`に`"-"`も書く

### RecountRowOverrides.json

メーターの行の手修正。**行の出入りと名前の2つを持つ。**

```json
{
  "55355:1":   { "Row": "21427:1" },
  "3210022:3": { "Row": null, "Name": { "ja-JP": "奥義！メテオフォール(パッシブ)" } },
  "50037:3":   { "Name": { "en-US": "Countercrush (Counter Storm)" } }
}
```

| | |
|---|---|
| キー | 発生源キー`ownerId:枝番`。**行のどのメンバーに書いてもよい** |
| `"Row": "<別のキー>"` | **追加** — そのキーが属する行へ入れる |
| `"Row": null` | **削除** — 行から外して単独の行にする |
| `Row`を書かない | 生成物のまま |
| `Name` | **その行**の名前。書いた言語だけ差し替わる |
| `Name`に空文字 | **生成された名前を消す。** その言語はzh-CNの名前で表示される |

- **行の代表のキーではなく、メンバーのキーで書く。** 代表のキーは版で変わるので、代表で書くと再生成のたびに効かなくなる
- 先に`"Row": null`を全部適用してから`"Row": "<キー>"`を適用する。「本体の行から外したキーへ、別のキーをまとめる」が1ファイルで書ける
- **生成物に無いキーも書ける。** `Name`だけ書けば単独の行として増える(`"Row": null`は要らない)
- **生成物の行にあるキーに`Name`だけ書くと、その行全体の名前が変わる。** そのキーだけ別の名前で出すなら`"Row": null`も書く
- キーや`Row`の値が`ownerId:枝番`の形でない・言語名の打ち間違いは、エラーログが出て読まれない。入れたらログを確かめる
- **`Row`の指す先が生成物にも手修正にも無いときは、エラーにならない。** 指す先のキーで行が新しく作られる。打ち間違えると、名前の無い行が黙って増える
