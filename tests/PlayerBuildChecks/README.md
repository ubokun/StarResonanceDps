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
