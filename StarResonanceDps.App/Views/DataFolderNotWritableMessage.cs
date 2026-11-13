// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>
/// 実行フォルダの Data に書けないので起動できないことを出す。詳細にはフォルダの場所を入れる。
/// ログはそのフォルダの中にあって書けないので、知らせるのはこの窓だけ。
/// </summary>
public static class DataFolderNotWritableMessage
{
    public static void Show(Window? owner, string directory)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("Startup_Error_Title"),
            localization.GetString("Startup_DataFolderNotWritable_Message"),
            localization.Format("Startup_DataFolderNotWritable_Detail", directory));
    }
}
