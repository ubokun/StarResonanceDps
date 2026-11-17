// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// マップの設定。表示設定「プレイヤー」「エンティティ」(印のフィルター)・「プレイヤー名」「エンティティ名」(印の名前を出す相手)・
/// 「交戦中の名前非表示」(戦闘状態の相手の名前)・「交戦中のプレイヤーを簡略化表示」「自分を常に簡略化表示」(プレイヤーの印の形)と「マップの不透明度」、
/// アイコンカラー(作りはエンティティ情報のアイコンカラーと同じで、行もエンティティリストと同じ)。
/// </summary>
public sealed partial class MapWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<string, MeterClassColorItemViewModel> _classColorItemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private MapWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    /// <summary>設定「プレイヤー」(<see cref="MapWidgetSettingsConfig.PartyDisplayModeIndex"/>)。選択肢はプレイヤーリストのフィルターと同じ。</summary>
    [ObservableProperty]
    private int _partyDisplayModeIndex = WidgetConfigDefaults.DefaultPartyDisplayModeIndex;

    /// <summary>設定「エンティティ」(<see cref="MapWidgetSettingsConfig.EntityDisplayModeIndex"/>)。選択肢はエンティティリストのフィルターと同じ。</summary>
    [ObservableProperty]
    private int _entityDisplayModeIndex = WidgetConfigDefaults.DefaultEntityDisplayModeIndex;

    /// <summary>設定「プレイヤー名」(<see cref="MapWidgetSettingsConfig.PlayerNamePartyDisplayModeIndex"/>)。選択肢の並びはフィルター「プレイヤー」と同じ。</summary>
    [ObservableProperty]
    private int _playerNamePartyDisplayModeIndex = WidgetConfigDefaults.DefaultMapPlayerNamePartyDisplayModeIndex;

    /// <summary>設定「エンティティ名」(<see cref="MapWidgetSettingsConfig.EntityNameDisplayModeIndex"/>)。</summary>
    [ObservableProperty]
    private int _entityNameDisplayModeIndex = WidgetConfigDefaults.DefaultMapEntityNameDisplayModeIndex;

    /// <summary>設定「交戦中の名前非表示」(<see cref="MapWidgetSettingsConfig.HideNamesInCombatModeIndex"/>)。</summary>
    [ObservableProperty]
    private int _hideNamesInCombatModeIndex = WidgetConfigDefaults.DefaultMapHideNamesInCombatModeIndex;

    /// <summary>設定「交戦中のプレイヤーを簡略化表示」(<see cref="MapWidgetSettingsConfig.SimplifiesPlayersInCombat"/>)。</summary>
    [ObservableProperty]
    private bool _simplifiesPlayersInCombat = WidgetConfigDefaults.DefaultMapSimplifiesPlayersInCombat;

    /// <summary>設定「自分を常に簡略化表示」(<see cref="MapWidgetSettingsConfig.AlwaysSimplifiesSelf"/>)。</summary>
    [ObservableProperty]
    private bool _alwaysSimplifiesSelf = WidgetConfigDefaults.DefaultMapAlwaysSimplifiesSelf;

    /// <summary>
    /// 設定「マップの不透明度」(<see cref="MapWidgetSettingsConfig.MapOpacity"/>)。スライダーの値なので実数で持ち、設定にするときに整数へ丸める。
    /// </summary>
    [ObservableProperty]
    private double _mapOpacity = WidgetConfigDefaults.DefaultMapOpacity;

    public MapWidgetSettingsViewModel(MapWidgetSettingsConfig? config)
    {
        var classItems = new ObservableCollection<MeterClassColorItemViewModel>();
        var classColorKeys = WidgetConfigDefaults.GetClassColorKeys(WidgetKind.Map);
        for (var index = 0; index < classColorKeys.Count; index++)
        {
            var key = classColorKeys[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultClassColors(WidgetKind.Map, key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += Colors_PaletteChanged;

            var item = new MeterClassColorItemViewModel(key, colors, index == classColorKeys.Count - 1);
            _classColorItemsByKey.Add(key, item);
            classItems.Add(item);
        }

        ClassColorItems = new ReadOnlyObservableCollection<MeterClassColorItemViewModel>(classItems);

        _lastSaved = WidgetConfigDefaults.CloneNormalizedMap(config);
        Load(_lastSaved);

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    public event Action<MapWidgetSettingsConfig>? PreviewChanged;

    /// <summary>アイコンカラーの行。行ごとに色見本(最大5枠)と、選んでいる枠を持つ。</summary>
    public ReadOnlyObservableCollection<MeterClassColorItemViewModel> ClassColorItems { get; }

    public string ClassColorSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_IconColors_Title");

    /// <summary>「交戦中のプレイヤーを簡略化表示」のスイッチの右に出す ON / OFF。</summary>
    public string SimplifiesPlayersInCombatStateText => LocalizationManager.Instance.GetString(
        SimplifiesPlayersInCombat ? "Settings_Switch_On" : "Settings_Switch_Off");

    /// <summary>「自分を常に簡略化表示」のスイッチの右に出す ON / OFF。</summary>
    public string AlwaysSimplifiesSelfStateText => LocalizationManager.Instance.GetString(
        AlwaysSimplifiesSelf ? "Settings_Switch_On" : "Settings_Switch_Off");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in ClassColorItems)
        {
            item.Colors.PaletteChanged -= Colors_PaletteChanged;
        }
    }

    public MapWidgetSettingsConfig CreateConfig()
    {
        var config = new MapWidgetSettingsConfig
        {
            PartyDisplayModeIndex = PartyDisplayModeIndex,
            EntityDisplayModeIndex = EntityDisplayModeIndex,
            PlayerNamePartyDisplayModeIndex = PlayerNamePartyDisplayModeIndex,
            EntityNameDisplayModeIndex = EntityNameDisplayModeIndex,
            HideNamesInCombatModeIndex = HideNamesInCombatModeIndex,
            SimplifiesPlayersInCombat = SimplifiesPlayersInCombat,
            AlwaysSimplifiesSelf = AlwaysSimplifiesSelf,
            MapOpacity = Math.Clamp(
                (int)Math.Round(MapOpacity, MidpointRounding.AwayFromZero),
                WidgetConfigDefaults.MinMapOpacity,
                WidgetConfigDefaults.MaxMapOpacity),
            ClassColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var item in ClassColorItems)
        {
            config.ClassColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.ClassColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        return WidgetConfigDefaults.CloneNormalizedMap(config);
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
        Load(WidgetConfigDefaults.CreateMapSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(MapWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedMap(config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Load(MapWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedMap(config);

        _isLoading = true;
        try
        {
            PartyDisplayModeIndex = normalized.PartyDisplayModeIndex;
            EntityDisplayModeIndex = normalized.EntityDisplayModeIndex;
            PlayerNamePartyDisplayModeIndex = normalized.PlayerNamePartyDisplayModeIndex;
            EntityNameDisplayModeIndex = normalized.EntityNameDisplayModeIndex;
            HideNamesInCombatModeIndex = normalized.HideNamesInCombatModeIndex;
            SimplifiesPlayersInCombat = normalized.SimplifiesPlayersInCombat;
            AlwaysSimplifiesSelf = normalized.AlwaysSimplifiesSelf;
            MapOpacity = normalized.MapOpacity;

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

    partial void OnPartyDisplayModeIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnEntityDisplayModeIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnPlayerNamePartyDisplayModeIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnEntityNameDisplayModeIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnHideNamesInCombatModeIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnSimplifiesPlayersInCombatChanged(bool value)
    {
        OnPropertyChanged(nameof(SimplifiesPlayersInCombatStateText));
        NotifyChanged();
    }

    partial void OnAlwaysSimplifiesSelfChanged(bool value)
    {
        OnPropertyChanged(nameof(AlwaysSimplifiesSelfStateText));
        NotifyChanged();
    }

    partial void OnMapOpacityChanged(double value)
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

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var item in ClassColorItems)
        {
            item.RefreshDisplayName();
        }

        OnPropertyChanged(nameof(ClassColorSectionTitle));
        OnPropertyChanged(nameof(SimplifiesPlayersInCombatStateText));
        OnPropertyChanged(nameof(AlwaysSimplifiesSelfStateText));
    }

    private static bool SettingsEqual(MapWidgetSettingsConfig left, MapWidgetSettingsConfig right)
    {
        if (left.PartyDisplayModeIndex != right.PartyDisplayModeIndex
            || left.EntityDisplayModeIndex != right.EntityDisplayModeIndex
            || left.PlayerNamePartyDisplayModeIndex != right.PlayerNamePartyDisplayModeIndex
            || left.EntityNameDisplayModeIndex != right.EntityNameDisplayModeIndex
            || left.HideNamesInCombatModeIndex != right.HideNamesInCombatModeIndex
            || left.SimplifiesPlayersInCombat != right.SimplifiesPlayersInCombat
            || left.AlwaysSimplifiesSelf != right.AlwaysSimplifiesSelf
            || left.MapOpacity != right.MapOpacity)
        {
            return false;
        }

        foreach (var key in WidgetConfigDefaults.GetClassColorKeys(WidgetKind.Map))
        {
            if (left.ClassColorIndexes[key] != right.ClassColorIndexes[key]
                || !left.ClassColorPalettes[key].SequenceEqual(right.ClassColorPalettes[key], StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
