// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

/// <summary>更新の置き換えが済んだ後の起動で、今のバージョンを出す。</summary>
public static class UpdateCompletedMessage
{
    public static void Show(Window? owner)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("Update_Completed_Title"),
            localization.Format("Update_Completed_Message", AppUpdateService.CurrentVersion.ToString(3)));
    }
}
