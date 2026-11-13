// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>
/// 集計タブで押した履歴の回を開けなかった(読み込みで例外)ことをメッセージウィンドウで出す。
/// 例外の中身はログに書いてあるので、画面には出さない。
/// </summary>
public static class HistoryLoadFailureMessage
{
    public static void Show(Window? owner)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("Aggregation_LoadFailed_Title"),
            localization.GetString("Aggregation_LoadFailed_Message"),
            localization.GetString("Aggregation_LoadFailed_Detail"));
    }
}
