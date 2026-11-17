// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Win32.SafeHandles;

namespace StarResonanceDps.App.Services;

/// <summary>
/// ファイルがほかのプロセスに開かれていて改名・削除できないかと、開いているプログラムの名前(Windows の Restart Manager)。
/// 更新の置き換え(<see cref="AppUpdateApplier"/>)が、置き換えを始められるかと、閉じてもらうプログラムを知らせるのに使う。
/// </summary>
internal static class FileUsage
{
    private const uint DeleteAccess = 0x00010000;
    private const uint ShareAll = 0x00000001 | 0x00000002 | 0x00000004;
    private const uint OpenExisting = 3;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorAccessDenied = 5;
    private const int ErrorMoreData = 234;

    /// <summary>
    /// 今すぐ改名・削除できないか。置き換え(改名)と同じ権限で、ほかの開き方を全部許して開いてみる。
    /// ほかのプロセスが改名を許す開き方で開いているだけなら開ける(置き換えも通る)ので、使用中にしない。
    /// 無いファイルと権限の無いファイルは使用中にしない(権限の問題は待っても直らず、置き換えの失敗として知らせる)。
    /// </summary>
    public static bool IsInUse(string path)
    {
        using var handle = CreateFile(path, DeleteAccess, ShareAll, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (!handle.IsInvalid)
        {
            return false;
        }

        return Marshal.GetLastWin32Error() is not (ErrorFileNotFound or ErrorPathNotFound or ErrorAccessDenied);
    }

    /// <summary>そのファイルを開いているプログラムの名前(重なりなし)。調べられなければ空。</summary>
    public static IReadOnlyList<string> FindUsers(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0 || RmStartSession(out var session, 0, Guid.NewGuid().ToString()) != 0)
        {
            return [];
        }

        try
        {
            if (RmRegisterResources(session, (uint)paths.Count, [.. paths], 0, IntPtr.Zero, 0, null) != 0)
            {
                return [];
            }

            uint count = 0;
            uint rebootReasons = 0;
            var result = RmGetList(session, out var needed, ref count, null, ref rebootReasons);
            if (result != ErrorMoreData)
            {
                return [];
            }

            var processes = new RmProcessInfo[needed];
            count = needed;
            if (RmGetList(session, out _, ref count, processes, ref rebootReasons) != 0)
            {
                return [];
            }

            return processes
                .Take((int)count)
                .Select(process => process.AppName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        finally
        {
            _ = RmEndSession(session);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RmUniqueProcess
    {
        public int ProcessId;
        public FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RmProcessInfo
    {
        public RmUniqueProcess Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AppName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string ServiceShortName;

        public int ApplicationType;
        public uint AppStatus;
        public uint TerminalServicesSessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool Restartable;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint session, int flags, string sessionKey);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint session);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(
        uint session,
        uint fileCount,
        string[] fileNames,
        uint applicationCount,
        IntPtr applications,
        uint serviceCount,
        string[]? serviceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(
        uint session,
        out uint needed,
        ref uint count,
        [In, Out] RmProcessInfo[]? processes,
        ref uint rebootReasons);
}
