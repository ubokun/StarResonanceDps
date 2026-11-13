// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Services;

/// <summary>ホットキーの表示の文字。全体設定の行・三点メニュー・登録できなかったときのメッセージが使う。</summary>
public static class HotkeyText
{
    /// <summary>
    /// 操作の名前。切り替えは2つの名前を「/」でつなぐ。起動・ピン留め・クリック透過は三点メニューの2項目(「お気に入りを起動/すべてを停止」)、
    /// 最前面は全体設定のウィジェット設定「ウィジェットウィンドウ」の2つの選択肢。計測とリセットは集計タブのボタンと同じ文言。
    /// </summary>
    public static string GetLabel(HotkeyAction action)
    {
        var localization = LocalizationManager.Instance;
        return action switch
        {
            HotkeyAction.PlayerNameShowOrHide => localization.GetString("Hotkey_PlayerNameShowOrHide"),
            HotkeyAction.StartFavoritesOrStopAll => JoinMenuTexts("Manager_WidgetAction_StartAllFavorites", "Manager_WidgetAction_StopAll"),
            HotkeyAction.PinRunningOrUnpinAll => JoinMenuTexts("Manager_WidgetAction_PinAllRunning", "Manager_WidgetAction_UnpinAll"),
            HotkeyAction.ClickThroughPinnedOrClearAll => JoinMenuTexts("Manager_WidgetAction_ClickThroughAllPinned", "Manager_WidgetAction_ClearAllClickThrough"),
            HotkeyAction.WidgetWindowTopmostAlwaysOrPinnedOnly => JoinMenuTexts("WidgetWindowTopmost_Always", "WidgetWindowTopmost_PinnedOnly"),
            HotkeyAction.Benchmark => localization.GetString("Meter_Benchmark"),
            HotkeyAction.ResetEncounter => localization.GetString("Meter_Reset"),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };
    }

    /// <summary>キーの表示(「Ctrl+Shift+F8」の形)。割り当てなしは空。数字キーの D は外す。</summary>
    public static string Format(HotkeyBindingConfig binding)
    {
        if (!binding.IsAssigned)
        {
            return string.Empty;
        }

        var prefix = string.Empty;
        if (binding.Modifiers.HasFlag(ModifierKeys.Control))
        {
            prefix += "Ctrl+";
        }

        if (binding.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            prefix += "Alt+";
        }

        if (binding.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            prefix += "Shift+";
        }

        var keyName = binding.Key is >= Key.D0 and <= Key.D9
            ? ((int)(binding.Key - Key.D0)).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : binding.Key.ToString();

        return prefix + keyName;
    }

    /// <summary>登録できなかった理由の1行(「項目名: 理由」)。</summary>
    public static string FormatFailure(HotkeyRegistrationFailure failure)
    {
        var localization = LocalizationManager.Instance;
        var keyText = Format(failure.Binding);
        var reason = failure.IsInUse
            ? localization.Format("Hotkey_RegisterFailed_InUse", keyText)
            : localization.Format("Hotkey_RegisterFailed_Error", keyText, failure.ErrorCode);

        return $"{GetLabel(failure.Action)}: {reason}";
    }

    private static string JoinMenuTexts(string firstKey, string secondKey)
    {
        var localization = LocalizationManager.Instance;
        return $"{localization.GetString(firstKey)}/{localization.GetString(secondKey)}";
    }
}
