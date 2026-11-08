# Player build checks

Run from the repository root with a .NET 10 SDK on Windows:

```powershell
dotnet run --project tests/PlayerBuildChecks/PlayerBuildChecks.csproj -c Release
```

Checks persistence, identity separation, immutable build history, encounter links,
corrupt-file isolation, shared ability classification, exclusion of live caches from
recorded information, and the actual WPF user → build → encounter selection handlers.
The rendered preview uses synthetic test data and is written into the test output folder.
The tests do not attach to the game or write to the running application's data directory.
Actual packet capture and opening a production combat-history record require in-game validation.

Additional checks cover quest-clock persistence, duplicate quest starts, idle/periodic
reset boundaries, phase grouping, shared-party auto-linking after database commit,
retention after history deletion, and multi-track timeline filtering and rendering.

The helper also supports an online SQLite backup to a new destination:

```powershell
dotnet run --project tests/PlayerBuildChecks/PlayerBuildChecks.csproj -c Release -- --backup-db SOURCE DESTINATION
```

`--check-history-db PATH` opens a database read-only and validates compatibility of
up to ten most recent compressed encounter metadata records. These two helper modes
return before running the synthetic tests.

NewFeatureChecks covers hate baseline/delta recording, ambiguous skill attribution,
conservation of player/skill proportions, enemy catalog persistence, defeated enemy
retention/despawn/revival, selected enemy targeting, metadata/database round trips,
and history loading indicator recovery when selection notifications are absent or
an application callback fails. New windows are rendered with synthetic data. The
checks keep the test Application alive explicitly while pumping asynchronous UI work.

WidgetIntegrationChecks: 4種類の共通ウィジェット枠・配置・停止時の更新解除、待機時間に依存しない頻度判定、戦闘記録なしビルドの誤紐づけ防止を検証。

`PlayerBuildChecks.exe --inspect-history-db <CombatHistory.db>` は読み取り専用接続で直近6件のヘイト記録数・表示行数・技別合計の一致・受信情報の有無を確認する。起動中アプリのDBも読める。

`PlayerBuildChecks.exe --analyze-hate-db <CombatHistory.db>` は帰属不明の理由ごとの件数と増加量、参加者ごとの割合、技開始からの時間差を読み取り専用で分析する。割合の分母は正のヘイト増加量。
