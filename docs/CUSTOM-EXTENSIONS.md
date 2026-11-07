# 独自拡張の管理

作業ブランチ: custom/quest-timeline-builds
基準コミット: dca400d

## GitHubと更新の管理

- origin: https://github.com/ubokun/StarResonanceDps.git （独自版の保存先）
- upstream: https://github.com/kokoukohiro/StarResonanceDps.git （元アプリの更新元）
- mainは元アプリに合わせて維持し、独自拡張はcustom/quest-timeline-buildsで管理する。
- 機能ごとに小さくコミットし、可能なものは既存のプラグイン機構を利用する。

## 更新の取り込み手順

作業中の変更をコミットしてから実施する。

```powershell
git fetch upstream
git switch main
git merge --ff-only upstream/main
git switch custom/quest-timeline-builds
git merge upstream/main
```

競合を修正し、ビルドと関連機能の確認後に独自版を保存する。共有済みブランチはrebaseやforce pushで書き換えない。GitHubのSync forkはmainに対して使う。機能追加のPRは自分のFork内で作成する。

```powershell
git push origin main
git push origin custom/quest-timeline-builds
```

## 要求と確認状況

| 項目 | ソース確認結果 | 残作業 |
| --- | --- | --- |
| 戦闘履歴 | EncounterHistoryProvider / AggregationViewに既存実装 | 実機確認、追加要件との差分確認 |
| リセット時間 | CombatExitSecondsは自動一時停止用 | リセット時間の設定・処理・表示 |
| クエスト遷移リセット | BattleStateMachineにダンジョン開始・終了連携あり | 遷移条件と既存設定の整合、実機確認 |
| クエスト時間同期 | ダンジョン状態履歴あり | 共通の時間基準、メーターとタイムラインへの適用、履歴保存 |
| PT DPS推移 | 未調査 | 時系列集計とグラフ表示 |
| スキル・イマジン・因子発動 | 未調査 | 発生イベントの特定、記録、表示 |
| 敵行動タイムライン | TimelineWidgetViewModelに既存実装 | 追跡エンティティ選択、他トラックとの併記 |
| ユーザービルド保存 | 専用機能は未確認 | 心相・アビリティ・イマジン・スキルタイムラインの保存・読込・編集UI |
| ヘイト上位5名ゲージ | EntityListEntry / EntityHateSlot / EntityListWidgetViewに既存実装 | 表示設定と実機動作の確認 |

## 検証方針

既存コードの存在、ビルド成功、実機動作を区別して記録する。クエスト開始・終了・再挑戦・マップ移動・手動リセット・履歴選択時の時間基準を確認する。

実行ファイル、個人設定、戦闘履歴DB、キャプチャやログは公開しない。ビルド成果物の配置先への反映は検証結果とともに記録する。

## 変更記録

- 2026-11-08: 公開Forkと独自拡張ブランチを作成。管理表と更新手順を追加。機能コードの変更・ビルド・実機確認は未実施。

- 2026-11-08: ユーザービルドの手入力保存・読込・更新・複製画面を追加。管理画面の「ユーザービルド」から開く。Data/UserBuildsに個別JSONで保存。Releaseビルド成功、保存処理7項目の確認成功。画面の実機確認と実行中アプリへの反映は未実施。クエスト同期とタイムライン拡張は未実装。
