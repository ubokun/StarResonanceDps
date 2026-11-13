// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels.Plugins;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// プラグインのウィジェットの設定(プラグインが定義した節と行)。ほかの節と同じく、値を変えるたびに保存前のプレビューを出し、
/// 保存・破棄・初期化は設定の窓(<see cref="WidgetSettingsViewModel"/>)が呼ぶ。
/// </summary>
public sealed class PluginWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private readonly IReadOnlyList<PluginSettingViewModel> _rows;
    private PluginWidgetSettingsConfig _lastSaved;

    public PluginWidgetSettingsViewModel(PluginWidgetRegistration registration, PluginWidgetSettingsConfig? config)
    {
        Sections = [.. registration.Definition.SettingSections.Select(section => new PluginWidgetSettingSectionViewModel(section))];
        _rows = [.. Sections.SelectMany(section => section.Rows)];
        foreach (var row in _rows)
        {
            row.Changed += Row_Changed;
            row.TextChanged += Row_Changed;
        }

        Load(config);
        _lastSaved = CreateConfig();
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    public event Action<PluginWidgetSettingsConfig>? PreviewChanged;

    public IReadOnlyList<PluginWidgetSettingSectionViewModel> Sections { get; }

    public bool HasUnsavedChanges => !ValuesEqual(CreateConfig(), _lastSaved);

    /// <summary>今の値。定義に無い設定の値(前の版のプラグインのもの)は残さない。</summary>
    public PluginWidgetSettingsConfig CreateConfig()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _rows)
        {
            values[row.Definition.Id] = row.CreateSavedValue();
        }

        return new PluginWidgetSettingsConfig { Values = values };
    }

    public void MarkSaved(PluginWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedPlugin(config);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void ResetToDefaults()
    {
        Load(null);
        OnPropertyChanged(nameof(HasUnsavedChanges));
        PreviewChanged?.Invoke(CreateConfig());
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
        foreach (var row in _rows)
        {
            row.Changed -= Row_Changed;
            row.TextChanged -= Row_Changed;
        }
    }

    private void Load(PluginWidgetSettingsConfig? config)
    {
        foreach (var row in _rows)
        {
            string? saved = null;
            config?.Values.TryGetValue(row.Definition.Id, out saved);
            row.Load(saved);
        }
    }

    private void Row_Changed(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        PreviewChanged?.Invoke(CreateConfig());
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var section in Sections)
        {
            section.RefreshLocalizedText();
        }
    }

    private static bool ValuesEqual(PluginWidgetSettingsConfig left, PluginWidgetSettingsConfig right)
    {
        return left.Values.Count == right.Values.Count
            && left.Values.All(pair => right.Values.TryGetValue(pair.Key, out var value)
                && string.Equals(pair.Value, value, StringComparison.Ordinal));
    }
}

/// <summary>プラグインのウィジェットの設定の節1つ。ナビのボタンと本文の節の見出しと行。</summary>
public sealed partial class PluginWidgetSettingSectionViewModel : ObservableObject
{
    private readonly PluginWidgetSettingSection _section;

    [ObservableProperty]
    private string _title = string.Empty;

    internal PluginWidgetSettingSectionViewModel(PluginWidgetSettingSection section)
    {
        _section = section;
        Rows = [.. section.Settings.Select(PluginSettingViewModel.Create)];
        Rows[^1].IsLast = true;

        // 鍵は検証で公開の一覧にあることを確かめてある。
        IconGeometry = Application.Current.TryFindResource(section.Icon) as Geometry
            ?? throw new InvalidOperationException($"The icon resource '{section.Icon}' was not found.");
        RefreshLocalizedText();
    }

    public IReadOnlyList<PluginSettingViewModel> Rows { get; }

    public Geometry IconGeometry { get; }

    /// <summary>ナビのアイコンを塗りで描くか(偽なら線で描く)。</summary>
    public bool UsesFillIcon => _section.IconStyle == PluginResourceKeys.Icons.FillStyle;

    public bool UsesStrokeIcon => !UsesFillIcon;

    public void RefreshLocalizedText()
    {
        Title = PluginTexts.Resolve(_section.Names, LocalizationManager.Instance.CurrentCulture) ?? string.Empty;
        foreach (var row in Rows)
        {
            row.RefreshLocalizedText();
        }
    }
}
