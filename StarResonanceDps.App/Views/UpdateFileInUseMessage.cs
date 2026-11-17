// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.IO;
using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>
/// 更新の置き換えるファイルをほかのプログラムが開いていて置き換えられないとき、そのプログラムを閉じてから続けるかを聞く。
/// 詳細には、開いているプログラムが分かればその名前、分からなければファイルの名前を出す。
/// </summary>
public static class UpdateFileInUseMessage
{
    public static bool Show(Window owner, IReadOnlyList<string> programs, string file)
    {
        var localization = LocalizationManager.Instance;
        var detail = programs.Count == 0
            ? localization.Format("Update_FileInUseUnknownProgram_Detail", Path.GetFileName(file))
            : localization.Format(
                "Update_FileInUse_Detail",
                string.Join(localization.GetString("Update_Progress_ProgramSeparator"), programs));
        return ConfirmWindow.ShowText(
            owner,
            localization.GetString("Update_Title"),
            localization.GetString("Update_FileInUse_Message"),
            detail);
    }
}
