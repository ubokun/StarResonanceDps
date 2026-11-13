// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// 被ダメログの設定。「プレイヤー名」(書式)と「自分の強調表示」とアイコンカラーは蘇生リスト(<see cref="ReviveListWidgetSettingsViewModel"/>)と同じ作り、
/// 「スキル名」(右の書式)はプレイヤー名と同じ作りで差し込める項目が スキル名 / エンティティ名 / タイプ(<see cref="DamageTakenLogHitInfoFormatter"/>)、
/// 「戦闘不能の原因を表示」は自分の強調表示と同じスイッチ。
/// 「フィルター設定」はタイムラインから移したもの(すべて表示 / 自傷・フレンドリーファイア以外)。
/// 「最新の被ダメのみ表示」は自分の強調表示と同じスイッチで、ウィジェットのヘッダーのボタンでも変わり、そのときは <see cref="SetShowsLatestHitOnlyFromWidget"/> で受ける。
/// 節「属性カラー」(バーの色)はスキル詳細と同じ <see cref="ElementColorWidgetSettingsViewModel"/> が持つ。
/// </summary>
public sealed partial class DamageTakenLogWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private readonly ObservableCollection<MeterPlayerInfoFormatField> _availablePlayerInfoFormatFields = [];
    private readonly ObservableCollection<MeterPlayerInfoFormatField> _availableHitInfoFormatFields = [];
    private readonly Dictionary<string, MeterClassColorItemViewModel> _classColorItemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private DamageTakenLogWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private string _playerInfoFormatString = WidgetConfigDefaults.DefaultDamageTakenLogPlayerInfoFormatString;

    [ObservableProperty]
    private MeterPlayerInfoFormatField? _selectedPlayerInfoFormatField;

    [ObservableProperty]
    private string _formatPreview = string.Empty;

    [ObservableProperty]
    private int _selfDisplayModeIndex = WidgetConfigDefaults.DefaultSelfDisplayModeIndex;

    [ObservableProperty]
    private string _hitInfoFormatString = WidgetConfigDefaults.DefaultDamageTakenLogHitInfoFormatString;

    [ObservableProperty]
    private MeterPlayerInfoFormatField? _selectedHitInfoFormatField;

    [ObservableProperty]
    private string _hitInfoFormatPreview = string.Empty;

    /// <summary>「戦闘不能の原因を表示」のスイッチ。オンなら、その被弾で倒れた行の被ダメ値の後ろに「(戦闘不能)」を出す。</summary>
    [ObservableProperty]
    private bool _showsDefeatCause = WidgetConfigDefaults.DefaultDamageTakenLogShowsDefeatCause;

    /// <summary>「フィルター設定」の選択(<see cref="DamageTakenLogWidgetSettingsConfig.AttackerFilterIndex"/>)。</summary>
    [ObservableProperty]
    private int _attackerFilterIndex = WidgetConfigDefaults.DefaultDamageTakenLogAttackerFilterIndex;

    /// <summary>「最新の被ダメのみ表示」のスイッチ。オンなら1人1行でその人の一番新しい被弾だけを出す。</summary>
    [ObservableProperty]
    private bool _showsLatestHitOnly = WidgetConfigDefaults.DefaultDamageTakenLogShowsLatestHitOnly;

    public DamageTakenLogWidgetSettingsViewModel(DamageTakenLogWidgetSettingsConfig? config)
    {
        AvailablePlayerInfoFormatFields = new ReadOnlyObservableCollection<MeterPlayerInfoFormatField>(_availablePlayerInfoFormatFields);
        AvailableHitInfoFormatFields = new ReadOnlyObservableCollection<MeterPlayerInfoFormatField>(_availableHitInfoFormatFields);
        RebuildFormatFields();

        var classItems = new ObservableCollection<MeterClassColorItemViewModel>();
        var classColorKeys = WidgetConfigDefaults.GetClassColorKeys(WidgetKind.DamageTakenLog);
        for (var index = 0; index < classColorKeys.Count; index++)
        {
            var key = classColorKeys[index];
            var colors = new ColorPaletteViewModel(
                WidgetConfigDefaults.CreateDefaultClassColors(WidgetKind.DamageTakenLog, key),
                WidgetConfigDefaults.MaxPaletteColorCount);
            colors.PaletteChanged += Colors_PaletteChanged;

            var item = new MeterClassColorItemViewModel(key, colors, index == classColorKeys.Count - 1);
            _classColorItemsByKey.Add(key, item);
            classItems.Add(item);
        }

        ClassColorItems = new ReadOnlyObservableCollection<MeterClassColorItemViewModel>(classItems);

        _lastSaved = WidgetConfigDefaults.CloneNormalizedDamageTakenLog(config);
        Load(_lastSaved);

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    public event Action<DamageTakenLogWidgetSettingsConfig>? PreviewChanged;

    public ReadOnlyObservableCollection<MeterPlayerInfoFormatField> AvailablePlayerInfoFormatFields { get; }

    /// <summary>「スキル名」の書式に差し込める項目(スキル名・エンティティ名・タイプ)。</summary>
    public ReadOnlyObservableCollection<MeterPlayerInfoFormatField> AvailableHitInfoFormatFields { get; }

    /// <summary>アイコンカラーの行。職ごとに色見本(最大5枠)と、選んでいる枠を持つ。</summary>
    public ReadOnlyObservableCollection<MeterClassColorItemViewModel> ClassColorItems { get; }

    public string ClassColorSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_IconColors_Title");

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

    /// <summary>「戦闘不能の原因を表示」のスイッチの右に出す ON / OFF。</summary>
    public string ShowsDefeatCauseStateText => LocalizationManager.Instance.GetString(
        ShowsDefeatCause ? "Settings_Switch_On" : "Settings_Switch_Off");

    /// <summary>「最新の被ダメのみ表示」のスイッチの右に出す ON / OFF。</summary>
    public string ShowsLatestHitOnlyStateText => LocalizationManager.Instance.GetString(
        ShowsLatestHitOnly ? "Settings_Switch_On" : "Settings_Switch_Off");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;

        foreach (var item in ClassColorItems)
        {
            item.Colors.PaletteChanged -= Colors_PaletteChanged;
        }
    }

    public DamageTakenLogWidgetSettingsConfig CreateConfig()
    {
        var config = new DamageTakenLogWidgetSettingsConfig
        {
            PlayerInfoFormatString = PlayerInfoFormatString ?? string.Empty,
            SelfDisplayModeIndex = SelfDisplayModeIndex,
            HitInfoFormatString = HitInfoFormatString ?? string.Empty,
            ShowsDefeatCause = ShowsDefeatCause,
            AttackerFilterIndex = AttackerFilterIndex,
            ShowsLatestHitOnly = ShowsLatestHitOnly,
            ClassColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var item in ClassColorItems)
        {
            config.ClassColorIndexes[item.Key] = item.Colors.SelectedIndex;
            config.ClassColorPalettes[item.Key] = [.. item.Colors.GetHexColors()];
        }

        return WidgetConfigDefaults.CloneNormalizedDamageTakenLog(config);
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
        Load(WidgetConfigDefaults.CreateDamageTakenLogSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(DamageTakenLogWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedDamageTakenLog(config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// ウィジェットのヘッダーのボタンで変わった「最新の被ダメのみ表示」を受ける。ウィジェットの側で保存するので、
    /// 保存済みの控えと画面の値の両方を合わせる(この項目を未保存に数えず、取り消しで戻さず、保存で古い値を書き戻さない)。
    /// ほかの項目の未保存の変更はそのまま残す。作りは推移グラフの横軸の長さ(<see cref="MetricGraphWidgetSettingsViewModel.SetVisibleSecondsFromWidget"/>)と同じ。
    /// </summary>
    public void SetShowsLatestHitOnlyFromWidget(bool showsLatestHitOnly)
    {
        _lastSaved.ShowsLatestHitOnly = showsLatestHitOnly;

        _isLoading = true;
        try
        {
            ShowsLatestHitOnly = showsLatestHitOnly;
        }
        finally
        {
            _isLoading = false;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Load(DamageTakenLogWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedDamageTakenLog(config);

        _isLoading = true;
        try
        {
            PlayerInfoFormatString = normalized.PlayerInfoFormatString ?? WidgetConfigDefaults.DefaultDamageTakenLogPlayerInfoFormatString;
            SelfDisplayModeIndex = normalized.SelfDisplayModeIndex;
            HitInfoFormatString = normalized.HitInfoFormatString ?? WidgetConfigDefaults.DefaultDamageTakenLogHitInfoFormatString;
            ShowsDefeatCause = normalized.ShowsDefeatCause;
            AttackerFilterIndex = normalized.AttackerFilterIndex;
            ShowsLatestHitOnly = normalized.ShowsLatestHitOnly;

            foreach (var item in ClassColorItems)
            {
                item.Colors.Load(normalized.ClassColorPalettes[item.Key], normalized.ClassColorIndexes[item.Key]);
            }
        }
        finally
        {
            _isLoading = false;
        }

        RefreshFormatPreviews();
    }

    partial void OnPlayerInfoFormatStringChanged(string value)
    {
        var normalized = NormalizeSingleLine(value);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            PlayerInfoFormatString = normalized;
            return;
        }

        RefreshFormatPreviews();
        NotifyChanged();
    }

    partial void OnHitInfoFormatStringChanged(string value)
    {
        var normalized = NormalizeSingleLine(value);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            HitInfoFormatString = normalized;
            return;
        }

        RefreshFormatPreviews();
        NotifyChanged();
    }

    partial void OnSelfDisplayModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(SelfHighlightEnabled));
        OnPropertyChanged(nameof(SelfHighlightStateText));
        NotifyChanged();
    }

    partial void OnShowsDefeatCauseChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowsDefeatCauseStateText));
        NotifyChanged();
    }

    partial void OnAttackerFilterIndexChanged(int value)
    {
        NotifyChanged();
    }

    partial void OnShowsLatestHitOnlyChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowsLatestHitOnlyStateText));
        NotifyChanged();
    }

    /// <summary>書式は1行。貼り付けた改行は空白にする(プレイヤーリストと同じ)。</summary>
    private static string NormalizeSingleLine(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
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

    private void RefreshFormatPreviews()
    {
        FormatPreview = PlayerInfoFormatFormatter.FormatPreview(PlayerInfoFormatString);
        HitInfoFormatPreview = DamageTakenLogHitInfoFormatter.FormatPreview(HitInfoFormatString);
    }

    private void RebuildFormatFields()
    {
        var selectedPlayerKey = SelectedPlayerInfoFormatField?.Key;
        _availablePlayerInfoFormatFields.Clear();
        foreach (var definition in PlayerInfoFormatFormatter.FieldDefinitions)
        {
            _availablePlayerInfoFormatFields.Add(new MeterPlayerInfoFormatField(
                definition.Key,
                LocalizationManager.Instance.GetString(definition.LabelResourceKey),
                definition.Placeholder));
        }

        SelectedPlayerInfoFormatField = _availablePlayerInfoFormatFields
            .FirstOrDefault(field => string.Equals(field.Key, selectedPlayerKey, StringComparison.Ordinal))
            ?? _availablePlayerInfoFormatFields.FirstOrDefault();

        var selectedHitKey = SelectedHitInfoFormatField?.Key;
        _availableHitInfoFormatFields.Clear();
        foreach (var definition in DamageTakenLogHitInfoFormatter.FieldDefinitions)
        {
            _availableHitInfoFormatFields.Add(new MeterPlayerInfoFormatField(
                definition.Key,
                LocalizationManager.Instance.GetString(definition.LabelResourceKey),
                definition.Placeholder));
        }

        SelectedHitInfoFormatField = _availableHitInfoFormatFields
            .FirstOrDefault(field => string.Equals(field.Key, selectedHitKey, StringComparison.Ordinal))
            ?? _availableHitInfoFormatFields.FirstOrDefault();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        RebuildFormatFields();
        RefreshFormatPreviews();

        foreach (var item in ClassColorItems)
        {
            item.RefreshDisplayName();
        }

        OnPropertyChanged(nameof(ClassColorSectionTitle));
        OnPropertyChanged(nameof(SelfHighlightStateText));
        OnPropertyChanged(nameof(ShowsDefeatCauseStateText));
        OnPropertyChanged(nameof(ShowsLatestHitOnlyStateText));
    }

    private static bool SettingsEqual(DamageTakenLogWidgetSettingsConfig left, DamageTakenLogWidgetSettingsConfig right)
    {
        if (!string.Equals(left.PlayerInfoFormatString, right.PlayerInfoFormatString, StringComparison.Ordinal)
            || left.SelfDisplayModeIndex != right.SelfDisplayModeIndex
            || !string.Equals(left.HitInfoFormatString, right.HitInfoFormatString, StringComparison.Ordinal)
            || left.ShowsDefeatCause != right.ShowsDefeatCause
            || left.AttackerFilterIndex != right.AttackerFilterIndex
            || left.ShowsLatestHitOnly != right.ShowsLatestHitOnly)
        {
            return false;
        }

        foreach (var key in WidgetConfigDefaults.GetClassColorKeys(WidgetKind.DamageTakenLog))
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
