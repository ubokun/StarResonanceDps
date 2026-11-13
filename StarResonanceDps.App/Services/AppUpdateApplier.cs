// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using Serilog;
using StarResonanceDps.App.Views;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

/// <summary>更新のモードへの依頼。<paramref name="ProcessId"/> は閉じるのを待つ今のバージョンのプロセス。</summary>
internal sealed record AppUpdateApplyRequest(string TargetDirectory, int ProcessId);

/// <summary>更新のモードが起動し直したことの知らせ。<paramref name="ProcessId"/> は更新のモードのプロセス。</summary>
internal sealed record AppUpdateRestart(bool IsUpdated, int ProcessId);

/// <summary>
/// 更新のモード。新しいバージョンのexeが作業フォルダ(Update\new)から動き、今のバージョンが閉じるのを待ってアプリのフォルダのファイルを置き換え、
/// 起動し直して終わる。動いている間は表示中の画像も置き換えられないので、置き換えはアプリを閉じてから行う。
///
/// <para>
/// 触るのはファイルの一覧(<see cref="FileListName"/>)にあるものだけ。設定・履歴・ログ・追加したプラグインは一覧に無い。
/// 前のファイルは Update\backup へ動かし、失敗したら全部戻して元のバージョンを起動する。
/// </para>
///
/// <para>
/// <b><see cref="ApplyArgument"/> の引数の形と <see cref="StartedEventName"/> は変えない。</b>古いバージョンが新しいバージョンへ渡す約束で、変えると古いバージョンから更新できなくなる。
/// </para>
/// </summary>
internal static class AppUpdateApplier
{
    public const string ExecutableFileName = "StarResonanceDps.exe";

    /// <summary>配るファイルの一覧(1行に1つ、アプリのフォルダからの相対パス)。公開のときに csproj が書く。</summary>
    public const string FileListName = "update-files.txt";

    public const string ApplyArgument = "--apply-update";

    private const string UpdatedArgument = "--update-finished";
    private const string NotUpdatedArgument = "--update-cancelled";
    private const string StartedEventName = @"Local\StarResonanceDps.UpdateApplyStarted";
    private const string PluginsFolderPrefix = @"Plugins\";
    private const int WaitPollMilliseconds = 500;

    public static bool HasFileList(string directory)
    {
        return File.Exists(Path.Combine(directory, FileListName));
    }

    /// <summary>新しいバージョンが置き換えを始められる状態になったことの合図。起動する側が作って待つ。</summary>
    public static EventWaitHandle CreateStartedSignal()
    {
        return new EventWaitHandle(false, EventResetMode.ManualReset, StartedEventName);
    }

    public static bool TryParseApplyRequest(string[] args, [NotNullWhen(true)] out AppUpdateApplyRequest? request)
    {
        request = null;
        if (args.Length != 3
            || args[0] != ApplyArgument
            || !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var processId))
        {
            return false;
        }

        request = new AppUpdateApplyRequest(args[1], processId);
        return true;
    }

    public static AppUpdateRestart? ParseRestartArguments(string[] args)
    {
        if (args.Length != 2
            || args[0] is not (UpdatedArgument or NotUpdatedArgument)
            || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var processId))
        {
            return null;
        }

        return new AppUpdateRestart(args[0] == UpdatedArgument, processId);
    }

    public static void WaitForProcessExit(int processId)
    {
        using var process = TryGetProcess(processId);
        process?.WaitForExit();
    }

    /// <summary>
    /// 更新のモードの本体。今のバージョンと二重起動の印とファイルが空くのを待ち(時間の上限なし)、置き換えて起動し直す。
    /// キャンセルしたら何も変えずに元のバージョンを起動し直す。今のバージョンは閉じる途中で止められないので、閉じるのは待つ。
    /// </summary>
    public static async Task RunAsync(AppUpdateApplyRequest request, Func<bool> tryAcquireInstance, Action releaseInstance)
    {
        using var cancellation = new CancellationTokenSource();
        var window = UpdateProgressWindow.Create(owner: null);
        window.CancelRequested += (_, _) =>
        {
            cancellation.Cancel();
            window.DisableCancel();
        };
        window.ShowStatus("Update_Progress_WaitingForExit");
        window.Show();

        using (var previousVersion = TryGetProcess(request.ProcessId))
        {
            SignalStarted();
            if (previousVersion is not null)
            {
                await previousVersion.WaitForExitAsync();
            }
        }

        // ログのファイルは今のバージョンが閉じるまで掴まれているので、ここから書く。
        CombatRuntimeHost.ConfigureLogging();
        Log.Information("Applying the update to {Directory}", request.TargetDirectory);

        var failure = await ReplaceFilesAsync(window, request, tryAcquireInstance, cancellation.Token);
        var isUpdated = failure is null && !cancellation.IsCancellationRequested;

        window.CloseNow();
        if (failure is { } value)
        {
            UpdateFailureMessage.Show(null, value);
        }

        releaseInstance();

        // 戻せなかったときは、起動できるとは限らない。窓の案内どおりzipで直してもらう。
        if (failure != AppUpdateFailure.RestoreFailed)
        {
            RestartApplication(request.TargetDirectory, isUpdated);
        }
    }

    private static async Task<AppUpdateFailure?> ReplaceFilesAsync(
        UpdateProgressWindow window,
        AppUpdateApplyRequest request,
        Func<bool> tryAcquireInstance,
        CancellationToken cancellationToken)
    {
        ReplacePlan plan;
        try
        {
            plan = await Task.Run(() => ReplacePlan.Create(AppContext.BaseDirectory, request.TargetDirectory));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Log.Error(exception, "Could not read the update file lists");
            return AppUpdateFailure.Replace;
        }

        if (!await WaitUntilReadyAsync(window, plan, tryAcquireInstance, cancellationToken))
        {
            Log.Information("The update was cancelled before replacing any file");
            return null;
        }

        window.DisableCancel();
        window.ShowStatus("Update_Progress_Replacing");
        window.ShowProgress(0);
        var progress = new Progress<double>(ratio => window.ShowProgress(ratio));
        return await Task.Run(() => plan.Apply(progress));
    }

    /// <summary>二重起動の印を取り、置き換えるファイルが全部空くのを待つ。キャンセルされたら false。</summary>
    private static async Task<bool> WaitUntilReadyAsync(
        UpdateProgressWindow window,
        ReplacePlan plan,
        Func<bool> tryAcquireInstance,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!tryAcquireInstance())
            {
                await Task.Delay(WaitPollMilliseconds, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            while (await Task.Run(plan.HasFileInUse, cancellationToken))
            {
                window.ShowStatus("Update_Progress_WaitingForFiles");
                await Task.Delay(WaitPollMilliseconds, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static void RestartApplication(string targetDirectory, bool isUpdated)
    {
        Log.Information("Starting the application (updated: {IsUpdated})", isUpdated);

        // 起動するアプリが同じログのファイルを開けるように、先に閉じる。
        Log.CloseAndFlush();
        try
        {
            var startInfo = new ProcessStartInfo(Path.Combine(targetDirectory, ExecutableFileName))
            {
                UseShellExecute = false,
                WorkingDirectory = targetDirectory
            };
            startInfo.ArgumentList.Add(isUpdated ? UpdatedArgument : NotUpdatedArgument);
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            using var process = Process.Start(startInfo);
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            UpdateFailureMessage.ShowRestartFailed(null, isUpdated);
        }
    }

    private static void SignalStarted()
    {
        if (EventWaitHandle.TryOpenExisting(StartedEventName, out var started))
        {
            using (started)
            {
                started.Set();
            }
        }
    }

    private static Process? TryGetProcess(int processId)
    {
        try
        {
            return Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// 置き換えるファイルの組。新しい一覧のファイルを置き、前の一覧にあって新しい一覧に無いものは消す。
    /// 前の一覧にあるのにフォルダに無い Plugins のファイルは、アンインストールした同梱プラグインなので戻さない。
    /// </summary>
    private sealed class ReplacePlan
    {
        private readonly string _newDirectory;
        private readonly string _targetDirectory;
        private readonly string _backupDirectory;
        private readonly List<string> _filesToInstall;
        private readonly List<string> _filesToRemove;

        private ReplacePlan(
            string newDirectory,
            string targetDirectory,
            List<string> filesToInstall,
            List<string> filesToRemove)
        {
            _newDirectory = newDirectory;
            _targetDirectory = targetDirectory;
            _backupDirectory = Path.Combine(CombatRuntimePaths.UpdateDirectory, "backup");
            _filesToInstall = filesToInstall;
            _filesToRemove = filesToRemove;
        }

        public static ReplacePlan Create(string newDirectory, string targetDirectory)
        {
            var newFiles = ReadFileList(newDirectory);
            var oldFiles = ReadFileList(targetDirectory);

            var newSet = new HashSet<string>(newFiles, StringComparer.OrdinalIgnoreCase);
            var oldSet = new HashSet<string>(oldFiles, StringComparer.OrdinalIgnoreCase);

            var filesToInstall = newFiles
                .Where(file => !IsUninstalledPluginFile(file, oldSet, targetDirectory))
                .ToList();
            filesToInstall.Add(FileListName);

            var filesToRemove = oldFiles
                .Where(file => !newSet.Contains(file))
                .ToList();

            return new ReplacePlan(newDirectory, targetDirectory, filesToInstall, filesToRemove);
        }

        private static bool IsUninstalledPluginFile(string file, HashSet<string> oldFiles, string targetDirectory)
        {
            return file.StartsWith(PluginsFolderPrefix, StringComparison.OrdinalIgnoreCase)
                && oldFiles.Contains(file)
                && !File.Exists(Path.Combine(targetDirectory, file));
        }

        private static List<string> ReadFileList(string directory)
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
            var files = new List<string>();
            foreach (var line in File.ReadAllLines(Path.Combine(directory, FileListName)))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (!Path.GetFullPath(Path.Combine(root, line)).StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"The file list has an entry outside the folder: {line}");
                }

                files.Add(line);
            }

            return files;
        }

        public bool HasFileInUse()
        {
            return _filesToInstall.Concat(_filesToRemove)
                .Any(file => IsInUse(Path.Combine(_targetDirectory, file)));
        }

        private static bool IsInUse(string path)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return false;
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // 権限の問題は待っても直らない。置き換えの失敗として知らせる。
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        }

        /// <summary>置き換える。失敗したら全部戻す。成功なら null。</summary>
        public AppUpdateFailure? Apply(IProgress<double> progress)
        {
            var moved = new List<(string Path, string BackupPath)>();
            var installed = new List<string>();
            try
            {
                foreach (var file in _filesToInstall.Concat(_filesToRemove))
                {
                    var path = Path.Combine(_targetDirectory, file);
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    var backupPath = Path.Combine(_backupDirectory, file);
                    Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                    File.Move(path, backupPath);
                    moved.Add((path, backupPath));
                }

                var lastPercent = -1;
                foreach (var file in _filesToInstall)
                {
                    var path = Path.Combine(_targetDirectory, file);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.Copy(Path.Combine(_newDirectory, file), path);
                    installed.Add(path);

                    var percent = installed.Count * 100 / _filesToInstall.Count;
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        progress.Report((double)installed.Count / _filesToInstall.Count);
                    }
                }

                foreach (var file in _filesToRemove)
                {
                    DeleteEmptyParentFolders(Path.Combine(_targetDirectory, file));
                }

                Log.Information(
                    "Replaced the application files ({Installed} installed, {Removed} removed)",
                    _filesToInstall.Count,
                    _filesToRemove.Count);
                return null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Log.Error(exception, "Could not replace the application files; restoring the previous files");
                return TryRestore(moved, installed) ? AppUpdateFailure.Replace : AppUpdateFailure.RestoreFailed;
            }
        }

        private bool TryRestore(List<(string Path, string BackupPath)> moved, List<string> installed)
        {
            try
            {
                foreach (var path in installed)
                {
                    File.Delete(path);
                    DeleteEmptyParentFolders(path);
                }

                foreach (var (path, backupPath) in moved)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.Move(backupPath, path);
                }

                Log.Information("Restored the previous application files");
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Log.Fatal(exception, "Could not restore the previous application files");
                return false;
            }
        }

        /// <summary>消したファイルのフォルダが空なら、アプリのフォルダの手前まで消していく。</summary>
        private void DeleteEmptyParentFolders(string path)
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_targetDirectory));
            var directory = Path.GetDirectoryName(path);
            while (directory is not null
                   && !string.Equals(Path.TrimEndingDirectorySeparator(directory), root, StringComparison.OrdinalIgnoreCase)
                   && Directory.Exists(directory)
                   && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
                directory = Path.GetDirectoryName(directory);
            }
        }
    }
}
