// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// エンティティ情報の設定。表示設定の「エンティティ名」(書式)・HP数値・戦闘状態・狙われるプレイヤー・ヘイト値はエンティティリスト
/// (<see cref="MeterWidgetSettingsViewModel"/>)と同じ作り、「バフ名」(書式)・同種バフの表示・付与者プレイヤー名の表示と優先表示リスト・非表示リストは
/// バフ・デバフ一覧(<see cref="BuffListWidgetSettingsViewModel"/>)と同じ作り、アイコンカラーは蘇生リスト
/// (<see cref="ReviveListWidgetSettingsViewModel"/>)と同じ作り(行はエンティティリストと同じ)。
/// </summary>
public sealed partial class EntityInfoWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private static readonly (string Key, string LabelResourceKey, string Placeholder)[] EntityInfoFormatFieldDefinitions =
    [
        ("EntityName", "Settings_EntityInfo_Field_Name", "{EntityName}"),
        ("Level", "Settings_EntityInfo_Field_Level", "{Level}")
    ];

    private static readonly (string Key, string LabelResourceKey, string Placeholder)[] BuffInfoFormatFieldDefinitions =
    [
        ("BuffName", "Settings_BuffInfo_Field_BuffName", "{BuffName}"),
        ("TargetName", "Settings_BuffInfo_Field_Name", "{TargetName}"),
        ("CasterName", "Settings_BuffInfo_Field_Caster", "{CasterName}"),
        ("Level", "Settings_BuffInfo_Field_Level", "{Level}")
    ];

    private readonly ObservableCollection<MeterPlayerInfoFormatField> _availableEntityInfoFormatFields = [];
    private readonly ObservableCollection<MeterPlayerInfoFormatField> _availableBuffInfoFormatFields = [];
    private readonly ObservableCollection<BuffSettingRowViewModel> _hiddenBuffItems = [];
    private readonly Dictionary<string, MeterClassColorItemViewModel> _classColorItemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private EntityInfoWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private string _entityInfoFormatString = WidgetConfigDefaults.DefaultEntityInfoFormatString;

    [ObservableProperty]
    private MeterPlayerInfoFormatField? _selectedEntityInfoFormatField;

    [ObservableProperty]
    private string _entityInfoFormatPreview = string.Empty;

    [ObservableProperty]
    private string _buffInfoFormatString = WidgetConfigDefaults.DefaultBuffListBuffInfoFormatString;

    [ObservableProperty]
    private MeterPlayerInfoFormatField? _selectedBuffInfoFormatField;

    [ObservableProperty]
    private string _buffInfoFormatPreview = string.Empty;

    [ObservableProperty]
    private int _healthValueDisplayModeIndex = WidgetConfigDefaults.DefaultHealthValueDisplayModeIndex;

    [ObservableProperty]
    private int _combatStateDisplayModeIndex = WidgetConfigDefaults.DefaultCombatStateDisplayModeIndex;

    [ObservableProperty]
    private int _targetedPlayerDisplayModeIndex = WidgetConfigDefaults.DefaultTargetedPlayerDisplayModeIndex;

    /// <summary>設定「ヘイト値」の選択(<see cref="EntityInfoWidgetSettingsConfig.HateRankDisplayModeIndex"/>)。</summary>
    [ObservableProperty]
    private int _hateRankDisplayModeIndex = WidgetConfigDefaults.DefaultEntityInfoHateRankDisplayModeIndex;

    /// <summary>設定「同種バフの表示」の選択(<see cref="EntityInfoWidgetSettingsConfig.SameBuffGroupingIndex"/>)。</summary>
    [ObservableProperty]
    private int _sameBuffGroupingIndex = WidgetConfigDefaults.DefaultEntityInfoSameBuffGroupingIndex;

    /// <summary>設定「付与者プレイヤー名の表示」の選択(<see cref="EntityInfoWidgetSettingsConfig.CasterNameDisplayModeIndex"/>)。</summary>
    [ObservableProperty]
    private int _casterNameDisplayModeIndex = WidgetConfigDefaults.DefaultCasterNameDisplayModeIndex;

    public EntityInfoWidgetSettingsViewModel(EntityInfoWidgetSettingsConfig? config)
    {
        AvailableEntityInfoFormatFields = new ReadOnlyObservableCollection<MeterPlayerInfoFormatField>(_availableEntityInfoFormatFields);
        AvailableBuffInfoFormatFields = new ReadOnlyObservableCollection<MeterPlayerInfoFormatField>(_availableBuffInfoFormatFields);
        PriorityBuffs = new BuffEntryListSettingsViewModel(
            WidgetConfigDefaults.DefaultEntityInfoPriorityBuffEntries,
            "Settings_Section_PriorityBuffs_Title",
            "Settings_Section_PriorityBuffDebuffs_Description",
            "Settings_ListReorderNote",
            NotifyChanged);
        HiddenBuffItems = new ReadOnlyObservableCollection<BuffSettingRowViewModel>(_hiddenBuffItems);
        RebuildFormatFields();

        var classItems = new ObservableCollection<MeterClassColorItemViewModel>();
        var classColorKeys = WidgetConfigDefaults.GetClassColorKeys(WidgetKind.EntityInfo);
        for (var index = 0; index < classColorKeys.Count; index++)
        {
            var key = classColorKeys[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultClassColors(WidgetKind.EntityInfo, key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += Colors_PaletteChanged;

            var item = new MeterClassColorItemViewModel(key, colors, index == classColorKeys.Count - 1);
            _classColorItemsByKey.Add(key, item);
            classItems.Add(item);
        }

        ClassColorItems = new ReadOnlyObservableCollection<MeterClassColorItemViewModel>(classItems);

        _lastSaved = WidgetConfigDefaults.CloneNormalizedEntityInfo(config);
        Load(_lastSaved);

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    public event Action<EntityInfoWidgetSettingsConfig>? PreviewChanged;

    /// <summary>「エンティティ名」の書式に差し込める項目。</summary>
    public ReadOnlyObservableCollection<MeterPlayerInfoFormatField> AvailableEntityInfoFormatFields { get; }

    /// <summary>「バフ名」の書式に差し込める項目。</summary>
    public ReadOnlyObservableCollection<MeterPlayerInfoFormatField> AvailableBuffInfoFormatFields { get; }

    /// <summary>「優先表示リスト」の節。並びはバフとデバフの枠の1枠目からの並び(並べ替えはこの一覧だけ)。</summary>
    public BuffEntryListSettingsViewModel PriorityBuffs { get; }

    /// <summary>
    /// 非表示のバフの行。並びは非表示にした順。スイッチがオンなら非表示のまま、オフにすると保存前のプレビューで枠に出て、
    /// 保存すると行が消える(バフ・デバフ一覧と同じ)。
    /// </summary>
    public ReadOnlyObservableCollection<BuffSettingRowViewModel> HiddenBuffItems { get; }

    /// <summary>非表示のバフが1つでもあるか。無いときは枠ごと出さない。</summary>
    public bool HasHiddenBuffs => _hiddenBuffItems.Count > 0;

    public string HiddenBuffSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_HiddenBuffs_Title");

    /// <summary>見出しの下の説明。バフとデバフの枠を右クリックして非表示にできることを書く。</summary>
    public string HiddenBuffSectionDescription => LocalizationManager.Instance.GetString("Settings_Section_HiddenBuffDebuffs_Description");

    /// <summary>アイコンカラーの行。行ごとに色見本(最大5枠)と、選んでいる枠を持つ。</summary>
    public ReadOnlyObservableCollection<MeterClassColorItemViewModel> ClassColorItems { get; }

    public string ClassColorSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_IconColors_Title");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in ClassColorItems)
        {
            item.Colors.PaletteChanged -= Colors_PaletteChanged;
        }
    }

    /// <summary>非表示リストは、スイッチがオンの行だけを残す。優先表示リストは足した行のうちスイッチがオフの行を落とす(<see cref="BuffEntryListSettingsViewModel.CreateEntries"/>)。</summary>
    public EntityInfoWidgetSettingsConfig CreateConfig()
    {
        var config = new EntityInfoWidgetSettingsConfig
        {
            EntityInfoFormatString = EntityInfoFormatString ?? string.Empty,
            BuffInfoFormatString = BuffInfoFormatString ?? string.Empty,
            HealthValueDisplayModeIndex = HealthValueDisplayModeIndex,
            CombatStateDisplayModeIndex = CombatStateDisplayModeIndex,
            TargetedPlayerDisplayModeIndex = TargetedPlayerDisplayModeIndex,
            HateRankDisplayModeIndex = HateRankDisplayModeIndex,
            SameBuffGroupingIndex = SameBuffGroupingIndex,
            CasterNameDisplayModeIndex = CasterNameDisplayModeIndex,
            PriorityBuffs = PriorityBuffs.CreateEntries(),
            HiddenBuffIds = [.. _hiddenBuffItems.Where(item => item.IsEnabled).Select(item => item.Key.BuffId)],
            ClassColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var item in ClassColorItems)
        {
            config.ClassColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.ClassColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        return WidgetConfigDefaults.CloneNormalizedEntityInfo(config);
    }

    public Color GetSelectedClassColor(string key)
    {
        return _classColorItemsByKey[key].Colors.SelectedColor;
    }

    public void ApplyClassColor(string key, Color color)
    {
        _classColorItemsByKey[key].Colors.AddOrSelect(color);
    }

    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreateEntityInfoSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(EntityInfoWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedEntityInfo(config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// 開いている間に、窓の枠の右クリックでバフが非表示になった。その分は既に保存されているので、
    /// 行と保存済みの値の両方に足す(バフ・デバフ一覧の <see cref="BuffListWidgetSettingsViewModel.AddHiddenBuffFromWidget"/> と同じ)。
    /// </summary>
    public void AddHiddenBuffFromWidget(int baseId)
    {
        if (baseId <= 0)
        {
            return;
        }

        if (!_lastSaved.HiddenBuffIds.Contains(baseId))
        {
            _lastSaved.HiddenBuffIds.Add(baseId);
        }

        var existing = _hiddenBuffItems.FirstOrDefault(item => item.Key.BuffId == baseId);
        if (existing is not null)
        {
            existing.LoadEnabled(true);
        }
        else
        {
            _hiddenBuffItems.Add(CreateHiddenBuffItem(baseId));
            OnHiddenBuffItemsChanged();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// 開いている間に、窓の枠の右クリックで優先表示リストの行がオン/オフになった。
    /// 扱いはバフ・デバフ一覧の <see cref="BuffListWidgetSettingsViewModel.SetPriorityBuffEnabledFromWidget"/> と同じ。
    /// </summary>
    public void SetPriorityBuffEnabledFromWidget(BuffEntryKey key, bool isEnabled)
    {
        if (!key.IsValid)
        {
            return;
        }

        WidgetConfigDefaults.SetBuffEntryEnabled(
            _lastSaved.PriorityBuffs ??= [],
            key,
            isEnabled,
            WidgetConfigDefaults.DefaultEntityInfoPriorityBuffEntries);
        PriorityBuffs.SetEntryEnabled(key, isEnabled);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Load(EntityInfoWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedEntityInfo(config);

        _isLoading = true;
        try
        {
            EntityInfoFormatString = normalized.EntityInfoFormatString ?? WidgetConfigDefaults.DefaultEntityInfoFormatString;
            BuffInfoFormatString = normalized.BuffInfoFormatString ?? WidgetConfigDefaults.DefaultBuffListBuffInfoFormatString;
            HealthValueDisplayModeIndex = normalized.HealthValueDisplayModeIndex;
            CombatStateDisplayModeIndex = normalized.CombatStateDisplayModeIndex;
            TargetedPlayerDisplayModeIndex = normalized.TargetedPlayerDisplayModeIndex;
            HateRankDisplayModeIndex = normalized.HateRankDisplayModeIndex;
            SameBuffGroupingIndex = normalized.SameBuffGroupingIndex;
            CasterNameDisplayModeIndex = normalized.CasterNameDisplayModeIndex;
            PriorityBuffs.Load(normalized.PriorityBuffs);

            _hiddenBuffItems.Clear();
            foreach (var baseId in normalized.HiddenBuffIds)
            {
                _hiddenBuffItems.Add(CreateHiddenBuffItem(baseId));
            }

            OnHiddenBuffItemsChanged();

            foreach (var item in ClassColorItems)
            {
                item.Colors.Load(normalized.ClassColorPalettes[item.Key], normalized.ClassColorIndexes[item.Key]);
            }
        }
        finally
        {
            _isLoading = false;
        }

        RefreshEntityInfoFormatPreview();
        RefreshBuffInfoFormatPreview();
    }

    partial void OnEntityInfoFormatStringChanged(string value)
    {
        // 書式は1行。貼り付けた改行は空白にする(エンティティリストと同じ)。
        var normalized = (value ?? string.Empty)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            EntityInfoFormatString = normalized;
            return;
        }

        RefreshEntityInfoFormatPreview();
        NotifyChanged();
    }

    partial void OnBuffInfoFormatStringChanged(string value)
    {
        RefreshBuffInfoFormatPreview();
        NotifyChanged();
    }

    partial void OnHealthValueDisplayModeIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnCombatStateDisplayModeIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnTargetedPlayerDisplayModeIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnHateRankDisplayModeIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnSameBuffGroupingIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnCasterNameDisplayModeIndexChanged(int value)
    {
        RefreshBuffInfoFormatPreview();
        NotifyChanged();
    }

    private void Colors_PaletteChanged(object? sender, EventArgs e)
    {
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        if (_isLoading)
        {
            return;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    private void RaisePreviewChanged()
    {
        PreviewChanged?.Invoke(CreateConfig());
    }

    private void RefreshEntityInfoFormatPreview()
    {
        EntityInfoFormatPreview = EntityInfoFormatFormatter.FormatPreview(EntityInfoFormatString);
    }

    /// <summary>見本の付与者プレイヤー名は、「付与者プレイヤー名の表示」が非表示なら入れない(バフ・デバフ一覧と同じ)。</summary>
    private void RefreshBuffInfoFormatPreview()
    {
        BuffInfoFormatPreview = BuffInfoFormatFormatter
            .FormatPreview(
                BuffInfoFormatString,
                showsCaster: CasterNameDisplayModeIndex != WidgetConfigDefaults.CasterNameDisplayHideIndex)
            .Replace(Environment.NewLine, " ", StringComparison.Ordinal);
    }

    private void RebuildFormatFields()
    {
        RebuildFormatFields(_availableEntityInfoFormatFields, EntityInfoFormatFieldDefinitions, SelectedEntityInfoFormatField?.Key, out var selectedEntityInfoField);
        SelectedEntityInfoFormatField = selectedEntityInfoField;
        RebuildFormatFields(_availableBuffInfoFormatFields, BuffInfoFormatFieldDefinitions, SelectedBuffInfoFormatField?.Key, out var selectedBuffInfoField);
        SelectedBuffInfoFormatField = selectedBuffInfoField;
    }

    private static void RebuildFormatFields(
        ObservableCollection<MeterPlayerInfoFormatField> fields,
        IReadOnlyList<(string Key, string LabelResourceKey, string Placeholder)> definitions,
        string? selectedKey,
        out MeterPlayerInfoFormatField? selectedField)
    {
        fields.Clear();
        foreach (var definition in definitions)
        {
            fields.Add(new MeterPlayerInfoFormatField(
                definition.Key,
                LocalizationManager.Instance.GetString(definition.LabelResourceKey),
                definition.Placeholder));
        }

        selectedField = fields.FirstOrDefault(field => string.Equals(field.Key, selectedKey, StringComparison.Ordinal))
            ?? fields.FirstOrDefault();
    }

    private BuffSettingRowViewModel CreateHiddenBuffItem(int baseId)
    {
        return new BuffSettingRowViewModel(BuffEntryKey.ForBuff(baseId), isDefault: false, isEnabled: true, NotifyChanged);
    }

    /// <summary>行が増減したら、最終行の印と枠の有無を当て直す。</summary>
    private void OnHiddenBuffItemsChanged()
    {
        for (var index = 0; index < _hiddenBuffItems.Count; index++)
        {
            _hiddenBuffItems[index].IsLast = index == _hiddenBuffItems.Count - 1;
        }

        OnPropertyChanged(nameof(HasHiddenBuffs));
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        RebuildFormatFields();
        RefreshEntityInfoFormatPreview();
        RefreshBuffInfoFormatPreview();

        foreach (var item in _hiddenBuffItems)
        {
            item.RefreshMetadata();
        }

        PriorityBuffs.RefreshLocalizedText();

        foreach (var item in ClassColorItems)
        {
            item.RefreshDisplayName();
        }

        OnPropertyChanged(nameof(HiddenBuffSectionTitle));
        OnPropertyChanged(nameof(HiddenBuffSectionDescription));
        OnPropertyChanged(nameof(ClassColorSectionTitle));
    }

    private static bool SettingsEqual(EntityInfoWidgetSettingsConfig left, EntityInfoWidgetSettingsConfig right)
    {
        if (!string.Equals(left.EntityInfoFormatString, right.EntityInfoFormatString, StringComparison.Ordinal)
            || !string.Equals(left.BuffInfoFormatString, right.BuffInfoFormatString, StringComparison.Ordinal)
            || left.HealthValueDisplayModeIndex != right.HealthValueDisplayModeIndex
            || left.CombatStateDisplayModeIndex != right.CombatStateDisplayModeIndex
            || left.TargetedPlayerDisplayModeIndex != right.TargetedPlayerDisplayModeIndex
            || left.HateRankDisplayModeIndex != right.HateRankDisplayModeIndex
            || left.SameBuffGroupingIndex != right.SameBuffGroupingIndex
            || left.CasterNameDisplayModeIndex != right.CasterNameDisplayModeIndex
            || !BuffEntryListSettingsViewModel.EntriesEqual(left.PriorityBuffs, right.PriorityBuffs)
            || !left.HiddenBuffIds.SequenceEqual(right.HiddenBuffIds))
        {
            return false;
        }

        foreach (var key in WidgetConfigDefaults.GetClassColorKeys(WidgetKind.EntityInfo))
        {
            if (left.ClassColorIndexes[key] != right.ClassColorIndexes[key]
                || !left.ClassColorPalettes[key].SequenceEqual(right.ClassColorPalettes[key], StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
