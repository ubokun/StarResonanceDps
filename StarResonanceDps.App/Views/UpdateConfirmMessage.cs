// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>新しいバージョンがあるときに、更新するかを聞く。</summary>
public static class UpdateConfirmMessage
{
    public static bool Show(Window owner, Version version)
    {
        var localization = LocalizationManager.Instance;
        return ConfirmWindow.ShowText(
            owner,
            localization.GetString("Update_Title"),
            localization.Format("Update_Confirm_Message", version.ToString(3)),
            localization.GetString("Update_Confirm_Detail"));
    }
}
