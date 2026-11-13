// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.IO;
using System.Text.Json;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.Config;

public sealed class ConfigManager
{
    private static readonly Lazy<ConfigManager> LazyInstance = new(() => new ConfigManager());
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _configPath;
    private SettingsConfig? _settingsPreview;

    private ConfigManager()
    {
        _configPath = AppDataPaths.AppSettingsPath;
        AppConfig = LoadAppConfig();
        AppConfigDefaults.Normalize(AppConfig);
        ApplyDisplaySettings(AppConfig.Settings);
        ApplyRuntimeSettings(AppConfig.Settings);
    }

    public static ConfigManager Instance => LazyInstance.Value;

    public AppConfig AppConfig { get; }

    /// <summary>起動時に、ファイルはあるのに読めなかった(壊れている・設定が入っていない)か。既定値で動いている。</summary>
    public bool HasLoadFailed { get; private set; }

    public event EventHandler? SettingsChanged;

    public event EventHandler? SettingsPreviewChanged;

    /// <summary><see cref="SaveWidgetWindowTopmostModeIndex"/> が保存した。開いている全体設定の画面が表示と保存済みの控えを合わせる。</summary>
    public event EventHandler? WidgetWindowTopmostModeSaved;

    /// <summary><see cref="SavePlayerNameDisplayModeIndex"/> が保存した。開いている全体設定の画面が表示と保存済みの控えを合わせる。</summary>
    public event EventHandler? PlayerNameDisplayModeSaved;

    /// <summary><see cref="SaveVoicevoxInstallFolder"/> が保存した。開いている全体設定の画面が表示と保存済みの控えを合わせる。</summary>
    public event EventHandler? VoicevoxInstallFolderSaved;

    /// <summary><see cref="SaveNotifications"/> が保存した(通知タブの値が変わった)。</summary>
    public event EventHandler? NotificationsChanged;

    /// <summary>
    /// 今の設定(プレビューがあればプレビュー)。<b>複製しない。読むだけで、書き換えない。</b>
    /// 定期更新と名簿の更新はこれを読む。値は読み込みと保存とプレビューの時点で正規化してある。
    /// </summary>
    public SettingsConfig CurrentSettings => _settingsPreview ?? AppConfig.Settings;

    public SettingsConfig GetSettingsSnapshot()
    {
        return AppConfigDefaults.CloneNormalizedSettings(CurrentSettings);
    }

    public void SetSettingsPreview(SettingsConfig settings)
    {
        _settingsPreview = AppConfigDefaults.CloneNormalizedSettings(settings);
        ApplyDisplaySettings(_settingsPreview);
        SettingsPreviewChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSettingsPreview()
    {
        if (_settingsPreview is null)
        {
            return;
        }

        _settingsPreview = null;
        ApplyDisplaySettings(AppConfig.Settings);
        SettingsPreviewChanged?.Invoke(this, EventArgs.Empty);
    }

    public ColorPickerConfig GetColorPickerSnapshot()
    {
        return AppConfig.ColorPicker.Clone();
    }

    public void SaveSettings(SettingsConfig settings)
    {
        AppConfig.Settings = AppConfigDefaults.CloneNormalizedSettings(settings);
        _settingsPreview = null;
        ApplyDisplaySettings(AppConfig.Settings);
        ApplyRuntimeSettings(AppConfig.Settings);
        Save();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        SettingsPreviewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// ウィジェット設定「ウィジェットウィンドウ」(最前面の選び方)だけを書き換えて保存する。ホットキーの切り替えが使う。
    /// 全体設定の画面で変更中の値(プレビュー)があれば、その中の同じ項目も合わせる。
    /// ほかの項目のプレビューは保存しない(画面の未保存の変更を巻き込まない)。
    /// </summary>
    public void SaveWidgetWindowTopmostModeIndex(int index)
    {
        AppConfig.Settings.WidgetWindowTopmostModeIndex = index;
        if (_settingsPreview is not null)
        {
            _settingsPreview.WidgetWindowTopmostModeIndex = index;
        }

        Save();
        WidgetWindowTopmostModeSaved?.Invoke(this, EventArgs.Empty);
        SettingsPreviewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 表示設定「プレイヤー名」だけを書き換えて保存し、名前の表示に当て直す。ホットキーの切り替えが使う。
    /// 全体設定の画面で変更中の値(プレビュー)があれば、その中の同じ項目も合わせる。
    /// ほかの項目のプレビューは保存しない(画面の未保存の変更を巻き込まない)。
    /// </summary>
    public void SavePlayerNameDisplayModeIndex(int index)
    {
        AppConfig.Settings.PlayerNameDisplayModeIndex = index;
        if (_settingsPreview is not null)
        {
            _settingsPreview.PlayerNameDisplayModeIndex = index;
        }

        ApplyPlayerNameDisplayMode(_settingsPreview ?? AppConfig.Settings);
        Save();
        PlayerNameDisplayModeSaved?.Invoke(this, EventArgs.Empty);
        SettingsPreviewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 起動設定「VOICEVOXのインストール先」だけを書き換えて保存する。見つけた VOICEVOX の場所を入れるときに使う。
    /// 全体設定の画面で変更中の値(プレビュー)があれば、その中の同じ項目も合わせる。
    /// ほかの項目のプレビューは保存しない(画面の未保存の変更を巻き込まない)。
    /// </summary>
    public void SaveVoicevoxInstallFolder(string folder)
    {
        AppConfig.Settings.VoicevoxInstallFolder = folder;
        if (_settingsPreview is not null)
        {
            _settingsPreview.VoicevoxInstallFolder = folder;
        }

        Save();
        VoicevoxInstallFolderSaved?.Invoke(this, EventArgs.Empty);
    }

    public void SaveColorPicker(ColorPickerConfig colorPicker)
    {
        AppConfig.ColorPicker = AppConfigDefaults.CloneNormalizedColorPicker(colorPicker);
        Save();
    }

    public NotificationsConfig GetNotificationsSnapshot()
    {
        return AppConfig.Notifications.Clone();
    }

    /// <summary>通知タブの1項目の今の値。<b>複製しない。読むだけで、書き換えない。</b>名簿の更新のたびに読まれる。</summary>
    public NotificationItemConfig GetNotificationItem(NotificationKind kind)
    {
        return AppConfig.Notifications.Get(kind);
    }

    /// <summary>プラグインの通知の項目の今の値(写し)。保存したことが無ければ null。鍵は <see cref="NotificationsConfig.GetPluginItemKey"/>。</summary>
    public PluginNotificationItemConfig? GetPluginNotificationItem(string itemKey)
    {
        return AppConfig.Notifications.PluginItems.TryGetValue(itemKey, out var item) ? item.Clone() : null;
    }

    /// <summary>通知タブの値を保存する。通知タブは保存前プレビューを持たず、変えたらその場で保存する。</summary>
    public void SaveNotifications(NotificationsConfig notifications)
    {
        AppConfig.Notifications = AppConfigDefaults.CloneNormalizedNotifications(notifications);
        Save();
        NotificationsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>プラグインの通知の項目の設定を全部消す(プラグインをアンインストールしたとき)。無ければ何もしない。</summary>
    public void RemovePluginNotifications(string pluginKey)
    {
        var notifications = GetNotificationsSnapshot();
        var removedKeys = notifications.PluginItems.Keys
            .Where(key => NotificationsConfig.IsPluginItemKeyOf(key, pluginKey))
            .ToArray();
        if (removedKeys.Length == 0)
        {
            return;
        }

        foreach (var key in removedKeys)
        {
            notifications.PluginItems.Remove(key);
        }

        SaveNotifications(notifications);
    }

    public void Save()
    {
        AppConfigDefaults.Normalize(AppConfig);

        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var root = new AppSettingsRoot
        {
            Config = AppConfig
        };

        var json = JsonSerializer.Serialize(root, JsonOptions);
        File.WriteAllText(_configPath, json);
    }

    /// <summary>
    /// Core が読む設定を流し込む。<b>プレビューでは呼ばない。</b>
    /// 見た目の下見でキャプチャ先が切り替わったり、戦闘の区切り方が変わっては困る。
    /// </summary>
    private static void ApplyRuntimeSettings(SettingsConfig settings)
    {
        CombatRuntimeSettings.Apply(
            settings.NetCaptureDeviceName,
            settings.GameCapturePreference,
            settings.GameCaptureCustomExeName,
            settings.StopAggregationInNonCombatAreas,
            settings.SplitEncountersOnNewPhases,
            settings.KeepPastEncounterInMeterUntilNextDamage,
            settings.ClearHistorySelectionOnNextEvent,
            settings.DatabaseMaxEncounterCount,
            settings.BenchmarkDurationSeconds,
            settings.BenchmarkFirstTargetOnly,
            settings.CombatExitSeconds);
    }

    /// <summary>
    /// <c>NetworkAdapterSession</c> がアダプターを選び直したときに呼ぶ。
    /// Core が持っている現在値を <c>AppSettings.json</c> へ写して保存する。
    /// </summary>
    public void PersistCaptureSettingsFromRuntime()
    {
        AppConfig.Settings.NetCaptureDeviceName = CombatRuntimeSettings.NetCaptureDeviceName;
        AppConfig.Settings.GameCapturePreference = CombatRuntimeSettings.GameCapturePreference;
        AppConfig.Settings.GameCaptureCustomExeName = CombatRuntimeSettings.GameCaptureCustomExeName;
        Save();
    }

    private static void ApplyDisplaySettings(SettingsConfig settings)
    {
        ApplyPlayerNameDisplayMode(settings);
        ApplyInternalIdDisplayMode(settings);
        ApplyUnnamedSkillInternalNameDisplay(settings);
    }

    private static void ApplyInternalIdDisplayMode(SettingsConfig settings)
    {
        CombatDataCatalog.SetInternalIdDisplay(
            (InternalIdDisplayMode)settings.InternalIdDisplayModeIndex);
    }

    private static void ApplyUnnamedSkillInternalNameDisplay(SettingsConfig settings)
    {
        CombatDataCatalog.SetUnnamedSkillInternalNameDisplay(
            settings.UnnamedSkillNameDisplayModeIndex == AppConfigDefaults.UnnamedSkillNameInternalNameIndex);
    }

    private static void ApplyPlayerNameDisplayMode(SettingsConfig settings)
    {
        PlayerRosterPresentationStore.Instance.SetNameDisplayMode(
            (PlayerNameDisplayMode)settings.PlayerNameDisplayModeIndex);
    }

    private AppConfig LoadAppConfig()
    {
        if (!File.Exists(_configPath))
        {
            return AppConfigDefaults.Create();
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var root = JsonSerializer.Deserialize<AppSettingsRoot>(json, JsonOptions);
            if (root?.Config is not { } config)
            {
                HasLoadFailed = true;
                return AppConfigDefaults.Create();
            }

            AppConfigDefaults.Normalize(config);
            return config;
        }
        catch
        {
            // 読めないファイルは丸ごと既定値で動く。メイン窓が出たところで知らせ(SettingsLoadFailureMessage)、次の保存で上書きする。
            HasLoadFailed = true;
            return AppConfigDefaults.Create();
        }
    }

    private sealed class AppSettingsRoot
    {
        public AppConfig? Config { get; set; }
    }
}
