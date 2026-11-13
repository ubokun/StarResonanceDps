// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// 蘇生リストの設定。表示設定の「プレイヤー名」(書式)と「自分の強調表示」はプレイヤーリスト(<see cref="MeterWidgetSettingsViewModel"/>)と同じ作り、
/// 「ゲージの長さ」とデバフカラーはデバフ一覧(<see cref="BuffListWidgetSettingsViewModel"/>)と同じ作り(色の節は共有の部品 <see cref="BuffGaugeColorSettingsViewModel"/>)、
/// アイコンカラーはプレイヤー情報(<see cref="PlayerInfoWidgetSettingsViewModel"/>)と同じ作り、
/// テキストカラーは装備詳細(<see cref="EquipmentWidgetSettingsViewModel"/>)と同じ作り(見本は「Aa」)。
/// </summary>
public sealed partial class ReviveListWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private readonly ObservableCollection<MeterPlayerInfoFormatField> _availablePlayerInfoFormatFields = [];
    private readonly Dictionary<string, MeterClassColorItemViewModel> _classColorItemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ReviveListTextColorItemViewModel> _textColorItemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private ReviveListWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private string _playerInfoFormatString = WidgetConfigDefaults.DefaultReviveListPlayerInfoFormatString;

    [ObservableProperty]
    private MeterPlayerInfoFormatField? _selectedPlayerInfoFormatField;

    [ObservableProperty]
    private string _formatPreview = string.Empty;

    [ObservableProperty]
    private int _selfDisplayModeIndex = WidgetConfigDefaults.DefaultSelfDisplayModeIndex;

    /// <summary>ゲージの長さ。添字は <see cref="WidgetConfigDefaults.BuffGaugeLengthSeconds"/> と同じ。</summary>
    [ObservableProperty]
    private int _gaugeLengthIndex = WidgetConfigDefaults.DefaultReviveListGaugeLengthIndex;

    public ReviveListWidgetSettingsViewModel(ReviveListWidgetSettingsConfig? config)
    {
        AvailablePlayerInfoFormatFields = new ReadOnlyObservableCollection<MeterPlayerInfoFormatField>(_availablePlayerInfoFormatFields);
        RebuildPlayerInfoFormatFields();

        GaugeColors = new BuffGaugeColorSettingsViewModel(PlayerBuffListKind.Debuff);
        GaugeColors.Changed += NotifyChanged;

        var classItems = new ObservableCollection<MeterClassColorItemViewModel>();
        var classColorKeys = WidgetConfigDefaults.GetClassColorKeys(WidgetKind.ReviveList);
        for (var index = 0; index < classColorKeys.Count; index++)
        {
            var key = classColorKeys[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultClassColors(WidgetKind.ReviveList, key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += Colors_PaletteChanged;

            var item = new MeterClassColorItemViewModel(key, colors, index == classColorKeys.Count - 1);
            _classColorItemsByKey.Add(key, item);
            classItems.Add(item);
        }

        ClassColorItems = new ReadOnlyObservableCollection<MeterClassColorItemViewModel>(classItems);

        var textItems = new ObservableCollection<ReviveListTextColorItemViewModel>();
        var textColorKeys = WidgetConfigDefaults.ReviveListTextColorKeys;
        for (var index = 0; index < textColorKeys.Length; index++)
        {
            var key = textColorKeys[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultReviveListTextColors(key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += Colors_PaletteChanged;

            var item = new ReviveListTextColorItemViewModel(key, colors, index == textColorKeys.Length - 1);
            _textColorItemsByKey.Add(key, item);
            textItems.Add(item);
        }

        TextColorItems = new ReadOnlyObservableCollection<ReviveListTextColorItemViewModel>(textItems);

        _lastSaved = WidgetConfigDefaults.CloneNormalizedReviveList(config);
        Load(_lastSaved);

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    public event Action<ReviveListWidgetSettingsConfig>? PreviewChanged;

    public ReadOnlyObservableCollection<MeterPlayerInfoFormatField> AvailablePlayerInfoFormatFields { get; }

    /// <summary>ゲージの色の節(「デバフカラー」)。</summary>
    public BuffGaugeColorSettingsViewModel GaugeColors { get; }

    /// <summary>アイコンカラーの行。職ごとに色見本(最大5枠)と、選んでいる枠を持つ。</summary>
    public ReadOnlyObservableCollection<MeterClassColorItemViewModel> ClassColorItems { get; }

    /// <summary>テキストカラーの行。行ごとに色見本(最大5枠)と、選んでいる枠を持つ。</summary>
    public ReadOnlyObservableCollection<ReviveListTextColorItemViewModel> TextColorItems { get; }

    public string ClassColorSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_IconColors_Title");

    public string TextColorSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_ReviveListTextColors_Title");

    /// <summary>
    /// 「自分の強調表示」のスイッチ。保存する値は <see cref="SelfDisplayModeIndex"/> のままで、
    /// オンが強調表示、オフが通常表示に当たる(プレイヤーリストと同じ)。
    /// </summary>
    public bool SelfHighlightEnabled
    {
        get => SelfDisplayModeIndex == WidgetConfigDefaults.DefaultSelfDisplayModeIndex;
        set => SelfDisplayModeIndex = value
            ? WidgetConfigDefaults.DefaultSelfDisplayModeIndex
            : WidgetConfigDefaults.MaxSelfDisplayModeIndex;
    }

    /// <summary>「自分の強調表示」のスイッチの右に出す ON / OFF。</summary>
    public string SelfHighlightStateText => LocalizationManager.Instance.GetString(
        SelfHighlightEnabled ? "Settings_Switch_On" : "Settings_Switch_Off");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        GaugeColors.Changed -= NotifyChanged;
        GaugeColors.Dispose();

        foreach (var item in ClassColorItems)
        {
            item.Colors.PaletteChanged -= Colors_PaletteChanged;
        }

        foreach (var item in TextColorItems)
        {
            item.Colors.PaletteChanged -= Colors_PaletteChanged;
        }
    }

    public ReviveListWidgetSettingsConfig CreateConfig()
    {
        var config = new ReviveListWidgetSettingsConfig
        {
            PlayerInfoFormatString = PlayerInfoFormatString ?? string.Empty,
            SelfDisplayModeIndex = SelfDisplayModeIndex,
            GaugeLengthIndex = GaugeLengthIndex,
            GaugeColors = GaugeColors.CreateConfig(),
            ClassColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
            TextColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            TextColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var item in ClassColorItems)
        {
            config.ClassColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.ClassColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        foreach (var item in TextColorItems)
        {
            config.TextColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.TextColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        return WidgetConfigDefaults.CloneNormalizedReviveList(config);
    }

    public Color GetSelectedClassColor(string key)
    {
        return _classColorItemsByKey[key].Colors.SelectedColor;
    }

    public void ApplyClassColor(string key, Color color)
    {
        _classColorItemsByKey[key].Colors.AddOrSelect(color);
    }

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
        Load(WidgetConfigDefaults.CreateReviveListSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(ReviveListWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedReviveList(config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Load(ReviveListWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedReviveList(config);

        _isLoading = true;
        try
        {
            PlayerInfoFormatString = normalized.PlayerInfoFormatString ?? WidgetConfigDefaults.DefaultReviveListPlayerInfoFormatString;
            SelfDisplayModeIndex = normalized.SelfDisplayModeIndex;
            GaugeLengthIndex = normalized.GaugeLengthIndex;
            GaugeColors.Load(normalized.GaugeColors);

            foreach (var item in ClassColorItems)
            {
                item.Colors.Load(normalized.ClassColorPalettes[item.Key], normalized.ClassColorIndexes[item.Key]);
            }

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

    partial void OnPlayerInfoFormatStringChanged(string value)
    {
        // 書式は1行。貼り付けた改行は空白にする(プレイヤーリストと同じ)。
        var normalized = (value ?? string.Empty)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            PlayerInfoFormatString = normalized;
            return;
        }

        RefreshFormatPreview();
        NotifyChanged();
    }

    partial void OnSelfDisplayModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(SelfHighlightEnabled));
        OnPropertyChanged(nameof(SelfHighlightStateText));
        NotifyChanged();
    }

    partial void OnGaugeLengthIndexChanged(int value)
    {
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

    private void RefreshFormatPreview()
    {
        FormatPreview = PlayerInfoFormatFormatter.FormatPreview(PlayerInfoFormatString);
    }

    private void RebuildPlayerInfoFormatFields()
    {
        var selectedKey = SelectedPlayerInfoFormatField?.Key;

        _availablePlayerInfoFormatFields.Clear();
        foreach (var definition in PlayerInfoFormatFormatter.FieldDefinitions)
        {
            _availablePlayerInfoFormatFields.Add(new MeterPlayerInfoFormatField(
                definition.Key,
                LocalizationManager.Instance.GetString(definition.LabelResourceKey),
                definition.Placeholder));
        }

        SelectedPlayerInfoFormatField = _availablePlayerInfoFormatFields
            .FirstOrDefault(field => string.Equals(field.Key, selectedKey, StringComparison.Ordinal))
            ?? _availablePlayerInfoFormatFields.FirstOrDefault();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        RebuildPlayerInfoFormatFields();
        RefreshFormatPreview();

        foreach (var item in ClassColorItems)
        {
            item.RefreshDisplayName();
        }

        foreach (var item in TextColorItems)
        {
            item.RefreshDisplayName();
        }

        OnPropertyChanged(nameof(ClassColorSectionTitle));
        OnPropertyChanged(nameof(TextColorSectionTitle));
        OnPropertyChanged(nameof(SelfHighlightStateText));
    }

    private static bool SettingsEqual(ReviveListWidgetSettingsConfig left, ReviveListWidgetSettingsConfig right)
    {
        if (!string.Equals(left.PlayerInfoFormatString, right.PlayerInfoFormatString, StringComparison.Ordinal)
            || left.SelfDisplayModeIndex != right.SelfDisplayModeIndex
            || left.GaugeLengthIndex != right.GaugeLengthIndex
            || !BuffGaugeColorSettingsViewModel.ConfigEquals(left.GaugeColors!, right.GaugeColors!))
        {
            return false;
        }

        foreach (var key in WidgetConfigDefaults.GetClassColorKeys(WidgetKind.ReviveList))
        {
            if (left.ClassColorIndexes[key] != right.ClassColorIndexes[key]
                || !left.ClassColorPalettes[key].SequenceEqual(right.ClassColorPalettes[key], StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        foreach (var key in WidgetConfigDefaults.ReviveListTextColorKeys)
        {
            if (left.TextColorIndexes[key] != right.TextColorIndexes[key]
                || !left.TextColorPalettes[key].SequenceEqual(right.TextColorPalettes[key], StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>テキストカラーの1行。行の作りは装備詳細のテキストカラー(<see cref="EquipmentTextColorItemViewModel"/>)と同じ。</summary>
public sealed class ReviveListTextColorItemViewModel : ObservableObject
{
    public ReviveListTextColorItemViewModel(string key, ColorPaletteViewModel colors, bool isLast)
    {
        Key = key;
        Colors = colors;
        IsLast = isLast;
    }

    public string Key { get; }

    public ColorPaletteViewModel Colors { get; }

    public bool IsLast { get; }

    /// <summary>項目名。窓の右の文字と同じ文言。</summary>
    public string DisplayName => LocalizationManager.Instance.GetString($"ReviveList_{Key}");

    public void RefreshDisplayName()
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
