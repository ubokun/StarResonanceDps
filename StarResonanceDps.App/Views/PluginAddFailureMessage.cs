// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Views;

/// <summary>プラグインの一覧の「＋」で選んだDLLを、プラグインとして足せなかったことを出す。</summary>
public static class PluginAddFailureMessage
{
    /// <summary>選んだファイルがこのアプリのプラグインでない(版が合わないものも含む)。</summary>
    public static void ShowInvalidFile(Window? owner)
    {
        Show(owner, LocalizationManager.Instance.GetString("PluginAddFailed_InvalidFile_Detail"));
    }

    /// <summary>プラグインとしては正しいが、Pluginsフォルダへ写せない(写すのに失敗)。</summary>
    public static void ShowCannotPlaceFile(Window? owner)
    {
        Show(owner, LocalizationManager.Instance.GetString("PluginAddFailed_CannotPlace_Detail"));
    }

    /// <summary>プラグインが使うファイルが、選んだDLLと同じフォルダに無い。</summary>
    public static void ShowMissingFiles(Window? owner, IReadOnlyList<string> missingFiles)
    {
        Show(owner, LocalizationManager.Instance.Format("PluginAddFailed_MissingDependency_Detail", string.Join(", ", missingFiles)));
    }

    /// <summary>同じプラグイン(アセンブリ名が同じ)が、アンインストールしてアプリの再起動での削除を待っている。</summary>
    public static void ShowPendingDeletion(Window? owner)
    {
        Show(owner, LocalizationManager.Instance.GetString("PluginAddFailed_PendingDeletion_Detail"));
    }

    private static void Show(Window? owner, string detail)
    {
        var localization = LocalizationManager.Instance;
        MessageWindow.Show(
            owner,
            localization.GetString("PluginAddFailed_Title"),
            localization.GetString("PluginAddFailed_Message"),
            detail);
    }
}
