// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Text.Json.Serialization;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Config;

/// <summary>通知タブの項目。並びは通知タブの一覧の並び(メイン窓のウィジェットの順)。</summary>
public enum NotificationKind
{
    EntryConfirmation,
    HealthLow,
    PlayerAppeared,
    EntityAppeared,
    Targeted,
    BuffExpired,
    CuisinePotionLow,
    TelegraphedSkill,
    EntityCast,
    PrivateChat,
    WorldChat
}

/// <summary>
/// 通知タブの設定。項目ごとに、通知するか・文章・出し方を持つ。全体設定(<see cref="SettingsConfig"/>)とは別に保存する
/// (全体設定の窓の保存がこの値を巻き戻さないため)。
/// </summary>
public sealed class NotificationsConfig
{
    /// <summary>プラグインの項目の鍵(<see cref="GetPluginItemKey"/>)の、プラグインの鍵と項目の ID のあいだの文字。</summary>
    public const char PluginItemKeySeparator = '/';

    public Dictionary<NotificationKind, NotificationItemConfig> Items { get; set; } = AppConfigDefaults.CreateNotificationItems();

    /// <summary>
    /// プラグインの項目。鍵は <see cref="GetPluginItemKey"/>(大文字と小文字を区別しない)。
    /// 止まっている・読めないプラグインの項目も残し、アンインストールしたときに消す(<see cref="ConfigManager.RemovePluginNotifications"/>)。
    /// </summary>
    public Dictionary<string, PluginNotificationItemConfig> PluginItems { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>本体とプラグインの全部の項目の設定。</summary>
    [JsonIgnore]
    public IEnumerable<NotificationItemConfig> AllItems => Items.Values.Concat(PluginItems.Values.Select(item => item.Item));

    public NotificationItemConfig Get(NotificationKind kind)
    {
        return Items[kind];
    }

    /// <summary>オンの項目(プラグインの項目も)のどれかが、その出し方を使っているか。</summary>
    public bool AnyEnabled(Func<NotificationItemConfig, bool> predicate)
    {
        return AllItems.Any(item => item.IsEnabled && predicate(item));
    }

    /// <summary>プラグインの項目の鍵。プラグインの鍵(フォルダの名前)と項目の ID を <see cref="PluginItemKeySeparator"/> でつなぐ。</summary>
    public static string GetPluginItemKey(string pluginKey, string itemId)
    {
        return pluginKey + PluginItemKeySeparator + itemId;
    }

    /// <summary>そのプラグインの項目の鍵か。</summary>
    public static bool IsPluginItemKeyOf(string itemKey, string pluginKey)
    {
        return itemKey.Length > pluginKey.Length
            && itemKey[pluginKey.Length] == PluginItemKeySeparator
            && itemKey.StartsWith(pluginKey, StringComparison.OrdinalIgnoreCase);
    }

    public NotificationsConfig Clone()
    {
        return new NotificationsConfig
        {
            Items = Items is null
                ? AppConfigDefaults.CreateNotificationItems()
                : Items.ToDictionary(pair => pair.Key, pair => pair.Value?.Clone() ?? AppConfigDefaults.CreateNotificationItem()),
            PluginItems = PluginItems is null
                ? new Dictionary<string, PluginNotificationItemConfig>(StringComparer.OrdinalIgnoreCase)
                : PluginItems
                    .Where(pair => pair.Value is not null)
                    .ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.OrdinalIgnoreCase)
        };
    }
}

/// <summary>
/// プラグインの通知の項目の設定。本体の項目と同じ設定(<see cref="Item"/>。使うのはスイッチ・文章・出し方・音量・声)に、
/// プラグインが定義した設定の値(<see cref="Settings"/>、鍵は設定の ID、値の形は <c>PluginSettingValueCodec</c>)を足す。
/// </summary>
public sealed class PluginNotificationItemConfig
{
    public NotificationItemConfig Item { get; set; } = AppConfigDefaults.CreateNotificationItem();

    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public PluginNotificationItemConfig Clone()
    {
        return new PluginNotificationItemConfig
        {
            Item = Item?.Clone() ?? AppConfigDefaults.CreateNotificationItem(),
            Settings = Settings is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(Settings, StringComparer.OrdinalIgnoreCase)
        };
    }
}

public sealed class NotificationItemConfig
{
    public bool IsEnabled { get; set; }

    /// <summary>通知の文章。null は既定のまま(表示言語の既定の文)。空欄は保存しない(既定に戻す)。</summary>
    public string? FormatString { get; set; }

    /// <summary>出し方。0=Windows の通知 / 1=読み上げ(<see cref="AppConfigDefaults"/> の定数)。通知しないはスイッチで決める。</summary>
    public int MethodIndex { get; set; } = AppConfigDefaults.NotificationMethodWindowsIndex;

    /// <summary>
    /// 通知音量 0〜100(%)。Windows の通知の音と読み上げの両方に掛ける(掛け方は <c>SpeechQueue</c>)。
    /// 100 は Windows の通知の音と Windows の音声では元の大きさ、VOICEVOX では元の大きさを上げた大きさ(50 が元の大きさ)。
    /// </summary>
    public int Volume { get; set; } = AppConfigDefaults.NotificationVolumeDefault;

    /// <summary>読み上げ方法(出し方が読み上げのときの声)。0=Windows の音声 / 1=VOICEVOX(<see cref="AppConfigDefaults"/> の定数)。</summary>
    public int SpeechVoiceIndex { get; set; } = AppConfigDefaults.SpeechVoiceWindowsIndex;

    // VOICEVOX の話者。名前も持つのは、エンジンが起動していないときにも選んだ話者と利用規約のリンクを出すため。
    public string VoicevoxSpeakerUuid { get; set; } = string.Empty;
    public string VoicevoxSpeakerName { get; set; } = string.Empty;

    /// <summary>VOICEVOX のスタイルの番号。0 も使われる番号なので、未選択は null。</summary>
    public int? VoicevoxStyleId { get; set; }

    public string VoicevoxStyleName { get; set; } = string.Empty;

    /// <summary>通知する人(<see cref="PartyDisplayMode"/> の番号)。使うのは HP低下 だけ。</summary>
    public int TargetModeIndex { get; set; } = (int)PartyDisplayMode.All;

    /// <summary>
    /// 通知する人(<see cref="AppConfigDefaults.TargetedPlayerAllIndex"/> など)。使うのは 狙われるプレイヤー だけ。
    /// </summary>
    public int TargetedPlayerModeIndex { get; set; } = AppConfigDefaults.DefaultTargetedPlayerModeIndex;

    /// <summary>
    /// 通知する敵の種類(<see cref="AppConfigDefaults.TargetEntityBossOnlyIndex"/> など)。使うのは 狙われるプレイヤー だけ。
    /// </summary>
    public int TargetEntityModeIndex { get; set; } = AppConfigDefaults.DefaultTargetEntityModeIndex;

    /// <summary>
    /// 狙われ状態が何秒続いたら出すかの番号(<see cref="AppConfigDefaults.TargetedDurationSeconds"/> の添字)。
    /// 使うのは 狙われるプレイヤー だけ。
    /// </summary>
    public int TargetedConditionIndex { get; set; } = AppConfigDefaults.DefaultTargetedConditionIndex;

    /// <summary>HP低下とみなす割合の番号(<see cref="AppConfigDefaults.HealthLowThresholdPercents"/> の添字)。使うのは HP低下 だけ。</summary>
    public int HealthLowThresholdIndex { get; set; } = AppConfigDefaults.DefaultHealthLowThresholdIndex;

    /// <summary>HP低下が何秒続いたら出すかの番号(<see cref="AppConfigDefaults.HealthLowDurationSeconds"/> の添字)。使うのは HP低下 だけ。</summary>
    public int HealthLowConditionIndex { get; set; } = AppConfigDefaults.DefaultHealthLowConditionIndex;

    /// <summary>
    /// 残り何分を下回ったら出すかの番号(<see cref="AppConfigDefaults.CuisinePotionLowMinutes"/> の添字)。使うのは 薬剤・料理効果切れ だけ。
    /// </summary>
    public int CuisinePotionConditionIndex { get; set; } = AppConfigDefaults.DefaultCuisinePotionConditionIndex;

    /// <summary>
    /// 予告のバーが終わる何秒前に出すかの番号(<see cref="AppConfigDefaults.TelegraphedSkillLeadSeconds"/> の添字)。使うのは 予兆技 の予告だけ(警告は警告が出たときに出す)。
    /// </summary>
    public int TelegraphedSkillConditionIndex { get; set; } = AppConfigDefaults.DefaultTelegraphedSkillConditionIndex;

    /// <summary>
    /// 対象の名前(どれかに当たれば通知する)。エンティティ詠唱は表示言語のスキル名、プレイヤー出現はゲームの中のプレイヤー名、エンティティ出現は表示言語のエンティティ名。
    /// <see cref="TargetIds"/> が1つでもあれば使わない。使うのは エンティティ詠唱・プレイヤー出現・エンティティ出現 だけ(名前もIDも無ければ通知しない)。
    /// ワールドチャットは一致ワード(入力欄でスペースで区切った語。全部が発言の本文に含まれていれば通知する。無ければ通知しない)に使う。
    /// </summary>
    public List<string> TargetNames { get; set; } = [];

    /// <summary>
    /// 対象のID(どれかに当たれば通知する)。エンティティ詠唱はスキルID、プレイヤー出現はUID(キャラクターID)、エンティティ出現はエンティティの種別ID。
    /// 1つでもあれば <see cref="TargetNames"/> より優先する。使うのは エンティティ詠唱・プレイヤー出現・エンティティ出現 だけ。
    /// </summary>
    public List<long> TargetIds { get; set; } = [];

    [JsonIgnore]
    public bool IsWindowsNotification => MethodIndex == AppConfigDefaults.NotificationMethodWindowsIndex;

    [JsonIgnore]
    public bool IsSpeech => MethodIndex == AppConfigDefaults.NotificationMethodSpeechIndex;

    [JsonIgnore]
    public bool UsesWindowsVoice => IsSpeech && SpeechVoiceIndex == AppConfigDefaults.SpeechVoiceWindowsIndex;

    [JsonIgnore]
    public bool UsesVoicevox => IsSpeech && SpeechVoiceIndex == AppConfigDefaults.SpeechVoiceVoicevoxIndex;

    public NotificationItemConfig Clone()
    {
        return new NotificationItemConfig
        {
            IsEnabled = IsEnabled,
            FormatString = FormatString,
            MethodIndex = MethodIndex,
            Volume = Volume,
            SpeechVoiceIndex = SpeechVoiceIndex,
            VoicevoxSpeakerUuid = VoicevoxSpeakerUuid,
            VoicevoxSpeakerName = VoicevoxSpeakerName,
            VoicevoxStyleId = VoicevoxStyleId,
            VoicevoxStyleName = VoicevoxStyleName,
            TargetModeIndex = TargetModeIndex,
            TargetedPlayerModeIndex = TargetedPlayerModeIndex,
            TargetEntityModeIndex = TargetEntityModeIndex,
            TargetedConditionIndex = TargetedConditionIndex,
            HealthLowThresholdIndex = HealthLowThresholdIndex,
            HealthLowConditionIndex = HealthLowConditionIndex,
            CuisinePotionConditionIndex = CuisinePotionConditionIndex,
            TelegraphedSkillConditionIndex = TelegraphedSkillConditionIndex,
            TargetNames = [.. TargetNames],
            TargetIds = [.. TargetIds]
        };
    }
}
