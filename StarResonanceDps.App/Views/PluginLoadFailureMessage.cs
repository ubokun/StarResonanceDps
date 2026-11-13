// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

/// <summary>
/// 読めなかった・初期化できなかった・削除や更新ができなかったプラグインを「名前: 理由」で並べて出す(起動時に1回、「＋」で足したとき)。
/// 最後の行は使う人のやり方で、プラグインを直す理由があれば再インストールか作者への連絡、削除や更新の理由があれば再起動(両方ならこの順に2行)。
/// </summary>
public static class PluginLoadFailureMessage
{
    public static void Show(Window? owner, IReadOnlyList<PluginLoadFailure> failures)
    {
        if (failures.Count == 0)
        {
            return;
        }

        var localization = LocalizationManager.Instance;
        var lines = failures
            .Select(failure => localization.Format("PluginLoadFailed_Line", failure.Name, FormatReason(failure)))
            .ToList();
        if (failures.Any(failure => !IsRetriedOnRestart(failure.Reason)))
        {
            lines.Add(localization.GetString("PluginLoadFailed_Detail"));
        }

        if (failures.Any(failure => IsRetriedOnRestart(failure.Reason)))
        {
            lines.Add(localization.GetString("PluginLoadFailed_RestartDetail"));
        }

        MessageWindow.Show(
            owner,
            localization.GetString("PluginError_Title"),
            localization.GetString("PluginLoadFailed_Message"),
            string.Join(Environment.NewLine, lines));
    }

    /// <summary>削除と更新は、アプリが次の起動の読み込みの前にやり直す。</summary>
    private static bool IsRetriedOnRestart(PluginErrorReason reason)
    {
        return reason is PluginErrorReason.DeletionFailed or PluginErrorReason.UpdateFailed;
    }

    /// <summary>作者にしか分からない理由(通知・ウィジェットの作りの誤りなど)は、まとめて「初期化できませんでした」にする。中身は英語でログに出ている。</summary>
    private static string FormatReason(PluginLoadFailure failure)
    {
        var localization = LocalizationManager.Instance;
        return failure.Reason switch
        {
            PluginErrorReason.MissingFiles => localization.Format("PluginLoadFailed_MissingFiles", string.Join(", ", failure.MissingFiles)),
            PluginErrorReason.Invalid => localization.GetString("PluginLoadFailed_Invalid"),
            PluginErrorReason.Stopped
                or PluginErrorReason.InvalidNotifications
                or PluginErrorReason.InvalidWidgets
                or PluginErrorReason.TerminatedApp => localization.GetString("PluginLoadFailed_InitializationFailed"),
            PluginErrorReason.DeletionFailed => localization.GetString("PluginLoadFailed_DeletionFailed"),
            PluginErrorReason.UpdateFailed => localization.GetString("PluginLoadFailed_UpdateFailed"),
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure.Reason, "The plugin error reason is not a load failure."),
        };
    }
}
