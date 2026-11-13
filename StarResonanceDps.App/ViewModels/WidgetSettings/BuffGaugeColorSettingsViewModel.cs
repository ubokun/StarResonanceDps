// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// ゲージの色の節(「バフカラー」「デバフカラー」)1つ。色の行(左端と右端)と不透明度。
/// バフ・デバフ一覧は1つ、バフ・デバフグループはバフとデバフの2つ、蘇生リストはデバフの1つを持つ。行の作りはクラスカラーと同じ。
/// </summary>
public sealed partial class BuffGaugeColorSettingsViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<string, BuffGaugeColorItemViewModel> _itemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly PlayerBuffListKind _side;
    private bool _isLoading;

    /// <summary>不透明度(0〜100)。UI がスライダーなので double で持つ(メーターのクラスカラーと同じ)。</summary>
    [ObservableProperty]
    private double _opacity = WidgetConfigDefaults.MaxClassColorOpacity;

    public BuffGaugeColorSettingsViewModel(PlayerBuffListKind side)
    {
        _side = side;

        var items = new ObservableCollection<BuffGaugeColorItemViewModel>();
        foreach (var key in WidgetConfigDefaults.BuffGaugeColorKeys)
        {
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultGaugeColors(key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += Colors_PaletteChanged;

            // 同じ窓に節が2つ並ぶ(バフ・デバフグループ)ので、色見本の組の名前に側を入れる。
            var item = new BuffGaugeColorItemViewModel(key, colors, $"{side}.{key}");
            _itemsByKey.Add(key, item);
            items.Add(item);
        }

        Items = new ReadOnlyObservableCollection<BuffGaugeColorItemViewModel>(items);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        Load(WidgetConfigDefaults.CreateBuffGaugeColors(side));
    }

    /// <summary>色か不透明度を変えた。読み込み(<see cref="Load"/>)では鳴らさない。</summary>
    public event Action? Changed;

    /// <summary>色の行。左端と右端の2行。</summary>
    public ReadOnlyObservableCollection<BuffGaugeColorItemViewModel> Items { get; }

    /// <summary>節の見出し。バフの色なら「バフカラー」、デバフの色なら「デバフカラー」。</summary>
    public string SectionTitle => LocalizationManager.Instance.GetString(
        _side == PlayerBuffListKind.Debuff
            ? "Settings_Section_DebuffListColors_Title"
            : "Settings_Section_BuffListColors_Title");

    /// <summary>不透明度の行名。見出しと同じく「バフカラー」「デバフカラー」に分かれる。</summary>
    public string OpacityLabel => LocalizationManager.Instance.GetString(
        _side == PlayerBuffListKind.Debuff
            ? "Settings_DebuffListColors_Opacity"
            : "Settings_BuffListColors_Opacity");

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in Items)
        {
            item.Colors.PaletteChanged -= Colors_PaletteChanged;
        }
    }

    public BuffGaugeColorConfig CreateConfig()
    {
        var config = new BuffGaugeColorConfig
        {
            Opacity = Math.Clamp(
                (int)Math.Round(Opacity, MidpointRounding.AwayFromZero),
                WidgetConfigDefaults.MinClassColorOpacity,
                WidgetConfigDefaults.MaxClassColorOpacity),
            Indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            Palettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var item in Items)
        {
            config.Indexes[item.Key] = item.Colors.SelectedIndex;
            config.Palettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        return WidgetConfigDefaults.CloneNormalizedBuffGaugeColors(config, _side);
    }

    /// <summary>値を入れる。<see cref="Changed"/> は鳴らさない。</summary>
    public void Load(BuffGaugeColorConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedBuffGaugeColors(config, _side);

        _isLoading = true;
        try
        {
            foreach (var item in Items)
            {
                item.Colors.Load(normalized.Palettes[item.Key], normalized.Indexes[item.Key]);
            }

            Opacity = normalized.Opacity;
        }
        finally
        {
            _isLoading = false;
        }
    }

    public Color GetSelectedColor(string key)
    {
        return _itemsByKey[key].Colors.SelectedColor;
    }

    public void ApplyColor(string key, Color color)
    {
        _itemsByKey[key].Colors.AddOrSelect(color);
    }

    /// <summary>正規化した2つの値(<see cref="WidgetConfigDefaults.CloneNormalizedBuffGaugeColors"/>)が同じか。</summary>
    public static bool ConfigEquals(BuffGaugeColorConfig left, BuffGaugeColorConfig right)
    {
        if (left.Opacity != right.Opacity)
        {
            return false;
        }

        foreach (var key in WidgetConfigDefaults.BuffGaugeColorKeys)
        {
            if (left.Indexes[key] != right.Indexes[key]
                || !left.Palettes[key].SequenceEqual(right.Palettes[key], StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    partial void OnOpacityChanged(double value)
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

        Changed?.Invoke();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var item in Items)
        {
            item.RefreshDisplayName();
        }

        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(OpacityLabel));
    }
}

/// <summary>ゲージの色の1行。作りはクラスカラーの行と同じ。</summary>
public sealed class BuffGaugeColorItemViewModel : ObservableObject
{
    public BuffGaugeColorItemViewModel(string key, ColorPaletteViewModel colors, string colorGroupName)
    {
        Key = key;
        Colors = colors;
        ColorGroupName = colorGroupName;
    }

    public string Key { get; }

    public ColorPaletteViewModel Colors { get; }

    /// <summary>色見本のラジオボタンの組の名前。窓の中で行ごとに違う名前にする。</summary>
    public string ColorGroupName { get; }

    public string DisplayName => LocalizationManager.Instance.GetString($"Settings_BuffListGauge_{Key}");

    public void RefreshDisplayName()
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
