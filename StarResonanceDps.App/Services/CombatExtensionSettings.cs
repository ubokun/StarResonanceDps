using System.IO;
using System.Text.Json;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

public sealed record CombatExtensionOptions
{
    public bool UseEnemyCatalog { get; init; }
    public bool FilterActions { get; init; } = true;
    public string[] HiddenActions { get; init; } = [];
    public string[] SelectedActions { get; init; } = [];
    public bool SyncQuestClock { get; init; }
    public bool ResetOnQuestTransition { get; init; } = true;
    public int IdleResetSeconds { get; init; }
    public int IntervalResetSeconds { get; init; }
    public int VisibleSeconds { get; init; } = 120;
    public bool PartyOnly { get; init; } = true;
    public bool ShowSkills { get; init; } = true;
    public bool ShowImagines { get; init; } = true;
    public bool ShowFactors { get; init; } = true;
    public bool ShowEnemies { get; init; } = true;
    public bool AutoLinkSavedPlayers { get; init; } = true;
}

public static class CombatExtensionSettings
{
    public static CombatExtensionOptions Current { get; private set; } = new();
    public static string? LoadIssue { get; private set; }
    private static System.Windows.Threading.DispatcherTimer? _resetTimer;
    private static string FilePath => Path.Combine(CombatRuntimePaths.DataDirectory, "CombatExtensions.json");

    public static void Initialize()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<CombatExtensionOptions>(File.ReadAllText(FilePath))
                    ?? throw new InvalidDataException("設定が空です。");
                Validate(loaded); Current = loaded;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentOutOfRangeException)
        { LoadIssue = "拡張設定を読み込めませんでした。既定値で動作します：" + ex.Message; }
        DB.EncounterSaved += encounter =>
        {
            foreach (var enemy in encounter.Entities.Values.Where(x => x.EntityType == Zproto.EEntityType.EntMonster))
                EnemyCatalogStore.Current.Observe(encounter, enemy.UUID, encounter.EndTime == DateTime.MinValue ? DateTime.UtcNow : encounter.EndTime.ToUniversalTime(), saved: true);
            _ = Task.Run(EnemyCatalogStore.Current.Save);
        };
        Apply(Current);
        _resetTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _resetTimer.Tick -= ResetTimer_Tick;
        _resetTimer.Tick += ResetTimer_Tick;
        _resetTimer.Start();
    }

    public static void Validate(CombatExtensionOptions options)
    {
        if (options.IdleResetSeconds is < 0 or > 86400 || options.IntervalResetSeconds is < 0 or > 86400 || options.VisibleSeconds is < 10 or > 3600)
            throw new ArgumentOutOfRangeException(nameof(options), "リセット時間は0～86400秒、表示幅は10～3600秒で指定してください。");
    }

    public static void Save(CombatExtensionOptions options)
    {
        Validate(options);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, FilePath, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        Current = options;
        Apply(options);
    }

    private static void Apply(CombatExtensionOptions options) => MessageManager.RunOnPacketThread(() =>
    {
        CustomCombatFeatures.SyncQuestClock = options.SyncQuestClock;
        CustomCombatFeatures.ResetOnQuestTransition = options.ResetOnQuestTransition;
        CustomCombatFeatures.IdleResetSeconds = options.IdleResetSeconds;
        CustomCombatFeatures.IntervalResetSeconds = options.IntervalResetSeconds;
    });

    private static int _catalogTicks;
    private static void ResetTimer_Tick(object? sender, EventArgs e)
    {
        if (++_catalogTicks % 10 == 0) _ = Task.Run(EnemyCatalogStore.Current.Save);
        if (Current.IdleResetSeconds > 0 || Current.IntervalResetSeconds > 0)
            MessageManager.RunOnPacketThread(() => CustomCombatFeatures.CheckReset(DateTime.UtcNow));
    }
}
