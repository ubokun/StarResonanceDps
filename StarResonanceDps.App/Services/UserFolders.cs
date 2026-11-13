// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Runtime.InteropServices;
using Serilog;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 今のユーザーのフォルダのパス。ファイル・フォルダを選ぶ窓が、前に使ったフォルダの記録が無いときに開くフォルダ(<see cref="Microsoft.Win32.CommonItemDialog.DefaultDirectory"/>)に使う。
/// 取れなければnullで、窓はWindowsが決めるフォルダで開く(取れなかったことはログに残す)。
/// </summary>
internal static class UserFolders
{
    // FOLDERID_Downloads。ダウンロードはEnvironment.SpecialFolderに無い。
    private static readonly Guid DownloadsFolderId = new("374DE290-123F-4565-9164-39C4925E467B");

    public static string? Pictures => GetSpecialFolder(Environment.SpecialFolder.MyPictures);

    public static string? Documents => GetSpecialFolder(Environment.SpecialFolder.MyDocuments);

    public static string? Downloads
    {
        get
        {
            var result = SHGetKnownFolderPath(DownloadsFolderId, 0, IntPtr.Zero, out var pathPointer);
            try
            {
                if (result != 0)
                {
                    Log.Warning("Could not resolve the user's Downloads folder (HRESULT 0x{Result:X8})", result);
                    return null;
                }

                return Marshal.PtrToStringUni(pathPointer);
            }
            finally
            {
                // 失敗したときも呼び出し側が解放する(SHGetKnownFolderPathの決まり)。
                Marshal.FreeCoTaskMem(pathPointer);
            }
        }
    }

    private static string? GetSpecialFolder(Environment.SpecialFolder folder)
    {
        var path = Environment.GetFolderPath(folder);
        if (string.IsNullOrEmpty(path))
        {
            Log.Warning("Could not resolve the user's {Folder} folder", folder);
            return null;
        }

        return path;
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
        uint dwFlags,
        IntPtr hToken,
        out IntPtr ppszPath);
}
