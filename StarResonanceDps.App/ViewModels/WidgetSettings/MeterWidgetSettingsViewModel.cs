using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

public sealed partial class MeterWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private static readonly (string Key, string LabelResourceKey, string Placeholder)[] EntityInfoFormatFieldDefinitions =
    [
        ("EntityName", "Settings_EntityInfo_Field_Name", "{EntityName}"),
        ("Level", "Settings_EntityInfo_Field_Level", "{Level}")
    ];

    private readonly WidgetKind _kind;
    private readonly Dictionary<string, MeterClassColorItemViewModel> _itemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<MeterPlayerInfoFormatField> _availablePlayerInfoFormatFields = [];
    private readonly ObservableCollection<OtherRoleSkillItemViewModel> _otherRoleSkills = [];
    private MeterWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private string _playerInfoFormatString = WidgetConfigDefaults.DefaultMeterPlayerInfoFormatString;

    [ObservableProperty]
    private MeterPlayerInfoFormatField? _selectedPlayerInfoFormatField;

    [ObservableProperty]
    private string _formatPreview = string.Empty;

    [ObservableProperty]
    private int _healthValueDisplayModeIndex = WidgetConfigDefaults.DefaultHealthValueDisplayModeIndex;

    [ObservableProperty] private bool _entityHpAsPercentage = true;
    public bool ShowsEntityHpPercentageSettings => _kind == WidgetKind.EntityList;
    partial void OnEntityHpAsPercentageChanged(bool value) => NotifyChanged();

    /// <summary>「潜在心相晶を表示」のスイッチ。</summary>
    [ObservableProperty]
    private bool _showSeasonTalent = true;

    /// <summary>「戦闘状態」の選択(<see cref="MeterWidgetSettingsConfig.CombatStateDisplayModeIndex"/>)。</summary>
    [ObservableProperty]
    private int _combatStateDisplayModeIndex = WidgetConfigDefaults.DefaultCombatStateDisplayModeIndex;

    /// <summary>「狙われるプレイヤー」の選択(<see cref="MeterWidgetSettingsConfig.TargetedPlayerDisplayModeIndex"/>)。</summary>
    [ObservableProperty]
    private int _targetedPlayerDisplayModeIndex = WidgetConfigDefaults.DefaultTargetedPlayerDisplayModeIndex;

    /// <summary>「ヘイト値」の選択(<see cref="MeterWidgetSettingsConfig.HateRankDisplayModeIndex"/>)。</summary>
    [ObservableProperty]
    private int _hateRankDisplayModeIndex = WidgetConfigDefaults.DefaultHateRankDisplayModeIndex;

    [ObservableProperty]
    private int _partyDisplayModeIndex = WidgetConfigDefaults.DefaultPartyDisplayModeIndex;

    [ObservableProperty]
    private int _entityDisplayModeIndex = WidgetConfigDefaults.DefaultEntityDisplayModeIndex;

    [ObservableProperty]
    private int _selfDisplayModeIndex = WidgetConfigDefaults.DefaultSelfDisplayModeIndex;

    [ObservableProperty]
    private int _listSortModeIndex = WidgetConfigDefaults.FirstSeenListSortModeIndex;

    [ObservableProperty]
    private double _classColorOpacity = WidgetConfigDefaults.MaxClassColorOpacity;

    public MeterWidgetSettingsViewModel(WidgetKind kind, MeterWidgetSettingsConfig? config)
    {
        _kind = kind;
        var items = new ObservableCollection<MeterClassColorItemViewModel>();
        var classColorKeys = WidgetConfigDefaults.GetClassColorKeys(_kind);
        for (var index = 0; index < classColorKeys.Count; index++)
        {
            var key = classColorKeys[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultClassColors(_kind, key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += Colors_PaletteChanged;

            // プレイヤーリストは不明が2行(クラスと潜在心相晶)あるので、クラスの不明を「不明(クラス)」と名乗る。
            var item = new MeterClassColorItemViewModel(
                key,
                colors,
                index == classColorKeys.Count - 1,
                _kind == WidgetKind.PlayerList ? "Settings_IconColors_UnknownClass" : null);
            _itemsByKey.Add(key, item);
            items.Add(item);
        }

        Items = new ReadOnlyObservableCollection<MeterClassColorItemViewModel>(items);

        var roleSkillIds = WidgetConfigDefaults.OtherRoleSkillIds;
        for (var index = 0; index < roleSkillIds.Count; index++)
        {
            _otherRoleSkills.Add(new OtherRoleSkillItemViewModel(
                roleSkillIds[index],
                index == roleSkillIds.Count - 1,
                OnOtherRoleSkillVisibilityChanged));
        }

        OtherRoleSkills = new ReadOnlyObservableCollection<OtherRoleSkillItemViewModel>(_otherRoleSkills);
        AvailablePlayerInfoFormatFields = new ReadOnlyObservableCollection<MeterPlayerInfoFormatField>(_availablePlayerInfoFormatFields);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        RebuildPlayerInfoFormatFields();
        _lastSaved = WidgetConfigDefaults.CloneNormalizedMeter(_kind, config);
        Load(_lastSaved);
    }

    public ReadOnlyObservableCollection<OtherRoleSkillItemViewModel> OtherRoleSkills { get; }

    /// <summary>「他人のロールスキル」設定を出すのはプレイヤーリストだけ。</summary>
    public bool ShowsOtherRoleSkillSettings => _kind == WidgetKind.PlayerList;

    public event Action<MeterWidgetSettingsConfig>? PreviewChanged;

    public ReadOnlyObservableCollection<MeterClassColorItemViewModel> Items { get; }

    public ReadOnlyObservableCollection<MeterPlayerInfoFormatField> AvailablePlayerInfoFormatFields { get; }

    public string PlayerInfoCustomizationTitle => LocalizationManager.Instance.GetString(
        _kind == WidgetKind.EntityList
            ? "Settings_EntityInfo_Customization"
            : "Settings_PlayerInfo_Customization");

    /// <summary>
    /// 色の節の見出し。DPS・HPS メーターはアイコンの背景を塗るので「クラスカラー」、
    /// プレイヤーリスト・エンティティリストはアイコン自体を塗るので「アイコンカラー」。
    /// </summary>
    public string ClassColorSectionTitle => LocalizationManager.Instance.GetString(
        UsesMeterClassColorIconBackground
            ? "Settings_Section_ClassColors_Title"
            : "Settings_Section_IconColors_Title");

    public bool UsesMeterClassColorIconBackground => WidgetConfigDefaults.UsesMeterClassColorOpacity(_kind);

    public bool ShowsPlayerListProfessionIcons => !UsesMeterClassColorIconBackground;

    public bool HasClassColorOpacity => UsesMeterClassColorIconBackground;

    public bool ShowsHealthValueSettings => _kind is WidgetKind.PlayerList or WidgetKind.EntityList;

    /// <summary>「潜在心相晶を表示」を出すか。プレイヤーリストだけ。</summary>
    public bool ShowsSeasonTalentSettings => _kind == WidgetKind.PlayerList;

    /// <summary>「潜在心相晶を表示」のスイッチの右に出す ON / OFF。</summary>
    public string ShowSeasonTalentStateText => LocalizationManager.Instance.GetString(
        ShowSeasonTalent ? "Settings_Switch_On" : "Settings_Switch_Off");

    /// <summary>「戦闘状態」を出すか。プレイヤーリストとエンティティリスト。</summary>
    public bool ShowsCombatStateSettings => _kind is WidgetKind.PlayerList or WidgetKind.EntityList;

    /// <summary>「戦闘状態」を 表示 / 非表示 / 自分のみ表示 の3択で出すか。プレイヤーリスト。</summary>
    public bool ShowsCombatStateSelfOnlyOption => _kind == WidgetKind.PlayerList;

    /// <summary>「戦闘状態」を 表示 / 非表示 の2択で出すか。エンティティリスト(自分の行が無い)。</summary>
    public bool ShowsCombatStateShowHideOption => _kind == WidgetKind.EntityList;

    /// <summary>
    /// 2択の「戦闘状態」の選択(0=表示 / 1=非表示)。保存する値は <see cref="CombatStateDisplayModeIndex"/> のまま。
    /// 読むときは非表示に丸める(プレイヤーリストで自分のみ表示を選んでいる間も、隠れている2択の選択に範囲外の添字を渡さない)。
    /// </summary>
    public int CombatStateShowHideIndex
    {
        get => Math.Min(CombatStateDisplayModeIndex, WidgetConfigDefaults.CombatStateDisplayHideIndex);
        set => CombatStateDisplayModeIndex = value;
    }

    public bool ShowsPartyDisplaySettings => _kind is WidgetKind.PlayerList or WidgetKind.DpsMeter or WidgetKind.HpsMeter;

    /// <summary>
    /// フィルター行の注記「履歴表示中では効果ありません」を出すか。
    /// 履歴表示中にフィルターを無視するのはメーター2種だけで、プレイヤーリストは常にライブの画面なので効き続ける。
    /// </summary>
    public bool ShowsPartyDisplayHistoryNote => _kind is WidgetKind.DpsMeter or WidgetKind.HpsMeter;

    /// <summary>エンティティリストのフィルター(すべて表示 / オブジェクト以外)を出すか。</summary>
    public bool ShowsEntityDisplaySettings => _kind == WidgetKind.EntityList;

    /// <summary>ヘイトの枠の設定(「狙われるプレイヤー」「ヘイト値」)を出すか。エンティティリストだけ。</summary>
    public bool ShowsHateDisplaySettings => _kind == WidgetKind.EntityList;

    /// <summary>「自分の強調表示」を出すのはプレイヤーリストとメーター2種。</summary>
    public bool ShowsSelfDisplaySettings =>
        _kind is WidgetKind.PlayerList or WidgetKind.DpsMeter or WidgetKind.HpsMeter;

    /// <summary>
    /// 「自分の強調表示」のスイッチ。保存する値は <see cref="SelfDisplayModeIndex"/> のままで、
    /// オンが強調表示、オフが通常表示に当たる。
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

    /// <summary>「並び替え」(発見順 / 名前順)を出すのはプレイヤーリストとエンティティリスト。</summary>
    public bool ShowsListSortSettings => WidgetConfigDefaults.UsesListSort(_kind);

    public bool HasAdditionalDisplaySettings =>
        ShowsHealthValueSettings || ShowsPartyDisplaySettings || ShowsEntityDisplaySettings
        || ShowsSelfDisplaySettings || ShowsListSortSettings;

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in Items)
        {
            item.Colors.PaletteChanged -= Colors_PaletteChanged;
        }
    }

    public MeterWidgetSettingsConfig CreateConfig()
    {
        var config = new MeterWidgetSettingsConfig
        {
            PlayerInfoFormatString = PlayerInfoFormatString ?? string.Empty,
            HealthValueDisplayModeIndex = HealthValueDisplayModeIndex,
            EntityHpAsPercentage = EntityHpAsPercentage,
            ShowSeasonTalent = ShowSeasonTalent,
            CombatStateDisplayModeIndex = CombatStateDisplayModeIndex,
            TargetedPlayerDisplayModeIndex = TargetedPlayerDisplayModeIndex,
            HateRankDisplayModeIndex = HateRankDisplayModeIndex,
            PartyDisplayModeIndex = PartyDisplayModeIndex,
            EntityDisplayModeIndex = EntityDisplayModeIndex,
            SelfDisplayModeIndex = SelfDisplayModeIndex,
            ListSortModeIndex = ListSortModeIndex,
            ClassColorOpacity = Math.Clamp(
                (int)Math.Round(ClassColorOpacity, MidpointRounding.AwayFromZero),
                WidgetConfigDefaults.MinClassColorOpacity,
                WidgetConfigDefaults.MaxClassColorOpacity),
            ClassColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var item in Items)
        {
            config.ClassColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.ClassColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        config.OtherRoleSkillVisibility = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var skill in _otherRoleSkills)
        {
            config.OtherRoleSkillVisibility[skill.Key] = skill.IsVisible;
        }

        WidgetConfigDefaults.NormalizeMeter(_kind, config);
        return config;
    }

    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreateMeterSettings(_kind));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(MeterWidgetSettingsConfig config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedMeter(_kind, config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public Color GetSelectedClassColor(string key)
    {
        return GetItem(key).Colors.SelectedColor;
    }

    public void ApplyClassColor(string key, Color color)
    {
        GetItem(key).Colors.AddOrSelect(color);
    }

    private void Load(MeterWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedMeter(_kind, config);

        _isLoading = true;
        try
        {
            foreach (var item in Items)
            {
                var colors = normalized.ClassColorPalettes.TryGetValue(item.Key, out var palette)
                    ? palette
                    : WidgetConfigDefaults.CreateDefaultClassColors(_kind, item.Key);
                var selectedIndex = normalized.ClassColorIndexes.TryGetValue(item.Key, out var index)
                    ? index
                    : WidgetConfigDefaults.MinClassColorIndex;
                item.Colors.Load(colors, selectedIndex);
            }

            foreach (var skill in _otherRoleSkills)
            {
                skill.LoadVisibility(
                    !normalized.OtherRoleSkillVisibility.TryGetValue(skill.Key, out var visible)
                    || visible);
            }

            PlayerInfoFormatString = normalized.PlayerInfoFormatString ?? string.Empty;
            HealthValueDisplayModeIndex = normalized.HealthValueDisplayModeIndex;
            EntityHpAsPercentage = normalized.EntityHpAsPercentage;
            ShowSeasonTalent = normalized.ShowSeasonTalent;
            CombatStateDisplayModeIndex = normalized.CombatStateDisplayModeIndex;
            TargetedPlayerDisplayModeIndex = normalized.TargetedPlayerDisplayModeIndex;
            HateRankDisplayModeIndex = normalized.HateRankDisplayModeIndex;
            PartyDisplayModeIndex = normalized.PartyDisplayModeIndex;
            EntityDisplayModeIndex = normalized.EntityDisplayModeIndex;
            SelfDisplayModeIndex = normalized.SelfDisplayModeIndex;
            ListSortModeIndex = normalized.ListSortModeIndex;
            ClassColorOpacity = normalized.ClassColorOpacity;
        }
        finally
        {
            _isLoading = false;
        }

        RefreshFormatPreview();
    }

    private MeterClassColorItemViewModel GetItem(string key)
    {
        return _itemsByKey.TryGetValue(key, out var item)
            ? item
            : _itemsByKey["Unknown"];
    }

    private bool SettingsEqual(MeterWidgetSettingsConfig left, MeterWidgetSettingsConfig right)
    {
        if (!string.Equals(left.PlayerInfoFormatString, right.PlayerInfoFormatString, StringComparison.Ordinal)
            || left.EntityHpAsPercentage != right.EntityHpAsPercentage
            || left.HealthValueDisplayModeIndex != right.HealthValueDisplayModeIndex
            || left.ShowSeasonTalent != right.ShowSeasonTalent
            || left.CombatStateDisplayModeIndex != right.CombatStateDisplayModeIndex
            || left.TargetedPlayerDisplayModeIndex != right.TargetedPlayerDisplayModeIndex
            || left.HateRankDisplayModeIndex != right.HateRankDisplayModeIndex
            || left.PartyDisplayModeIndex != right.PartyDisplayModeIndex
            || left.EntityDisplayModeIndex != right.EntityDisplayModeIndex
            || left.SelfDisplayModeIndex != right.SelfDisplayModeIndex
            || left.ListSortModeIndex != right.ListSortModeIndex
            || left.ClassColorOpacity != right.ClassColorOpacity)
        {
            return false;
        }

        foreach (var key in WidgetConfigDefaults.GetClassColorKeys(_kind))
        {
            if (!left.ClassColorIndexes.TryGetValue(key, out var leftIndex)
                || !right.ClassColorIndexes.TryGetValue(key, out var rightIndex)
                || leftIndex != rightIndex)
            {
                return false;
            }

            if (!left.ClassColorPalettes.TryGetValue(key, out var leftColors)
                || !right.ClassColorPalettes.TryGetValue(key, out var rightColors)
                || !leftColors.SequenceEqual(rightColors, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        foreach (var skillId in WidgetConfigDefaults.OtherRoleSkillIds)
        {
            var key = skillId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!left.OtherRoleSkillVisibility.TryGetValue(key, out var leftVisible)
                || !right.OtherRoleSkillVisibility.TryGetValue(key, out var rightVisible)
                || leftVisible != rightVisible)
            {
                return false;
            }
        }

        return true;
    }

    private void RebuildPlayerInfoFormatFields()
    {
        var selectedKey = SelectedPlayerInfoFormatField?.Key;

        _availablePlayerInfoFormatFields.Clear();
        IReadOnlyList<(string Key, string LabelResourceKey, string Placeholder)> definitions = _kind == WidgetKind.EntityList
            ? EntityInfoFormatFieldDefinitions
            : PlayerInfoFormatFormatter.FieldDefinitions;
        foreach (var definition in definitions)
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

    private void RefreshFormatPreview()
    {
        FormatPreview = _kind == WidgetKind.EntityList
            ? EntityInfoFormatFormatter.FormatPreview(PlayerInfoFormatString)
            : PlayerInfoFormatFormatter.FormatPreview(PlayerInfoFormatString);
    }

    private void Colors_PaletteChanged(object? sender, EventArgs e)
    {
        NotifyChanged();
    }

    /// <summary>
    /// スイッチを触った瞬間に呼ぶ。既存の色設定と同じ経路で、
    /// プレビュー反映と「未保存あり」の判定を更新する。
    /// </summary>
    private void OnOtherRoleSkillVisibilityChanged()
    {
        NotifyChanged();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var item in Items)
        {
            item.RefreshDisplayName();
        }

        foreach (var skill in _otherRoleSkills)
        {
            skill.RefreshMetadata();
        }

        OnPropertyChanged(nameof(PlayerInfoCustomizationTitle));
        OnPropertyChanged(nameof(ClassColorSectionTitle));
        OnPropertyChanged(nameof(ShowSeasonTalentStateText));
        OnPropertyChanged(nameof(SelfHighlightStateText));
        RebuildPlayerInfoFormatFields();
        RefreshFormatPreview();
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

    partial void OnPlayerInfoFormatStringChanged(string value)
    {
        var normalized = NormalizeFormatString(value);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            PlayerInfoFormatString = normalized;
            return;
        }

        RefreshFormatPreview();
        NotifyChanged();
    }

    partial void OnHealthValueDisplayModeIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnShowSeasonTalentChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSeasonTalentStateText));
        NotifyChanged();
    }

    partial void OnCombatStateDisplayModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CombatStateShowHideIndex));
        NotifyChanged();
    }

    partial void OnTargetedPlayerDisplayModeIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnHateRankDisplayModeIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnPartyDisplayModeIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnEntityDisplayModeIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnSelfDisplayModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(SelfHighlightEnabled));
        OnPropertyChanged(nameof(SelfHighlightStateText));
        NotifyChanged();
    }

    partial void OnListSortModeIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnClassColorOpacityChanged(double value)
    {
        NotifyChanged();
    }

    private static string NormalizeFormatString(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }
}

public sealed class MeterPlayerInfoFormatField
{
    public MeterPlayerInfoFormatField(string key, string displayName, string placeholder)
    {
        Key = key;
        DisplayName = displayName;
        Placeholder = placeholder;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public string Placeholder { get; }

    public override string ToString()
    {
        return DisplayName;
    }
}

public sealed class MeterClassColorItemViewModel : ObservableObject
{
    private const string UnknownKey = "Unknown";

    private readonly string? _unknownDisplayNameResourceKey;

    /// <param name="unknownDisplayNameResourceKey">
    /// 不明の行の名前のリソース。null ならクラスの不明と同じ名前。
    /// 不明が2行あるプレイヤーリストは「不明(クラス)」、不明を1行にまとめたプレイヤー情報は「不明」。
    /// </param>
    public MeterClassColorItemViewModel(
        string key,
        ColorPaletteViewModel colors,
        bool isLast,
        string? unknownDisplayNameResourceKey = null)
    {
        Key = key;
        Colors = colors;
        IsLast = isLast;
        _unknownDisplayNameResourceKey = unknownDisplayNameResourceKey;
    }

    public string Key { get; }

    public ColorPaletteViewModel Colors { get; }

    public bool IsLast { get; }

    /// <summary>潜在心相晶の行か。クラスアイコンの代わりに型の絵(無効・不明の行はその絵)を出す。</summary>
    public bool IsSeasonTalent => SeasonTalentIcons.IsColorKey(Key);

    /// <summary>潜在心相晶の行の型の絵の形。不明の行はクラスの不明と同じ絵、無効の行は無効の絵。</summary>
    public Brush? SeasonTalentIconMask => IsSeasonTalent ? SeasonTalentIcons.GetIconMask(Key) : null;

    /// <summary>シーズンの行か(プレイヤー情報だけ)。クラスアイコンの代わりにシーズンの絵を出す。</summary>
    public bool IsSeason => SeasonIcons.IsColorKey(Key);

    /// <summary>シーズンの行の絵の形。</summary>
    public Brush? SeasonIconMask => IsSeason ? SeasonIcons.GetIconMask(Key) : null;

    public string DisplayName => IsSeasonTalent
        ? SeasonTalentIcons.GetDisplayName(Key)
        : IsSeason
            ? SeasonIcons.GetDisplayName(Key)
            : string.Equals(Key, UnknownKey, StringComparison.OrdinalIgnoreCase) && _unknownDisplayNameResourceKey is not null
                ? LocalizationManager.Instance.GetString(_unknownDisplayNameResourceKey)
                : LocalizationManager.Instance.GetString($"Classes_{Key}");

    public void RefreshDisplayName()
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
