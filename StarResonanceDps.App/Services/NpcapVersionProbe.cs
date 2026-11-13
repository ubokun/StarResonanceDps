// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace StarResonanceDps.App.Services;

public static partial class NpcapVersionProbe
{
    /// <summary>
    /// これより古い版は、フィルターを掛けて捕捉している最中に OS ごと落ちる不具合(1.86 で修正)が残っている。
    /// 無いときは起動しない、古いときは知らせて続ける。
    /// </summary>
    public static readonly Version MinimumVersion = new(1, 86);

    public const string DownloadUrl = "https://npcap.com/";

    /// <summary>Npcap が入っていない(版を検出できない)。<see cref="GetVersion"/> は検出できなければ空の版を返す。</summary>
    public static bool IsMissing(Version version)
    {
        return version == new Version();
    }

    public static bool IsOutdated(Version version)
    {
        return !IsMissing(version) && version < MinimumVersion;
    }

    public static string GetVersionString()
    {
        try
        {
            var pointer = pcap_lib_version();
            return Marshal.PtrToStringAnsi(pointer) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static Version GetVersion()
    {
        var versionString = GetVersionString();
        if (string.IsNullOrWhiteSpace(versionString))
        {
            return new Version();
        }

        var match = NpcapVersionRegex().Match(versionString);
        return match.Success && Version.TryParse(match.Groups[1].Value, out var version)
            ? version
            : new Version();
    }

    [GeneratedRegex(@"\w+ version (\d+(?:\.\d+)+)", RegexOptions.IgnoreCase)]
    private static partial Regex NpcapVersionRegex();

    [DllImport("wpcap.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr pcap_lib_version();
}
