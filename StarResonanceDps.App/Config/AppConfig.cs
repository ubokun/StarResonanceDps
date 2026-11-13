// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.CombatRuntime.DataTypes;

namespace StarResonanceDps.App.Config;

public sealed class AppConfig
{
    public WindowBounds? StartUpState { get; set; }

    public SettingsConfig Settings { get; set; } = AppConfigDefaults.CreateSettings();

    public ColorPickerConfig ColorPicker { get; set; } = AppConfigDefaults.CreateColorPicker();

    public NotificationsConfig Notifications { get; set; } = AppConfigDefaults.CreateNotifications();
}

public sealed class WindowBounds
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class ColorPickerConfig
{
    public List<string> RecentColors { get; set; } = [];

    public ColorPickerConfig Clone()
    {
        return new ColorPickerConfig
        {
            RecentColors = RecentColors is null ? [] : [.. RecentColors]
        };
    }
}

public sealed class SettingsConfig
{
    public int LanguageIndex { get; set; }

    // --- 基本設定(キャプチャ) ---
    // Core は AppSettings.json を読めないので、起動時と保存時に
    // CombatRuntimeSettings.Apply で流し込む。

    public string NetCaptureDeviceName { get; set; } = CombatRuntimeSettings.AutomaticNetCaptureDeviceName;
    public EGameCapturePreference GameCapturePreference { get; set; } = EGameCapturePreference.Auto;
    public string GameCaptureCustomExeName { get; set; } = string.Empty;

    // --- 起動設定 ---

    /// <summary>Windows にサインインしたときにアプリを起動する。反映は <c>WindowsStartupRegistration</c>(保存時とアプリの起動時)。</summary>
    public bool LaunchAtWindowsStartup { get; set; }

    /// <summary>アプリの起動時に新しいバージョンがあるかを確かめる。次の起動から効く。</summary>
    public bool CheckForUpdatesOnStartup { get; set; } = true;

    /// <summary>アプリの起動時にホットキーの補助を起動する。オフなら補助を起動せず、ホットキーは無効。次の起動から効く。</summary>
    public bool StartHotkeyHelperOnLaunch { get; set; } = true;

    /// <summary>アプリの起動時に VOICEVOX を起動する(動いていなければ)。次の起動から効く。</summary>
    public bool LaunchVoicevoxOnLaunch { get; set; }

    /// <summary>
    /// VOICEVOX のインストール先のフォルダー(中の <c>VOICEVOX.exe</c> を起動する)。空は未設定。
    /// 動いている VOICEVOX を見たらそのフォルダーに書き換える(<c>VoicevoxLauncher.UpdateInstallFolder</c>)。
    /// </summary>
    public string VoicevoxInstallFolder { get; set; } = string.Empty;

    // --- 集計設定 ---

    /// <summary>非戦闘エリア(アステルリーズ・ホームガーデン・ホームハウス)では戦闘の時計を立てず、何も記録しない。</summary>
    public bool StopAggregationInNonCombatAreas { get; set; } = true;
    public bool SplitEncountersOnNewPhases { get; set; } = true;
    public bool KeepPastEncounterInMeterUntilNextDamage { get; set; } = true;
    public bool ClearHistorySelectionOnNextEvent { get; set; } = true;

    /// <summary>戦闘履歴を残す最大の件数。<b>0 は無限。</b></summary>
    public int DatabaseMaxEncounterCount { get; set; } = 99;

    /// <summary>計測の長さ(秒)。範囲は <see cref="AppConfigDefaults"/> の定数。計測を始めた時点の値で測る。</summary>
    public int BenchmarkDurationSeconds { get; set; } = AppConfigDefaults.BenchmarkDurationDefaultSeconds;

    /// <summary>計測で、自分が最初にダメージを与えた敵へのダメージだけを数える。計測を始めた時点の値で測る。</summary>
    public bool BenchmarkFirstTargetOnly { get; set; } = true;

    /// <summary>自動一時停止の秒数。選択肢は <see cref="AppConfigDefaults.CombatExitChoicesSeconds"/>、<b>0 は一時停止しない。</b></summary>
    public int CombatExitSeconds { get; set; } = AppConfigDefaults.CombatExitDefaultSeconds;
    public int NumberDisplayFormatIndex { get; set; }
    public int PlayerNameDisplayModeIndex { get; set; }
    public int InternalIdDisplayModeIndex { get; set; }

    /// <summary>
    /// 名前の無い技とバフの出し方。0=空欄にする / 1=内部名で表示(画面の技名とタイムラインのバフの名前だけ。予兆技の通知と空欄の検知には効かない)。
    /// Core へは <c>CombatDataCatalog.SetUnnamedSkillInternalNameDisplay</c> で渡す。
    /// </summary>
    public int UnnamedSkillNameDisplayModeIndex { get; set; } = AppConfigDefaults.UnnamedSkillNameInternalNameIndex;

    /// <summary>
    /// ウィジェットの窓を最前面に出す条件。0=常に最前面 / 1=ピン留め時のみ最前面。
    /// 反映は <c>WidgetWindow.ApplyTopmost</c>。
    /// </summary>
    public int WidgetWindowTopmostModeIndex { get; set; } = AppConfigDefaults.AlwaysWidgetWindowTopmostModeIndex;

    /// <summary>
    /// ウィジェットのタイトルの状態の円と TIPS を出す条件。0=表示 / 1=非表示 / 2=正常状態以外のみ。
    /// 反映は <c>WidgetWindow.ApplyDataStatus</c>。
    /// </summary>
    public int WidgetDataStatusDisplayModeIndex { get; set; } = AppConfigDefaults.WidgetDataStatusDisplayShowIndex;
    public int WindowColorIndex { get; set; }
    public List<string> WindowColors { get; set; } = AppConfigDefaults.CreateDefaultWindowColors();

    // --- ホットキー ---

    /// <summary>
    /// ホットキーの割り当て。登録は <c>GlobalHotkeyService</c> が起動時と保存時に行い、
    /// 全体設定で変えている間はその割り当てでプレビューする(保存せずに閉じたら戻す)。
    /// </summary>
    public HotkeySettingsConfig Hotkeys { get; set; } = AppConfigDefaults.CreateDefaultHotkeys();

    public SettingsConfig Clone()
    {
        return new SettingsConfig
        {
            LanguageIndex = LanguageIndex,
            NetCaptureDeviceName = NetCaptureDeviceName,
            GameCapturePreference = GameCapturePreference,
            GameCaptureCustomExeName = GameCaptureCustomExeName,
            LaunchAtWindowsStartup = LaunchAtWindowsStartup,
            CheckForUpdatesOnStartup = CheckForUpdatesOnStartup,
            StartHotkeyHelperOnLaunch = StartHotkeyHelperOnLaunch,
            LaunchVoicevoxOnLaunch = LaunchVoicevoxOnLaunch,
            VoicevoxInstallFolder = VoicevoxInstallFolder,
            StopAggregationInNonCombatAreas = StopAggregationInNonCombatAreas,
            SplitEncountersOnNewPhases = SplitEncountersOnNewPhases,
            KeepPastEncounterInMeterUntilNextDamage = KeepPastEncounterInMeterUntilNextDamage,
            ClearHistorySelectionOnNextEvent = ClearHistorySelectionOnNextEvent,
            DatabaseMaxEncounterCount = DatabaseMaxEncounterCount,
            BenchmarkDurationSeconds = BenchmarkDurationSeconds,
            BenchmarkFirstTargetOnly = BenchmarkFirstTargetOnly,
            CombatExitSeconds = CombatExitSeconds,
            NumberDisplayFormatIndex = NumberDisplayFormatIndex,
            PlayerNameDisplayModeIndex = PlayerNameDisplayModeIndex,
            InternalIdDisplayModeIndex = InternalIdDisplayModeIndex,
            UnnamedSkillNameDisplayModeIndex = UnnamedSkillNameDisplayModeIndex,
            WidgetWindowTopmostModeIndex = WidgetWindowTopmostModeIndex,
            WidgetDataStatusDisplayModeIndex = WidgetDataStatusDisplayModeIndex,
            WindowColorIndex = WindowColorIndex,
            WindowColors = WindowColors is null ? AppConfigDefaults.CreateDefaultWindowColors() : [.. WindowColors],
            Hotkeys = Hotkeys?.Clone() ?? AppConfigDefaults.CreateDefaultHotkeys()
        };
    }
}
