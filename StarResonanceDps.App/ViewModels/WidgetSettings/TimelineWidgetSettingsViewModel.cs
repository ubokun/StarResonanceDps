// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// タイムラインの表示設定。敵の回復を出すか、相手がプレイヤーの行のクラスアイコンの色(アイコンカラー)、文字の色(テキストカラー)。
/// アイコンカラーの形はプレイヤーリストのクラスカラーと同じで、不透明度は持たない。
/// </summary>
public sealed partial class TimelineWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<string, MeterClassColorItemViewModel> _classColorItemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TimelineTextColorItemViewModel> _textColorItemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private TimelineWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private int _enemyHealDisplayModeIndex = WidgetConfigDefaults.DefaultTimelineEnemyHealDisplayModeIndex;

    public TimelineWidgetSettingsViewModel(TimelineWidgetSettingsConfig? config)
    {
        var items = new ObservableCollection<MeterClassColorItemViewModel>();
        var classColorKeys = WidgetConfigDefaults.GetClassColorKeys(WidgetKind.Timeline);
        for (var index = 0; index < classColorKeys.Count; index++)
        {
            var key = classColorKeys[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultClassColors(WidgetKind.Timeline, key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += ClassColors_PaletteChanged;

            var item = new MeterClassColorItemViewModel(key, colors, index == classColorKeys.Count - 1);
            _classColorItemsByKey.Add(key, item);
            items.Add(item);
        }

        ClassColorItems = new ReadOnlyObservableCollection<MeterClassColorItemViewModel>(items);

        var textItems = new ObservableCollection<TimelineTextColorItemViewModel>();
        var textColorKeys = WidgetConfigDefaults.TimelineTextColorKeys;
        for (var index = 0; index < textColorKeys.Length; index++)
        {
            var key = textColorKeys[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultTextColors(key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += TextColors_PaletteChanged;

            var item = new TimelineTextColorItemViewModel(key, colors, index == textColorKeys.Length - 1);
            _textColorItemsByKey.Add(key, item);
            textItems.Add(item);
        }

        TextColorItems = new ReadOnlyObservableCollection<TimelineTextColorItemViewModel>(textItems);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        _lastSaved = WidgetConfigDefaults.CloneNormalizedTimeline(config);
        Load(_lastSaved);
    }

    public event Action<TimelineWidgetSettingsConfig>? PreviewChanged;

    /// <summary>アイコンカラーの行。職ごとに色見本(最大5枠)と、選んでいる枠を持つ。</summary>
    public ReadOnlyObservableCollection<MeterClassColorItemViewModel> ClassColorItems { get; }

    /// <summary>テキストカラーの行。行ごとに色見本(最大5枠)と、選んでいる枠を持つ。</summary>
    public ReadOnlyObservableCollection<TimelineTextColorItemViewModel> TextColorItems { get; }

    public string ClassColorSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_IconColors_Title");

    public string TextColorSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_TimelineTextColors_Title");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in ClassColorItems)
        {
            item.Colors.PaletteChanged -= ClassColors_PaletteChanged;
        }

        foreach (var item in TextColorItems)
        {
            item.Colors.PaletteChanged -= TextColors_PaletteChanged;
        }
    }

    public TimelineWidgetSettingsConfig CreateConfig()
    {
        var config = new TimelineWidgetSettingsConfig
        {
            EnemyHealDisplayModeIndex = EnemyHealDisplayModeIndex,
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

        return WidgetConfigDefaults.CloneNormalizedTimeline(config);
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
        Load(WidgetConfigDefaults.CreateTimelineSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(TimelineWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedTimeline(config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Load(TimelineWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedTimeline(config);

        _isLoading = true;
        try
        {
            foreach (var item in ClassColorItems)
            {
                item.Colors.Load(normalized.ClassColorPalettes[item.Key], normalized.ClassColorIndexes[item.Key]);
            }

            foreach (var item in TextColorItems)
            {
                item.Colors.Load(normalized.TextColorPalettes[item.Key], normalized.TextColorIndexes[item.Key]);
            }

            EnemyHealDisplayModeIndex = normalized.EnemyHealDisplayModeIndex;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void RaisePreviewChanged()
    {
        PreviewChanged?.Invoke(CreateConfig());
    }

    private static bool SettingsEqual(
        TimelineWidgetSettingsConfig left,
        TimelineWidgetSettingsConfig right)
    {
        if (left.EnemyHealDisplayModeIndex != right.EnemyHealDisplayModeIndex)
        {
            return false;
        }

        foreach (var key in WidgetConfigDefaults.GetClassColorKeys(WidgetKind.Timeline))
        {
            if (left.ClassColorIndexes[key] != right.ClassColorIndexes[key]
                || !left.ClassColorPalettes[key].SequenceEqual(right.ClassColorPalettes[key], StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        foreach (var key in WidgetConfigDefaults.TimelineTextColorKeys)
        {
            if (left.TextColorIndexes[key] != right.TextColorIndexes[key]
                || !left.TextColorPalettes[key].SequenceEqual(right.TextColorPalettes[key], StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private void ClassColors_PaletteChanged(object? sender, EventArgs e)
    {
        NotifyChanged();
    }

    private void TextColors_PaletteChanged(object? sender, EventArgs e)
    {
        NotifyChanged();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
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
    }

    partial void OnEnemyHealDisplayModeIndexChanged(int value)
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
}

/// <summary>
/// テキストカラーの1行。行の作りはクラスカラー(<see cref="MeterClassColorItemViewModel"/>)と同じで、左の欄は「Aa」の見本。
/// </summary>
public sealed class TimelineTextColorItemViewModel : ObservableObject
{
    public TimelineTextColorItemViewModel(string key, ColorPaletteViewModel colors, bool isLast)
    {
        Key = key;
        Colors = colors;
        IsLast = isLast;
    }

    public string Key { get; }

    public ColorPaletteViewModel Colors { get; }

    public bool IsLast { get; }

    /// <summary>タイムライン専用の項目名。</summary>
    public string DisplayName => LocalizationManager.Instance.GetString($"Settings_TimelineTextColor_{Key}");

    public void RefreshDisplayName()
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
