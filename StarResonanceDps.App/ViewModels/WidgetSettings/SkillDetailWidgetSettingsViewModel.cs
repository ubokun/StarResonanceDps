// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// スキル詳細の表示設定。いまは行名の書式だけで、作りはメーターのプレイヤー名(<see cref="MeterWidgetSettingsViewModel"/>)と同じ。
/// </summary>
public sealed partial class SkillDetailWidgetSettingsViewModel : ObservableObject, IDisposable
{
    /// <summary>項目名は列の見出しと同じ文言を使う(別に文言を持たない)。</summary>
    private static readonly (string Key, string LabelResourceKey, string Placeholder)[] SkillInfoFormatFieldDefinitions =
    [
        ("SkillName", "Metric_SkillName", "{SkillName}"),
        ("Element", "Metric_Element", "{Element}"),
        ("Type", "Metric_DamageMode", "{Type}"),
        ("Hits", "Metric_HitCount", "{Hits}"),
        ("CritRate", "Metric_CritRate", "{CritRate}")
    ];

    private readonly ObservableCollection<MeterPlayerInfoFormatField> _availableSkillInfoFormatFields = [];
    private SkillDetailWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private string _skillInfoFormatString = WidgetConfigDefaults.DefaultSkillInfoFormatString;

    [ObservableProperty]
    private MeterPlayerInfoFormatField? _selectedSkillInfoFormatField;

    [ObservableProperty]
    private string _formatPreview = string.Empty;

    public SkillDetailWidgetSettingsViewModel(SkillDetailWidgetSettingsConfig? config)
    {
        AvailableSkillInfoFormatFields = new ReadOnlyObservableCollection<MeterPlayerInfoFormatField>(_availableSkillInfoFormatFields);
        RebuildSkillInfoFormatFields();

        Load(WidgetConfigDefaults.CloneNormalizedSkillDetail(config));
        _lastSaved = CreateConfig();

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    public event Action<SkillDetailWidgetSettingsConfig>? PreviewChanged;

    public ReadOnlyObservableCollection<MeterPlayerInfoFormatField> AvailableSkillInfoFormatFields { get; }

    public string SectionTitle => LocalizationManager.Instance.GetString("Settings_Section_Display_Title");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    public SkillDetailWidgetSettingsConfig CreateConfig()
    {
        return new SkillDetailWidgetSettingsConfig
        {
            SkillInfoFormatString = SkillInfoFormatString ?? string.Empty
        };
    }

    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreateSkillDetailSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(SkillDetailWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedSkillDetail(config);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>選んでいる項目のプレースホルダを、書式の文字列の末尾に足す。挿入位置は画面側が決める。</summary>
    public string? GetSelectedFieldPlaceholder()
    {
        return SelectedSkillInfoFormatField?.Placeholder;
    }

    partial void OnSkillInfoFormatStringChanged(string value)
    {
        RefreshFormatPreview();
        NotifyChanged();
    }

    private void Load(SkillDetailWidgetSettingsConfig config)
    {
        _isLoading = true;
        try
        {
            SkillInfoFormatString = config.SkillInfoFormatString ?? WidgetConfigDefaults.DefaultSkillInfoFormatString;
        }
        finally
        {
            _isLoading = false;
        }

        RefreshFormatPreview();
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

    private void RefreshFormatPreview()
    {
        FormatPreview = SkillInfoFormatFormatter.FormatPreview(SkillInfoFormatString);
    }

    private void RebuildSkillInfoFormatFields()
    {
        var selectedKey = SelectedSkillInfoFormatField?.Key;

        _availableSkillInfoFormatFields.Clear();
        foreach (var definition in SkillInfoFormatFieldDefinitions)
        {
            _availableSkillInfoFormatFields.Add(new MeterPlayerInfoFormatField(
                definition.Key,
                LocalizationManager.Instance.GetString(definition.LabelResourceKey),
                definition.Placeholder));
        }

        SelectedSkillInfoFormatField = _availableSkillInfoFormatFields
            .FirstOrDefault(field => string.Equals(field.Key, selectedKey, StringComparison.Ordinal))
            ?? _availableSkillInfoFormatFields.FirstOrDefault();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        RebuildSkillInfoFormatFields();
        RefreshFormatPreview();
        OnPropertyChanged(nameof(SectionTitle));
    }

    private static bool SettingsEqual(SkillDetailWidgetSettingsConfig left, SkillDetailWidgetSettingsConfig right)
    {
        return string.Equals(left.SkillInfoFormatString, right.SkillInfoFormatString, StringComparison.Ordinal);
    }
}
