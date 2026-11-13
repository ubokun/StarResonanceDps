// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>動いていたプラグインを、例外か通信の取り残しで止めたことを出す。次の起動でまた読む。</summary>
public static class PluginStoppedMessage
{
    /// <param name="isPacketBacklog">通信の受け取りが追いつかなかったために止めたか(偽なら例外)。</param>
    public static void Show(Window? owner, string pluginName, bool isPacketBacklog)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("PluginError_Title"),
            localization.Format("PluginStopped_Message", pluginName),
            isPacketBacklog
                ? localization.GetString("PluginStoppedPacketBacklog_Detail")
                : localization.GetString("PluginStopped_Detail"));
    }
}
