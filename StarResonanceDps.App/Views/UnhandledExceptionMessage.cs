// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>
/// どこにも受け手の無い例外で終わることを出す。持ち主はメイン窓(まだ無ければ無し)。
/// 例外の全文はログに書く。
/// </summary>
public static class UnhandledExceptionMessage
{
    public static void Show(Window? owner)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("Error_Unhandled_Title"),
            localization.GetString("Error_Unhandled_Message"),
            localization.GetString("Error_Unhandled_Detail"));
    }
}
