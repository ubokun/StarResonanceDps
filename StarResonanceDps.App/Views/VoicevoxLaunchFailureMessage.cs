// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>起動設定「アプリ起動時にVOICEVOXを起動」で、VOICEVOX を起動できなかったことを出す。</summary>
public static class VoicevoxLaunchFailureMessage
{
    public static void Show(Window? owner)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("Voicevox_LaunchFailed_Title"),
            localization.GetString("Voicevox_LaunchFailed_Message"),
            localization.GetString("Voicevox_LaunchFailed_Detail"));
    }
}
