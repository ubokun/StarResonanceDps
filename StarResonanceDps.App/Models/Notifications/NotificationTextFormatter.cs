// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Text.RegularExpressions;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>
/// 通知タブの項目の文章。通知タブのプレビューと通知が同じ組み立てを使う。
///
/// <para>
/// 保存した値が null なら表示言語の既定の文(空欄は保存の時点で null にしている)。
/// 差し込める項目は、バフ・デバフ効果切れが {TargetName}(対象の名前)と {BuffName}(カードのバフ名)と {CasterName}(付与者プレイヤー名)、
/// 薬剤・料理効果切れが {TargetName} と {BuffName}、
/// 予兆技が {EntityName}(タイムラインの行と同じ名前)と {SkillName}、入場確認が {Content}(マッチング先のコンテンツ名か、パーティで入る場所の名前)、
/// HP低下とプレイヤー出現が {PlayerName}(プレイヤーリストの行と同じ名前)、エンティティ出現が {EntityName}(内部IDの注記なしのエンティティ名)、
/// 狙われるプレイヤーが {PlayerName} と {EntityName}、エンティティ詠唱が {EntityName}(詠唱した敵の名前)、
/// プライベートトークが {PlayerName}(送った人の名前。チャットの窓のタイトルと同じ規則)、
/// ワールドチャットが {PlayerName}(発言した人の名前。プライベートトークと同じ規則)。
/// </para>
/// </summary>
public static partial class NotificationTextFormatter
{
    /// <summary>項目の既定の文の鍵。</summary>
    public static string GetDefaultKey(NotificationKind kind)
    {
        return kind switch
        {
            NotificationKind.EntryConfirmation => "EntryConfirmationNotification_Default",
            NotificationKind.HealthLow => "PlayerListNotification_HealthLow_Default",
            NotificationKind.PlayerAppeared => "PlayerAppearedNotification_Default",
            NotificationKind.EntityAppeared => "EntityAppearedNotification_Default",
            NotificationKind.Targeted => "TargetedNotification_Default",
            NotificationKind.BuffExpired => "BuffCardNotification_Expired_Default",
            NotificationKind.CuisinePotionLow => "BuffCardNotification_CuisinePotionLow_Default",
            NotificationKind.TelegraphedSkill => "TimelineNotification_TelegraphedSkill_Default",
            NotificationKind.EntityCast => "EntityCastNotification_Default",
            NotificationKind.PrivateChat => "PrivateChatNotification_Default",
            NotificationKind.WorldChat => "WorldChatNotification_Default",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No default text for the notification.")
        };
    }

    /// <summary>今の設定で使う書式。保存した値が無ければ表示言語の既定の文。</summary>
    public static string ResolveFormat(NotificationKind kind)
    {
        return ConfigManager.Instance.GetNotificationItem(kind).FormatString
            ?? LocalizationManager.Instance.GetString(GetDefaultKey(kind));
    }

    /// <summary>
    /// バフの通知の文。付与者プレイヤー名は付与者がプレイヤーでなければ空で、空になって残った <c>()</c> <c>[]</c> はバフ・デバフカードと同じく消す
    /// (<see cref="Widgets.BuffInfoFormatFormatter"/>)。通知の文は改行を持つので、空白は詰めない。
    /// </summary>
    public static string FormatBuff(string format, string name, string buffName, string casterName)
    {
        var result = GetBuffFieldRegex().Replace(
            format,
            match => match.Groups[1].Value.ToUpperInvariant() switch
            {
                "TARGETNAME" => name,
                "CASTERNAME" => casterName,
                _ => buffName
            });
        result = GetEmptyParenthesisRegex().Replace(result, string.Empty);
        return GetEmptyBracketRegex().Replace(result, string.Empty);
    }

    public static string FormatTelegraphedSkill(string format, string name, string skillName)
    {
        return GetTelegraphedSkillFieldRegex().Replace(
            format,
            match => string.Equals(match.Groups[1].Value, "EntityName", StringComparison.OrdinalIgnoreCase) ? name : skillName);
    }

    public static string FormatEntryConfirmation(string format, string content)
    {
        return GetEntryConfirmationFieldRegex().Replace(format, _ => content);
    }

    public static string FormatHealthLow(string format, string name)
    {
        return GetPlayerNameFieldRegex().Replace(format, _ => name);
    }

    /// <summary>
    /// プレイヤー出現・エンティティ出現の文。差し込めるのは出現した相手の名前だけで、プレイヤー出現は {PlayerName}、エンティティ出現は {EntityName}。
    /// </summary>
    public static string FormatAppeared(NotificationKind kind, string format, string name)
    {
        var regex = kind == NotificationKind.PlayerAppeared ? GetPlayerNameFieldRegex() : GetEntityNameFieldRegex();
        return regex.Replace(format, _ => name);
    }

    /// <summary>狙われるプレイヤーの文。{PlayerName} は狙われている人、{EntityName} は狙っている敵。</summary>
    public static string FormatTargeted(string format, string playerName, string entityName)
    {
        return GetTargetedFieldRegex().Replace(
            format,
            match => string.Equals(match.Groups[1].Value, "PlayerName", StringComparison.OrdinalIgnoreCase) ? playerName : entityName);
    }

    /// <summary>エンティティ詠唱の文。{EntityName} は詠唱した敵。</summary>
    public static string FormatEntityCast(string format, string entityName)
    {
        return GetEntityNameFieldRegex().Replace(format, _ => entityName);
    }

    /// <summary>プライベートトークの文。{PlayerName} は送った人。</summary>
    public static string FormatPrivateChat(string format, string playerName)
    {
        return GetPlayerNameFieldRegex().Replace(format, _ => playerName);
    }

    /// <summary>ワールドチャットの文。{PlayerName} は発言した人。</summary>
    public static string FormatWorldChat(string format, string playerName)
    {
        return GetPlayerNameFieldRegex().Replace(format, _ => playerName);
    }

    /// <summary>通知タブの見本。値は実データではなく、書式の形を見せるためのもの。</summary>
    public static string FormatPreview(NotificationKind kind, string format)
    {
        var localization = LocalizationManager.Instance;
        return kind switch
        {
            NotificationKind.EntryConfirmation => FormatEntryConfirmation(
                format,
                localization.GetString("Settings_PlayerListNotification_PreviewContent")),

            // 名前はプレイヤー名の書式の見本と同じ。
            NotificationKind.HealthLow => FormatHealthLow(format, localization.GetString("Settings_PlayerInfo_PreviewName")),
            NotificationKind.PlayerAppeared => FormatAppeared(kind, format, localization.GetString("Settings_PlayerInfo_PreviewName")),
            NotificationKind.EntityAppeared => FormatAppeared(kind, format, localization.GetString("Settings_EntityInfo_PreviewName")),
            NotificationKind.Targeted => FormatTargeted(
                format,
                localization.GetString("Settings_PlayerInfo_PreviewName"),
                localization.GetString("Settings_EntityInfo_PreviewName")),
            NotificationKind.BuffExpired => FormatBuff(
                format,
                localization.GetString("Settings_BuffInfo_PreviewName"),
                localization.GetString("Settings_BuffInfo_PreviewBuffName"),
                localization.GetString("Settings_BuffInfo_PreviewCaster")),

            // バフ名は料理・薬剤のまとまりの名前になるので「料理」を見せる。
            NotificationKind.CuisinePotionLow => FormatBuff(
                format,
                localization.GetString("Settings_BuffInfo_PreviewName"),
                localization.GetString("Widget_BuffDebuffCard_Group_Cuisine"),
                localization.GetString("Settings_BuffInfo_PreviewCaster")),
            NotificationKind.TelegraphedSkill => FormatTelegraphedSkill(
                format,
                localization.GetString("Settings_EntityInfo_PreviewName"),
                localization.GetString("Settings_SkillInfo_PreviewName")),
            NotificationKind.EntityCast => FormatEntityCast(format, localization.GetString("Settings_EntityInfo_PreviewName")),
            NotificationKind.PrivateChat => FormatPrivateChat(format, localization.GetString("Settings_PlayerInfo_PreviewName")),
            NotificationKind.WorldChat => FormatWorldChat(format, localization.GetString("Settings_PlayerInfo_PreviewName")),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No preview for the notification.")
        };
    }

    [GeneratedRegex(@"\{(TargetName|BuffName|CasterName)\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetBuffFieldRegex();

    [GeneratedRegex(@"\(\s*\)")]
    private static partial Regex GetEmptyParenthesisRegex();

    [GeneratedRegex(@"\[\s*\]")]
    private static partial Regex GetEmptyBracketRegex();

    [GeneratedRegex(@"\{(EntityName|SkillName)\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetTelegraphedSkillFieldRegex();

    [GeneratedRegex(@"\{Content\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetEntryConfirmationFieldRegex();

    [GeneratedRegex(@"\{PlayerName\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetPlayerNameFieldRegex();

    [GeneratedRegex(@"\{EntityName\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetEntityNameFieldRegex();

    [GeneratedRegex(@"\{(PlayerName|EntityName)\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetTargetedFieldRegex();}
