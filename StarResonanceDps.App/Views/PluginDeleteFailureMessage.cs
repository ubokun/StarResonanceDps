// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>プラグインの一覧のゴミ箱で、削除の印をプラグインのフォルダに書けなかったことを出す(プラグインは止めていない)。</summary>
public static class PluginDeleteFailureMessage
{
    /// <param name="folderName">Plugins フォルダの中の、そのプラグインのフォルダの名前。</param>
    public static void Show(Window? owner, string folderName)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("PluginDeleteFailed_Title"),
            localization.GetString("PluginDeleteFailed_Message"),
            localization.Format("PluginDeleteFailed_Detail", folderName));
    }
}
