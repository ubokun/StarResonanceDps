// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Windows;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

/// <summary>
/// 入っている Npcap が古い(<see cref="NpcapVersionProbe.MinimumVersion"/> 未満)ことを出す。「はい」で Npcap のダウンロードページを開く。
/// どれを押しても起動は続ける。
/// </summary>
public static class NpcapOutdatedMessage
{
    public static void Show(Window owner, Version version)
    {
        var localization = LocalizationManager.Instance;
        var confirmed = ConfirmWindow.ShowText(
            owner,
            localization.GetString("Confirm_NpcapUpdate_Title"),
            localization.GetString("Confirm_NpcapOutdated_Message"),
            localization.Format("Confirm_NpcapOutdated_Detail", version));

        if (confirmed)
        {
            ExternalLinkOpener.Open(NpcapVersionProbe.DownloadUrl);
        }
    }
}
