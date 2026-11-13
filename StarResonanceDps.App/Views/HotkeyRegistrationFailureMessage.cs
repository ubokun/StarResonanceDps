// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

/// <summary>
/// ホットキーを登録できなかったことをメッセージウィンドウで出す。本文は短い概要で、
/// 1件でも複数でも「項目名: 理由」は詳細に1行ずつ並べ、最後に直し方を1行置く。
/// </summary>
public static class HotkeyRegistrationFailureMessage
{
    public static void Show(Window? owner, IReadOnlyList<HotkeyRegistrationFailure> failures)
    {
        if (failures.Count == 0)
        {
            return;
        }

        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("Hotkey_Error_Title"),
            localization.GetString("Hotkey_RegisterFailed_Message"),
            string.Join(
                Environment.NewLine,
                failures.Select(HotkeyText.FormatFailure).Append(localization.GetString("Hotkey_RegisterFailed_Detail"))));
    }
}
