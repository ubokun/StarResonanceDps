// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>チャットの設定。テキストカラーは蘇生リスト(<see cref="ReviveListWidgetSettingsViewModel"/>)と同じ作り(見本は「Aa」)。</summary>
public sealed class ChatWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<string, ChatTextColorItemViewModel> _textColorItemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private ChatWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    public ChatWidgetSettingsViewModel(ChatWidgetSettingsConfig? config)
    {
        var textItems = new ObservableCollection<ChatTextColorItemViewModel>();
        var textColorKeys = WidgetConfigDefaults.ChatTextColorKeys;
        for (var index = 0; index < textColorKeys.Length; index++)
        {
            var key = textColorKeys[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultChatTextColors(key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += Colors_PaletteChanged;

            var item = new ChatTextColorItemViewModel(key, colors, index == textColorKeys.Length - 1);
            _textColorItemsByKey.Add(key, item);
            textItems.Add(item);
        }

        TextColorItems = new ReadOnlyObservableCollection<ChatTextColorItemViewModel>(textItems);

        _lastSaved = WidgetConfigDefaults.CloneNormalizedChat(config);
        Load(_lastSaved);

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    public event Action<ChatWidgetSettingsConfig>? PreviewChanged;

    /// <summary>テキストカラーの行。行ごとに色見本(最大5枠)と、選んでいる枠を持つ。</summary>
    public ReadOnlyObservableCollection<ChatTextColorItemViewModel> TextColorItems { get; }

    public string TextColorSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_ChatTextColors_Title");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in TextColorItems)
        {
            item.Colors.PaletteChanged -= Colors_PaletteChanged;
        }
    }

    public ChatWidgetSettingsConfig CreateConfig()
    {
        var config = new ChatWidgetSettingsConfig
        {
            TextColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            TextColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var item in TextColorItems)
        {
            config.TextColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.TextColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        return WidgetConfigDefaults.CloneNormalizedChat(config);
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
        Load(WidgetConfigDefaults.CreateChatSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(ChatWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedChat(config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Load(ChatWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedChat(config);

        _isLoading = true;
        try
        {
            foreach (var item in TextColorItems)
            {
                item.Colors.Load(normalized.TextColorPalettes[item.Key], normalized.TextColorIndexes[item.Key]);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void Colors_PaletteChanged(object? sender, EventArgs e)
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

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var item in TextColorItems)
        {
            item.RefreshDisplayName();
        }

        OnPropertyChanged(nameof(TextColorSectionTitle));
    }

    private static bool SettingsEqual(ChatWidgetSettingsConfig left, ChatWidgetSettingsConfig right)
    {
        foreach (var key in WidgetConfigDefaults.ChatTextColorKeys)
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

/// <summary>テキストカラーの1行。行の作りは蘇生リストのテキストカラー(<see cref="ReviveListTextColorItemViewModel"/>)と同じ。</summary>
public sealed class ChatTextColorItemViewModel : ObservableObject
{
    public ChatTextColorItemViewModel(string key, ColorPaletteViewModel colors, bool isLast)
    {
        Key = key;
        Colors = colors;
        IsLast = isLast;
    }

    public string Key { get; }

    public ColorPaletteViewModel Colors { get; }

    public bool IsLast { get; }

    public string DisplayName => LocalizationManager.Instance.GetString($"Settings_ChatTextColor_{Key}");

    public void RefreshDisplayName()
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
