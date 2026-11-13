// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// バフ・デバフ一覧の表示設定。行のゲージの色(左端と右端)と、満タンになる残り時間、
/// 同種バフの表示、付与者プレイヤー名の表示、行のバフ名の書式、優先表示リスト、一覧に出さないバフ。
/// 色の節はバフ・デバフグループと共有の部品(<see cref="BuffGaugeColorSettingsViewModel"/>)、
/// 優先表示リストはバフ・デバフカードのショートカットと共有の部品(<see cref="BuffEntryListSettingsViewModel"/>)。
/// 書式の欄の作りはバフ・デバフカードと同じ(差し込む項目に改行は無い。行は1行のため)。
/// </summary>
public sealed partial class BuffListWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private static readonly (string Key, string LabelResourceKey, string Placeholder)[] FormatFieldDefinitions =
    [
        ("BuffName", "Settings_BuffInfo_Field_BuffName", "{BuffName}"),
        ("TargetName", "Settings_BuffInfo_Field_Name", "{TargetName}"),
        ("CasterName", "Settings_BuffInfo_Field_Caster", "{CasterName}"),
        ("Level", "Settings_BuffInfo_Field_Level", "{Level}")
    ];

    private readonly ObservableCollection<BuffSettingRowViewModel> _hiddenBuffItems = [];
    private readonly ObservableCollection<MeterPlayerInfoFormatField> _availableFormatFields = [];
    private readonly WidgetKind _kind;
    private BuffListWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private int _gaugeLengthIndex;

    /// <summary>設定「同種バフの表示」の選択(<see cref="BuffListWidgetSettingsConfig.SameBuffGroupingIndex"/>)。</summary>
    [ObservableProperty]
    private int _sameBuffGroupingIndex = WidgetConfigDefaults.DefaultSameBuffGroupingIndex;

    /// <summary>設定「付与者プレイヤー名の表示」の選択(<see cref="BuffListWidgetSettingsConfig.CasterNameDisplayModeIndex"/>)。</summary>
    [ObservableProperty]
    private int _casterNameDisplayModeIndex = WidgetConfigDefaults.DefaultCasterNameDisplayModeIndex;

    /// <summary>設定「バフ名」の書式。</summary>
    [ObservableProperty]
    private string _buffInfoFormatString = WidgetConfigDefaults.DefaultBuffListBuffInfoFormatString;

    [ObservableProperty]
    private MeterPlayerInfoFormatField? _selectedFormatField;

    [ObservableProperty]
    private string _formatPreview = string.Empty;

    [ObservableProperty]
    private string _customizationTitle = string.Empty;

    public BuffListWidgetSettingsViewModel(WidgetKind kind, BuffListWidgetSettingsConfig? config)
    {
        _kind = kind;
        AvailableFormatFields = new ReadOnlyObservableCollection<MeterPlayerInfoFormatField>(_availableFormatFields);

        GaugeColors = new BuffGaugeColorSettingsViewModel(WidgetConfigDefaults.GetBuffListSide(kind));
        GaugeColors.Changed += NotifyChanged;
        PriorityBuffs = new BuffEntryListSettingsViewModel(
            WidgetConfigDefaults.GetBuffListPriorityDefaults(kind),
            "Settings_Section_PriorityBuffs_Title",
            kind == WidgetKind.DebuffList
                ? "Settings_Section_PriorityDebuffs_Description"
                : "Settings_Section_PriorityBuffs_Description",
            "Settings_ListReorderNote",
            NotifyChanged);
        HiddenBuffItems = new ReadOnlyObservableCollection<BuffSettingRowViewModel>(_hiddenBuffItems);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        RebuildFormatFields();
        RefreshLocalizedText();
        _lastSaved = WidgetConfigDefaults.CloneNormalizedBuffList(kind, config);
        Load(_lastSaved);
    }

    public event Action<BuffListWidgetSettingsConfig>? PreviewChanged;

    /// <summary>「バフ名」の書式に差し込める項目。</summary>
    public ReadOnlyObservableCollection<MeterPlayerInfoFormatField> AvailableFormatFields { get; }

    /// <summary>ゲージの色の節。バフ一覧なら「バフカラー」、デバフ一覧なら「デバフカラー」。</summary>
    public BuffGaugeColorSettingsViewModel GaugeColors { get; }

    /// <summary>「優先表示リスト」の節。並びは一覧の先頭からの並び(並べ替えはこの一覧だけ)。</summary>
    public BuffEntryListSettingsViewModel PriorityBuffs { get; }

    /// <summary>
    /// 非表示のバフの行。並びは非表示にした順。スイッチがオンなら非表示のまま、オフにすると保存前のプレビューで一覧に出て、
    /// 保存すると行が消える。
    /// </summary>
    public ReadOnlyObservableCollection<BuffSettingRowViewModel> HiddenBuffItems { get; }

    /// <summary>非表示のバフが1つでもあるか。無いときは枠ごと出さない。</summary>
    public bool HasHiddenBuffs => _hiddenBuffItems.Count > 0;

    /// <summary>節の見出し(「非表示リスト」)。今は種別で同じ文言だが、鍵はバフ一覧とデバフ一覧で分けてある。</summary>
    public string HiddenBuffSectionTitle => LocalizationManager.Instance.GetString(
        _kind == WidgetKind.DebuffList
            ? "Settings_Section_HiddenDebuffs_Title"
            : "Settings_Section_HiddenBuffs_Title");

    /// <summary>見出しの下の説明。右クリックで非表示にできることを書く。</summary>
    public string HiddenBuffSectionDescription => LocalizationManager.Instance.GetString(
        _kind == WidgetKind.DebuffList
            ? "Settings_Section_HiddenDebuffs_Description"
            : "Settings_Section_HiddenBuffs_Description");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
        GaugeColors.Changed -= NotifyChanged;
        GaugeColors.Dispose();
    }

    /// <summary>非表示リストは、スイッチがオンの行だけを残す。優先表示リストは足した行のうちスイッチがオフの行を落とす(<see cref="BuffEntryListSettingsViewModel.CreateEntries"/>)。</summary>
    public BuffListWidgetSettingsConfig CreateConfig()
    {
        var config = new BuffListWidgetSettingsConfig
        {
            GaugeLengthIndex = GaugeLengthIndex,
            SameBuffGroupingIndex = SameBuffGroupingIndex,
            CasterNameDisplayModeIndex = CasterNameDisplayModeIndex,
            BuffInfoFormatString = BuffInfoFormatString ?? string.Empty,
            GaugeColors = GaugeColors.CreateConfig(),
            PriorityBuffs = PriorityBuffs.CreateEntries(),
            HiddenBuffIds = [.. _hiddenBuffItems.Where(item => item.IsEnabled).Select(item => item.Key.BuffId)]
        };

        return WidgetConfigDefaults.CloneNormalizedBuffList(_kind, config);
    }

    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreateBuffListSettings(_kind));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(BuffListWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedBuffList(_kind, config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// 開いている間に、一覧の窓の右クリックでバフが非表示になった。その分は既に保存されているので、
    /// 行と保存済みの値の両方に足す(「保存」で消えず、未保存の印も付かない)。スイッチをオフにしていた行ならオンに戻す。
    /// プレビューは鳴らさない(ウィジェット側は既に非表示にしている)。
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
    /// 開いている間に、一覧の窓の右クリックで優先表示リストの行がオン/オフになった(「このバフを優先表示」「このバフの優先表示をやめる」)。
    /// 扱いはバフ・デバフカードのショートカット(<see cref="BuffCardWidgetSettingsViewModel.SetShortcutEnabledFromWidget"/>)と同じで、
    /// 保存済みの値と行の両方に同じ決まり(<see cref="WidgetConfigDefaults.SetBuffEntryEnabled"/>)で入れる。プレビューは鳴らさない。
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
            WidgetConfigDefaults.GetBuffListPriorityDefaults(_kind));
        PriorityBuffs.SetEntryEnabled(key, isEnabled);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Load(BuffListWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedBuffList(_kind, config);

        _isLoading = true;
        try
        {
            GaugeColors.Load(normalized.GaugeColors);
            GaugeLengthIndex = normalized.GaugeLengthIndex;
            SameBuffGroupingIndex = normalized.SameBuffGroupingIndex;
            CasterNameDisplayModeIndex = normalized.CasterNameDisplayModeIndex;
            BuffInfoFormatString = normalized.BuffInfoFormatString ?? string.Empty;
            PriorityBuffs.Load(normalized.PriorityBuffs);

            _hiddenBuffItems.Clear();
            foreach (var baseId in normalized.HiddenBuffIds)
            {
                _hiddenBuffItems.Add(CreateHiddenBuffItem(baseId));
            }

            OnHiddenBuffItemsChanged();
        }
        finally
        {
            _isLoading = false;
        }

        RefreshFormatPreview();
    }

    private void RebuildFormatFields()
    {
        var selectedKey = SelectedFormatField?.Key;

        _availableFormatFields.Clear();
        foreach (var definition in FormatFieldDefinitions)
        {
            _availableFormatFields.Add(new MeterPlayerInfoFormatField(
                definition.Key,
                LocalizationManager.Instance.GetString(definition.LabelResourceKey),
                definition.Placeholder));
        }

        SelectedFormatField = _availableFormatFields
            .FirstOrDefault(field => string.Equals(field.Key, selectedKey, StringComparison.Ordinal))
            ?? _availableFormatFields.FirstOrDefault();
    }

    /// <summary>項目名「バフ名」はバフ・デバフカードと同じ文言。</summary>
    private void RefreshLocalizedText()
    {
        CustomizationTitle = LocalizationManager.Instance.GetString("Settings_BuffInfo_Customization");
    }

    /// <summary>見本の付与者プレイヤー名は、「付与者プレイヤー名の表示」が非表示なら入れない(見本の付与者は対象者と別人)。</summary>
    private void RefreshFormatPreview()
    {
        FormatPreview = BuffInfoFormatFormatter
            .FormatPreview(
                BuffInfoFormatString,
                showsCaster: CasterNameDisplayModeIndex != WidgetConfigDefaults.CasterNameDisplayHideIndex)
            .Replace(Environment.NewLine, " ", StringComparison.Ordinal);
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

    private void RaisePreviewChanged()
    {
        PreviewChanged?.Invoke(CreateConfig());
    }

    private static bool SettingsEqual(BuffListWidgetSettingsConfig left, BuffListWidgetSettingsConfig right)
    {
        if (left.GaugeLengthIndex != right.GaugeLengthIndex
            || left.SameBuffGroupingIndex != right.SameBuffGroupingIndex
            || left.CasterNameDisplayModeIndex != right.CasterNameDisplayModeIndex
            || !string.Equals(left.BuffInfoFormatString, right.BuffInfoFormatString, StringComparison.Ordinal)
            || !BuffEntryListSettingsViewModel.EntriesEqual(left.PriorityBuffs, right.PriorityBuffs)
            || !left.HiddenBuffIds.SequenceEqual(right.HiddenBuffIds))
        {
            return false;
        }

        return BuffGaugeColorSettingsViewModel.ConfigEquals(left.GaugeColors, right.GaugeColors);
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var item in _hiddenBuffItems)
        {
            item.RefreshMetadata();
        }

        PriorityBuffs.RefreshLocalizedText();
        OnPropertyChanged(nameof(HiddenBuffSectionTitle));
        OnPropertyChanged(nameof(HiddenBuffSectionDescription));
        RebuildFormatFields();
        RefreshLocalizedText();
        RefreshFormatPreview();
    }

    partial void OnGaugeLengthIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnSameBuffGroupingIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnCasterNameDisplayModeIndexChanged(int value)
    {
        RefreshFormatPreview();
        NotifyChanged();
    }

    partial void OnBuffInfoFormatStringChanged(string value)
    {
        RefreshFormatPreview();
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
}
