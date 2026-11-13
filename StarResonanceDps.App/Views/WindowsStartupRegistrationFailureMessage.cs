// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>起動設定「Windows起動時にアプリを起動」を Windows に書けなかったことを出す。</summary>
public static class WindowsStartupRegistrationFailureMessage
{
    public static void Show(Window? owner)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("StartupSettings_Error_Title"),
            localization.GetString("StartupSettings_WindowsRegistrationFailed_Message"),
            localization.GetString("StartupSettings_WindowsRegistrationFailed_Detail"));
    }
}
