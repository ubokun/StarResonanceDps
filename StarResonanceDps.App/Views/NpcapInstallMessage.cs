// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

/// <summary>
/// Npcap が入っていない(版を検出できない)ので起動できないことを出す。「はい」で Npcap のダウンロードページを開く。
/// どれを押しても起動は続けない(終わらせるのは呼び出し元)。
/// </summary>
public static class NpcapInstallMessage
{
    public static void Show(Window owner)
    {
        var localization = LocalizationManager.Instance;
        var confirmed = ConfirmWindow.ShowText(
            owner,
            localization.GetString("Confirm_NpcapInstall_Title"),
            localization.GetString("Confirm_NpcapMissing_Message"),
            localization.GetString("Confirm_NpcapMissing_Detail"));

        if (confirmed)
        {
            ExternalLinkOpener.Open(NpcapVersionProbe.DownloadUrl);
        }
    }
}
