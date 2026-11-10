using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using Serilog;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Views;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

/// <summary>更新の確認の状態。ヘルプタブのバージョンの行に出す。</summary>
public enum AppUpdateStatus
{
    NotChecked,
    Checking,
    UpToDate,
    Available,
    NoRelease,
    Failed
}

/// <summary>更新が失敗した理由。エラーの窓の詳細を決める。</summary>
public enum AppUpdateFailure
{
    Network,
    Server,
    RateLimit,
    InvalidRelease,
    NotFromZip,
    Corrupt,
    Write,
    Start,
    Replace,
    RestoreFailed
}

/// <summary>
/// アプリの更新の確認・取得・新しいバージョンへの交代。ファイルの置き換えは新しいバージョンが行う(<see cref="AppUpdateApplier"/>)。
/// 画面のスレッドから呼ぶ。
///
/// <para>
/// 更新があるかはバージョンの比較だけで決める。データだけの更新も、バージョンを上げて同じ経路で配る。
/// </para>
/// </summary>
public static class AppUpdateService
{
    private const string Sha256DigestPrefix = "sha256:";
    private const int BufferSize = 81920;
    private const int StartCheckMilliseconds = 50;
    private const double BytesPerMegabyte = 1024 * 1024;

    private static bool _isBusy;
    private static Task _cleanUpTask = Task.CompletedTask;

    /// <summary>確認の途中だけある。</summary>
    private static CancellationTokenSource? _checkCancellation;

    /// <summary>今のバージョン。実行ファイルの版(csprojのVersion)の先頭3つの数字。</summary>
    public static Version CurrentVersion { get; } = ReadCurrentVersion();

    /// <summary>最後の確認の結果。</summary>
    public static AppUpdateStatus Status { get; private set; } = AppUpdateStatus.NotChecked;

    /// <summary>確認の途中か。途中はヘルプタブのボタンがキャンセルになる。</summary>
    public static bool IsChecking => Status == AppUpdateStatus.Checking;

    /// <summary>今の起動が、更新の置き換えが済んだ後の起動か。</summary>
    public static bool WasJustUpdated { get; private set; }

    /// <summary><see cref="Status"/> が変わった。画面のスレッドで届く。</summary>
    public static event EventHandler? StatusChanged;

    /// <summary>
    /// 起動の最初に呼ぶ。更新の作業フォルダが残っていれば消す。
    /// 更新のモードから起動されたときは、そのプロセスが終わってから消す(動いている実行ファイルは消せない)。
    /// </summary>
    public static void CleanUpAtStartup(string[] args)
    {
        var restart = AppUpdateApplier.ParseRestartArguments(args);
        WasJustUpdated = restart is { IsUpdated: true };
        _cleanUpTask = Task.Run(() =>
        {
            if (restart is not null)
            {
                AppUpdateApplier.WaitForProcessExit(restart.ProcessId);
            }

            DeleteWorkFolder();
        });
    }

    /// <summary>
    /// 起動時の確認。失敗は窓を出さず、ヘルプタブの行とログに残す(オフラインで起動するたびに窓が出ないように)。
    /// 窓を持つ子の窓(設定の窓)を開いている間は、確認の窓を出さず行の表示だけにする(閉じるときに未保存の確認が割り込むため)。
    /// </summary>
    public static Task CheckOnStartupAsync(Window owner)
    {
        return CheckAsync(owner, isManual: false);
    }

    /// <summary>ヘルプタブの「更新を確認」。失敗も窓で知らせる。</summary>
    public static Task CheckManuallyAsync(Window owner)
    {
        return CheckAsync(owner, isManual: true);
    }

    /// <summary>確認をやめる。ヘルプタブの行は確認していない表示に戻る。</summary>
    public static void CancelCheck()
    {
        _checkCancellation?.Cancel();
    }

    private static async Task CheckAsync(Window owner, bool isManual)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        SetStatus(AppUpdateStatus.Checking);
        try
        {
            var check = await FetchLatestReleaseAsync();
            SetStatus(check.Status);

            if (check.Failure is { } failure)
            {
                if (isManual)
                {
                    UpdateFailureMessage.ShowCheckFailed(owner, failure);
                }

                return;
            }

            if (check.Release is not { } release)
            {
                return;
            }

            if (!AppUpdateApplier.HasFileList(CombatRuntimePaths.BaseDirectory))
            {
                Log.Information("Version {Version} is available, but this folder has no update file list", release.Version);
                if (isManual)
                {
                    UpdateFailureMessage.Show(owner, AppUpdateFailure.NotFromZip);
                }

                return;
            }

            if (!isManual && owner.OwnedWindows.Count > 0)
            {
                return;
            }

            if (await ConfirmAsync(owner, release))
            {
                await DownloadAndRestartAsync(owner, release);
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    private static void SetStatus(AppUpdateStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(null, EventArgs.Empty);
    }

    private static Version ReadCurrentVersion()
    {
        var version = typeof(AppUpdateService).Assembly.GetName().Version
            ?? throw new InvalidOperationException("The application assembly has no version.");
        return new Version(version.Major, version.Minor, version.Build);
    }

    private static async Task<ReleaseCheck> FetchLatestReleaseAsync()
    {
        using var cancellation = new CancellationTokenSource();
        _checkCancellation = cancellation;
        try
        {
            using var request = GitHubHttp.CreateApiRequest(ProjectLinks.LatestReleaseApiUrl);
            using var response = await GitHubHttp.Client.SendAsync(request, cancellation.Token);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Log.Information("No release has been published yet");
                return new ReleaseCheck(AppUpdateStatus.NoRelease);
            }

            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                Log.Warning("GitHub limited the update check (HTTP {StatusCode})", (int)response.StatusCode);
                return ReleaseCheck.Failed(AppUpdateFailure.RateLimit);
            }

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("The update check failed (HTTP {StatusCode})", (int)response.StatusCode);
                return ReleaseCheck.Failed(GetHttpFailure(response.StatusCode));
            }

            return ParseLatestRelease(await response.Content.ReadAsStringAsync(cancellation.Token));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Log.Information("The update check was cancelled");
            return new ReleaseCheck(AppUpdateStatus.NotChecked);
        }
        catch (HttpRequestException exception)
        {
            Log.Warning(exception, "The update check failed");
            return ReleaseCheck.Failed(AppUpdateFailure.Network);
        }
        finally
        {
            _checkCancellation = null;
        }
    }

    /// <summary>GitHubが5xxを返したならサーバーの側、それ以外(応答が無いときも)は接続の側として知らせる。</summary>
    private static AppUpdateFailure GetHttpFailure(HttpStatusCode? statusCode)
    {
        return (int?)statusCode >= 500 ? AppUpdateFailure.Server : AppUpdateFailure.Network;
    }

    private static ReleaseCheck ParseLatestRelease(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var tag = root.GetProperty("tag_name").GetString();
            if (!TryParseTag(tag, out var version))
            {
                Log.Error("The latest release has an unreadable tag {Tag}", tag);
                return ReleaseCheck.Failed(AppUpdateFailure.InvalidRelease);
            }

            if (version <= CurrentVersion)
            {
                return new ReleaseCheck(AppUpdateStatus.UpToDate);
            }

            var packageName = $"StarResonanceDps-{version.ToString(3)}.zip";
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() != packageName)
                {
                    continue;
                }

                var digest = asset.TryGetProperty("digest", out var digestElement) ? digestElement.GetString() : null;
                if (digest is null || !digest.StartsWith(Sha256DigestPrefix, StringComparison.Ordinal))
                {
                    Log.Error("{Package} has no SHA-256 digest ({Digest})", packageName, digest);
                    return ReleaseCheck.Failed(AppUpdateFailure.InvalidRelease);
                }

                Log.Information("Version {Version} is available", version);
                return new ReleaseCheck(
                    AppUpdateStatus.Available,
                    new AppRelease(
                        version,
                        asset.GetProperty("browser_download_url").GetString()!,
                        digest[Sha256DigestPrefix.Length..],
                        asset.GetProperty("size").GetInt64()));
            }

            Log.Error("The latest release {Tag} has no {Package}", tag, packageName);
            return ReleaseCheck.Failed(AppUpdateFailure.InvalidRelease);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            Log.Error(exception, "Could not read the latest release");
            return ReleaseCheck.Failed(AppUpdateFailure.InvalidRelease);
        }
    }

    /// <summary>タグは「v」と3つの数字(v4.0.1)。</summary>
    private static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version();
        if (tag is null || !tag.StartsWith('v') || !Version.TryParse(tag[1..], out var parsed) || parsed.Build < 0 || parsed.Revision >= 0)
        {
            return false;
        }

        version = parsed;
        return true;
    }

    /// <summary>確認の窓を出す。窓の移動の最中なら、終わってから出す(待ちを移動のループの内側に入れ子にしない)。</summary>
    private static Task<bool> ConfirmAsync(Window owner, AppRelease release)
    {
        var completion = new TaskCompletionSource<bool>();
        OwnerModalWindow.RunWhenNotMovingOrSizing(
            () => completion.SetResult(UpdateConfirmMessage.Show(owner, release.Version)));
        return completion.Task;
    }

    /// <summary>
    /// 取得して新しいバージョンを起動し、起動できたらメイン窓を普通に閉じる(開いていたウィジェットは次の起動で開き直す)。
    /// 起動できなければ今のバージョンのまま知らせる。
    /// </summary>
    private static async Task DownloadAndRestartAsync(Window owner, AppRelease release)
    {
        using var cancellation = new CancellationTokenSource();
        var window = UpdateProgressWindow.Create(owner);
        window.CancelRequested += (_, _) => cancellation.Cancel();
        OwnerModalWindow.Show(window, owner);

        var hasStartedNewVersion = false;
        AppUpdateFailure? failure = null;
        try
        {
            hasStartedNewVersion = await DownloadAndStartNewVersionAsync(window, release, cancellation.Token);
            if (!hasStartedNewVersion)
            {
                Log.Error("The new version exited before it started applying the update");
                failure = AppUpdateFailure.Start;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Log.Information("The update was cancelled");
        }
        catch (AppUpdateException exception)
        {
            Log.Error(exception, "Could not update ({Failure})", exception.Failure);
            failure = exception.Failure;
        }
        catch (HttpRequestException exception)
        {
            Log.Error(exception, "Could not download the update");
            failure = GetHttpFailure(exception.StatusCode);
        }
        catch (InvalidDataException exception)
        {
            Log.Error(exception, "Could not read the update package");
            failure = AppUpdateFailure.Corrupt;
        }
        catch (Win32Exception exception)
        {
            Log.Error(exception, "Could not start the new version");
            failure = AppUpdateFailure.Start;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Error(exception, "Could not write the update files");
            failure = AppUpdateFailure.Write;
        }

        window.CloseNow();
        if (hasStartedNewVersion)
        {
            owner.Close();
            return;
        }

        await Task.Run(DeleteWorkFolder);
        if (failure is { } value)
        {
            UpdateFailureMessage.Show(owner, value);
        }
    }

    private static async Task<bool> DownloadAndStartNewVersionAsync(
        UpdateProgressWindow window,
        AppRelease release,
        CancellationToken cancellationToken)
    {
        await _cleanUpTask;

        var workDirectory = CombatRuntimePaths.UpdateDirectory;
        var packagePath = Path.Combine(workDirectory, "package.zip");
        var newDirectory = Path.Combine(workDirectory, "new");
        await Task.Run(() =>
        {
            DeleteWorkFolder();
            Directory.CreateDirectory(newDirectory);
        }, cancellationToken);

        window.ShowStatus("Update_Progress_Downloading");
        window.ShowProgress(0, FormatDownloadSize(0, release.PackageSize));
        var downloadProgress = new Progress<(long Done, long Total)>(value => window.ShowProgress(
            (double)value.Done / value.Total,
            FormatDownloadSize(value.Done, value.Total)));
        await Task.Run(() => DownloadPackageAsync(release, packagePath, downloadProgress, cancellationToken), cancellationToken);

        window.ShowStatus("Update_Progress_Extracting");
        window.ShowProgress(0);
        var extractProgress = new Progress<double>(ratio => window.ShowProgress(ratio));
        await Task.Run(() => ExtractPackage(packagePath, newDirectory, extractProgress, cancellationToken), cancellationToken);

        // ここから先は取り消せない。
        window.DisableCancel();
        return await StartNewVersionAsync(newDirectory);
    }

    private static string FormatDownloadSize(long done, long total)
    {
        return LocalizationManager.Instance.Format(
            "Update_Progress_Size",
            done / BytesPerMegabyte,
            total / BytesPerMegabyte);
    }

    /// <summary>zipを取り、GitHubが返すSHA-256と照らす。合わなければ止める。</summary>
    private static async Task DownloadPackageAsync(
        AppRelease release,
        string path,
        IProgress<(long Done, long Total)> progress,
        CancellationToken cancellationToken)
    {
        using var response = await GitHubHttp.Client.GetAsync(release.PackageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? release.PackageSize;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var buffer = new byte[BufferSize];
        long done = 0;
        var lastPercent = -1L;
        while (true)
        {
            int read;
            try
            {
                read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                throw new AppUpdateException(AppUpdateFailure.Network, exception);
            }

            if (read == 0)
            {
                break;
            }

            hash.AppendData(buffer, 0, read);
            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            done += read;

            var percent = done * 100 / Math.Max(total, 1);
            if (percent != lastPercent)
            {
                lastPercent = percent;
                progress.Report((done, Math.Max(total, done)));
            }
        }

        var actual = Convert.ToHexString(hash.GetHashAndReset());
        if (!string.Equals(actual, release.PackageSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new AppUpdateException(
                AppUpdateFailure.Corrupt,
                new InvalidDataException($"The SHA-256 of the package is {actual}, expected {release.PackageSha256}."));
        }
    }

    private static void ExtractPackage(string packagePath, string directory, IProgress<double> progress, CancellationToken cancellationToken)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(packagePath);

        var total = archive.Entries.Count;
        var done = 0;
        var lastPercent = -1;
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"The package has an entry outside its folder: {entry.FullName}");
            }

            // フォルダの項目は名前が空。
            if (entry.Name.Length > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                entry.ExtractToFile(path, overwrite: true);
            }

            done++;
            var percent = done * 100 / total;
            if (percent != lastPercent)
            {
                lastPercent = percent;
                progress.Report((double)done / total);
            }
        }

        if (!AppUpdateApplier.HasFileList(directory))
        {
            throw new InvalidDataException($"The package has no {AppUpdateApplier.FileListName}.");
        }
    }

    /// <summary>
    /// 新しいバージョンを更新のモードで起動し、置き換えを始められる状態になったかを待つ。
    /// 起動はしたがすぐ終わった(必要なものが足りない、セキュリティソフトに止められた)ときは false。
    /// </summary>
    private static async Task<bool> StartNewVersionAsync(string newDirectory)
    {
        var executablePath = Path.Combine(newDirectory, AppUpdateApplier.ExecutableFileName);
        if (!File.Exists(executablePath))
        {
            throw new AppUpdateException(AppUpdateFailure.Start, new FileNotFoundException("The new version is missing.", executablePath));
        }

        using var started = AppUpdateApplier.CreateStartedSignal();
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = newDirectory
        };
        startInfo.ArgumentList.Add(AppUpdateApplier.ApplyArgument);
        startInfo.ArgumentList.Add(Path.TrimEndingDirectorySeparator(CombatRuntimePaths.BaseDirectory));
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));

        using var process = Process.Start(startInfo)
            ?? throw new AppUpdateException(AppUpdateFailure.Start, new InvalidOperationException("The new version did not start."));

        return await Task.Run(() =>
        {
            while (!started.WaitOne(StartCheckMilliseconds))
            {
                if (process.HasExited)
                {
                    return started.WaitOne(0);
                }
            }

            return true;
        });
    }

    private static void DeleteWorkFolder()
    {
        var directory = CombatRuntimePaths.UpdateDirectory;
        if (!Directory.Exists(directory))
        {
            return;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
            Log.Information("Deleted the update work folder");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Warning(exception, "Could not delete the update work folder; it is deleted at the next start");
        }
    }

    /// <summary>最新のリリース。<paramref name="PackageSha256"/> はzipのSHA-256(16進)。</summary>
    private sealed record AppRelease(Version Version, string PackageUrl, string PackageSha256, long PackageSize);

    private sealed record ReleaseCheck(AppUpdateStatus Status, AppRelease? Release = null, AppUpdateFailure? Failure = null)
    {
        public static ReleaseCheck Failed(AppUpdateFailure failure)
        {
            return new ReleaseCheck(AppUpdateStatus.Failed, Failure: failure);
        }
    }

    private sealed class AppUpdateException(AppUpdateFailure failure, Exception innerException)
        : Exception(failure.ToString(), innerException)
    {
        public AppUpdateFailure Failure { get; } = failure;
    }
}
