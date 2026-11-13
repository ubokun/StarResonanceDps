// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.IO;
using System.Windows;
using Serilog;
using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 読み上げに使う VOICEVOX のスタイルを、通知より前に初期化しておく。
///
/// <para>
/// エンジンは初めて使うスタイルの合成に時間がかかり、合成の打ち切りを越えるとその通知は読み上げられない。
/// 合成の直前に初期化すると通知が遅れるので、アプリの起動時、通知タブの値が変わったとき、
/// VOICEVOX のエディタが起動して <c>runtime-info.json</c> を書いたときに送る。
/// 送るのは、オンで読み上げ方法が VOICEVOX の項目が選んでいるスタイルだけ(項目ごとに違ってよいので全部)。
/// </para>
///
/// <para>
/// 返事は待たない(起動も通知タブも止めない)。初期化が通ったスタイルは、次の設定の変更では送らない(音量を変えるたびに送らないため)。
/// 失敗したら、次の設定の変更か起動でまた送る。失敗はログに残す(つながらないことは起動時と設定時の確認がメッセージで知らせる)。
/// </para>
///
/// <para>
/// エディタはエンジンが要求を受けられるようになる前に <c>runtime-info.json</c> を書くので、書かれたら
/// 初期化が通るまで <see cref="RetryInterval"/> ごとに送り直す。もう一度書かれたら最初からやり直す。
/// </para>
/// </summary>
public static class VoicevoxStyleInitializer
{
    private const string RuntimeInfoFileName = "runtime-info.json";

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    private static readonly object Sync = new();
    private static bool _isStarted;
    private static FileSystemWatcher? _runtimeInfoWatcher;

    /// <summary>初期化が通ったスタイル。</summary>
    private static readonly HashSet<int> InitializedStyleIds = [];

    /// <summary>送って返事を待っているスタイル。</summary>
    private static readonly HashSet<int> PendingStyleIds = [];

    /// <summary><c>runtime-info.json</c> が書かれるたびに進める。送り直しは自分の番号が古くなったら止まる。</summary>
    private static int _runtimeInfoGeneration;

    /// <summary>アプリの起動時に1回呼ぶ。通知タブの変更と <c>runtime-info.json</c> の書き込みを受け始め、今の設定のスタイルを初期化する。</summary>
    public static void Start()
    {
        lock (Sync)
        {
            if (_isStarted)
            {
                return;
            }

            _isStarted = true;
        }

        ConfigManager.Instance.NotificationsChanged += ConfigManager_NotificationsChanged;
        WatchRuntimeInfo();
        InitializeSelectedStyles();
    }

    private static void WatchRuntimeInfo()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "voicevox");
        if (!Directory.Exists(directory))
        {
            Log.Information("VOICEVOX folder {Directory} does not exist; not watching {FileName}", directory, RuntimeInfoFileName);
            return;
        }

        try
        {
            var watcher = new FileSystemWatcher(directory, RuntimeInfoFileName)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size
            };
            watcher.Created += RuntimeInfo_Written;
            watcher.Changed += RuntimeInfo_Written;
            watcher.Renamed += RuntimeInfo_Written;
            watcher.EnableRaisingEvents = true;
            _runtimeInfoWatcher = watcher;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or PlatformNotSupportedException)
        {
            Log.Warning(ex, "Could not watch VOICEVOX {FileName} in {Directory}", RuntimeInfoFileName, directory);
        }
    }

    private static void ConfigManager_NotificationsChanged(object? sender, EventArgs e)
    {
        InitializeSelectedStyles();
    }

    /// <summary>
    /// エディタが起動した(エンジンも起動し直した)。覚えている初期化を捨て、通るまで送り直す。
    /// 起動設定の VOICEVOX のインストール先も、動いているエディタの場所に合わせる(設定の保存は UI スレッドで)。
    /// </summary>
    private static void RuntimeInfo_Written(object sender, FileSystemEventArgs e)
    {
        Application.Current?.Dispatcher.InvokeAsync(VoicevoxLauncher.UpdateInstallFolder);

        int generation;
        lock (Sync)
        {
            InitializedStyleIds.Clear();
            PendingStyleIds.Clear();
            generation = ++_runtimeInfoGeneration;
        }

        _ = RetryUntilInitializedAsync(generation);
    }

    private static async Task RetryUntilInitializedAsync(int generation)
    {
        var hasLoggedWaiting = false;

        // つながったが応答を読めなかったスタイルは、送り直しても変わらないのでこの回はもう送らない。
        var failedStyleIds = new HashSet<int>();
        while (true)
        {
            int[] styleIds;
            lock (Sync)
            {
                if (generation != _runtimeInfoGeneration)
                {
                    return;
                }

                styleIds = [.. GetSelectedStyleIds().Where(id => !InitializedStyleIds.Contains(id) && !failedStyleIds.Contains(id))];
            }

            if (styleIds.Length == 0)
            {
                return;
            }

            var isConnectFailed = false;
            foreach (var styleId in styleIds)
            {
                var result = await VoicevoxClient.InitializeStyleAsync(styleId).ConfigureAwait(false);
                if (result.Kind == VoicevoxResultKind.Success)
                {
                    lock (Sync)
                    {
                        if (generation == _runtimeInfoGeneration)
                        {
                            InitializedStyleIds.Add(styleId);
                        }
                    }

                    Log.Information("Initialized VOICEVOX style {StyleId} after VOICEVOX started", styleId);
                    continue;
                }

                if (result.Kind != VoicevoxResultKind.ConnectFailed)
                {
                    failedStyleIds.Add(styleId);
                    Log.Warning("Could not initialize VOICEVOX style {StyleId} after VOICEVOX started: {Detail}", styleId, result.Detail);
                    continue;
                }

                if (!hasLoggedWaiting)
                {
                    hasLoggedWaiting = true;
                    Log.Information("VOICEVOX started; waiting for the engine to initialize style {StyleId} ({Detail})", styleId, result.Detail);
                }

                isConnectFailed = true;
                break;
            }

            if (isConnectFailed)
            {
                await Task.Delay(RetryInterval).ConfigureAwait(false);
            }
        }
    }

    private static void InitializeSelectedStyles()
    {
        var styleIds = new List<int>();
        lock (Sync)
        {
            foreach (var styleId in GetSelectedStyleIds())
            {
                if (InitializedStyleIds.Contains(styleId) || !PendingStyleIds.Add(styleId))
                {
                    continue;
                }

                styleIds.Add(styleId);
            }
        }

        foreach (var styleId in styleIds)
        {
            _ = InitializeAsync(styleId);
        }
    }

    /// <summary>オンで、読み上げ方法が VOICEVOX の項目(プラグインの項目も)が選んでいるスタイル(重なりなし)。</summary>
    private static IEnumerable<int> GetSelectedStyleIds()
    {
        return ConfigManager.Instance.GetNotificationsSnapshot().AllItems
            .Where(item => item.IsEnabled && item.UsesVoicevox && item.VoicevoxStyleId is not null)
            .Select(item => item.VoicevoxStyleId!.Value)
            .Distinct();
    }

    private static async Task InitializeAsync(int styleId)
    {
        var result = await VoicevoxClient.InitializeStyleAsync(styleId).ConfigureAwait(false);
        lock (Sync)
        {
            PendingStyleIds.Remove(styleId);
            if (result.Kind == VoicevoxResultKind.Success)
            {
                InitializedStyleIds.Add(styleId);
            }
        }

        if (result.Kind != VoicevoxResultKind.Success)
        {
            Log.Warning("Could not initialize VOICEVOX style {StyleId}: {Detail}", styleId, result.Detail);
        }
    }
}
