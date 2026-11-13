// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using System.Windows.Media;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels.WidgetSettings;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class WidgetSettingsViewModel : ViewModelBase, IDisposable
{
    private readonly WidgetStateManager _stateManager = WidgetStateManager.Instance;
    private readonly WidgetListItemViewModel _widget;
    private readonly WidgetKey _key;
    private WidgetThemeConfig _lastSavedTheme;
    private bool _isLoadingTheme;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private double _windowOpacity = 50;

    [ObservableProperty]
    private string? _backgroundImagePath;

    [ObservableProperty]
    private string? _backgroundImageAverageColor;

    [ObservableProperty]
    private string? _backgroundImageAverageColorSourcePath;

    public WidgetSettingsViewModel(WidgetListItemViewModel widget)
    {
        _widget = widget;
        _key = widget.Key;
        var kind = widget.Kind;
        DisplayName = GetWidgetDisplayName();
        WindowColors = new ColorPaletteViewModel(
            WidgetConfigDefaults.CreateDefaultWindowColors(),
            WidgetConfigDefaults.MaxPaletteColorCount);
        WindowColors.PaletteChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));

            if (!_isLoadingTheme)
            {
                RaiseThemePreviewChanged();
            }
        };

        var config = _stateManager.GetWidgetSnapshot(_key);
        _lastSavedTheme = WidgetConfigDefaults.CloneNormalizedTheme(config.Theme);
        LoadFromTheme(config.Theme, raisePreview: false);

        if (WidgetConfigDefaults.SupportsMeterSettings(kind))
        {
            MeterSettings = new MeterWidgetSettingsViewModel(kind, config.Meter);
            MeterSettings.PropertyChanged += MeterSettings_PropertyChanged;
            MeterSettings.PreviewChanged += MeterSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsMetricGraphSettings(kind))
        {
            MetricGraphSettings = new MetricGraphWidgetSettingsViewModel(kind, config.MetricGraph);
            MetricGraphSettings.PropertyChanged += MetricGraphSettings_PropertyChanged;
            MetricGraphSettings.PreviewChanged += MetricGraphSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsBuffCardSettings(kind))
        {
            BuffCardSettings = new BuffCardWidgetSettingsViewModel(config.BuffCard);
            BuffCardSettings.PropertyChanged += BuffCardSettings_PropertyChanged;
            BuffCardSettings.PreviewChanged += BuffCardSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsTimelineSettings(kind))
        {
            TimelineSettings = new TimelineWidgetSettingsViewModel(config.Timeline);
            TimelineSettings.PropertyChanged += TimelineSettings_PropertyChanged;
            TimelineSettings.PreviewChanged += TimelineSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsPlayerInfoSettings(kind))
        {
            PlayerInfoSettings = new PlayerInfoWidgetSettingsViewModel(config.PlayerInfo);
            PlayerInfoSettings.PropertyChanged += PlayerInfoSettings_PropertyChanged;
            PlayerInfoSettings.PreviewChanged += PlayerInfoSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsBuffListSettings(kind))
        {
            BuffListSettings = new BuffListWidgetSettingsViewModel(kind, config.BuffList);
            BuffListSettings.PropertyChanged += BuffListSettings_PropertyChanged;
            BuffListSettings.PreviewChanged += BuffListSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsElementColorSettings(kind))
        {
            ElementColorSettings = new ElementColorWidgetSettingsViewModel(kind, config.ElementColor);
            ElementColorSettings.PropertyChanged += ElementColorSettings_PropertyChanged;
            ElementColorSettings.PreviewChanged += ElementColorSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsSkillDetailSettings(kind))
        {
            SkillDetailSettings = new SkillDetailWidgetSettingsViewModel(config.SkillDetail);
            SkillDetailSettings.PropertyChanged += SkillDetailSettings_PropertyChanged;
            SkillDetailSettings.PreviewChanged += SkillDetailSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsEquipmentSettings(kind))
        {
            EquipmentSettings = new EquipmentWidgetSettingsViewModel(config.Equipment);
            EquipmentSettings.PropertyChanged += EquipmentSettings_PropertyChanged;
            EquipmentSettings.PreviewChanged += EquipmentSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsPlayerStatusSettings(kind))
        {
            PlayerStatusSettings = new PlayerStatusWidgetSettingsViewModel(config.PlayerStatus, config.PlayerStatusRowOrder);
            PlayerStatusSettings.PropertyChanged += PlayerStatusSettings_PropertyChanged;
            PlayerStatusSettings.PreviewChanged += PlayerStatusSettings_PreviewChanged;
            PlayerStatusSettings.RowOrderPreviewChanged += PlayerStatusSettings_RowOrderPreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsBuffDebuffGroupSettings(kind))
        {
            BuffDebuffGroupSettings = new BuffDebuffGroupWidgetSettingsViewModel(config.BuffDebuffGroup);
            BuffDebuffGroupSettings.PropertyChanged += BuffDebuffGroupSettings_PropertyChanged;
            BuffDebuffGroupSettings.PreviewChanged += BuffDebuffGroupSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsReviveListSettings(kind))
        {
            ReviveListSettings = new ReviveListWidgetSettingsViewModel(config.ReviveList);
            ReviveListSettings.PropertyChanged += ReviveListSettings_PropertyChanged;
            ReviveListSettings.PreviewChanged += ReviveListSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsDamageTakenLogSettings(kind))
        {
            DamageTakenLogSettings = new DamageTakenLogWidgetSettingsViewModel(config.DamageTakenLog);
            DamageTakenLogSettings.PropertyChanged += DamageTakenLogSettings_PropertyChanged;
            DamageTakenLogSettings.PreviewChanged += DamageTakenLogSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsEntityInfoSettings(kind))
        {
            EntityInfoSettings = new EntityInfoWidgetSettingsViewModel(config.EntityInfo);
            EntityInfoSettings.PropertyChanged += EntityInfoSettings_PropertyChanged;
            EntityInfoSettings.PreviewChanged += EntityInfoSettings_PreviewChanged;
        }

        if (WidgetConfigDefaults.SupportsChatSettings(kind))
        {
            ChatSettings = new ChatWidgetSettingsViewModel(config.Chat);
            ChatSettings.PropertyChanged += ChatSettings_PropertyChanged;
            ChatSettings.PreviewChanged += ChatSettings_PreviewChanged;
        }

        if (widget.PluginWidget is { } pluginWidget)
        {
            PluginSettings = new PluginWidgetSettingsViewModel(pluginWidget, config.Plugin);
            PluginSettings.PropertyChanged += PluginSettings_PropertyChanged;
            PluginSettings.PreviewChanged += PluginSettings_PreviewChanged;
        }

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    /// <summary>プラグインのウィジェットの節(プラグインのウィジェットだけ)。</summary>
    public PluginWidgetSettingsViewModel? PluginSettings { get; }

    public bool HasPluginSettings => PluginSettings is not null;

    public event Action<PluginWidgetSettingsConfig>? PluginPreviewChanged;

    private void PluginSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PluginWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void PluginSettings_PreviewChanged(PluginWidgetSettingsConfig config)
    {
        PluginPreviewChanged?.Invoke(config);
    }

    /// <summary>カードと同じ名前(本体は文言の鍵、プラグインのウィジェットは定義の名前)。</summary>
    private string GetWidgetDisplayName()
    {
        return _widget.PluginWidget is { } pluginWidget
            ? pluginWidget.GetDisplayName(LocalizationManager.Instance.CurrentCulture)
            : LocalizationManager.Instance.GetString(_widget.DisplayNameResourceKey);
    }

    public event Action<WidgetThemeConfig>? ThemePreviewChanged;

    public event Action<MeterWidgetSettingsConfig>? MeterPreviewChanged;

    public event Action<MetricGraphWidgetSettingsConfig>? MetricGraphPreviewChanged;

    public event Action<BuffCardWidgetSettingsConfig>? BuffCardPreviewChanged;

    public event Action<TimelineWidgetSettingsConfig>? TimelinePreviewChanged;

    public event Action<PlayerInfoWidgetSettingsConfig>? PlayerInfoPreviewChanged;

    public event Action<BuffListWidgetSettingsConfig>? BuffListPreviewChanged;

    public event Action<ElementColorWidgetSettingsConfig>? ElementColorPreviewChanged;

    public event Action<SkillDetailWidgetSettingsConfig>? SkillDetailPreviewChanged;

    public event Action<EquipmentWidgetSettingsConfig>? EquipmentPreviewChanged;

    public event Action<PlayerStatusWidgetSettingsConfig>? PlayerStatusPreviewChanged;

    /// <summary>ステータス詳細の行の並びの保存前のプレビュー(「ステータス」の一覧の並べ替えと、破棄で保存された並びへ戻すとき)。</summary>
    public event Action<IReadOnlyList<int>>? PlayerStatusRowOrderPreviewChanged;

    public event Action<BuffDebuffGroupWidgetSettingsConfig>? BuffDebuffGroupPreviewChanged;

    public event Action<ReviveListWidgetSettingsConfig>? ReviveListPreviewChanged;

    public event Action<DamageTakenLogWidgetSettingsConfig>? DamageTakenLogPreviewChanged;

    public event Action<EntityInfoWidgetSettingsConfig>? EntityInfoPreviewChanged;

    public event Action<ChatWidgetSettingsConfig>? ChatPreviewChanged;

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        if (MeterSettings is not null)
        {
            MeterSettings.PropertyChanged -= MeterSettings_PropertyChanged;
            MeterSettings.PreviewChanged -= MeterSettings_PreviewChanged;
            MeterSettings.Dispose();
        }

        if (MetricGraphSettings is not null)
        {
            MetricGraphSettings.PropertyChanged -= MetricGraphSettings_PropertyChanged;
            MetricGraphSettings.PreviewChanged -= MetricGraphSettings_PreviewChanged;
            MetricGraphSettings.Dispose();
        }

        if (BuffCardSettings is not null)
        {
            BuffCardSettings.PropertyChanged -= BuffCardSettings_PropertyChanged;
            BuffCardSettings.PreviewChanged -= BuffCardSettings_PreviewChanged;
            BuffCardSettings.Dispose();
        }

        if (TimelineSettings is not null)
        {
            TimelineSettings.PropertyChanged -= TimelineSettings_PropertyChanged;
            TimelineSettings.PreviewChanged -= TimelineSettings_PreviewChanged;
            TimelineSettings.Dispose();
        }

        if (PlayerInfoSettings is not null)
        {
            PlayerInfoSettings.PropertyChanged -= PlayerInfoSettings_PropertyChanged;
            PlayerInfoSettings.PreviewChanged -= PlayerInfoSettings_PreviewChanged;
            PlayerInfoSettings.Dispose();
        }

        if (BuffListSettings is not null)
        {
            BuffListSettings.PropertyChanged -= BuffListSettings_PropertyChanged;
            BuffListSettings.PreviewChanged -= BuffListSettings_PreviewChanged;
            BuffListSettings.Dispose();
        }

        if (ElementColorSettings is not null)
        {
            ElementColorSettings.PropertyChanged -= ElementColorSettings_PropertyChanged;
            ElementColorSettings.PreviewChanged -= ElementColorSettings_PreviewChanged;
            ElementColorSettings.Dispose();
        }

        if (SkillDetailSettings is not null)
        {
            SkillDetailSettings.PropertyChanged -= SkillDetailSettings_PropertyChanged;
            SkillDetailSettings.PreviewChanged -= SkillDetailSettings_PreviewChanged;
            SkillDetailSettings.Dispose();
        }

        if (EquipmentSettings is not null)
        {
            EquipmentSettings.PropertyChanged -= EquipmentSettings_PropertyChanged;
            EquipmentSettings.PreviewChanged -= EquipmentSettings_PreviewChanged;
            EquipmentSettings.Dispose();
        }

        if (PlayerStatusSettings is not null)
        {
            PlayerStatusSettings.PropertyChanged -= PlayerStatusSettings_PropertyChanged;
            PlayerStatusSettings.PreviewChanged -= PlayerStatusSettings_PreviewChanged;
            PlayerStatusSettings.RowOrderPreviewChanged -= PlayerStatusSettings_RowOrderPreviewChanged;
            PlayerStatusSettings.Dispose();
        }

        if (BuffDebuffGroupSettings is not null)
        {
            BuffDebuffGroupSettings.PropertyChanged -= BuffDebuffGroupSettings_PropertyChanged;
            BuffDebuffGroupSettings.PreviewChanged -= BuffDebuffGroupSettings_PreviewChanged;
            BuffDebuffGroupSettings.Dispose();
        }

        if (ReviveListSettings is not null)
        {
            ReviveListSettings.PropertyChanged -= ReviveListSettings_PropertyChanged;
            ReviveListSettings.PreviewChanged -= ReviveListSettings_PreviewChanged;
            ReviveListSettings.Dispose();
        }

        if (DamageTakenLogSettings is not null)
        {
            DamageTakenLogSettings.PropertyChanged -= DamageTakenLogSettings_PropertyChanged;
            DamageTakenLogSettings.PreviewChanged -= DamageTakenLogSettings_PreviewChanged;
            DamageTakenLogSettings.Dispose();
        }

        if (EntityInfoSettings is not null)
        {
            EntityInfoSettings.PropertyChanged -= EntityInfoSettings_PropertyChanged;
            EntityInfoSettings.PreviewChanged -= EntityInfoSettings_PreviewChanged;
            EntityInfoSettings.Dispose();
        }

        if (ChatSettings is not null)
        {
            ChatSettings.PropertyChanged -= ChatSettings_PropertyChanged;
            ChatSettings.PreviewChanged -= ChatSettings_PreviewChanged;
            ChatSettings.Dispose();
        }

        if (PluginSettings is not null)
        {
            PluginSettings.PropertyChanged -= PluginSettings_PropertyChanged;
            PluginSettings.PreviewChanged -= PluginSettings_PreviewChanged;
            PluginSettings.Dispose();
        }
    }

    private void ChatSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void ChatSettings_PreviewChanged(ChatWidgetSettingsConfig config)
    {
        ChatPreviewChanged?.Invoke(config);
    }

    private void EntityInfoSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EntityInfoWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void EntityInfoSettings_PreviewChanged(EntityInfoWidgetSettingsConfig config)
    {
        EntityInfoPreviewChanged?.Invoke(config);
    }

    private void ReviveListSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReviveListWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void ReviveListSettings_PreviewChanged(ReviveListWidgetSettingsConfig config)
    {
        ReviveListPreviewChanged?.Invoke(config);
    }

    private void DamageTakenLogSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DamageTakenLogWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void DamageTakenLogSettings_PreviewChanged(DamageTakenLogWidgetSettingsConfig config)
    {
        DamageTakenLogPreviewChanged?.Invoke(config);
    }

    private void BuffDebuffGroupSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BuffDebuffGroupWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void BuffDebuffGroupSettings_PreviewChanged(BuffDebuffGroupWidgetSettingsConfig config)
    {
        BuffDebuffGroupPreviewChanged?.Invoke(config);
    }

    private void SkillDetailSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SkillDetailWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void SkillDetailSettings_PreviewChanged(SkillDetailWidgetSettingsConfig config)
    {
        SkillDetailPreviewChanged?.Invoke(config);
    }

    private void EquipmentSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EquipmentWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void EquipmentSettings_PreviewChanged(EquipmentWidgetSettingsConfig config)
    {
        EquipmentPreviewChanged?.Invoke(config);
    }

    private void PlayerStatusSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerStatusWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void PlayerStatusSettings_PreviewChanged(PlayerStatusWidgetSettingsConfig config)
    {
        PlayerStatusPreviewChanged?.Invoke(config);
    }

    private void PlayerStatusSettings_RowOrderPreviewChanged(IReadOnlyList<int> rowOrder)
    {
        PlayerStatusRowOrderPreviewChanged?.Invoke(rowOrder);
    }

    private void TimelineSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TimelineWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void TimelineSettings_PreviewChanged(TimelineWidgetSettingsConfig config)
    {
        TimelinePreviewChanged?.Invoke(config);
    }

    private void PlayerInfoSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerInfoWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void PlayerInfoSettings_PreviewChanged(PlayerInfoWidgetSettingsConfig config)
    {
        PlayerInfoPreviewChanged?.Invoke(config);
    }

    private void BuffListSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BuffListWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void BuffListSettings_PreviewChanged(BuffListWidgetSettingsConfig config)
    {
        BuffListPreviewChanged?.Invoke(config);
    }

    private void ElementColorSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ElementColorWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void ElementColorSettings_PreviewChanged(ElementColorWidgetSettingsConfig config)
    {
        ElementColorPreviewChanged?.Invoke(config);
    }

    private void BuffCardSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BuffCardWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void BuffCardSettings_PreviewChanged(BuffCardWidgetSettingsConfig config)
    {
        BuffCardPreviewChanged?.Invoke(config);
    }

    private void MeterSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MeterWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void MeterSettings_PreviewChanged(MeterWidgetSettingsConfig config)
    {
        MeterPreviewChanged?.Invoke(config);
    }

    private void MetricGraphSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MetricGraphWidgetSettingsViewModel.HasUnsavedChanges))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void MetricGraphSettings_PreviewChanged(MetricGraphWidgetSettingsConfig config)
    {
        MetricGraphPreviewChanged?.Invoke(config);
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        DisplayName = GetWidgetDisplayName();
        OnPropertyChanged(nameof(WindowTitle));
    }

    public string WindowTitle => LocalizationManager.Instance.Format("Window_WidgetSettings_Title", GetWidgetDisplayName());

    public ColorPaletteViewModel WindowColors { get; }

    public MeterWidgetSettingsViewModel? MeterSettings { get; }

    public MetricGraphWidgetSettingsViewModel? MetricGraphSettings { get; }

    public BuffCardWidgetSettingsViewModel? BuffCardSettings { get; }

    public TimelineWidgetSettingsViewModel? TimelineSettings { get; }

    public PlayerInfoWidgetSettingsViewModel? PlayerInfoSettings { get; }

    public BuffListWidgetSettingsViewModel? BuffListSettings { get; }

    /// <summary>属性カラー(与ダメ詳細・ヒール詳細・被ダメ詳細ではアイコンカラー)。</summary>
    public ElementColorWidgetSettingsViewModel? ElementColorSettings { get; }

    public SkillDetailWidgetSettingsViewModel? SkillDetailSettings { get; }

    public EquipmentWidgetSettingsViewModel? EquipmentSettings { get; }

    public PlayerStatusWidgetSettingsViewModel? PlayerStatusSettings { get; }

    public BuffDebuffGroupWidgetSettingsViewModel? BuffDebuffGroupSettings { get; }

    public ReviveListWidgetSettingsViewModel? ReviveListSettings { get; }

    public DamageTakenLogWidgetSettingsViewModel? DamageTakenLogSettings { get; }

    public EntityInfoWidgetSettingsViewModel? EntityInfoSettings { get; }

    public ChatWidgetSettingsViewModel? ChatSettings { get; }

    public bool HasMeterSettings => MeterSettings is not null;

    /// <summary>属性カラー(アイコンカラー)の節を出すか。表示の節とは別なので <c>HasDisplaySettings</c> には入れない。</summary>
    public bool HasElementColorSettings => ElementColorSettings is not null;

    public bool HasMeterDisplaySettings => HasMeterSettings;

    public bool HasMetricGraphDisplaySettings => MetricGraphSettings is not null;

    public bool HasBuffCardDisplaySettings => BuffCardSettings is not null;

    public bool HasTimelineDisplaySettings => TimelineSettings is not null;

    /// <summary>プレイヤー情報のアイコンカラーの節を出すか。表示の節とは別なので <c>HasDisplaySettings</c> には入れない。</summary>
    public bool HasPlayerInfoSettings => PlayerInfoSettings is not null;

    public bool HasBuffListDisplaySettings => BuffListSettings is not null;

    public bool HasSkillDetailDisplaySettings => SkillDetailSettings is not null;

    public bool HasEquipmentDisplaySettings => EquipmentSettings is not null;

    public bool HasPlayerStatusDisplaySettings => PlayerStatusSettings is not null;

    public bool HasBuffDebuffGroupDisplaySettings => BuffDebuffGroupSettings is not null;

    public bool HasReviveListDisplaySettings => ReviveListSettings is not null;

    public bool HasDamageTakenLogDisplaySettings => DamageTakenLogSettings is not null;

    public bool HasEntityInfoDisplaySettings => EntityInfoSettings is not null;

    /// <summary>チャットのテキストカラーの節を出すか。チャットに表示の節は無いので <c>HasDisplaySettings</c> には入れない。</summary>
    public bool HasChatSettings => ChatSettings is not null;

    public bool HasDisplaySettings => HasMeterDisplaySettings
        || HasReviveListDisplaySettings
        || HasDamageTakenLogDisplaySettings
        || HasEntityInfoDisplaySettings
        || HasMetricGraphDisplaySettings
        || HasBuffCardDisplaySettings
        || HasTimelineDisplaySettings
        || HasBuffListDisplaySettings
        || HasSkillDetailDisplaySettings
        || HasEquipmentDisplaySettings
        || HasPlayerStatusDisplaySettings
        || HasBuffDebuffGroupDisplaySettings;

    public bool HasUnsavedChanges => !ThemeEquals(CreateTheme(), _lastSavedTheme)
        || (MeterSettings?.HasUnsavedChanges ?? false)
        || (MetricGraphSettings?.HasUnsavedChanges ?? false)
        || (BuffCardSettings?.HasUnsavedChanges ?? false)
        || (TimelineSettings?.HasUnsavedChanges ?? false)
        || (PlayerInfoSettings?.HasUnsavedChanges ?? false)
        || (BuffListSettings?.HasUnsavedChanges ?? false)
        || (ElementColorSettings?.HasUnsavedChanges ?? false)
        || (SkillDetailSettings?.HasUnsavedChanges ?? false)
        || (EquipmentSettings?.HasUnsavedChanges ?? false)
        || (PlayerStatusSettings?.HasUnsavedChanges ?? false)
        || (BuffDebuffGroupSettings?.HasUnsavedChanges ?? false)
        || (ReviveListSettings?.HasUnsavedChanges ?? false)
        || (DamageTakenLogSettings?.HasUnsavedChanges ?? false)
        || (EntityInfoSettings?.HasUnsavedChanges ?? false)
        || (ChatSettings?.HasUnsavedChanges ?? false)
        || (PluginSettings?.HasUnsavedChanges ?? false);

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

    public WidgetConfig SaveSettings()
    {
        var config = _stateManager.GetWidgetSnapshot(_key);
        var theme = CreateTheme();
        config.Theme = theme;
        if (MeterSettings is not null)
        {
            config.Meter = MeterSettings.CreateConfig();
        }

        if (MetricGraphSettings is not null)
        {
            config.MetricGraph = MetricGraphSettings.CreateConfig();
        }

        if (BuffCardSettings is not null)
        {
            config.BuffCard = BuffCardSettings.CreateConfig();
        }

        if (TimelineSettings is not null)
        {
            config.Timeline = TimelineSettings.CreateConfig();
        }

        if (PlayerInfoSettings is not null)
        {
            config.PlayerInfo = PlayerInfoSettings.CreateConfig();
        }

        if (BuffListSettings is not null)
        {
            config.BuffList = BuffListSettings.CreateConfig();
        }

        if (ElementColorSettings is not null)
        {
            config.ElementColor = ElementColorSettings.CreateConfig();
        }

        if (PlayerStatusSettings is not null)
        {
            config.PlayerStatus = PlayerStatusSettings.CreateConfig();
            config.PlayerStatusRowOrder = PlayerStatusSettings.GetRowOrderSnapshot();
        }

        if (SkillDetailSettings is not null)
        {
            config.SkillDetail = SkillDetailSettings.CreateConfig();
        }

        if (EquipmentSettings is not null)
        {
            config.Equipment = EquipmentSettings.CreateConfig();
        }

        if (BuffDebuffGroupSettings is not null)
        {
            config.BuffDebuffGroup = BuffDebuffGroupSettings.CreateConfig();
        }

        if (ReviveListSettings is not null)
        {
            config.ReviveList = ReviveListSettings.CreateConfig();
        }

        if (DamageTakenLogSettings is not null)
        {
            config.DamageTakenLog = DamageTakenLogSettings.CreateConfig();
        }

        if (EntityInfoSettings is not null)
        {
            config.EntityInfo = EntityInfoSettings.CreateConfig();
        }

        if (ChatSettings is not null)
        {
            config.Chat = ChatSettings.CreateConfig();
        }

        if (PluginSettings is not null)
        {
            config.Plugin = PluginSettings.CreateConfig();
        }

        _stateManager.SaveWidget(_key, config);

        _lastSavedTheme = theme.Clone();
        MeterSettings?.MarkSaved(config.Meter);
        MetricGraphSettings?.MarkSaved(config.MetricGraph);
        BuffCardSettings?.MarkSaved(config.BuffCard);
        TimelineSettings?.MarkSaved(config.Timeline);
        PlayerInfoSettings?.MarkSaved(config.PlayerInfo);
        BuffListSettings?.MarkSaved(config.BuffList);
        ElementColorSettings?.MarkSaved(config.ElementColor);
        SkillDetailSettings?.MarkSaved(config.SkillDetail);
        EquipmentSettings?.MarkSaved(config.Equipment);
        PlayerStatusSettings?.MarkSaved(config.PlayerStatus, config.PlayerStatusRowOrder);
        BuffDebuffGroupSettings?.MarkSaved(config.BuffDebuffGroup);
        ReviveListSettings?.MarkSaved(config.ReviveList);
        DamageTakenLogSettings?.MarkSaved(config.DamageTakenLog);
        EntityInfoSettings?.MarkSaved(config.EntityInfo);
        ChatSettings?.MarkSaved(config.Chat);
        PluginSettings?.MarkSaved(config.Plugin);
        OnPropertyChanged(nameof(HasUnsavedChanges));
        return config.Clone();
    }

    public void ResetToDefaults()
    {
        LoadFromTheme(WidgetConfigDefaults.CreateTheme(), raisePreview: true);
        MeterSettings?.ResetToDefaults();
        MetricGraphSettings?.ResetToDefaults();
        BuffCardSettings?.ResetToDefaults();
        TimelineSettings?.ResetToDefaults();
        PlayerInfoSettings?.ResetToDefaults();
        BuffListSettings?.ResetToDefaults();
        ElementColorSettings?.ResetToDefaults();
        SkillDetailSettings?.ResetToDefaults();
        EquipmentSettings?.ResetToDefaults();
        PlayerStatusSettings?.ResetToDefaults();
        BuffDebuffGroupSettings?.ResetToDefaults();
        ReviveListSettings?.ResetToDefaults();
        DamageTakenLogSettings?.ResetToDefaults();
        EntityInfoSettings?.ResetToDefaults();
        ChatSettings?.ResetToDefaults();
        PluginSettings?.ResetToDefaults();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void RestoreSavedPreviews()
    {
        ThemePreviewChanged?.Invoke(_lastSavedTheme.Clone());
        MeterSettings?.RestoreSavedPreview();
        MetricGraphSettings?.RestoreSavedPreview();
        BuffCardSettings?.RestoreSavedPreview();
        TimelineSettings?.RestoreSavedPreview();
        PlayerInfoSettings?.RestoreSavedPreview();
        BuffListSettings?.RestoreSavedPreview();
        ElementColorSettings?.RestoreSavedPreview();
        SkillDetailSettings?.RestoreSavedPreview();
        EquipmentSettings?.RestoreSavedPreview();
        PlayerStatusSettings?.RestoreSavedPreview();
        BuffDebuffGroupSettings?.RestoreSavedPreview();
        ReviveListSettings?.RestoreSavedPreview();
        DamageTakenLogSettings?.RestoreSavedPreview();
        EntityInfoSettings?.RestoreSavedPreview();
        ChatSettings?.RestoreSavedPreview();
        PluginSettings?.RestoreSavedPreview();
    }

    public Color GetSelectedWindowColor()
    {
        return WindowColors.SelectedColor;
    }

    public void ApplyWindowColor(Color color)
    {
        var previousLoadingState = _isLoadingTheme;
        _isLoadingTheme = true;

        try
        {
            WindowColors.AddOrSelect(color);
            BackgroundImagePath = null;
            BackgroundImageAverageColor = null;
            BackgroundImageAverageColorSourcePath = null;
        }
        finally
        {
            _isLoadingTheme = previousLoadingState;
        }

        if (!previousLoadingState)
        {
            RaiseThemePreviewChanged();
        }
    }

    public void SetBackgroundImagePath(string? path)
    {
        var normalizedPath = string.IsNullOrWhiteSpace(path)
            ? null
            : path.Trim();
        var previousLoadingState = _isLoadingTheme;
        _isLoadingTheme = true;

        try
        {
            BackgroundImagePath = normalizedPath;
            BackgroundImageAverageColorSourcePath = normalizedPath;
            BackgroundImageAverageColor = normalizedPath is not null
                && BackgroundImageColorAnalyzer.TryCalculateAverageColor(normalizedPath, out var averageColor)
                    ? ColorUtilities.ToHex(averageColor)
                    : null;
        }
        finally
        {
            _isLoadingTheme = previousLoadingState;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
        if (!previousLoadingState)
        {
            RaiseThemePreviewChanged();
        }
    }

    /// <summary>ピン留め中にヘッダーを隠す。</summary>
    [ObservableProperty]
    private bool _hideHeaderWhenInactive = true;

    /// <summary>ピン留め中フッターを隠す。</summary>
    [ObservableProperty]
    private bool _hideFooterWhenInactive;

    /// <summary>ウィンドウの枠とフォーカスの設定。<b>全ウィジェットで出す。</b></summary>
    public bool HasWindowSettings => true;

    /// <summary>「ピン留め中フッターを隠す」を出すか。フッターを持つウィジェットだけ。</summary>
    public bool HasFooterSetting => _widget.HasFooter;

    /// <summary>スイッチの右に出す ON / OFF。</summary>
    public string HideHeaderWhenInactiveStateText => GetSwitchStateText(HideHeaderWhenInactive);

    public string HideFooterWhenInactiveStateText => GetSwitchStateText(HideFooterWhenInactive);

    private static string GetSwitchStateText(bool isOn)
    {
        return LocalizationManager.Instance.GetString(isOn ? "Settings_Switch_On" : "Settings_Switch_Off");
    }

    private WidgetThemeConfig CreateTheme()
    {
        var theme = new WidgetThemeConfig
        {
            WindowColorIndex = WindowColors.SelectedIndex,
            WindowOpacity = Math.Clamp(
                (int)Math.Round(WindowOpacity, MidpointRounding.AwayFromZero),
                WidgetConfigDefaults.MinWindowOpacity,
                WidgetConfigDefaults.MaxWindowOpacity),
            WindowColors = [.. WindowColors.GetHexColors()],
            BackgroundImagePath = string.IsNullOrWhiteSpace(BackgroundImagePath)
                ? null
                : BackgroundImagePath.Trim(),
            BackgroundImageAverageColor = BackgroundImageAverageColor,
            BackgroundImageAverageColorSourcePath = BackgroundImageAverageColorSourcePath,
            HideHeaderWhenInactive = HideHeaderWhenInactive,
            HideFooterWhenInactive = HideFooterWhenInactive
        };

        WidgetConfigDefaults.NormalizeTheme(theme);
        return theme;
    }

    private void LoadFromTheme(WidgetThemeConfig theme, bool raisePreview)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedTheme(theme);

        _isLoadingTheme = true;
        try
        {
            WindowColors.Load(normalized.WindowColors, normalized.WindowColorIndex);
            WindowOpacity = normalized.WindowOpacity;
            BackgroundImagePath = normalized.BackgroundImagePath;
            BackgroundImageAverageColor = normalized.BackgroundImageAverageColor;
            BackgroundImageAverageColorSourcePath = normalized.BackgroundImageAverageColorSourcePath;
            HideHeaderWhenInactive = normalized.HideHeaderWhenInactive;
            HideFooterWhenInactive = normalized.HideFooterWhenInactive;
        }
        finally
        {
            _isLoadingTheme = false;
        }

        if (raisePreview)
        {
            RaiseThemePreviewChanged();
        }
    }

    private void RaiseThemePreviewChanged()
    {
        ThemePreviewChanged?.Invoke(CreateTheme());
    }

    private static bool ThemeEquals(WidgetThemeConfig left, WidgetThemeConfig right)
    {
        WidgetConfigDefaults.NormalizeTheme(left);
        WidgetConfigDefaults.NormalizeTheme(right);

        return left.WindowColorIndex == right.WindowColorIndex
            && left.WindowOpacity == right.WindowOpacity
            && string.Equals(left.BackgroundImagePath, right.BackgroundImagePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.BackgroundImageAverageColor, right.BackgroundImageAverageColor, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.BackgroundImageAverageColorSourcePath, right.BackgroundImageAverageColorSourcePath, StringComparison.OrdinalIgnoreCase)
            && left.HideHeaderWhenInactive == right.HideHeaderWhenInactive
            && left.HideFooterWhenInactive == right.HideFooterWhenInactive
            && left.WindowColors.SequenceEqual(right.WindowColors, StringComparer.OrdinalIgnoreCase);
    }

    partial void OnHideHeaderWhenInactiveChanged(bool value)
    {
        OnPropertyChanged(nameof(HideHeaderWhenInactiveStateText));
        NotifyWindowDisplaySettingChanged();
    }

    partial void OnHideFooterWhenInactiveChanged(bool value)
    {
        OnPropertyChanged(nameof(HideFooterWhenInactiveStateText));
        NotifyWindowDisplaySettingChanged();
    }

    /// <summary>スイッチを触った瞬間にプレビューへ反映する。既存のテーマ設定と同じ経路。</summary>
    private void NotifyWindowDisplaySettingChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (!_isLoadingTheme)
        {
            RaiseThemePreviewChanged();
        }
    }

    partial void OnWindowOpacityChanged(double value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (!_isLoadingTheme)
        {
            RaiseThemePreviewChanged();
        }
    }

    partial void OnBackgroundImagePathChanged(string? value)
    {
        // 背景画像を選んでいる間は、ウィンドウの色の枠をどれも選んでいない表示にする。
        // 選んでいる色は保存し、画像が見つからないときの塗りに使う。
        WindowColors.IsSelectionHidden = !string.IsNullOrWhiteSpace(value);
        NotifyBackgroundImageChanged();
    }

    partial void OnBackgroundImageAverageColorChanged(string? value)
    {
        NotifyBackgroundImageChanged();
    }

    partial void OnBackgroundImageAverageColorSourcePathChanged(string? value)
    {
        NotifyBackgroundImageChanged();
    }

    private void NotifyBackgroundImageChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));

        if (!_isLoadingTheme)
        {
            RaiseThemePreviewChanged();
        }
    }
}
