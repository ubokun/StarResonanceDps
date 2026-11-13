// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Threading;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>
/// 通知「プライベートトーク」の判定。ほかの人からの新着を <see cref="ReadWaitTime"/> だけ控え、その間にゲームがその相手を
/// その新着の番号以上まで既読にしなければ出す。ウィジェットを開いていなくても動く(<see cref="NotificationMonitor"/>)。画面のスレッドで使う。
///
/// <para>
/// ゲームはその相手とのプライベートトークを画面で開いている間だけ、新着のたびにすぐ既読を送る(閉じたことは通信に出ないので、
/// 開いているかは新着ごとに既読が来たかで見分ける)。新着は1件ずつ出す。
/// </para>
/// </summary>
public sealed class PrivateChatNotificationTracker
{
    /// <summary>既読を待つ長さ。</summary>
    private static readonly TimeSpan ReadWaitTime = TimeSpan.FromSeconds(1);

    private readonly List<PendingMessage> _pending = [];

    /// <param name="senderName">発言者名。出すときにプレイヤー名の規則を当てる。</param>
    public void Observe(long partnerId, long messageId, string senderName)
    {
        var timer = new DispatcherTimer
        {
            Interval = ReadWaitTime
        };
        var pending = new PendingMessage(partnerId, messageId, senderName, timer);
        timer.Tick += (_, _) => Notify(pending);
        _pending.Add(pending);
        timer.Start();
    }

    /// <summary>ゲームがその相手を <paramref name="maxReadMessageId"/> まで既読にした。それまでの新着は出さない。</summary>
    public void MarkRead(long partnerId, long maxReadMessageId)
    {
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var pending = _pending[i];
            if (pending.PartnerId == partnerId && pending.MessageId <= maxReadMessageId)
            {
                pending.Timer.Stop();
                _pending.RemoveAt(i);
            }
        }
    }

    /// <summary>控えを捨てる(通知をオフにした)。</summary>
    public void Clear()
    {
        foreach (var pending in _pending)
        {
            pending.Timer.Stop();
        }

        _pending.Clear();
    }

    private void Notify(PendingMessage pending)
    {
        pending.Timer.Stop();
        if (!_pending.Remove(pending))
        {
            return;
        }

        var playerName = PlayerInfoFormatFormatter.GetDisplayName(
            pending.SenderName,
            pending.PartnerId,
            false,
            false,
            0,
            PlayerRosterPresentationStore.Instance.NameDisplayMode);
        NotificationService.Instance.Notify(
            NotificationKind.PrivateChat,
            NotificationTextFormatter.FormatPrivateChat(
                NotificationTextFormatter.ResolveFormat(NotificationKind.PrivateChat),
                playerName));
    }

    private sealed record PendingMessage(long PartnerId, long MessageId, string SenderName, DispatcherTimer Timer);
}
