// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// プレイヤー情報の設定。バッジの絵の色(アイコンカラー)。
/// 行の作りはプレイヤーリストのアイコンカラーと同じで、心相晶の無効とシーズンの行があり、不明は末尾の1行だけ。
/// 不透明度は持たない。
/// </summary>
public sealed class PlayerInfoWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<string, MeterClassColorItemViewModel> _classColorItemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private PlayerInfoWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    public PlayerInfoWidgetSettingsViewModel(PlayerInfoWidgetSettingsConfig? config)
    {
        var items = new ObservableCollection<MeterClassColorItemViewModel>();
        var classColorKeys = WidgetConfigDefaults.GetClassColorKeys(WidgetKind.PlayerInfo);
        for (var index = 0; index < classColorKeys.Count; index++)
        {
            var key = classColorKeys[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultClassColors(WidgetKind.PlayerInfo, key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += ClassColors_PaletteChanged;

            var item = new MeterClassColorItemViewModel(
                key,
                colors,
                index == classColorKeys.Count - 1,
                "PlayerInfo_Unknown");
            _classColorItemsByKey.Add(key, item);
            items.Add(item);
        }

        ClassColorItems = new ReadOnlyObservableCollection<MeterClassColorItemViewModel>(items);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        _lastSaved = WidgetConfigDefaults.CloneNormalizedPlayerInfo(config);
        Load(_lastSaved);
    }

    public event Action<PlayerInfoWidgetSettingsConfig>? PreviewChanged;

    /// <summary>アイコンカラーの行。行ごとに色見本(最大5枠)と、選んでいる枠を持つ。</summary>
    public ReadOnlyObservableCollection<MeterClassColorItemViewModel> ClassColorItems { get; }

    public string ClassColorSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_IconColors_Title");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in ClassColorItems)
        {
            item.Colors.PaletteChanged -= ClassColors_PaletteChanged;
        }
    }

    public PlayerInfoWidgetSettingsConfig CreateConfig()
    {
        var config = new PlayerInfoWidgetSettingsConfig
        {
            ClassColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var item in ClassColorItems)
        {
            config.ClassColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.ClassColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        return WidgetConfigDefaults.CloneNormalizedPlayerInfo(config);
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
        Load(WidgetConfigDefaults.CreatePlayerInfoSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(PlayerInfoWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedPlayerInfo(config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Load(PlayerInfoWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedPlayerInfo(config);

        _isLoading = true;
        try
        {
            foreach (var item in ClassColorItems)
            {
                item.Colors.Load(normalized.ClassColorPalettes[item.Key], normalized.ClassColorIndexes[item.Key]);
            }
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
        PlayerInfoWidgetSettingsConfig left,
        PlayerInfoWidgetSettingsConfig right)
    {
        foreach (var key in WidgetConfigDefaults.GetClassColorKeys(WidgetKind.PlayerInfo))
        {
            if (left.ClassColorIndexes[key] != right.ClassColorIndexes[key]
                || !left.ClassColorPalettes[key].SequenceEqual(right.ClassColorPalettes[key], StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private void ClassColors_PaletteChanged(object? sender, EventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var item in ClassColorItems)
        {
            item.RefreshDisplayName();
        }

        OnPropertyChanged(nameof(ClassColorSectionTitle));
    }
}
