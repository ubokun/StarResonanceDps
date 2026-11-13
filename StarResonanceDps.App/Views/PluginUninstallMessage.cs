// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>プラグインの一覧のゴミ箱。プラグインを削除するかを確かめる(フォルダを消すのはアプリの再起動のとき)。</summary>
public static class PluginUninstallMessage
{
    public static bool Confirm(Window owner, string pluginName)
    {
        var localization = LocalizationManager.Instance;
        return ConfirmWindow.ShowText(
            owner,
            localization.GetString("Confirm_UninstallPlugin_Title"),
            localization.Format("Confirm_UninstallPlugin_Message", pluginName),
            localization.GetString("Confirm_UninstallPlugin_Detail"));
    }
}
