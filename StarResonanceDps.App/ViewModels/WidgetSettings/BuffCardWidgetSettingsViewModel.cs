// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// バフ・デバフカードの設定。表示テキストの書式と、ショートカット(メイン画面のカードの選択リストの先頭に並べる行、
/// バフ・デバフグループの「バフ・デバフ」と共有の部品)と、通知対象(効果切れを通知するバフ・デバフ、同じ部品で並べ替えなし)。
///
/// <para>
/// 倍率(<see cref="BuffCardWidgetSettingsConfig.Scales"/>)は<b>この画面では触らない</b>。
/// ウィンドウのヘッダーボタンが直接書き込むので、ここで作り直すと開いているカードの
/// 倍率を巻き戻してしまう。読み込んだ値をそのまま持ち回す。
/// </para>
/// </summary>
public sealed partial class BuffCardWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private static readonly (string Key, string LabelResourceKey, string Placeholder)[] FormatFieldDefinitions =
    [
        ("BuffName", "Settings_BuffInfo_Field_BuffName", "{BuffName}"),
        ("TargetName", "Settings_BuffInfo_Field_Name", "{TargetName}"),
        ("CasterName", "Settings_BuffInfo_Field_Caster", "{CasterName}"),
        ("Level", "Settings_BuffInfo_Field_Level", "{Level}"),
        ("NewLine", "Settings_BuffInfo_Field_NewLine", "{NewLine}")
    ];

    private readonly ObservableCollection<MeterPlayerInfoFormatField> _availableFormatFields = [];
    private BuffCardWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private string _buffInfoFormatString = WidgetConfigDefaults.DefaultBuffInfoFormatString;

    [ObservableProperty]
    private MeterPlayerInfoFormatField? _selectedFormatField;

    [ObservableProperty]
    private string _formatPreview = string.Empty;

    [ObservableProperty]
    private string _customizationTitle = string.Empty;

    public BuffCardWidgetSettingsViewModel(BuffCardWidgetSettingsConfig? config)
    {
        AvailableFormatFields = new ReadOnlyObservableCollection<MeterPlayerInfoFormatField>(_availableFormatFields);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        Shortcuts = new BuffEntryListSettingsViewModel(
            WidgetConfigDefaults.DefaultBuffCardShortcutEntries,
            "Settings_Section_BuffCardShortcuts_Title",
            "Settings_Section_BuffDebuffGroupEntries_Description",
            "Settings_ListReorderNote",
            EntryList_Changed);
        NotificationTargets = new BuffEntryListSettingsViewModel(
            WidgetConfigDefaults.DefaultBuffCardNotificationTargetEntries,
            "Settings_Section_NotificationTargets_Title",
            "Settings_Section_BuffDebuffGroupEntries_Description",
            reorderNoteResourceKey: null,
            EntryList_Changed);

        RebuildFormatFields();
        RefreshLocalizedText();
        _lastSaved = WidgetConfigDefaults.CloneNormalizedBuffCard(config);
        Load(_lastSaved);
    }

    public event Action<BuffCardWidgetSettingsConfig>? PreviewChanged;

    public ReadOnlyObservableCollection<MeterPlayerInfoFormatField> AvailableFormatFields { get; }

    /// <summary>「ショートカット」の節。並びはメイン画面のカードの選択リストの並び(並べ替えはこの一覧だけ)。</summary>
    public BuffEntryListSettingsViewModel Shortcuts { get; }

    /// <summary>「通知対象」の節。全部のカードで共通。並べ替えない。</summary>
    public BuffEntryListSettingsViewModel NotificationTargets { get; }

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    public BuffCardWidgetSettingsConfig CreateConfig()
    {
        var config = _lastSaved.Clone();
        config.BuffInfoFormatString = BuffInfoFormatString ?? string.Empty;
        config.Shortcuts = Shortcuts.CreateEntries();
        config.NotificationTargets = NotificationTargets.CreateEntries();
        return WidgetConfigDefaults.CloneNormalizedBuffCard(config);
    }

    /// <summary>
    /// 開いている間に、右クリックで通知対象の行がオン/オフになった(カードの「このバフを通知対象にする」「〜しない」)。
    /// 扱いは <see cref="SetShortcutEnabledFromWidget"/> と同じ。
    /// </summary>
    public void SetNotificationTargetEnabledFromWidget(BuffEntryKey key, bool isEnabled)
    {
        if (!key.IsValid)
        {
            return;
        }

        WidgetConfigDefaults.SetBuffEntryEnabled(
            _lastSaved.NotificationTargets ??= [],
            key,
            isEnabled,
            WidgetConfigDefaults.DefaultBuffCardNotificationTargetEntries);
        NotificationTargets.SetEntryEnabled(key, isEnabled);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// 開いている間に、右クリックでショートカットの行がオンになった(一覧・カードの「バフ・デバフカードのショートカットに追加」)。
    /// その分は既に保存されているので、保存済みの値と行の両方に同じ決まり(<see cref="WidgetConfigDefaults.SetBuffEntryEnabled"/>)で入れる
    /// (「保存」で消えず、未保存の印も付かない)。プレビューは鳴らさない(ウィジェット側は既に変えている)。
    /// </summary>
    public void SetShortcutEnabledFromWidget(BuffEntryKey key, bool isEnabled)
    {
        if (!key.IsValid)
        {
            return;
        }

        WidgetConfigDefaults.SetBuffEntryEnabled(
            _lastSaved.Shortcuts ??= [],
            key,
            isEnabled,
            WidgetConfigDefaults.DefaultBuffCardShortcutEntries);
        Shortcuts.SetEntryEnabled(key, isEnabled);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public void ResetToDefaults()
    {
        var defaults = WidgetConfigDefaults.CreateBuffCardSettings();
        defaults.Scales = new Dictionary<string, int>(_lastSaved.Scales, StringComparer.OrdinalIgnoreCase);
        Load(defaults);
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(BuffCardWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedBuffCard(config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Load(BuffCardWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedBuffCard(config);

        _isLoading = true;
        try
        {
            BuffInfoFormatString = normalized.BuffInfoFormatString ?? string.Empty;
            Shortcuts.Load(normalized.Shortcuts);
            NotificationTargets.Load(normalized.NotificationTargets);
        }
        finally
        {
            _isLoading = false;
        }

        RefreshFormatPreview();
    }

    private void RebuildFormatFields()
    {
        var selectedKey = SelectedFormatField?.Key;

        _availableFormatFields.Clear();
        foreach (var definition in FormatFieldDefinitions)
        {
            _availableFormatFields.Add(new MeterPlayerInfoFormatField(
                definition.Key,
                LocalizationManager.Instance.GetString(definition.LabelResourceKey),
                definition.Placeholder));
        }

        SelectedFormatField = _availableFormatFields
            .FirstOrDefault(field => string.Equals(field.Key, selectedKey, StringComparison.Ordinal))
            ?? _availableFormatFields.FirstOrDefault();
    }

    private void RefreshLocalizedText()
    {
        CustomizationTitle = LocalizationManager.Instance.GetString("Settings_BuffInfo_Customization");
    }

    private void RefreshFormatPreview()
    {
        // 改行はプレビューでは1行に潰す。設定欄の高さが変わらないようにするため。
        FormatPreview = BuffInfoFormatFormatter
            .FormatPreview(BuffInfoFormatString)
            .Replace(Environment.NewLine, " ", StringComparison.Ordinal);
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        RebuildFormatFields();
        RefreshLocalizedText();
        RefreshFormatPreview();
        Shortcuts.RefreshLocalizedText();
        NotificationTargets.RefreshLocalizedText();
    }

    private void RaisePreviewChanged()
    {
        PreviewChanged?.Invoke(CreateConfig());
    }

    private static bool SettingsEqual(
        BuffCardWidgetSettingsConfig left,
        BuffCardWidgetSettingsConfig right)
    {
        return string.Equals(left.BuffInfoFormatString, right.BuffInfoFormatString, StringComparison.Ordinal)
            && BuffEntryListSettingsViewModel.EntriesEqual(left.Shortcuts, right.Shortcuts)
            && BuffEntryListSettingsViewModel.EntriesEqual(left.NotificationTargets, right.NotificationTargets);
    }

    partial void OnBuffInfoFormatStringChanged(string value)
    {
        RefreshFormatPreview();

        if (_isLoading)
        {
            return;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    private void EntryList_Changed()
    {
        if (_isLoading)
        {
            return;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }
}
