// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>
/// 起動時に読めなかった設定ファイル(AppSettings.json・WidgetSettings.json)を、1ファイルにつき1枚出す。
/// 詳細にはファイルの場所を入れる。
/// </summary>
public static class SettingsLoadFailureMessage
{
    public static void Show(Window? owner)
    {
        if (ConfigManager.Instance.HasLoadFailed)
        {
            Show(owner, AppDataPaths.AppSettingsPath);
        }

        if (WidgetStateManager.Instance.HasLoadFailed)
        {
            Show(owner, AppDataPaths.WidgetStatePath);
        }
    }

    public static void Show(Window? owner, string filePath)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("SettingsFile_LoadFailed_Title"),
            localization.GetString("SettingsFile_LoadFailed_Message"),
            localization.Format("SettingsFile_LoadFailed_Detail", filePath));
    }
}
