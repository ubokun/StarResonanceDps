// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.Models;
using Zproto;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// ウィジェットの窓のタイトル「接頭 - 相手」。窓のタイトルと、メイン画面のカードの名前の欄(選んでいる項目)が同じ文字になるよう、
/// 組み立てはここにだけ書く。
/// </summary>
public static class WidgetHeaderText
{
    /// <summary>相手の名前が空なら接頭だけ。</summary>
    public static string Join(string prefix, string? targetName)
    {
        return string.IsNullOrWhiteSpace(targetName)
            ? prefix
            : $"{prefix} - {targetName}";
    }

    /// <summary>
    /// 人の窓。名前も UID も分からなければ接頭だけ。名前の出し方はプレイヤー一覧・メーターと同じ規則
    /// (名前がまだ取れていなければ UID、伏せる設定なら伏せ字、NPC なら職業名)。
    /// </summary>
    public static string ForPlayer(
        string prefix,
        string? name,
        long userId,
        bool isSelf,
        bool isNpc,
        int professionId,
        PlayerNameDisplayMode nameDisplayMode)
    {
        return string.IsNullOrWhiteSpace(name) && userId == 0
            ? prefix
            : Join(prefix, PlayerInfoFormatFormatter.GetDisplayName(name, userId, isSelf, isNpc, professionId, nameDisplayMode));
    }

    /// <summary>
    /// チャットの窓。チャンネルはチャンネル名、プライベートトークは「プライベートトーク(相手の名前)」。
    /// 相手の名前の出し方は人の窓と同じ規則(名前がまだ分からなければ UID、伏せる設定なら伏せ字)。
    /// </summary>
    public static string ForChat(
        string prefix,
        ChitChatChannelType channel,
        long partnerId,
        string? partnerName,
        PlayerNameDisplayMode nameDisplayMode)
    {
        var localization = LocalizationManager.Instance;
        if (channel == ChitChatChannelType.ChannelPrivate)
        {
            return Join(prefix, localization.Format(
                "Chat_PrivateFormat",
                PlayerInfoFormatFormatter.GetDisplayName(partnerName, partnerId, false, false, 0, nameDisplayMode)));
        }

        var channelKey = channel switch
        {
            ChitChatChannelType.ChannelWorld => "Chat_Channel_World",
            ChitChatChannelType.ChannelUnion => "Chat_Channel_Guild",
            ChitChatChannelType.ChannelTeam => "Chat_Channel_Party",
            ChitChatChannelType.ChannelScene => "Chat_Channel_Local",
            ChitChatChannelType.ChannelNewbie => "Chat_Channel_Newbie",
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null)
        };
        return Join(prefix, localization.GetString(channelKey));
    }
}
