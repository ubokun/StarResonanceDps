// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Text;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>
/// 通知「ワールドチャット」の判定。ワールドチャットのほかの人の文の発言で、本文に一致ワードが全部含まれていれば、1つの発言につき1回出す。
/// ウィジェットを開いていなくても動く(<see cref="NotificationMonitor"/>)。画面のスレッドで使う。
///
/// <para>
/// 一致ワードはスペースで区切った語(<see cref="NotificationTargetList.SplitMatchWords"/>)。
/// 一致は部分一致で、大文字・小文字と全角・半角を区別しない(語も本文も互換の正規化 NFKC と大文字化をそろえてから比べる)。
/// 一致ワードが無ければ出さない。
/// </para>
/// </summary>
public sealed class WorldChatNotificationTracker
{
    private string[] _matchWords = [];

    /// <summary>一致ワードを入れ替える。空の語は使わない。</summary>
    public void SetMatchWords(IEnumerable<string> matchWords)
    {
        _matchWords = [.. matchWords.Select(Normalize).Where(word => word.Length > 0).Distinct()];
    }

    /// <param name="senderName">発言者名。出すときにプレイヤー名の規則を当てる。</param>
    public void Observe(long senderId, string senderName, string message)
    {
        if (_matchWords.Length == 0)
        {
            return;
        }

        var normalizedMessage = Normalize(message);
        if (!_matchWords.All(word => normalizedMessage.Contains(word, StringComparison.Ordinal)))
        {
            return;
        }

        var playerName = PlayerInfoFormatFormatter.GetDisplayName(
            senderName,
            senderId,
            false,
            false,
            0,
            PlayerRosterPresentationStore.Instance.NameDisplayMode);
        NotificationService.Instance.Notify(
            NotificationKind.WorldChat,
            NotificationTextFormatter.FormatWorldChat(
                NotificationTextFormatter.ResolveFormat(NotificationKind.WorldChat),
                playerName));
    }

    private static string Normalize(string text)
    {
        return text.Normalize(NormalizationForm.FormKC).ToUpperInvariant();
    }
}
