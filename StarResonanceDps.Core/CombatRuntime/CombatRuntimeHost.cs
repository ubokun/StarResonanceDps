using Serilog;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using StarResonanceDps.Core.CombatRuntime.Database;
using StarResonanceDps.Core.Logging;

namespace StarResonanceDps.Core.CombatRuntime;

public sealed class CombatRuntimeHost
{
    private static readonly Lazy<CombatRuntimeHost> LazyInstance = new(() => new CombatRuntimeHost());
    private static bool _isLoggingConfigured;
    private readonly object _sync = new();
    private bool _isInitialized;

    private CombatRuntimeHost()
    {
    }

    public static CombatRuntimeHost Instance => LazyInstance.Value;

    public bool IsInitialized
    {
        get
        {
            lock (_sync)
            {
                return _isInitialized;
            }
        }
    }

    public void Initialize()
    {
        lock (_sync)
        {
            if (_isInitialized)
            {
                return;
            }

            // 設定は App 側が持っている。Core にファイルを読む口は無いので、
            // 呼ぶ前に CombatRuntimeSettings.Apply を通してもらう必要がある。
            // 忘れると既定値(キャプチャ自動・分割あり・保持なし)で黙って動いてしまうため、
            // ここで落とす。
            if (!CombatRuntimeSettings.HasBeenApplied)
            {
                throw new InvalidOperationException(
                    "Call CombatRuntimeSettings.Apply before CombatRuntimeHost.Initialize.");
            }

            // ログの設定は起動の最初に App が行う(ConfigureLogging)。初期化の中で行うと、それより前の失敗が残らない。
            if (!_isLoggingConfigured)
            {
                throw new InvalidOperationException(
                    "Call CombatRuntimeHost.ConfigureLogging before CombatRuntimeHost.Initialize.");
            }

            Directory.CreateDirectory(CombatRuntimePaths.DataDirectory);
            DB.Init();
            AppState.LoadDataTables();

            if (string.IsNullOrEmpty(MessageManager.NetCaptureDeviceName))
            {
                var bestDefaultDevice = MessageManager.TryFindBestNetworkDevice();
                if (bestDefaultDevice is not null)
                {
                    MessageManager.NetCaptureDeviceName = bestDefaultDevice.Name;
                }
            }

            if (DB.CheckIfMigrationsNeeded())
            {
                DB.CheckAndRunMigrations();
            }

            if (!string.IsNullOrEmpty(MessageManager.NetCaptureDeviceName))
            {
                MessageManager.InitializeCapturing();
            }

            _isInitialized = true;
        }
    }

    public void Shutdown()
    {
        lock (_sync)
        {
            if (!_isInitialized)
            {
                return;
            }

            // 履歴の読み込みが閉じた接続に当たらないよう、先に止めて待つ。
            EncounterHistoryProvider.ShutdownLoads();

            // 受信だけを止め、控えは消さずに最後の回を保存する(保存が控えの値を焼き付けるため)。
            MessageManager.StopReceiving();
            if (EncounterManager.Current is not null)
            {
                EncounterManager.ShutdownManager();
            }

            DB.CloseAndSave();

            // 0 は無限。掃除を回さない。
            if (CombatRuntimeSettings.DatabaseMaxEncounterCount > 0)
            {
                DB.TrimEncountersToLimit(CombatRuntimeSettings.DatabaseMaxEncounterCount, vacuum: true);
            }

            _isInitialized = false;
        }
    }

    /// <summary>
    /// ログ(Data\CombatRuntime.log とログタブ)の設定。起動の最初、Data に書けると確かめた直後に App が呼ぶ。
    /// それより前に出る例外はどこにも残らない。
    /// </summary>
    public static void ConfigureLogging()
    {
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Sink(new ManagerLogSink(PacketDiagnosticLogStore.Instance))
            .WriteTo.File(CombatRuntimePaths.LogFilePath);

        Log.Logger = loggerConfiguration.CreateLogger();
        _isLoggingConfigured = true;
    }

    /// <summary>
    /// 前のバージョンのログ(Data\CombatRuntime.log)を消す。アプリのバージョンが変わった最初の起動で、<see cref="ConfigureLogging"/> の後に App が呼ぶ。
    /// ログのファイルは開いている間は消せないので、閉じて消して開き直す。消せなくてもアプリの処理は止めない。
    /// </summary>
    public static void ClearLogs()
    {
        Log.CloseAndFlush();

        Exception? failure = null;
        try
        {
            File.Delete(CombatRuntimePaths.LogFilePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            failure = exception;
        }

        ConfigureLogging();
        if (failure is not null)
        {
            Log.Warning(failure, "Could not delete {Path}", CombatRuntimePaths.LogFilePath);
        }
    }
}
