using System.IO;
using Serilog;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグインの更新のファイルの扱い。読み込んだ DLL は動いている間は置き換えられないので、新しい版のファイルをプラグインのフォルダの中へ写しておき、
/// 次の起動で読む前に置き換える。フォルダの中の設定ファイルには触らない。
/// </summary>
internal static class PluginUpdateFiles
{
    private const string PendingFolderName = "update.pending";
    private const string StagingFolderName = "update.pending.tmp";
    private const string DiscardedFolderName = "update.pending.old";

    public static bool HasPending(string pluginFolder)
    {
        return Directory.Exists(Path.Combine(pluginFolder, PendingFolderName));
    }

    /// <summary>
    /// 新しい版のファイルを写す。全部写してから名前を変えるので、途中で失敗しても半端なものは残らない。
    /// 待っている更新があれば、消さずに名前を変えて入れ替える。消す途中で失敗すると欠けた更新が残り、次の起動で置き換えるとそこに無いファイルを今の版から消してしまう。
    /// </summary>
    public static bool TryStage(string pluginFolder, PluginDependencyScan scan)
    {
        var stagingFolder = Path.Combine(pluginFolder, StagingFolderName);
        var pendingFolder = Path.Combine(pluginFolder, PendingFolderName);
        var discardedFolder = Path.Combine(pluginFolder, DiscardedFolderName);
        try
        {
            if (Directory.Exists(stagingFolder))
            {
                Directory.Delete(stagingFolder, recursive: true);
            }

            foreach (var file in scan.Files)
            {
                var targetPath = Path.Combine(stagingFolder, Path.GetRelativePath(scan.Folder, file));
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(file, targetPath);
            }

            var replacesPending = Directory.Exists(pendingFolder);
            if (replacesPending)
            {
                if (Directory.Exists(discardedFolder))
                {
                    Directory.Delete(discardedFolder, recursive: true);
                }

                Directory.Move(pendingFolder, discardedFolder);
            }

            try
            {
                Directory.Move(stagingFolder, pendingFolder);
            }
            catch (Exception exception) when (replacesPending && exception is IOException or UnauthorizedAccessException)
            {
                Directory.Move(discardedFolder, pendingFolder);
                throw;
            }

            PluginManager.TryDeleteFolder(discardedFolder);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Error(exception, "Failed to copy the plugin update files: {Folder}", pluginFolder);
            PluginManager.TryDeleteFolder(stagingFolder);
            return false;
        }
    }

    /// <summary>
    /// 待っている更新があれば、読む前に置き換える。新しい版は入れ直しと同じ扱いなので、アプリを終わらせた印も消す。
    /// 待っているファイルは最後に片付ける(置き換えの途中で失敗しても残り、次の起動でもう一度置き換える)。
    /// </summary>
    public static bool TryApplyPending(string pluginFolder)
    {
        PluginManager.TryDeleteFolder(Path.Combine(pluginFolder, StagingFolderName));
        PluginManager.TryDeleteFolder(Path.Combine(pluginFolder, DiscardedFolderName));

        var pendingFolder = Path.Combine(pluginFolder, PendingFolderName);
        if (!Directory.Exists(pendingFolder))
        {
            return true;
        }

        try
        {
            var currentFiles = ReadCurrentFiles(pluginFolder);
            var newFiles = Directory.GetFiles(pendingFolder, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(pendingFolder, path))
                .ToArray();

            foreach (var file in newFiles)
            {
                var targetPath = Path.Combine(pluginFolder, file);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(Path.Combine(pendingFolder, file), targetPath, overwrite: true);
            }

            foreach (var file in currentFiles.Except(newFiles, StringComparer.OrdinalIgnoreCase))
            {
                DeleteOldFile(pluginFolder, file);
            }

            File.Delete(Path.Combine(pluginFolder, PluginManager.TerminatedAppMarkerFileName));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Error(exception, "Failed to apply the pending plugin update; it is applied again at the next start: {Folder}", pluginFolder);
            return false;
        }

        Log.Information("Applied the pending plugin update: {Folder}", pluginFolder);
        DiscardAppliedPending(pluginFolder);
        return true;
    }

    /// <summary>
    /// 置き換え終わった更新のファイルを片付ける。消す前に名前を変えるので、消す途中で失敗しても欠けた更新は残らない。
    /// 名前を変えられなければそのまま残す。次の起動で同じ版をもう一度置き換えるだけなので、プラグインは今の起動でも動かす。
    /// </summary>
    private static void DiscardAppliedPending(string pluginFolder)
    {
        var discardedFolder = Path.Combine(pluginFolder, DiscardedFolderName);
        try
        {
            Directory.Move(Path.Combine(pluginFolder, PendingFolderName), discardedFolder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Warning(exception, "Could not clean up the applied plugin update; it is applied again at the next start: {Folder}", pluginFolder);
            return;
        }

        PluginManager.TryDeleteFolder(discardedFolder);
    }

    /// <summary>今の版が使うファイル(フォルダからの相対パス)。読めなければ空で、新しい版の上書きだけをする(残った古い部品は読まれない)。</summary>
    private static IReadOnlyList<string> ReadCurrentFiles(string pluginFolder)
    {
        var mainAssemblyPath = Path.Combine(pluginFolder, Path.GetFileName(pluginFolder) + ".dll");
        if (!File.Exists(mainAssemblyPath))
        {
            return [];
        }

        try
        {
            var scan = PluginDependencyScanner.Scan(mainAssemblyPath);
            return scan.Files.Select(file => Path.GetRelativePath(scan.Folder, file)).ToArray();
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Could not read the files of the current plugin version; old files may remain: {Folder}", pluginFolder);
            return [];
        }
    }

    /// <summary>新しい版が使わないファイルを消す。空になった言語のフォルダも消す。</summary>
    private static void DeleteOldFile(string pluginFolder, string relativePath)
    {
        File.Delete(Path.Combine(pluginFolder, relativePath));
        for (var parent = Path.GetDirectoryName(relativePath); !string.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent))
        {
            var folder = Path.Combine(pluginFolder, parent);
            if (Directory.EnumerateFileSystemEntries(folder).Any())
            {
                return;
            }

            Directory.Delete(folder);
        }
    }
}
