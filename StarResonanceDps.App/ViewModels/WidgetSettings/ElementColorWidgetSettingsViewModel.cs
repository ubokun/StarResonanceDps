// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// 属性カラー(スキル詳細・被ダメログ)と、与ダメ詳細・ヒール詳細・被ダメ詳細のアイコンカラー。作りはメーターのクラスカラーと同じで、
/// 行は9属性、アイコンカラーはその後に物理・魔法・無分類が続く(<see cref="WidgetConfigDefaults.HasDamageModeColorRows"/>)。
/// 不透明度は全部の行で1組。保存の型は <see cref="ElementColorWidgetSettingsConfig"/>。
///
/// <para>
/// ウィジェットのバーは、この色を使う(スキル詳細の行のバーは、その行に出た属性の割合で混ぜたもの)。
/// 不透明度が掛かるのもウィジェットの側だけで、<b>ここの色見本は素のまま</b>。
/// </para>
/// </summary>
public sealed partial class ElementColorWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<string, ElementColorItemViewModel> _itemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly WidgetKind _kind;
    private readonly bool _hasDamageModeRows;
    private ElementColorWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private double _colorOpacity = WidgetConfigDefaults.MaxClassColorOpacity;

    public ElementColorWidgetSettingsViewModel(WidgetKind kind, ElementColorWidgetSettingsConfig? config)
    {
        _kind = kind;
        _hasDamageModeRows = WidgetConfigDefaults.HasDamageModeColorRows(kind);

        var items = new ObservableCollection<ElementColorItemViewModel>();
        foreach (var key in WidgetConfigDefaults.DamagePropertyKeys)
        {
            items.Add(CreateItem(
                key,
                WidgetConfigDefaults.CreateDefaultElementColors(key),
                ElementIconMasks.Get(Enum.Parse<Zproto.EDamageProperty>(key)),
                usesPlayerStatusAsset: false,
                $"DamageProperty_{key}"));
        }

        if (_hasDamageModeRows)
        {
            foreach (var key in WidgetConfigDefaults.DamageModeKeys)
            {
                items.Add(CreateItem(
                    key,
                    WidgetConfigDefaults.CreateDefaultDamageModeColors(key),
                    DamageModeIconMasks.Get(Enum.Parse<Zproto.EDamageMode>(key)),
                    usesPlayerStatusAsset: true,
                    GetDamageModeNameKey(key)));
            }
        }

        Items = new ReadOnlyObservableCollection<ElementColorItemViewModel>(items);

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        _lastSaved = CloneNormalized(config);
        Load(_lastSaved);
    }

    public event Action<ElementColorWidgetSettingsConfig>? PreviewChanged;

    /// <summary>色の行。9属性を属性ID順、アイコンカラーはその後に物理・魔法・無分類。</summary>
    public ReadOnlyObservableCollection<ElementColorItemViewModel> Items { get; }

    public string SectionTitle => LocalizationManager.Instance.GetString(
        _hasDamageModeRows
            ? "Settings_Section_IconColors_Title"
            : "Settings_Section_ElementColors_Title");

    /// <summary>不透明度の行の名前。節の名前に合わせる(属性カラーの不透明度 / アイコンカラーの不透明度)。</summary>
    public string OpacityLabel => LocalizationManager.Instance.GetString(
        _hasDamageModeRows
            ? "Settings_IconColors_Opacity"
            : "Settings_ElementColors_Opacity");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in Items)
        {
            item.Colors.PaletteChanged -= Colors_PaletteChanged;
        }
    }

    public ElementColorWidgetSettingsConfig CreateConfig()
    {
        var config = new ElementColorWidgetSettingsConfig
        {
            ColorOpacity = Math.Clamp(
                (int)Math.Round(ColorOpacity, MidpointRounding.AwayFromZero),
                WidgetConfigDefaults.MinClassColorOpacity,
                WidgetConfigDefaults.MaxClassColorOpacity),
            ColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var item in Items)
        {
            config.ColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.ColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        WidgetConfigDefaults.NormalizeElementColor(_kind, config);
        return config;
    }

    public Color GetSelectedColor(string key)
    {
        return _itemsByKey[key].Colors.SelectedColor;
    }

    public void ApplyColor(string key, Color color)
    {
        _itemsByKey[key].Colors.AddOrSelect(color);
    }

    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreateElementColorSettings(_kind));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(ElementColorWidgetSettingsConfig? config)
    {
        _lastSaved = CloneNormalized(config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private ElementColorWidgetSettingsConfig CloneNormalized(ElementColorWidgetSettingsConfig? config)
    {
        return WidgetConfigDefaults.CloneNormalizedElementColor(_kind, config);
    }

    private ElementColorItemViewModel CreateItem(
        string key,
        List<string> defaultColors,
        Brush? iconMask,
        bool usesPlayerStatusAsset,
        string displayNameKey)
    {
        var colors = new ColorPaletteViewModel(defaultColors, WidgetConfigDefaults.MaxPaletteColorCount);
        colors.PaletteChanged += Colors_PaletteChanged;

        var item = new ElementColorItemViewModel(key, colors, iconMask, usesPlayerStatusAsset, displayNameKey);
        _itemsByKey.Add(key, item);
        return item;
    }

    private static string GetDamageModeNameKey(string key)
    {
        return Enum.Parse<Zproto.EDamageMode>(key) switch
        {
            Zproto.EDamageMode.DamagePhysical => "Metric_DamageMode_Physical",
            Zproto.EDamageMode.DamageMagical => "Metric_DamageMode_Magical",
            _ => "Metric_DamageMode_Normal"
        };
    }

    private void Load(ElementColorWidgetSettingsConfig? config)
    {
        var normalized = CloneNormalized(config);

        _isLoading = true;
        try
        {
            foreach (var item in Items)
            {
                item.Colors.Load(normalized.ColorPalettes[item.Key], normalized.ColorIndexes[item.Key]);
            }

            ColorOpacity = normalized.ColorOpacity;
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

    private bool SettingsEqual(ElementColorWidgetSettingsConfig left, ElementColorWidgetSettingsConfig right)
    {
        if (left.ColorOpacity != right.ColorOpacity)
        {
            return false;
        }

        foreach (var key in _itemsByKey.Keys)
        {
            if (left.ColorIndexes[key] != right.ColorIndexes[key]
                || !left.ColorPalettes[key].SequenceEqual(right.ColorPalettes[key], StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private void Colors_PaletteChanged(object? sender, EventArgs e)
    {
        NotifyChanged();
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

    partial void OnColorOpacityChanged(double value)
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

/// <summary>属性カラー・アイコンカラーの1行。作りはメーターのクラスカラーの行と同じで、アイコンは白で染めて影を付ける。</summary>
public sealed class ElementColorItemViewModel : ObservableObject
{
    private readonly string _displayNameKey;

    public ElementColorItemViewModel(
        string key,
        ColorPaletteViewModel colors,
        Brush? iconMask,
        bool usesPlayerStatusAsset,
        string displayNameKey)
    {
        Key = key;
        Colors = colors;
        IconMask = iconMask;
        UsesPlayerStatusAsset = usesPlayerStatusAsset;
        _displayNameKey = displayNameKey;
    }

    public string Key { get; }

    public ColorPaletteViewModel Colors { get; }

    /// <summary>アイコンの形。塗りをこの形で抜く。絵の無い行(無分類)は null で、はてなを出す。</summary>
    public Brush? IconMask { get; }

    /// <summary>アイコンがステータス詳細の絵(物理攻撃力・魔法攻撃力)か。画面はステータス詳細と同じ大きさで出す。</summary>
    public bool UsesPlayerStatusAsset { get; }

    /// <summary>行名は属性名かタイプ名。その種類だけで出来たときの色、という意味。</summary>
    public string DisplayName => LocalizationManager.Instance.GetString(_displayNameKey);

    public void RefreshDisplayName()
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
