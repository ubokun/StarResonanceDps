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
