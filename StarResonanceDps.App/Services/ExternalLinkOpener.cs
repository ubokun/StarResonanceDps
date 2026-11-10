using System.Diagnostics;
using Serilog;

namespace StarResonanceDps.App.Services;

/// <summary>アプリの外のリンクを既定のブラウザで開く。開けなかったらログに残して false を返す。</summary>
internal static class ExternalLinkOpener
{
    public static bool Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not open {Url}", url);
            return false;
        }
    }
}
