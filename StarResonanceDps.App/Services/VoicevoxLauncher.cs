// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Serilog;
using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.Services;

/// <summary>起動設定「アプリ起動時にVOICEVOXを起動」の結果。</summary>
public enum VoicevoxLaunchResult
{
    /// <summary>VOICEVOX がもう動いていたので起動しなかった。</summary>
    AlreadyRunning,

    /// <summary>起動した。</summary>
    Launched,

    /// <summary>インストール先が空・<c>VOICEVOX.exe</c> が無い・起動できなかった。</summary>
    Failed
}

/// <summary>
/// VOICEVOX(公式のエディター)を起動する・インストール先を見つける。起動するのはインストール先の <c>VOICEVOX.exe</c>。
///
/// <para>
/// インストール先は起動設定の欄(<see cref="SettingsConfig.VoicevoxInstallFolder"/>)。手で選べるほか、
/// 動いている VOICEVOX を見たらそのフォルダーに書き換える。欄が空で VOICEVOX も動いていなければ、
/// Windows のアプリの一覧(アンインストールの情報)の VOICEVOX の行の <c>InstallLocation</c> で埋める
/// (zip で展開した VOICEVOX はこの一覧に載らない)。
/// </para>
///
/// <para>呼ぶのは UI スレッドだけ(設定を保存する)。</para>
/// </summary>
public static class VoicevoxLauncher
{
    private const string ExecutableFileName = "VOICEVOX.exe";
    private const string ProcessName = "VOICEVOX";

    private static readonly string[] UninstallKeyPaths =
    [
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
        @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    ];

    /// <summary>
    /// 欄のインストール先を見つけた場所に合わせて保存する。動いている VOICEVOX があればそのフォルダー(欄と違えば書き換える)、
    /// 無くて欄が空ならアプリの一覧の場所。どちらも無ければ何もしない。
    /// アプリの起動時・VOICEVOX が起動したとき・全体設定の画面を開いたときに呼ぶ。
    /// </summary>
    public static void UpdateInstallFolder()
    {
        var configManager = ConfigManager.Instance;
        var saved = configManager.AppConfig.Settings.VoicevoxInstallFolder;
        var folder = FindRunningInstallFolder()
            ?? (string.IsNullOrWhiteSpace(saved) ? FindInstalledFolder() : null);
        if (folder is null || string.Equals(folder, saved, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Log.Information("VOICEVOX install folder set to {Folder}", folder);
        configManager.SaveVoicevoxInstallFolder(folder);
    }

    /// <summary>
    /// アプリの起動時に VOICEVOX を起動する。もう動いていれば起動しない。
    /// 起動できなかったときは <see cref="VoicevoxLaunchResult.Failed"/> で、ログに残す(知らせるのは呼ぶ側)。
    /// </summary>
    public static VoicevoxLaunchResult LaunchOnStartup(string installFolder)
    {
        if (IsRunning())
        {
            return VoicevoxLaunchResult.AlreadyRunning;
        }

        if (string.IsNullOrWhiteSpace(installFolder))
        {
            Log.Warning("Could not launch VOICEVOX: the install folder is not set");
            return VoicevoxLaunchResult.Failed;
        }

        var executablePath = Path.Combine(installFolder, ExecutableFileName);
        if (!File.Exists(executablePath))
        {
            Log.Warning("Could not launch VOICEVOX: {Path} does not exist", executablePath);
            return VoicevoxLaunchResult.Failed;
        }

        try
        {
            Process.Start(new ProcessStartInfo(executablePath)
            {
                UseShellExecute = true,
                WorkingDirectory = installFolder
            })?.Dispose();
            Log.Information("Launched VOICEVOX from {Path}", executablePath);
            return VoicevoxLaunchResult.Launched;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not launch VOICEVOX from {Path}", executablePath);
            return VoicevoxLaunchResult.Failed;
        }
    }

    private static bool IsRunning()
    {
        var processes = Process.GetProcessesByName(ProcessName);
        foreach (var process in processes)
        {
            process.Dispose();
        }

        return processes.Length > 0;
    }

    /// <summary>動いている VOICEVOX の実行ファイルのフォルダー。動いていない・場所を読めなければ null。</summary>
    private static string? FindRunningInstallFolder()
    {
        string? folder = null;
        foreach (var process in Process.GetProcessesByName(ProcessName))
        {
            using (process)
            {
                if (folder is not null)
                {
                    continue;
                }

                try
                {
                    if (process.MainModule?.FileName is { Length: > 0 } fileName)
                    {
                        folder = Path.GetDirectoryName(fileName);
                    }
                }
                catch (Exception exception)
                {
                    Log.Warning(exception, "Could not read the path of the running VOICEVOX process {ProcessId}", process.Id);
                }
            }
        }

        return folder;
    }

    /// <summary>アプリの一覧(現在のユーザー・全ユーザー)で表示名が VOICEVOX の行の、<c>VOICEVOX.exe</c> があるインストール先。無ければ null。</summary>
    private static string? FindInstalledFolder()
    {
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            foreach (var keyPath in UninstallKeyPaths)
            {
                try
                {
                    using var uninstallKey = root.OpenSubKey(keyPath);
                    if (uninstallKey is null)
                    {
                        continue;
                    }

                    foreach (var name in uninstallKey.GetSubKeyNames())
                    {
                        using var appKey = uninstallKey.OpenSubKey(name);
                        if (appKey?.GetValue("DisplayName") is not string displayName
                            || !displayName.StartsWith(ProcessName, StringComparison.OrdinalIgnoreCase)
                            || appKey.GetValue("InstallLocation") is not string { Length: > 0 } location)
                        {
                            continue;
                        }

                        var folder = location.Trim().Trim('"');
                        if (File.Exists(Path.Combine(folder, ExecutableFileName)))
                        {
                            return folder;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Log.Warning(exception, "Could not read the installed apps under {Root}\\{KeyPath}", root.Name, keyPath);
                }
            }
        }

        return null;
    }
}
