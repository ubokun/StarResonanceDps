// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>プラグインの一覧の「＋」で同じプラグインの新しいバージョンを選んだときの窓(置き換えるのはアプリの再起動のとき)。</summary>
public static class PluginUpdateMessage
{
    public static void ShowPending(Window? owner, string pluginName, string version)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("PluginUpdate_Title"),
            localization.Format("PluginUpdate_Message", pluginName, version),
            localization.GetString("PluginUpdate_Detail"));
    }

    /// <summary>新しいバージョンのファイルをプラグインのフォルダへ写せなかった。</summary>
    public static void ShowFailed(Window? owner, string pluginFolderName)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("PluginUpdateFailed_Title"),
            localization.GetString("PluginUpdateFailed_Message"),
            localization.Format("PluginUpdateFailed_Detail", pluginFolderName));
    }
}
