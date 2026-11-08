using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Serilog;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.Views;
using StarResonanceDps.Core.Logging;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\StarResonanceDps.SingleInstance";
    private const int ShowWindowRestore = 9;

    private static Mutex? _singleInstanceMutex;

    /// <summary>受け手の無い例外の窓は最初の1件だけ出す(2件目からはログだけ)。</summary>
    private static int _unhandledExceptionShown;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!TryAcquireSingleInstance())
        {
            BringExistingInstanceToFront();
            Shutdown(0);
            return;
        }

        RegisterUnhandledExceptionHandlers();
        ManagerTraceOutput.Configure();

        // 設定・履歴・ログは実行フォルダの Data に書く。書けなければ動かせない(ログもそのフォルダの中なので書けない)。
        // 窓の言語とテーマは、設定を読むだけ読んで当てる。
        if (!TryEnsureWritableDataDirectory())
        {
            ApplyLanguageAndTheme(ConfigManager.Instance.GetSettingsSnapshot());
            ShowStartupFailureAndExit(mainWindow => DataFolderNotWritableMessage.Show(mainWindow, CombatRuntimePaths.DataDirectory));
            return;
        }

        // ログはここから。これより後の失敗(設定・言語・テーマ・Npcap・初期化・受け手の無い例外)は全部 Data\CombatRuntime.log に残る。
        CombatRuntimeHost.ConfigureLogging();

        var configManager = ConfigManager.Instance;

        // アダプターを選び直したときの保存は Core からは行えない(Core にファイルの口が無い)。
        // 合図だけ受け取って、こちらで AppSettings.json へ書く。
        NetworkAdapterSession.Instance.CaptureSettingsPersistRequested +=
            (_, _) => configManager.PersistCaptureSettingsFromRuntime();

        // 起動できないときの窓もこの言語で出す。
        ApplyLanguageAndTheme(configManager.GetSettingsSnapshot());
        base.OnStartup(e);

        // Npcap が無いとキャプチャ用のアダプタを引けない(初期化の中で SharpPcap が例外になる)ので、初期化の前に見る。
        // 古いときは初期化の後、メイン窓が出たところで知らせて続ける(MainWindow)。
        var npcapVersion = NpcapVersionProbe.GetVersion();
        if (NpcapVersionProbe.IsMissing(npcapVersion))
        {
            ShowStartupFailureAndExit(NpcapInstallMessage.Show);
            return;
        }

        try
        {
            CombatExtensionSettings.Initialize();
            CombatRuntimeHost.Instance.Initialize();
            EntityHistoryProjection.Initialize();
            SavedPlayerBuildAutoLink.Initialize();
            SkillCooldownTracker.Instance.Initialize();
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Startup initialization failed");
            ShowStartupFailureAndExit(StartupInitializationFailedMessage.Show);
            return;
        }

        if (NpcapVersionProbe.IsOutdated(npcapVersion))
        {
            Log.Warning("Npcap version {Version} is below {MinimumVersion}", npcapVersion, NpcapVersionProbe.MinimumVersion);
        }

        // メイン窓はここで作る(App.xaml に StartupUri を置かない)。StartupUri だと、上の枝で終わるときも
        // OnStartup の後にメイン窓が作られて表示される。
        // MainWindow は Application のプロパティと同じ名前なので、型は名前空間から書く。
        var mainWindow = StarResonanceDps.App.MainWindow.Create(npcapVersion);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private static void ApplyLanguageAndTheme(SettingsConfig settings)
    {
        LocalizationManager.Instance.ApplyLanguageIndex(settings.LanguageIndex);
        ThemeManager.Instance.ApplyGlobalTheme(settings);
    }

    private static bool TryEnsureWritableDataDirectory()
    {
        try
        {
            CombatRuntimePaths.EnsureWritableDataDirectory();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// 起動できないことを知らせて終わる。メイン窓を見せるだけの形で出し(どのアプリの窓かが分かる。Loaded の処理は走らない)、
    /// 描かれたところでメイン窓が <paramref name="showMessage"/> の窓を持ち主つきで出す。持ち主は止まるので、操作できるのは出した窓だけ。
    /// 閉じたら終了コード 1 で終わる(MainWindow が行う)。
    /// </summary>
    private void ShowStartupFailureAndExit(Action<Window> showMessage)
    {
        // 窓を閉じたときに WPF が終了コード0で終わらせないよう、終わりは自分で決める。
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var mainWindow = StarResonanceDps.App.MainWindow.CreateForStartupFailure(showMessage);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    /// <summary>
    /// 受け手の無い例外は、ログに全文を書き、「エラー」の窓を出してから終了コード 1 で終わる。
    /// 画面のスレッドの例外は WPF に渡さず(渡すと窓を出さずに落ちる)、ほかのスレッドと Task の例外は画面のスレッドで窓を出す。
    ///
    /// <para>
    /// プラグインのコードから出た例外(<see cref="PluginManager"/> が例外の経路で見分ける)は、画面のスレッドと Task のものならそのプラグインを止めて続ける。
    /// ほかのスレッドのものは .NET が終わらせるので止められない。次の起動から初期化しないよう、プラグインに印を書いてから今までどおり終わる。
    /// </para>
    /// </summary>
    private void RegisterUnhandledExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            if (PluginManager.Instance.TryStopForUnhandledException(args.Exception))
            {
                return;
            }

            HandleUnhandledException(args.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var exception = args.ExceptionObject as Exception ?? new InvalidOperationException(args.ExceptionObject?.ToString());
            PluginManager.Instance.MarkTerminatedApp(exception);
            HandleUnhandledException(exception);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
            if (PluginManager.Instance.TryStopForUnobservedException(args.Exception))
            {
                return;
            }

            HandleUnhandledException(args.Exception);
        };
    }

    private void HandleUnhandledException(Exception exception)
    {
        Log.Fatal(exception, "Unhandled exception");
        if (Interlocked.Exchange(ref _unhandledExceptionShown, 1) != 0)
        {
            return;
        }

        void ShowAndExit()
        {
            UnhandledExceptionMessage.Show(MainWindow is { IsVisible: true } mainWindow ? mainWindow : null);
            Shutdown(1);
        }

        if (Dispatcher.CheckAccess())
        {
            ShowAndExit();
        }
        else
        {
            Dispatcher.Invoke(ShowAndExit);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 通知タブで打ったばかりの文章は、保存を待っていることがある。
        (MainWindow?.DataContext as ViewModels.MainViewModel)?.Notifications.FlushPendingSave();
        GlobalHotkeyService.Instance.Dispose();
        NotificationService.Shutdown();
        SkillCooldownTracker.Instance.Shutdown();
        CombatRuntimeHost.Instance.Shutdown();
        EnemyCatalogStore.Current.Save();
        PluginManager.Instance.ShutdownAll();
        ReleaseSingleInstance();
        base.OnExit(e);
    }

    private static bool TryAcquireSingleInstance()
    {
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (createdNew)
        {
            return true;
        }

        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
        return false;
    }

    private static void ReleaseSingleInstance()
    {
        if (_singleInstanceMutex is null)
        {
            return;
        }

        _singleInstanceMutex.ReleaseMutex();
        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
    }

    private static void BringExistingInstanceToFront()
    {
        using var currentProcess = Process.GetCurrentProcess();
        foreach (var process in Process.GetProcessesByName(currentProcess.ProcessName))
        {
            using (process)
            {
                if (process.Id == currentProcess.Id)
                {
                    continue;
                }

                var handle = process.MainWindowHandle;
                if (handle == IntPtr.Zero)
                {
                    process.Refresh();
                    handle = process.MainWindowHandle;
                }

                if (handle == IntPtr.Zero)
                {
                    continue;
                }

                ShowWindow(handle, ShowWindowRestore);
                SetForegroundWindow(handle);
                return;
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}

