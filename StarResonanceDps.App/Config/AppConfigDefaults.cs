// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Input;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Config;

public static class AppConfigDefaults
{
    public const int MaxPaletteColorCount = 5;
    public const int MaxRecentColorCount = 10;

    /// <summary>ウィジェットの窓を常に最前面に出す(既定)。</summary>
    public const int AlwaysWidgetWindowTopmostModeIndex = 0;

    /// <summary>ウィジェットの窓をピン留め中だけ最前面に出す。</summary>
    public const int PinnedOnlyWidgetWindowTopmostModeIndex = 1;

    /// <summary>名前の無い技を空欄にする(内部名を出さない)。</summary>
    public const int UnnamedSkillNameBlankIndex = 0;

    /// <summary>名前の無い技を内部名で表示する(既定)。</summary>
    public const int UnnamedSkillNameInternalNameIndex = 1;

    /// <summary>ウィジェットのタイトルの状態の円と TIPS を出す(既定)。</summary>
    public const int WidgetDataStatusDisplayShowIndex = 0;

    /// <summary>ウィジェットのタイトルの状態の円と TIPS を出さない。</summary>
    public const int WidgetDataStatusDisplayHideIndex = 1;

    /// <summary>ウィジェットのタイトルの状態の円と TIPS を、正常(緑)でないときだけ出す。</summary>
    public const int WidgetDataStatusDisplayNonNormalOnlyIndex = 2;

    /// <summary>Windows の通知(トースト)で知らせる(既定)。</summary>
    public const int NotificationMethodWindowsIndex = 0;

    /// <summary>読み上げで知らせる。声は <see cref="NotificationItemConfig.SpeechVoiceIndex"/>。</summary>
    public const int NotificationMethodSpeechIndex = 1;

    /// <summary>Windows に入っている声で読み上げる(既定)。</summary>
    public const int SpeechVoiceWindowsIndex = 0;

    /// <summary>利用者が入れた VOICEVOX で読み上げる。</summary>
    public const int SpeechVoiceVoicevoxIndex = 1;

    /// <summary>通知「HP低下」の判定の選択肢(最大HPに対する%)。並びは通知タブの選択リストと同じ。</summary>
    public static readonly int[] HealthLowThresholdPercents = [45, 35, 25, 15, 5];

    /// <summary>既定は15%(ゲーム内で画面の周辺が赤くなる割合)。</summary>
    public const int DefaultHealthLowThresholdIndex = 3;

    /// <summary>通知「HP低下」の通知条件の選択肢(HP低下が続いた秒数)。0 は無条件(下回ったその場で出す)。並びは通知タブの選択リストと同じ。</summary>
    public static readonly int[] HealthLowDurationSeconds = [0, 1, 2, 3];

    /// <summary>既定は3秒。</summary>
    public const int DefaultHealthLowConditionIndex = 3;

    /// <summary>通知「薬剤・料理効果切れ」の通知条件の選択肢(残りの分数)。並びは通知タブの選択リストと同じ。</summary>
    public static readonly int[] CuisinePotionLowMinutes = [1, 2, 5, 10];

    /// <summary>既定は2分。</summary>
    public const int DefaultCuisinePotionConditionIndex = 1;

    // 通知「狙われるプレイヤー」の対象プレイヤー。並びは通知タブの選択リストと同じ。
    public const int TargetedPlayerAllIndex = 0;
    public const int TargetedPlayerPartyMembersIndex = 1;
    public const int TargetedPlayerNonGuardianIndex = 2;
    public const int TargetedPlayerNonGuardianPartyMembersIndex = 3;

    /// <summary>既定は防御クラス以外。</summary>
    public const int DefaultTargetedPlayerModeIndex = TargetedPlayerNonGuardianIndex;

    // 通知「狙われるプレイヤー」の対象エンティティ。並びは通知タブの選択リストと同じ。
    public const int TargetEntityBossOnlyIndex = 0;
    public const int TargetEntityBossAndEliteIndex = 1;
    public const int TargetEntityAllIndex = 2;

    /// <summary>既定はボスのみ。</summary>
    public const int DefaultTargetEntityModeIndex = TargetEntityBossOnlyIndex;

    /// <summary>通知「狙われるプレイヤー」の通知条件の選択肢(狙われ状態が続いた秒数)。0 は無条件(狙われたその場で出す)。</summary>
    public static readonly int[] TargetedDurationSeconds = [0, 1, 2, 3];

    /// <summary>既定は3秒。</summary>
    public const int DefaultTargetedConditionIndex = 3;

    /// <summary>通知「予兆技」の通知条件の選択肢(予告のバーが終わる何秒前に出すか)。0 は発動時(バーが終わったとき)。並びは通知タブの選択リストと同じ。</summary>
    public static readonly int[] TelegraphedSkillLeadSeconds = [0, 1, 2, 3];

    /// <summary>既定は発動時。</summary>
    public const int DefaultTelegraphedSkillConditionIndex = 0;

    // 通知音量(Windows の通知の音と読み上げ、%)。0 は鳴らさない。
    // 100 は Windows の通知の音と Windows の音声では元の大きさ、VOICEVOX では値を2倍して掛ける(SpeechQueue.VoicevoxVolumeScale)ので 50 が元の大きさ。
    public const int NotificationVolumeMin = 0;
    public const int NotificationVolumeOriginal = 100;
    public const int NotificationVolumeMax = 100;
    public const int NotificationVolumeDefault = 100;

    // 計測時間(秒)。全体設定の入力欄で受け付ける範囲。範囲の外は近いほうの端にする(ClampBenchmarkDurationSeconds)。
    public const int BenchmarkDurationMinSeconds = 30;
    public const int BenchmarkDurationMaxSeconds = 1200;
    public const int BenchmarkDurationDefaultSeconds = 180;

    // 自動一時停止(秒)。全体設定の選択リストの選択肢で、0 は一時停止しない。選択肢に無い値は既定にする。
    public static readonly int[] CombatExitChoicesSeconds = [5, 8, 10, 0];
    public const int CombatExitDefaultSeconds = 10;

    private static readonly string[] DefaultWindowColorHexes =
    [
        "#1F1F1F",
        "#FCFCFC"
    ];

    public static AppConfig Create()
    {
        return new AppConfig
        {
            StartUpState = null,
            Settings = CreateSettings(),
            ColorPicker = CreateColorPicker(),
            Notifications = CreateNotifications()
        };
    }

    public static SettingsConfig CreateSettings()
    {
        return new SettingsConfig
        {
            LanguageIndex = 0,
            NetCaptureDeviceName = CombatRuntimeSettings.AutomaticNetCaptureDeviceName,
            GameCapturePreference = EGameCapturePreference.Auto,
            GameCaptureCustomExeName = string.Empty,
            LaunchAtWindowsStartup = false,
            CheckForUpdatesOnStartup = true,
            StartHotkeyHelperOnLaunch = true,
            LaunchVoicevoxOnLaunch = false,
            VoicevoxInstallFolder = string.Empty,
            StopAggregationInNonCombatAreas = true,
            SplitEncountersOnNewPhases = true,
            KeepPastEncounterInMeterUntilNextDamage = true,
            ClearHistorySelectionOnNextEvent = true,
            DatabaseMaxEncounterCount = 99,
            BenchmarkDurationSeconds = BenchmarkDurationDefaultSeconds,
            BenchmarkFirstTargetOnly = true,
            CombatExitSeconds = CombatExitDefaultSeconds,
            NumberDisplayFormatIndex = 0,
            PlayerNameDisplayModeIndex = 0,
            InternalIdDisplayModeIndex = 0,
            UnnamedSkillNameDisplayModeIndex = UnnamedSkillNameInternalNameIndex,
            WidgetWindowTopmostModeIndex = AlwaysWidgetWindowTopmostModeIndex,
            WidgetDataStatusDisplayModeIndex = WidgetDataStatusDisplayShowIndex,
            WindowColorIndex = 1,
            WindowColors = CreateDefaultWindowColors(),
            Hotkeys = CreateDefaultHotkeys()
        };
    }

    public static NotificationsConfig CreateNotifications()
    {
        return new NotificationsConfig
        {
            Items = CreateNotificationItems()
        };
    }

    /// <summary>全部の項目を既定(オフ・既定の文・Windows の通知)で作る。</summary>
    public static Dictionary<NotificationKind, NotificationItemConfig> CreateNotificationItems()
    {
        return System.Enum.GetValues<NotificationKind>().ToDictionary(kind => kind, _ => CreateNotificationItem());
    }

    public static NotificationItemConfig CreateNotificationItem()
    {
        return new NotificationItemConfig
        {
            IsEnabled = false,
            FormatString = null,
            MethodIndex = NotificationMethodWindowsIndex,
            Volume = NotificationVolumeDefault,
            SpeechVoiceIndex = SpeechVoiceWindowsIndex,
            VoicevoxSpeakerUuid = string.Empty,
            VoicevoxSpeakerName = string.Empty,
            VoicevoxStyleId = null,
            VoicevoxStyleName = string.Empty,
            TargetModeIndex = (int)PartyDisplayMode.All,
            TargetedPlayerModeIndex = DefaultTargetedPlayerModeIndex,
            TargetEntityModeIndex = DefaultTargetEntityModeIndex,
            TargetedConditionIndex = DefaultTargetedConditionIndex,
            HealthLowThresholdIndex = DefaultHealthLowThresholdIndex,
            HealthLowConditionIndex = DefaultHealthLowConditionIndex,
            CuisinePotionConditionIndex = DefaultCuisinePotionConditionIndex,
            TelegraphedSkillConditionIndex = DefaultTelegraphedSkillConditionIndex,
            TargetNames = [],
            TargetIds = []
        };
    }

    /// <summary>
    /// ホットキーの既定。プレイヤー名 Ctrl+F11 / お気に入り Ctrl+F8 / ピン留め Ctrl+F7 / クリック透過 Ctrl+F6 / 最前面 Ctrl+F5 /
    /// 計測 Ctrl+F10 / リセット Ctrl+F9。
    /// </summary>
    public static HotkeySettingsConfig CreateDefaultHotkeys()
    {
        return new HotkeySettingsConfig
        {
            PlayerNameShowOrHide = HotkeyBindingConfig.Create(Key.F11, ModifierKeys.Control),
            StartFavoritesOrStopAll = HotkeyBindingConfig.Create(Key.F8, ModifierKeys.Control),
            PinRunningOrUnpinAll = HotkeyBindingConfig.Create(Key.F7, ModifierKeys.Control),
            ClickThroughPinnedOrClearAll = HotkeyBindingConfig.Create(Key.F6, ModifierKeys.Control),
            WidgetWindowTopmostAlwaysOrPinnedOnly = HotkeyBindingConfig.Create(Key.F5, ModifierKeys.Control),
            Benchmark = HotkeyBindingConfig.Create(Key.F10, ModifierKeys.Control),
            ResetEncounter = HotkeyBindingConfig.Create(Key.F9, ModifierKeys.Control)
        };
    }

    public static ColorPickerConfig CreateColorPicker()
    {
        return new ColorPickerConfig
        {
            RecentColors = []
        };
    }

    public static List<string> CreateDefaultWindowColors()
    {
        return [.. DefaultWindowColorHexes];
    }

    public static void Normalize(AppConfig config)
    {
        config.Settings ??= CreateSettings();
        config.ColorPicker ??= CreateColorPicker();
        config.Notifications ??= CreateNotifications();
        NormalizeSettings(config.Settings);
        NormalizeColorPicker(config.ColorPicker);
        NormalizeNotifications(config.Notifications);
    }

    public static NotificationsConfig CloneNormalizedNotifications(NotificationsConfig notifications)
    {
        var normalized = notifications.Clone();
        NormalizeNotifications(normalized);
        return normalized;
    }

    /// <summary>
    /// 足りない項目は既定で足し、知らない項目は捨てる。空欄の文は既定に戻す。
    /// プラグインの項目は、鍵の形(プラグインの鍵 / 項目の ID)が合うものを残す(どのプラグインの項目かは読み込みの後でないと分からない)。
    /// 設定の値を定義に合わせるのは、読むとき(<c>PluginSettingValueCodec</c>)。
    /// </summary>
    public static void NormalizeNotifications(NotificationsConfig notifications)
    {
        var items = new Dictionary<NotificationKind, NotificationItemConfig>();
        foreach (var kind in System.Enum.GetValues<NotificationKind>())
        {
            var item = notifications.Items is not null && notifications.Items.TryGetValue(kind, out var saved) && saved is not null
                ? saved
                : CreateNotificationItem();
            NormalizeNotificationItem(item);
            items[kind] = item;
        }

        notifications.Items = items;

        var pluginItems = new Dictionary<string, PluginNotificationItemConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, pluginItem) in notifications.PluginItems ?? [])
        {
            var separatorIndex = key?.IndexOf(NotificationsConfig.PluginItemKeySeparator) ?? -1;
            if (pluginItem is null || separatorIndex <= 0 || separatorIndex == key!.Length - 1)
            {
                continue;
            }

            pluginItem.Item ??= CreateNotificationItem();
            NormalizeNotificationItem(pluginItem.Item);
            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (settingId, value) in pluginItem.Settings ?? [])
            {
                if (settingId is not null && value is not null)
                {
                    settings[settingId] = value;
                }
            }

            pluginItem.Settings = settings;
            pluginItems[key] = pluginItem;
        }

        notifications.PluginItems = pluginItems;
    }

    private static void NormalizeNotificationItem(NotificationItemConfig item)
    {
        if (string.IsNullOrWhiteSpace(item.FormatString))
        {
            item.FormatString = null;
        }

        item.MethodIndex = Clamp(item.MethodIndex, NotificationMethodWindowsIndex, NotificationMethodSpeechIndex);
        item.Volume = Clamp(item.Volume, NotificationVolumeMin, NotificationVolumeMax);
        item.SpeechVoiceIndex = Clamp(item.SpeechVoiceIndex, SpeechVoiceWindowsIndex, SpeechVoiceVoicevoxIndex);
        item.VoicevoxSpeakerUuid = item.VoicevoxSpeakerUuid?.Trim() ?? string.Empty;
        item.VoicevoxSpeakerName = item.VoicevoxSpeakerName?.Trim() ?? string.Empty;
        item.VoicevoxStyleName = item.VoicevoxStyleName?.Trim() ?? string.Empty;
        item.TargetModeIndex = Clamp(item.TargetModeIndex, (int)PartyDisplayMode.All, (int)PartyDisplayMode.SelfOnly);
        item.TargetedPlayerModeIndex = Clamp(item.TargetedPlayerModeIndex, TargetedPlayerAllIndex, TargetedPlayerNonGuardianPartyMembersIndex);
        item.TargetEntityModeIndex = Clamp(item.TargetEntityModeIndex, TargetEntityBossOnlyIndex, TargetEntityAllIndex);
        item.TargetedConditionIndex = IndexOrDefault(
            item.TargetedConditionIndex,
            TargetedDurationSeconds.Length,
            DefaultTargetedConditionIndex);
        item.HealthLowThresholdIndex = IndexOrDefault(item.HealthLowThresholdIndex, HealthLowThresholdPercents.Length, DefaultHealthLowThresholdIndex);
        item.HealthLowConditionIndex = IndexOrDefault(item.HealthLowConditionIndex, HealthLowDurationSeconds.Length, DefaultHealthLowConditionIndex);
        item.CuisinePotionConditionIndex = IndexOrDefault(item.CuisinePotionConditionIndex, CuisinePotionLowMinutes.Length, DefaultCuisinePotionConditionIndex);
        item.TelegraphedSkillConditionIndex = IndexOrDefault(
            item.TelegraphedSkillConditionIndex,
            TelegraphedSkillLeadSeconds.Length,
            DefaultTelegraphedSkillConditionIndex);
        item.TargetNames = [.. (item.TargetNames ?? []).Select(name => name?.Trim() ?? string.Empty).Where(name => name.Length > 0)];
        item.TargetIds = [.. (item.TargetIds ?? []).Where(id => id >= 0)];
    }

    /// <summary>選択肢の番号。範囲の外なら既定。</summary>
    private static int IndexOrDefault(int index, int count, int defaultIndex)
    {
        return index >= 0 && index < count ? index : defaultIndex;
    }

    public static SettingsConfig CloneNormalizedSettings(SettingsConfig settings)
    {
        var normalized = settings.Clone();
        NormalizeSettings(normalized);
        return normalized;
    }

    public static ColorPickerConfig CloneNormalizedColorPicker(ColorPickerConfig colorPicker)
    {
        var normalized = colorPicker.Clone();
        NormalizeColorPicker(normalized);
        return normalized;
    }

    public static void NormalizeSettings(SettingsConfig settings)
    {
        settings.LanguageIndex = Clamp(settings.LanguageIndex, 0, 4);
        settings.NumberDisplayFormatIndex = Clamp(settings.NumberDisplayFormatIndex, 0, 1);
        settings.PlayerNameDisplayModeIndex = Clamp(settings.PlayerNameDisplayModeIndex, 0, 2);
        settings.InternalIdDisplayModeIndex = Clamp(settings.InternalIdDisplayModeIndex, 0, 5);
        settings.UnnamedSkillNameDisplayModeIndex = Clamp(
            settings.UnnamedSkillNameDisplayModeIndex,
            UnnamedSkillNameBlankIndex,
            UnnamedSkillNameInternalNameIndex);
        settings.WidgetWindowTopmostModeIndex = Clamp(
            settings.WidgetWindowTopmostModeIndex,
            AlwaysWidgetWindowTopmostModeIndex,
            PinnedOnlyWidgetWindowTopmostModeIndex);
        settings.WidgetDataStatusDisplayModeIndex = Clamp(
            settings.WidgetDataStatusDisplayModeIndex,
            WidgetDataStatusDisplayShowIndex,
            WidgetDataStatusDisplayNonNormalOnlyIndex);
        settings.WindowColors = NormalizeColorList(settings.WindowColors, DefaultWindowColorHexes, MaxPaletteColorCount);
        settings.WindowColorIndex = Clamp(settings.WindowColorIndex, 0, settings.WindowColors.Count - 1);

        settings.NetCaptureDeviceName = string.IsNullOrWhiteSpace(settings.NetCaptureDeviceName)
            ? CombatRuntimeSettings.AutomaticNetCaptureDeviceName
            : settings.NetCaptureDeviceName.Trim();
        settings.GameCaptureCustomExeName = settings.GameCaptureCustomExeName?.Trim() ?? string.Empty;
        settings.VoicevoxInstallFolder = settings.VoicevoxInstallFolder?.Trim() ?? string.Empty;

        // 0 は無限。選べる最大は 99 件。
        settings.DatabaseMaxEncounterCount = Clamp(settings.DatabaseMaxEncounterCount, 0, 99);

        settings.BenchmarkDurationSeconds = ClampBenchmarkDurationSeconds(settings.BenchmarkDurationSeconds);

        if (Array.IndexOf(CombatExitChoicesSeconds, settings.CombatExitSeconds) < 0)
        {
            settings.CombatExitSeconds = CombatExitDefaultSeconds;
        }

        NormalizeHotkeys(settings);
    }

    /// <summary>計測時間を範囲に収める。30 より小さければ 30、1200 より大きければ 1200。全体設定の入力欄と保存した値の両方に使う。</summary>
    public static int ClampBenchmarkDurationSeconds(int seconds)
    {
        return Clamp(seconds, BenchmarkDurationMinSeconds, BenchmarkDurationMaxSeconds);
    }

    /// <summary>
    /// 項目そのものが無ければ既定にする。読めないキー・修飾キーだけのキーは割り当てなし(空欄)にし、
    /// 修飾キーは Ctrl・Alt・Shift だけ残す。
    /// </summary>
    private static void NormalizeHotkeys(SettingsConfig settings)
    {
        var defaults = CreateDefaultHotkeys();
        settings.Hotkeys ??= defaults.Clone();

        foreach (var action in System.Enum.GetValues<HotkeyAction>())
        {
            var binding = settings.Hotkeys.Get(action);
            if (binding is null)
            {
                settings.Hotkeys.Set(action, defaults.Get(action).Clone());
                continue;
            }

            if (!System.Enum.IsDefined(binding.Key) || HotkeyBindingConfig.IsModifierKey(binding.Key))
            {
                binding.Key = Key.None;
            }

            binding.Modifiers = binding.IsAssigned
                ? binding.Modifiers & HotkeyBindingConfig.SupportedModifiers
                : ModifierKeys.None;
        }
    }

    public static void NormalizeColorPicker(ColorPickerConfig colorPicker)
    {
        colorPicker.RecentColors = NormalizeColorList(colorPicker.RecentColors, [], MaxRecentColorCount, allowEmpty: true);
    }

    private static List<string> NormalizeColorList(
        IEnumerable<string>? colors,
        IEnumerable<string> fallback,
        int maxCount,
        bool allowEmpty = false)
    {
        var result = new List<string>();

        if (colors is not null)
        {
            foreach (var color in colors)
            {
                if (!TryNormalizeHexColor(color, out var normalized)
                    || result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(normalized);
                if (result.Count >= maxCount)
                {
                    break;
                }
            }
        }

        if (result.Count == 0 && !allowEmpty)
        {
            foreach (var color in fallback)
            {
                if (!TryNormalizeHexColor(color, out var normalized)
                    || result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(normalized);
                if (result.Count >= maxCount)
                {
                    break;
                }
            }
        }

        if (result.Count == 0 && !allowEmpty)
        {
            result.Add("#FFFFFF");
        }

        return result;
    }

    private static bool TryNormalizeHexColor(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var text = raw.Trim();
        if (text.StartsWith("#", StringComparison.Ordinal))
        {
            text = text[1..];
        }

        if (text.Length != 6)
        {
            return false;
        }

        foreach (var ch in text)
        {
            if (!Uri.IsHexDigit(ch))
            {
                return false;
            }
        }

        normalized = "#" + text.ToUpperInvariant();
        return true;
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Min(Math.Max(value, min), max);
    }
}
