// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// 装備詳細の表示設定。行の書式(作りはスキル詳細の行名 <see cref="SkillDetailWidgetSettingsViewModel"/> と同じ)と、
/// 品質ごとのテキストカラー(作りはタイムライン <see cref="TimelineWidgetSettingsViewModel"/> と同じ)。
/// </summary>
public sealed partial class EquipmentWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private static readonly (string Key, string LabelResourceKey, string Placeholder)[] EquipmentInfoFormatFieldDefinitions =
    [
        ("EquipName", "Settings_EquipmentInfo_Field_EquipName", "{EquipName}"),
        ("EquipLevel", "Settings_EquipmentInfo_Field_EquipLevel", "{EquipLevel}"),
        ("MainStat", "Settings_EquipmentInfo_Field_MainStat", "{MainStat}")
    ];

    private readonly ObservableCollection<MeterPlayerInfoFormatField> _availableEquipmentInfoFormatFields = [];
    private readonly Dictionary<string, EquipmentTextColorItemViewModel> _textColorItemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private EquipmentWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private string _infoFormat = WidgetConfigDefaults.DefaultEquipmentInfoFormatString;

    [ObservableProperty]
    private MeterPlayerInfoFormatField? _selectedEquipmentInfoFormatField;

    [ObservableProperty]
    private string _formatPreview = string.Empty;

    public EquipmentWidgetSettingsViewModel(EquipmentWidgetSettingsConfig? config)
    {
        AvailableEquipmentInfoFormatFields = new ReadOnlyObservableCollection<MeterPlayerInfoFormatField>(_availableEquipmentInfoFormatFields);
        RebuildEquipmentInfoFormatFields();

        var textItems = new ObservableCollection<EquipmentTextColorItemViewModel>();
        var textColorKeys = WidgetConfigDefaults.EquipmentTextColorKeys;
        for (var index = 0; index < textColorKeys.Length; index++)
        {
            var key = textColorKeys[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultEquipmentTextColors(key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += TextColors_PaletteChanged;

            var item = new EquipmentTextColorItemViewModel(key, colors, index == textColorKeys.Length - 1);
            _textColorItemsByKey.Add(key, item);
            textItems.Add(item);
        }

        TextColorItems = new ReadOnlyObservableCollection<EquipmentTextColorItemViewModel>(textItems);

        Load(WidgetConfigDefaults.CloneNormalizedEquipment(config));
        _lastSaved = CreateConfig();

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    public event Action<EquipmentWidgetSettingsConfig>? PreviewChanged;

    public ReadOnlyObservableCollection<MeterPlayerInfoFormatField> AvailableEquipmentInfoFormatFields { get; }

    /// <summary>テキストカラーの行。品質ごとに色見本(最大5枠)と、選んでいる枠を持つ。</summary>
    public ReadOnlyObservableCollection<EquipmentTextColorItemViewModel> TextColorItems { get; }

    public string SectionTitle => LocalizationManager.Instance.GetString("Settings_Section_Display_Title");

    public string TextColorSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_EquipmentTextColors_Title");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in TextColorItems)
        {
            item.Colors.PaletteChanged -= TextColors_PaletteChanged;
        }
    }

    public EquipmentWidgetSettingsConfig CreateConfig()
    {
        var config = new EquipmentWidgetSettingsConfig
        {
            InfoFormat = InfoFormat ?? string.Empty,
            TextColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            TextColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var item in TextColorItems)
        {
            config.TextColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.TextColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        return config;
    }

    /// <summary>色選択ウィンドウの初期値。宛先は品質の番号。</summary>
    public Color GetSelectedTextColor(string key)
    {
        return _textColorItemsByKey[key].Colors.SelectedColor;
    }

    public void ApplyTextColor(string key, Color color)
    {
        _textColorItemsByKey[key].Colors.AddOrSelect(color);
    }

    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreateEquipmentSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(EquipmentWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedEquipment(config);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>選んでいる項目のプレースホルダ。挿入位置は画面側が決める。</summary>
    public string? GetSelectedFieldPlaceholder()
    {
        return SelectedEquipmentInfoFormatField?.Placeholder;
    }

    partial void OnInfoFormatChanged(string value)
    {
        RefreshFormatPreview();
        NotifyChanged();
    }

    private void Load(EquipmentWidgetSettingsConfig config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedEquipment(config);

        _isLoading = true;
        try
        {
            InfoFormat = normalized.InfoFormat ?? WidgetConfigDefaults.DefaultEquipmentInfoFormatString;

            foreach (var item in TextColorItems)
            {
                item.Colors.Load(normalized.TextColorPalettes[item.Key], normalized.TextColorIndexes[item.Key]);
            }
        }
        finally
        {
            _isLoading = false;
        }

        RefreshFormatPreview();
    }

    private void TextColors_PaletteChanged(object? sender, EventArgs e)
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

    private void RefreshFormatPreview()
    {
        FormatPreview = EquipmentInfoFormatFormatter.FormatPreview(InfoFormat);
    }

    private void RebuildEquipmentInfoFormatFields()
    {
        var selectedKey = SelectedEquipmentInfoFormatField?.Key;

        _availableEquipmentInfoFormatFields.Clear();
        foreach (var definition in EquipmentInfoFormatFieldDefinitions)
        {
            _availableEquipmentInfoFormatFields.Add(new MeterPlayerInfoFormatField(
                definition.Key,
                LocalizationManager.Instance.GetString(definition.LabelResourceKey),
                definition.Placeholder));
        }

        SelectedEquipmentInfoFormatField = _availableEquipmentInfoFormatFields
            .FirstOrDefault(field => string.Equals(field.Key, selectedKey, StringComparison.Ordinal))
            ?? _availableEquipmentInfoFormatFields.FirstOrDefault();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        RebuildEquipmentInfoFormatFields();
        RefreshFormatPreview();

        foreach (var item in TextColorItems)
        {
            item.RefreshDisplayName();
        }

        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(TextColorSectionTitle));
    }

    private static bool SettingsEqual(EquipmentWidgetSettingsConfig left, EquipmentWidgetSettingsConfig right)
    {
        if (!string.Equals(left.InfoFormat, right.InfoFormat, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var key in WidgetConfigDefaults.EquipmentTextColorKeys)
        {
            if (!left.TextColorIndexes.TryGetValue(key, out var leftIndex)
                || !right.TextColorIndexes.TryGetValue(key, out var rightIndex)
                || leftIndex != rightIndex
                || !left.TextColorPalettes.TryGetValue(key, out var leftPalette)
                || !right.TextColorPalettes.TryGetValue(key, out var rightPalette)
                || !leftPalette.SequenceEqual(rightPalette, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// テキストカラーの1行(アイテムの品質1つ)。行の作りはタイムラインのテキストカラー
/// (<see cref="TimelineTextColorItemViewModel"/>)と同じ。
/// </summary>
public sealed class EquipmentTextColorItemViewModel : ObservableObject
{
    public EquipmentTextColorItemViewModel(string key, ColorPaletteViewModel colors, bool isLast)
    {
        Key = key;
        Colors = colors;
        IsLast = isLast;
    }

    /// <summary>品質の番号。</summary>
    public string Key { get; }

    public ColorPaletteViewModel Colors { get; }

    public bool IsLast { get; }

    /// <summary>「レアリティ＋番号」。番号は品質の番号に 1 を足した数(品質 0〜5 を 1〜6 と出す)。</summary>
    public string DisplayName => LocalizationManager.Instance.Format("PlayerEquipment_Quality", int.Parse(Key, System.Globalization.CultureInfo.InvariantCulture) + 1);

    public void RefreshDisplayName()
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
