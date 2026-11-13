// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>プラグインの一覧の「＋」で選んだプラグインが、同じか新しいバージョンですでに一覧にあることを出す。</summary>
public static class PluginAlreadyAddedMessage
{
    public static void Show(Window? owner)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("PluginAlreadyAdded_Title"),
            localization.GetString("PluginAlreadyAdded_Message"));
    }
}
