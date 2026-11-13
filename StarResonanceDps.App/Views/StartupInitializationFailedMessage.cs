// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>
/// 起動時の初期化(DB・データの表・キャプチャ)で例外が出たので起動できないことを出す。
/// 例外の全文はログに書く。
/// </summary>
public static class StartupInitializationFailedMessage
{
    public static void Show(Window owner)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("Startup_Error_Title"),
            localization.GetString("Startup_InitializationFailed_Message"),
            localization.GetString("Startup_InitializationFailed_Detail"));
    }
}
