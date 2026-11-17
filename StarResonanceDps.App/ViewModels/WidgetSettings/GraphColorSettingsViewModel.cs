// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// グラフカラーの節。推移グラフ(線の色)とバフ稼働率(バーの色)が持つ。
/// 形はメーターのクラスカラーと同じ(クラスごとに色見本(最大5枠)と選んでいる枠。不透明度は持たない)で、
/// 行の並びと既定は読み替え先のメーター(<see cref="WidgetConfigDefaults.GetMetricGraphColorDefaultSource"/>)と同じ。
/// 値の正規化と保存は持ち主の設定の型が行う。
/// 行の見本は、推移グラフは折れ線、バフ稼働率はバー(<see cref="UsesBarSwatch"/>)。
/// </summary>
public sealed class GraphColorSettingsViewModel : ObservableObject, IDisposable
{
    private readonly WidgetKind _colorDefaultSource;
    private readonly Dictionary<string, MeterClassColorItemViewModel> _itemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private bool _isLoading;

    public GraphColorSettingsViewModel(WidgetKind colorDefaultSource, bool usesBarSwatch)
    {
        _colorDefaultSource = colorDefaultSource;
        UsesBarSwatch = usesBarSwatch;

        var items = new ObservableCollection<MeterClassColorItemViewModel>();
        var classColorKeys = WidgetConfigDefaults.GetClassColorKeys(colorDefaultSource);
        for (var index = 0; index < classColorKeys.Count; index++)
        {
            var key = classColorKeys[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultClassColors(colorDefaultSource, key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += Colors_PaletteChanged;

            var item = new MeterClassColorItemViewModel(key, colors, index == classColorKeys.Count - 1);
            _itemsByKey.Add(key, item);
            items.Add(item);
        }

        Items = new ReadOnlyObservableCollection<MeterClassColorItemViewModel>(items);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    /// <summary>色を変えた。読み込み(<see cref="Load"/>)では鳴らさない。</summary>
    public event Action? Changed;

    /// <summary>グラフカラーの行。クラスごとに色見本(最大5枠)と、選んでいる枠を持つ。</summary>
    public ReadOnlyObservableCollection<MeterClassColorItemViewModel> Items { get; }

    /// <summary>行の見本をバーで出すか。偽なら折れ線。</summary>
    public bool UsesBarSwatch { get; }

    public string SectionTitle => LocalizationManager.Instance.GetString("Settings_Section_GraphColors_Title");

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in Items)
        {
            item.Colors.PaletteChanged -= Colors_PaletteChanged;
        }
    }

    /// <summary>行ごとの選んでいる枠と色見本を書き込む。</summary>
    public void WriteTo(Dictionary<string, int> indexes, Dictionary<string, List<string>> palettes)
    {
        foreach (var item in Items)
        {
            indexes[item.Key] = item.Colors.SelectedIndex;
            palettes[item.Key] = [.. item.Colors.GetHexColors()];
        }
    }

    /// <summary>正規化した値を入れる。<see cref="Changed"/> は鳴らさない。</summary>
    public void Load(IReadOnlyDictionary<string, int> indexes, IReadOnlyDictionary<string, List<string>> palettes)
    {
        _isLoading = true;
        try
        {
            foreach (var item in Items)
            {
                item.Colors.Load(palettes[item.Key], indexes[item.Key]);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>正規化した2つの値の色が同じか。</summary>
    public bool ColorsEqual(
        IReadOnlyDictionary<string, int> leftIndexes,
        IReadOnlyDictionary<string, List<string>> leftPalettes,
        IReadOnlyDictionary<string, int> rightIndexes,
        IReadOnlyDictionary<string, List<string>> rightPalettes)
    {
        foreach (var key in WidgetConfigDefaults.GetClassColorKeys(_colorDefaultSource))
        {
            if (leftIndexes[key] != rightIndexes[key]
                || !leftPalettes[key].SequenceEqual(rightPalettes[key], StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public Color GetSelectedColor(string key)
    {
        return _itemsByKey[key].Colors.SelectedColor;
    }

    public void ApplyColor(string key, Color color)
    {
        _itemsByKey[key].Colors.AddOrSelect(color);
    }

    private void Colors_PaletteChanged(object? sender, EventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        Changed?.Invoke();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var item in Items)
        {
            item.RefreshDisplayName();
        }

        OnPropertyChanged(nameof(SectionTitle));
    }
}
