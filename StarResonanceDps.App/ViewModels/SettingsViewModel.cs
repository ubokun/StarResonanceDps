// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private static readonly bool IsInDesignMode =
        DesignerProperties.GetIsInDesignMode(new DependencyObject());

    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly NetworkAdapterSession _networkAdapterSession = NetworkAdapterSession.Instance;
    private SettingsConfig _lastSavedSettings;
    private string _lastSavedNetworkAdapterDeviceName = string.Empty;
    private EGameCapturePreference _lastSavedGameCapturePreference = EGameCapturePreference.Auto;
    private string _lastSavedGameCaptureCustomExeName = string.Empty;
    private bool _isLoadingSettings;
    private bool _isLoadingNetworkAdapters;
    private bool _isLoadingCaptureSettings;

    [ObservableProperty]
    private IReadOnlyList<NetworkAdapterOption> _availableNetworkAdapters = [];

    [ObservableProperty]
    private NetworkAdapterOption? _selectedNetworkAdapter;

    [ObservableProperty]
    private GameCapturePreferenceOption? _selectedGameCapturePreference;

    [ObservableProperty]
    private string _gameCaptureCustomExeName = string.Empty;

    [ObservableProperty]
    private int _languageIndex;

    [ObservableProperty]
    private int _numberDisplayFormatIndex;

    [ObservableProperty]
    private int _playerNameDisplayModeIndex;

    [ObservableProperty]
    private int _internalIdDisplayModeIndex;

    [ObservableProperty]
    private int _unnamedSkillNameDisplayModeIndex = AppConfigDefaults.UnnamedSkillNameInternalNameIndex;

    [ObservableProperty]
    private int _widgetWindowTopmostModeIndex = AppConfigDefaults.AlwaysWidgetWindowTopmostModeIndex;

    [ObservableProperty]
    private int _widgetDataStatusDisplayModeIndex = AppConfigDefaults.WidgetDataStatusDisplayShowIndex;

    // --- 起動設定 ---

    [ObservableProperty]
    private bool _launchAtWindowsStartup;

    [ObservableProperty]
    private bool _checkForUpdatesOnStartup = true;

    [ObservableProperty]
    private bool _startHotkeyHelperOnLaunch = true;

    [ObservableProperty]
    private bool _launchVoicevoxOnLaunch;

    [ObservableProperty]
    private string _voicevoxInstallFolder = string.Empty;

    // --- 集計設定 ---

    [ObservableProperty]
    private bool _stopAggregationInNonCombatAreas = true;

    [ObservableProperty]
    private bool _splitEncountersOnNewPhases = true;

    [ObservableProperty]
    private bool _keepPastEncounterInMeterUntilNextDamage;

    [ObservableProperty]
    private bool _clearHistorySelectionOnNextEvent = true;

    /// <summary>戦闘履歴を残す最大の件数。<b>0 は無限。</b></summary>
    [ObservableProperty]
    private int _databaseMaxEncounterCount;

    /// <summary>計測時間(秒)。入力欄の文字は <see cref="TryCommitBenchmarkDurationText"/> で確定したときだけここへ入る。</summary>
    [ObservableProperty]
    private int _benchmarkDurationSeconds = AppConfigDefaults.BenchmarkDurationDefaultSeconds;

    /// <summary>自動一時停止の秒数。値は <see cref="AppConfigDefaults.CombatExitChoicesSeconds"/> のどれかで、<b>0 は一時停止しない。</b></summary>
    [ObservableProperty]
    private int _combatExitSeconds = AppConfigDefaults.CombatExitDefaultSeconds;

    [ObservableProperty]
    private bool _benchmarkFirstTargetOnly;

    /// <summary>
    /// 最大保持数の選択肢。**値は件数そのもので、0 が無制限。**
    /// 選べる値をここだけで決めているので、増やすならこの配列に足す
    /// (<c>AppConfigDefaults.Normalize</c> の上限とずれないようにすること)。
    /// </summary>
    private static readonly int[] MaxEncounterCountChoices = [20, 50, 99, 0];

    private readonly ObservableCollection<RetentionPolicyOption> _retentionPolicyOptions = [];

    private readonly ObservableCollection<CombatExitOption> _combatExitOptions = [];

    // --- ホットキー ---

    private readonly GlobalHotkeyService _hotkeyService = GlobalHotkeyService.Instance;

    /// <summary>キーを受け付けている行。受付中はホットキーを全部外している(<see cref="GlobalHotkeyService.Suspend"/>)。</summary>
    private HotkeyItemViewModel? _capturingHotkeyItem;

    /// <summary>
    /// Windows の音声を確かめる時機(通知タブで Windows の音声で読み上げるオンの項目があるまま、言語を変えた)。
    /// Windows の音声は表示言語の声で読むので、画面が確かめてメッセージを出す。読み込み(開いたとき・既定に戻す)では上げない。
    /// </summary>
    public event EventHandler? WindowsVoiceCheckRequested;

    public SettingsViewModel()
    {
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        var hotkeyActions = System.Enum.GetValues<HotkeyAction>();
        HotkeyItems = new ReadOnlyObservableCollection<HotkeyItemViewModel>(new ObservableCollection<HotkeyItemViewModel>(
            hotkeyActions.Select((action, index) => new HotkeyItemViewModel(action, isLast: index == hotkeyActions.Length - 1))));
        RetentionPolicyOptions = new ReadOnlyObservableCollection<RetentionPolicyOption>(_retentionPolicyOptions);
        RebuildRetentionPolicyOptions();
        CombatExitOptions = new ReadOnlyObservableCollection<CombatExitOption>(_combatExitOptions);
        RebuildCombatExitOptions();
        GameCapturePreferences = new ReadOnlyObservableCollection<GameCapturePreferenceOption>(
            new ObservableCollection<GameCapturePreferenceOption>(CreateGameCapturePreferences()));
        WindowColors = new ColorPaletteViewModel(AppConfigDefaults.CreateDefaultWindowColors(), AppConfigDefaults.MaxPaletteColorCount);
        WindowColors.PaletteChanged += WindowColors_PaletteChanged;

        // 開いたときに動いている VOICEVOX があれば、起動設定のインストール先をその場所に合わせてから読む。
        if (!IsInDesignMode)
        {
            VoicevoxLauncher.UpdateInstallFolder();
        }

        var settings = _configManager.GetSettingsSnapshot();
        _lastSavedSettings = settings.Clone();
        LoadFromSettings(settings, applyLanguage: false, applyPreview: false);
        LoadCaptureSettings();
        _configManager.WidgetWindowTopmostModeSaved += ConfigManager_WidgetWindowTopmostModeSaved;
        _configManager.PlayerNameDisplayModeSaved += ConfigManager_PlayerNameDisplayModeSaved;
        _configManager.VoicevoxInstallFolderSaved += ConfigManager_VoicevoxInstallFolderSaved;
        _hotkeyService.RegistrationChanged += HotkeyService_RegistrationChanged;
    }

    public ColorPaletteViewModel WindowColors { get; }

    public ReadOnlyObservableCollection<GameCapturePreferenceOption> GameCapturePreferences { get; }

    public bool IsCustomGameCapturePreference => SelectedGameCapturePreference?.Preference == EGameCapturePreference.Custom;

    public bool HasUnsavedChanges => !SettingsEquals(CreateSettings(), _lastSavedSettings) || !CaptureSettingsEqualsSaved();

    /// <summary>ホットキーの行。並びは <see cref="HotkeyAction"/> の並び。</summary>
    public ReadOnlyObservableCollection<HotkeyItemViewModel> HotkeyItems { get; }

    public bool IsCapturingHotkey => _capturingHotkeyItem is not null;

    /// <summary>ホットキーのナビと節を出すか。補助が使えない間(起動設定でオフ・起動できない・途中で止まった)はホットキーが無効なので隠す。</summary>
    public bool IsHotkeySectionVisible => _hotkeyService.IsAvailable;

    public void Dispose()
    {
        // 受付中に閉じたら取り消して、外していたホットキーを戻す(戻せなかったものはサービスがログに書く)。
        EndHotkeyCapture();
        _configManager.WidgetWindowTopmostModeSaved -= ConfigManager_WidgetWindowTopmostModeSaved;
        _configManager.PlayerNameDisplayModeSaved -= ConfigManager_PlayerNameDisplayModeSaved;
        _configManager.VoicevoxInstallFolderSaved -= ConfigManager_VoicevoxInstallFolderSaved;
        _hotkeyService.RegistrationChanged -= HotkeyService_RegistrationChanged;
        _configManager.ClearSettingsPreview();
        _hotkeyService.PreviewGameProcessNames(null);
        WindowColors.PaletteChanged -= WindowColors_PaletteChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    [RelayCommand]
    private void Save()
    {
        SaveSettings();
    }

    [RelayCommand]
    private void Reset()
    {
        ResetToDefaults();
    }

    /// <summary>保存してホットキーを登録し直す。登録できなかったホットキーを返す(画面がメッセージで知らせる)。</summary>
    public IReadOnlyList<HotkeyRegistrationFailure> SaveSettings()
    {
        var settings = CreateSettings();
        _configManager.SaveSettings(settings);
        _lastSavedSettings = settings.Clone();
        SaveCaptureSettings();
        ApplyCurrentGlobalTheme();
        OnPropertyChanged(nameof(HasUnsavedChanges));
        return _hotkeyService.Apply(settings.Hotkeys);
    }

    /// <summary>保存した「Windows起動時にアプリを起動」を Windows に書く。書けなかったら false(画面がメッセージで知らせる)。</summary>
    public bool ApplyWindowsStartupRegistration()
    {
        return WindowsStartupRegistration.Apply(_lastSavedSettings.LaunchAtWindowsStartup);
    }

    /// <summary>
    /// その行でキーの受付を始める。ほかの行が受付中ならそちらは取り消す。
    /// 受付の間はホットキーを全部外す(外さないと押したキーがホットキーとして動いて欄に届かない)。
    /// </summary>
    public void BeginHotkeyCapture(HotkeyItemViewModel item)
    {
        if (ReferenceEquals(_capturingHotkeyItem, item))
        {
            return;
        }

        if (_capturingHotkeyItem is not null)
        {
            _capturingHotkeyItem.IsCapturing = false;
        }
        else
        {
            _hotkeyService.Suspend();
        }

        _capturingHotkeyItem = item;
        item.IsCapturing = true;
        OnPropertyChanged(nameof(IsCapturingHotkey));
    }

    /// <summary>
    /// 受付中の行に、押されたキーを割り当てる。
    ///
    /// <para>
    /// その場で試しに登録し、できなければ割り当てずに元のキーのまま受付を終える。
    /// できたら、同じキーを持つほかの行は空欄にする。受付を終えると、画面で編集中の割り当てで登録し直す
    /// (プレビュー。保存せずに閉じたら <see cref="RestoreSavedSettingsPreview"/> が保存してある割り当てへ戻す)。
    /// 返すのは登録できなかったもの(試しの登録と、登録し直したときの両方)。
    /// </para>
    /// </summary>
    public IReadOnlyList<HotkeyRegistrationFailure> CompleteHotkeyCapture(Key key, ModifierKeys modifiers)
    {
        if (_capturingHotkeyItem is not { } item)
        {
            return [];
        }

        var failures = new List<HotkeyRegistrationFailure>();
        var binding = HotkeyBindingConfig.Create(key, modifiers);

        if (!binding.SameAs(item.Binding))
        {
            if (_hotkeyService.TestRegistration(binding) is { } error)
            {
                failures.Add(new HotkeyRegistrationFailure(item.Action, binding, error));
            }
            else
            {
                foreach (var other in HotkeyItems)
                {
                    if (!ReferenceEquals(other, item) && other.Binding.SameAs(binding))
                    {
                        other.Binding = new HotkeyBindingConfig();
                    }
                }

                item.Binding = binding;

                // 受付中(一時停止中)なので控えるだけ。下の受付の終了で登録される。
                _hotkeyService.Apply(CreateHotkeySettings());
            }
        }

        failures.AddRange(EndHotkeyCapture());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        return failures;
    }

    /// <summary>受付を取り消す(欄の外をクリックした)。受付の前の割り当てで登録し直し、登録できなかったものを返す。</summary>
    public IReadOnlyList<HotkeyRegistrationFailure> CancelHotkeyCapture()
    {
        return EndHotkeyCapture();
    }

    private IReadOnlyList<HotkeyRegistrationFailure> EndHotkeyCapture()
    {
        if (_capturingHotkeyItem is null)
        {
            return [];
        }

        _capturingHotkeyItem.IsCapturing = false;
        _capturingHotkeyItem = null;
        OnPropertyChanged(nameof(IsCapturingHotkey));
        return _hotkeyService.Resume();
    }

    /// <summary>既定に戻してプレビューする。ホットキーも既定のキーで登録し直し、登録できなかったものを返す。</summary>
    public IReadOnlyList<HotkeyRegistrationFailure> ResetToDefaults()
    {
        LoadFromSettings(AppConfigDefaults.CreateSettings(), applyLanguage: true, applyPreview: true);
        LoadCaptureSettingsFromValues(string.Empty, EGameCapturePreference.Auto, string.Empty);
        ApplyCaptureSettingsPreview();
        ApplyCurrentGlobalTheme();
        OnPropertyChanged(nameof(HasUnsavedChanges));
        return _hotkeyService.Apply(CreateHotkeySettings());
    }

    /// <summary>保存せずに閉じる。プレビューを保存してある値へ戻し、ホットキーも登録し直して、登録できなかったものを返す。</summary>
    public IReadOnlyList<HotkeyRegistrationFailure> RestoreSavedSettingsPreview()
    {
        LocalizationManager.Instance.ApplyLanguageIndex(_lastSavedSettings.LanguageIndex);
        _configManager.ClearSettingsPreview();
        ThemeManager.Instance.ApplyGlobalTheme(_lastSavedSettings);
        LoadCaptureSettingsFromValues(
            _lastSavedNetworkAdapterDeviceName,
            _lastSavedGameCapturePreference,
            _lastSavedGameCaptureCustomExeName);
        ApplyCaptureSettingsPreview();
        return _hotkeyService.Apply(_lastSavedSettings.Hotkeys);
    }

    public Color GetSelectedWindowColor()
    {
        return WindowColors.SelectedColor;
    }

    public void ApplyWindowColor(Color color)
    {
        WindowColors.AddOrSelect(color);
    }

    private SettingsConfig CreateSettings()
    {
        var settings = new SettingsConfig
        {
            LanguageIndex = LanguageIndex,
            NumberDisplayFormatIndex = NumberDisplayFormatIndex,
            PlayerNameDisplayModeIndex = PlayerNameDisplayModeIndex,
            InternalIdDisplayModeIndex = InternalIdDisplayModeIndex,
            UnnamedSkillNameDisplayModeIndex = UnnamedSkillNameDisplayModeIndex,
            WidgetWindowTopmostModeIndex = WidgetWindowTopmostModeIndex,
            WidgetDataStatusDisplayModeIndex = WidgetDataStatusDisplayModeIndex,
            WindowColorIndex = WindowColors.SelectedIndex,
            WindowColors = [.. WindowColors.GetHexColors()],
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
            Hotkeys = CreateHotkeySettings(),

            // キャプチャ3項目はこの画面では SettingsConfig 経由で編集しない
            // (NetworkAdapterSession が持ち、保存も別経路)。ただしここで落とすと
            // AppConfig.Settings 側が既定値に戻ってしまうので、現在値を持ち回す。
            NetCaptureDeviceName = CombatRuntimeSettings.NetCaptureDeviceName,
            GameCapturePreference = CombatRuntimeSettings.GameCapturePreference,
            GameCaptureCustomExeName = CombatRuntimeSettings.GameCaptureCustomExeName
        };

        AppConfigDefaults.NormalizeSettings(settings);
        return settings;
    }

    private HotkeySettingsConfig CreateHotkeySettings()
    {
        var hotkeys = new HotkeySettingsConfig();
        foreach (var item in HotkeyItems)
        {
            hotkeys.Set(item.Action, item.Binding.Clone());
        }

        return hotkeys;
    }

    private void LoadFromSettings(SettingsConfig settings, bool applyLanguage, bool applyPreview)
    {
        AppConfigDefaults.NormalizeSettings(settings);

        _isLoadingSettings = true;
        try
        {
            LanguageIndex = settings.LanguageIndex;
            NumberDisplayFormatIndex = settings.NumberDisplayFormatIndex;
            PlayerNameDisplayModeIndex = settings.PlayerNameDisplayModeIndex;
            InternalIdDisplayModeIndex = settings.InternalIdDisplayModeIndex;
            UnnamedSkillNameDisplayModeIndex = settings.UnnamedSkillNameDisplayModeIndex;
            WidgetWindowTopmostModeIndex = settings.WidgetWindowTopmostModeIndex;
            WidgetDataStatusDisplayModeIndex = settings.WidgetDataStatusDisplayModeIndex;
            WindowColors.Load(settings.WindowColors, settings.WindowColorIndex);
            LaunchAtWindowsStartup = settings.LaunchAtWindowsStartup;
            CheckForUpdatesOnStartup = settings.CheckForUpdatesOnStartup;
            StartHotkeyHelperOnLaunch = settings.StartHotkeyHelperOnLaunch;
            LaunchVoicevoxOnLaunch = settings.LaunchVoicevoxOnLaunch;
            VoicevoxInstallFolder = settings.VoicevoxInstallFolder;
            StopAggregationInNonCombatAreas = settings.StopAggregationInNonCombatAreas;
            SplitEncountersOnNewPhases = settings.SplitEncountersOnNewPhases;
            KeepPastEncounterInMeterUntilNextDamage = settings.KeepPastEncounterInMeterUntilNextDamage;
            ClearHistorySelectionOnNextEvent = settings.ClearHistorySelectionOnNextEvent;
            DatabaseMaxEncounterCount = settings.DatabaseMaxEncounterCount;
            BenchmarkDurationSeconds = settings.BenchmarkDurationSeconds;
            BenchmarkFirstTargetOnly = settings.BenchmarkFirstTargetOnly;
            CombatExitSeconds = settings.CombatExitSeconds;

            foreach (var item in HotkeyItems)
            {
                item.Binding = settings.Hotkeys.Get(item.Action).Clone();
            }
        }
        finally
        {
            _isLoadingSettings = false;
        }

        if (applyLanguage)
        {
            LocalizationManager.Instance.ApplyLanguageIndex(LanguageIndex);
        }

        if (applyPreview)
        {
            ApplySettingsPreview();
        }
    }

    private void LoadCaptureSettings()
    {
        if (IsInDesignMode)
        {
            return;
        }

        _isLoadingNetworkAdapters = true;
        _isLoadingCaptureSettings = true;
        try
        {
            _networkAdapterSession.Initialize();
            AvailableNetworkAdapters = CreateNetworkAdapterOptions(_networkAdapterSession.AvailableAdapters);
            _lastSavedNetworkAdapterDeviceName = NormalizeAdapterDeviceName(CombatRuntimeSettings.NetCaptureDeviceName);
            _lastSavedGameCapturePreference = CombatRuntimeSettings.GameCapturePreference;
            _lastSavedGameCaptureCustomExeName = NormalizeCustomExeName(CombatRuntimeSettings.GameCaptureCustomExeName);
            LoadCaptureSettingsFromValues(
                _lastSavedNetworkAdapterDeviceName,
                _lastSavedGameCapturePreference,
                _lastSavedGameCaptureCustomExeName);
        }
        finally
        {
            _isLoadingCaptureSettings = false;
            _isLoadingNetworkAdapters = false;
        }
    }

    private void LoadCaptureSettingsFromValues(
        string networkAdapterDeviceName,
        EGameCapturePreference gameCapturePreference,
        string gameCaptureCustomExeName)
    {
        var wasLoadingCaptureSettings = _isLoadingCaptureSettings;
        _isLoadingCaptureSettings = true;
        try
        {
            SelectedNetworkAdapter = FindNetworkAdapterOption(networkAdapterDeviceName) ?? FindAutomaticNetworkAdapterOption();
            SelectedGameCapturePreference = GameCapturePreferences.FirstOrDefault(option =>
                    option.Preference == gameCapturePreference)
                ?? GameCapturePreferences.FirstOrDefault(option => option.Preference == EGameCapturePreference.Auto)
                ?? GameCapturePreferences.FirstOrDefault();
            GameCaptureCustomExeName = NormalizeCustomExeName(gameCaptureCustomExeName);
        }
        finally
        {
            _isLoadingCaptureSettings = wasLoadingCaptureSettings;
        }

        OnPropertyChanged(nameof(IsCustomGameCapturePreference));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void SaveCaptureSettings()
    {
        if (IsInDesignMode)
        {
            return;
        }

        var networkAdapterDeviceName = NormalizeAdapterDeviceName(SelectedNetworkAdapter?.DeviceName);
        var gameCapturePreference = SelectedGameCapturePreference?.Preference ?? EGameCapturePreference.Auto;
        var gameCaptureCustomExeName = NormalizeCustomExeName(GameCaptureCustomExeName);

        _networkAdapterSession.ApplyCaptureSettings(
            networkAdapterDeviceName,
            gameCapturePreference,
            gameCaptureCustomExeName);

        networkAdapterDeviceName = NormalizeAdapterDeviceName(CombatRuntimeSettings.NetCaptureDeviceName);
        gameCapturePreference = CombatRuntimeSettings.GameCapturePreference;
        gameCaptureCustomExeName = NormalizeCustomExeName(CombatRuntimeSettings.GameCaptureCustomExeName);

        _lastSavedNetworkAdapterDeviceName = networkAdapterDeviceName;
        _lastSavedGameCapturePreference = gameCapturePreference;
        _lastSavedGameCaptureCustomExeName = gameCaptureCustomExeName;
        LoadCaptureSettingsFromValues(networkAdapterDeviceName, gameCapturePreference, gameCaptureCustomExeName);
    }

    private void ApplyCaptureSettingsPreview()
    {
        if (IsInDesignMode || _isLoadingNetworkAdapters || _isLoadingCaptureSettings)
        {
            return;
        }

        var gameCapturePreference = SelectedGameCapturePreference?.Preference ?? EGameCapturePreference.Auto;
        var gameCaptureCustomExeName = NormalizeCustomExeName(GameCaptureCustomExeName);
        _networkAdapterSession.PreviewCaptureSettings(
            NormalizeAdapterDeviceName(SelectedNetworkAdapter?.DeviceName),
            gameCapturePreference,
            gameCaptureCustomExeName);

        // ホットキーの「ゲームが前面か」の判定も、プレビューのゲームにそろえる(閉じるときに Dispose で戻す)。
        _hotkeyService.PreviewGameProcessNames(
            Utils.GameCapturePreferenceToExeNames(gameCapturePreference, gameCaptureCustomExeName));
    }

    private void WindowColors_PaletteChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (_isLoadingSettings)
        {
            return;
        }

        ApplySettingsPreview();
        ApplyCurrentGlobalTheme();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var option in AvailableNetworkAdapters)
        {
            option.RefreshDisplayName();
        }

        foreach (var option in GameCapturePreferences)
        {
            option.RefreshDisplayName();
        }

        // ON / OFF は言語で変わるので、開いたまま切り替えられても追従させる
        // (言語の選択肢はこの画面の中にあるので、開いたままの切り替えが普通に起きる)。
        OnPropertyChanged(nameof(LaunchAtWindowsStartupStateText));
        OnPropertyChanged(nameof(CheckForUpdatesOnStartupStateText));
        OnPropertyChanged(nameof(StartHotkeyHelperOnLaunchStateText));
        OnPropertyChanged(nameof(LaunchVoicevoxOnLaunchStateText));
        OnPropertyChanged(nameof(StopAggregationInNonCombatAreasStateText));
        OnPropertyChanged(nameof(SplitEncountersOnNewPhasesStateText));
        OnPropertyChanged(nameof(KeepPastEncounterInMeterUntilNextDamageStateText));
        OnPropertyChanged(nameof(ClearHistorySelectionOnNextEventStateText));
        OnPropertyChanged(nameof(BenchmarkFirstTargetOnlyStateText));

        RebuildRetentionPolicyOptions();
        RebuildCombatExitOptions();

        foreach (var item in HotkeyItems)
        {
            item.RefreshText();
        }
    }

    private void ApplySettingsPreview()
    {
        _configManager.SetSettingsPreview(CreateSettings());
    }

    private void ApplyCurrentGlobalTheme()
    {
        ThemeManager.Instance.ApplyGlobalTheme(CreateSettings());
    }

    private bool CaptureSettingsEqualsSaved()
    {
        if (IsInDesignMode)
        {
            return true;
        }

        return string.Equals(NormalizeAdapterDeviceName(SelectedNetworkAdapter?.DeviceName), _lastSavedNetworkAdapterDeviceName, StringComparison.Ordinal)
            && (SelectedGameCapturePreference?.Preference ?? EGameCapturePreference.Auto) == _lastSavedGameCapturePreference
            && string.Equals(NormalizeCustomExeName(GameCaptureCustomExeName), _lastSavedGameCaptureCustomExeName, StringComparison.Ordinal);
    }

    private NetworkAdapterOption? FindAutomaticNetworkAdapterOption()
    {
        return AvailableNetworkAdapters.FirstOrDefault(option => option.IsAutomatic);
    }

    private NetworkAdapterOption? FindNetworkAdapterOption(string? deviceName)
    {
        var normalizedDeviceName = NormalizeAdapterDeviceName(deviceName);
        if (CombatRuntimeSettings.IsAutomaticNetCaptureDeviceName(normalizedDeviceName))
        {
            return FindAutomaticNetworkAdapterOption();
        }

        return AvailableNetworkAdapters.FirstOrDefault(option =>
            string.Equals(option.DeviceName, normalizedDeviceName, StringComparison.Ordinal));
    }

    private static IReadOnlyList<NetworkAdapterOption> CreateNetworkAdapterOptions(IReadOnlyList<NetworkAdapterInfo> adapters)
    {
        var options = new List<NetworkAdapterOption>
        {
            NetworkAdapterOption.CreateAutomatic()
        };

        foreach (var adapter in adapters)
        {
            options.Add(NetworkAdapterOption.Create(adapter));
        }

        return options;
    }

    private static IReadOnlyList<GameCapturePreferenceOption> CreateGameCapturePreferences()
    {
        return
        [
            CreateGameCapturePreferenceOption(EGameCapturePreference.Auto),
            CreateGameCapturePreferenceOption(EGameCapturePreference.Standalone),
            CreateGameCapturePreferenceOption(EGameCapturePreference.Steam),
            CreateGameCapturePreferenceOption(EGameCapturePreference.Epic),
            CreateGameCapturePreferenceOption(EGameCapturePreference.HaoPlaySea),
            CreateGameCapturePreferenceOption(EGameCapturePreference.XDG),
            CreateGameCapturePreferenceOption(EGameCapturePreference.HaoPlaySeaSteam),
            CreateGameCapturePreferenceOption(EGameCapturePreference.XDGSteam),
            CreateGameCapturePreferenceOption(EGameCapturePreference.WeGame),
            CreateGameCapturePreferenceOption(EGameCapturePreference.Custom)
        ];
    }

    private static GameCapturePreferenceOption CreateGameCapturePreferenceOption(EGameCapturePreference preference)
    {
        return new GameCapturePreferenceOption(preference);
    }

    private static string NormalizeAdapterDeviceName(string? deviceName)
    {
        return CombatRuntimeSettings.IsAutomaticNetCaptureDeviceName(deviceName)
            ? CombatRuntimeSettings.AutomaticNetCaptureDeviceName
            : deviceName!.Trim();
    }

    private static string NormalizeCustomExeName(string? customExeName)
    {
        return Path.GetFileNameWithoutExtension(customExeName ?? string.Empty);
    }

    private static bool SettingsEquals(SettingsConfig left, SettingsConfig right)
    {
        AppConfigDefaults.NormalizeSettings(left);
        AppConfigDefaults.NormalizeSettings(right);

        return left.LanguageIndex == right.LanguageIndex
            && left.NumberDisplayFormatIndex == right.NumberDisplayFormatIndex
            && left.PlayerNameDisplayModeIndex == right.PlayerNameDisplayModeIndex
            && left.InternalIdDisplayModeIndex == right.InternalIdDisplayModeIndex
            && left.UnnamedSkillNameDisplayModeIndex == right.UnnamedSkillNameDisplayModeIndex
            && left.WidgetWindowTopmostModeIndex == right.WidgetWindowTopmostModeIndex
            && left.WidgetDataStatusDisplayModeIndex == right.WidgetDataStatusDisplayModeIndex
            && left.WindowColorIndex == right.WindowColorIndex
            && left.LaunchAtWindowsStartup == right.LaunchAtWindowsStartup
            && left.CheckForUpdatesOnStartup == right.CheckForUpdatesOnStartup
            && left.StartHotkeyHelperOnLaunch == right.StartHotkeyHelperOnLaunch
            && left.LaunchVoicevoxOnLaunch == right.LaunchVoicevoxOnLaunch
            && string.Equals(left.VoicevoxInstallFolder, right.VoicevoxInstallFolder, StringComparison.OrdinalIgnoreCase)
            && left.StopAggregationInNonCombatAreas == right.StopAggregationInNonCombatAreas
            && left.SplitEncountersOnNewPhases == right.SplitEncountersOnNewPhases
            && left.KeepPastEncounterInMeterUntilNextDamage == right.KeepPastEncounterInMeterUntilNextDamage
            && left.ClearHistorySelectionOnNextEvent == right.ClearHistorySelectionOnNextEvent
            && left.DatabaseMaxEncounterCount == right.DatabaseMaxEncounterCount
            && left.BenchmarkDurationSeconds == right.BenchmarkDurationSeconds
            && left.BenchmarkFirstTargetOnly == right.BenchmarkFirstTargetOnly
            && left.CombatExitSeconds == right.CombatExitSeconds
            && left.WindowColors.SequenceEqual(right.WindowColors, StringComparer.OrdinalIgnoreCase)
            && left.Hotkeys.HasSameBindings(right.Hotkeys);
    }

    partial void OnSelectedNetworkAdapterChanged(NetworkAdapterOption? value)
    {
        if (!_isLoadingNetworkAdapters && !_isLoadingCaptureSettings)
        {
            ApplyCaptureSettingsPreview();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnSelectedGameCapturePreferenceChanged(GameCapturePreferenceOption? value)
    {
        OnPropertyChanged(nameof(IsCustomGameCapturePreference));

        if (!_isLoadingCaptureSettings)
        {
            ApplyCaptureSettingsPreview();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnGameCaptureCustomExeNameChanged(string value)
    {
        if (!_isLoadingCaptureSettings)
        {
            ApplyCaptureSettingsPreview();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnLanguageIndexChanged(int value)
    {
        if (!_isLoadingSettings)
        {
            LocalizationManager.Instance.ApplyLanguageIndex(value);
            ApplySettingsPreview();

            // Windows の音声は表示言語の声で読むので、言語を変えたら声を確かめ直す。
            if (_configManager.GetNotificationsSnapshot().AnyEnabled(item => item.UsesWindowsVoice))
            {
                WindowsVoiceCheckRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    // 起動設定もプレビューを持たない。Windows の登録は保存したときに書き、ほかはアプリの次の起動から効く。未保存の印だけ更新する。
    partial void OnLaunchAtWindowsStartupChanged(bool value)
    {
        OnPropertyChanged(nameof(LaunchAtWindowsStartupStateText));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnCheckForUpdatesOnStartupChanged(bool value)
    {
        OnPropertyChanged(nameof(CheckForUpdatesOnStartupStateText));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnStartHotkeyHelperOnLaunchChanged(bool value)
    {
        OnPropertyChanged(nameof(StartHotkeyHelperOnLaunchStateText));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnLaunchVoicevoxOnLaunchChanged(bool value)
    {
        OnPropertyChanged(nameof(LaunchVoicevoxOnLaunchStateText));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnVoicevoxInstallFolderChanged(string value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public string LaunchAtWindowsStartupStateText => GetSwitchStateText(LaunchAtWindowsStartup);

    public string CheckForUpdatesOnStartupStateText => GetSwitchStateText(CheckForUpdatesOnStartup);

    public string StartHotkeyHelperOnLaunchStateText => GetSwitchStateText(StartHotkeyHelperOnLaunch);

    public string LaunchVoicevoxOnLaunchStateText => GetSwitchStateText(LaunchVoicevoxOnLaunch);

    /// <summary>補助が使えるかが変わった(途中で止まったなど)。ホットキーのナビと節の出し方を合わせる。</summary>
    private void HotkeyService_RegistrationChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsHotkeySectionVisible));
    }

    /// <summary>
    /// 動いている VOICEVOX の場所でインストール先が保存された。表示と保存済みの控えの両方を保存した値に合わせる
    /// (未保存の扱いにしない。画面で選んで保存していないフォルダーも、見つけた場所で置き換える)。
    /// </summary>
    private void ConfigManager_VoicevoxInstallFolderSaved(object? sender, EventArgs e)
    {
        var savedFolder = _configManager.AppConfig.Settings.VoicevoxInstallFolder;
        _lastSavedSettings.VoicevoxInstallFolder = savedFolder;

        _isLoadingSettings = true;
        try
        {
            VoicevoxInstallFolder = savedFolder;
        }
        finally
        {
            _isLoadingSettings = false;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    // 集計設定はプレビューを持たない。戦闘の区切り方やDBの掃除は「下見」できる類ではなく、
    // 保存したときにだけ効かせる。未保存の印だけ更新する。
    partial void OnStopAggregationInNonCombatAreasChanged(bool value)
    {
        OnPropertyChanged(nameof(StopAggregationInNonCombatAreasStateText));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnSplitEncountersOnNewPhasesChanged(bool value)
    {
        OnPropertyChanged(nameof(SplitEncountersOnNewPhasesStateText));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnKeepPastEncounterInMeterUntilNextDamageChanged(bool value)
    {
        OnPropertyChanged(nameof(KeepPastEncounterInMeterUntilNextDamageStateText));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnClearHistorySelectionOnNextEventChanged(bool value)
    {
        OnPropertyChanged(nameof(ClearHistorySelectionOnNextEventStateText));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>スイッチの右に出す ON / OFF。ウィジェット設定と同じ形。</summary>
    /// <summary>「名前のないスキル」の注意書き(内部名は中国語のみ)を出すか。「内部名で表示」を選んでいるときだけ。</summary>
    public bool ShowsUnnamedSkillNameNote =>
        UnnamedSkillNameDisplayModeIndex == AppConfigDefaults.UnnamedSkillNameInternalNameIndex;

    public string StopAggregationInNonCombatAreasStateText => GetSwitchStateText(StopAggregationInNonCombatAreas);

    public string SplitEncountersOnNewPhasesStateText => GetSwitchStateText(SplitEncountersOnNewPhases);

    public string KeepPastEncounterInMeterUntilNextDamageStateText =>
        GetSwitchStateText(KeepPastEncounterInMeterUntilNextDamage);

    public string ClearHistorySelectionOnNextEventStateText =>
        GetSwitchStateText(ClearHistorySelectionOnNextEvent);

    partial void OnBenchmarkDurationSecondsChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// 計測時間の入力欄の文字を確定する。30 より小さければ 30、1200 より大きければ 1200 にする。
    /// 空など数字として読めないときは値を変えずに false を返す。欄の書き直しはビューが行う(確定のたびに今の値で)。
    /// </summary>
    public bool TryCommitBenchmarkDurationText(string text)
    {
        if (text.Length == 0 || !text.All(char.IsAsciiDigit))
        {
            return false;
        }

        // 桁が多すぎて int に入らない値も 1200 より大きいので、上の端にする。
        var seconds = int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : int.MaxValue;
        BenchmarkDurationSeconds = AppConfigDefaults.ClampBenchmarkDurationSeconds(seconds);
        return true;
    }

    partial void OnBenchmarkFirstTargetOnlyChanged(bool value)
    {
        OnPropertyChanged(nameof(BenchmarkFirstTargetOnlyStateText));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public string BenchmarkFirstTargetOnlyStateText => GetSwitchStateText(BenchmarkFirstTargetOnly);

    private static string GetSwitchStateText(bool isOn)
    {
        return LocalizationManager.Instance.GetString(isOn ? "Settings_Switch_On" : "Settings_Switch_Off");
    }

    partial void OnDatabaseMaxEncounterCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public ReadOnlyObservableCollection<RetentionPolicyOption> RetentionPolicyOptions { get; }

    /// <summary>
    /// 選択肢を作り直す。**言語切替のたびに呼ぶ** — 文言が言語で変わるので、
    /// 作りっぱなしだと開いたまま切り替えたときに古い言語のまま残る。
    /// </summary>
    private void RebuildRetentionPolicyOptions()
    {
        // 作り直すと SelectedValue の参照先が消えるので、選択を戻せるよう控えておく。
        // 通知を出さない ＝ 空欄のままになる。控えた値を押し直して選び直させる。
        var selected = DatabaseMaxEncounterCount;

        _retentionPolicyOptions.Clear();
        foreach (var count in MaxEncounterCountChoices)
        {
            _retentionPolicyOptions.Add(new RetentionPolicyOption(count, FormatRetentionPolicy(count)));
        }

        DatabaseMaxEncounterCount = selected;
        OnPropertyChanged(nameof(DatabaseMaxEncounterCount));
    }

    partial void OnCombatExitSecondsChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public ReadOnlyObservableCollection<CombatExitOption> CombatExitOptions { get; }

    /// <summary>
    /// 自動一時停止の選択肢を作り直す。<see cref="RebuildRetentionPolicyOptions"/> と同じく言語切替のたびに呼び、選択を戻す。
    /// </summary>
    private void RebuildCombatExitOptions()
    {
        var selected = CombatExitSeconds;

        _combatExitOptions.Clear();
        foreach (var seconds in AppConfigDefaults.CombatExitChoicesSeconds)
        {
            _combatExitOptions.Add(new CombatExitOption(seconds, FormatCombatExit(seconds)));
        }

        CombatExitSeconds = selected;
        OnPropertyChanged(nameof(CombatExitSeconds));
    }

    /// <summary><b>0 は秒数ではなく「一時停止しない」</b>なので数字を出さない。</summary>
    private static string FormatCombatExit(int seconds)
    {
        return seconds <= 0
            ? LocalizationManager.Instance.GetString("Settings_Aggregation_CombatExit_None")
            : string.Format(
                LocalizationManager.Instance.GetString("Settings_Aggregation_CombatExit_Value"),
                seconds);
    }

    /// <summary><b>0 は件数ではなく「無限」</b>なので数字を出さない。</summary>
    private static string FormatRetentionPolicy(int count)
    {
        return count <= 0
            ? LocalizationManager.Instance.GetString("Settings_Aggregation_MaxEncounterCount_Unlimited")
            : string.Format(
                LocalizationManager.Instance.GetString("Settings_Aggregation_MaxEncounterCount_Value"),
                count);
    }

    partial void OnNumberDisplayFormatIndexChanged(int value)
    {
        if (!_isLoadingSettings)
        {
            ApplySettingsPreview();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnPlayerNameDisplayModeIndexChanged(int value)
    {
        if (!_isLoadingSettings)
        {
            ApplySettingsPreview();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnInternalIdDisplayModeIndexChanged(int value)
    {
        if (!_isLoadingSettings)
        {
            ApplySettingsPreview();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnUnnamedSkillNameDisplayModeIndexChanged(int value)
    {
        if (!_isLoadingSettings)
        {
            ApplySettingsPreview();
        }

        OnPropertyChanged(nameof(ShowsUnnamedSkillNameNote));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// ホットキーがウィジェット設定「ウィジェットウィンドウ」を切り替えて保存した。表示と保存済みの控えの両方を保存した値に合わせる
    /// (未保存の扱いにしない)。プレビューは保存の側で合わせてあるので出し直さない。
    /// </summary>
    private void ConfigManager_WidgetWindowTopmostModeSaved(object? sender, EventArgs e)
    {
        var savedIndex = _configManager.AppConfig.Settings.WidgetWindowTopmostModeIndex;
        _lastSavedSettings.WidgetWindowTopmostModeIndex = savedIndex;

        _isLoadingSettings = true;
        try
        {
            WidgetWindowTopmostModeIndex = savedIndex;
        }
        finally
        {
            _isLoadingSettings = false;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// ホットキーが表示設定「プレイヤー名」を切り替えて保存した。表示と保存済みの控えの両方を保存した値に合わせる
    /// (未保存の扱いにしない)。プレビューと名前の表示は保存の側で合わせてあるので出し直さない。
    /// </summary>
    private void ConfigManager_PlayerNameDisplayModeSaved(object? sender, EventArgs e)
    {
        var savedIndex = _configManager.AppConfig.Settings.PlayerNameDisplayModeIndex;
        _lastSavedSettings.PlayerNameDisplayModeIndex = savedIndex;

        _isLoadingSettings = true;
        try
        {
            PlayerNameDisplayModeIndex = savedIndex;
        }
        finally
        {
            _isLoadingSettings = false;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnWidgetWindowTopmostModeIndexChanged(int value)
    {
        if (!_isLoadingSettings)
        {
            ApplySettingsPreview();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnWidgetDataStatusDisplayModeIndexChanged(int value)
    {
        if (!_isLoadingSettings)
        {
            ApplySettingsPreview();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }
}
