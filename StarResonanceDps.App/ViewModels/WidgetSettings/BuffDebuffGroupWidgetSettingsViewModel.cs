// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// バフ・デバフグループの設定。ゲージの長さ、ゲージの色(バフカラーとデバフカラーの2節、バフ・デバフ一覧と共有の部品)、
/// ウィジェットに出すバフ・デバフの行(「バフ・デバフ」の一覧、バフ・デバフカードのショートカットと共有の部品)、
/// 効果切れを通知するバフ・デバフ(「通知対象」の一覧、同じ部品で並べ替えなし)。
///
/// <para>
/// 右クリックで足した行はオフのまま保存すると消え、既定の行(<see cref="WidgetConfigDefaults.DefaultBuffDebuffGroupEntries"/>、
/// <see cref="WidgetConfigDefaults.DefaultBuffDebuffGroupNotificationTargetEntries"/>)は消えない。
/// </para>
/// </summary>
public sealed partial class BuffDebuffGroupWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private BuffDebuffGroupWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    [ObservableProperty]
    private int _gaugeLengthIndex = WidgetConfigDefaults.DefaultBuffDebuffGroupGaugeLengthIndex;

    public BuffDebuffGroupWidgetSettingsViewModel(BuffDebuffGroupWidgetSettingsConfig? config)
    {
        BuffGaugeColors = new BuffGaugeColorSettingsViewModel(PlayerBuffListKind.Buff);
        BuffGaugeColors.Changed += NotifyChanged;
        DebuffGaugeColors = new BuffGaugeColorSettingsViewModel(PlayerBuffListKind.Debuff);
        DebuffGaugeColors.Changed += NotifyChanged;
        Entries = new BuffEntryListSettingsViewModel(
            WidgetConfigDefaults.DefaultBuffDebuffGroupEntries,
            "Settings_Section_BuffDebuffGroupEntries_Title",
            "Settings_Section_BuffDebuffGroupEntries_Description",
            "Settings_RowReorderNote",
            NotifyChanged);
        NotificationTargets = new BuffEntryListSettingsViewModel(
            WidgetConfigDefaults.DefaultBuffDebuffGroupNotificationTargetEntries,
            "Settings_Section_NotificationTargets_Title",
            "Settings_Section_BuffDebuffGroupEntries_Description",
            reorderNoteResourceKey: null,
            NotifyChanged);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        _lastSaved = WidgetConfigDefaults.CloneNormalizedBuffDebuffGroup(config);
        Load(_lastSaved);
    }

    public event Action<BuffDebuffGroupWidgetSettingsConfig>? PreviewChanged;

    /// <summary>「バフカラー」の節。バフの行のゲージの色。</summary>
    public BuffGaugeColorSettingsViewModel BuffGaugeColors { get; }

    /// <summary>「デバフカラー」の節。デバフの行のゲージの色。</summary>
    public BuffGaugeColorSettingsViewModel DebuffGaugeColors { get; }

    /// <summary>「バフ・デバフ」の節。並びはウィジェットの行の並び。</summary>
    public BuffEntryListSettingsViewModel Entries { get; }

    /// <summary>「通知対象」の節。並べ替えない。</summary>
    public BuffEntryListSettingsViewModel NotificationTargets { get; }

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
        BuffGaugeColors.Changed -= NotifyChanged;
        BuffGaugeColors.Dispose();
        DebuffGaugeColors.Changed -= NotifyChanged;
        DebuffGaugeColors.Dispose();
    }

    public BuffDebuffGroupWidgetSettingsConfig CreateConfig()
    {
        var config = new BuffDebuffGroupWidgetSettingsConfig
        {
            GaugeLengthIndex = GaugeLengthIndex,
            BuffGaugeColors = BuffGaugeColors.CreateConfig(),
            DebuffGaugeColors = DebuffGaugeColors.CreateConfig(),
            Entries = Entries.CreateEntries(),
            NotificationTargets = NotificationTargets.CreateEntries()
        };

        return WidgetConfigDefaults.CloneNormalizedBuffDebuffGroup(config);
    }

    /// <summary>既定に戻す。行も既定の7行と既定の並び・オン/オフに戻す(ほかの項目と同じく保存前の値。確定は「保存」)。</summary>
    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreateBuffDebuffGroupSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    /// <summary>保存した。保存した値で行を作り直す(オフのまま保存した足した行が消える)。</summary>
    public void MarkSaved(BuffDebuffGroupWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedBuffDebuffGroup(config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// 開いている間に、右クリックで行がオン/オフになった(一覧・カードの「グループに追加」、グループの窓の「グループから削除」)。
    /// その分は既に保存されているので、保存済みの値と行の両方に同じ決まり(<see cref="WidgetConfigDefaults.SetBuffEntryEnabled"/>)で入れる
    /// (「保存」で消えず、未保存の印も付かない)。プレビューは鳴らさない(ウィジェット側は既に変えている)。
    /// </summary>
    public void SetEntryEnabledFromWidget(BuffEntryKey key, bool isEnabled)
    {
        if (!key.IsValid)
        {
            return;
        }

        WidgetConfigDefaults.SetBuffEntryEnabled(
            _lastSaved.Entries ??= [],
            key,
            isEnabled,
            WidgetConfigDefaults.DefaultBuffDebuffGroupEntries);
        Entries.SetEntryEnabled(key, isEnabled);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// 開いている間に、右クリックで通知対象の行がオン/オフになった(グループの窓の「このバフを通知対象にする」「〜しない」)。
    /// 扱いは <see cref="SetEntryEnabledFromWidget"/> と同じ。
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
            WidgetConfigDefaults.DefaultBuffDebuffGroupNotificationTargetEntries);
        NotificationTargets.SetEntryEnabled(key, isEnabled);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// 開いている間に、バフ・デバフグループの窓で行が並べ替えられた。行を同じ順に並べ直す(作り直さない)。
    /// 保存前の並びになるので未保存の印は付くが、プレビューは鳴らさない(ウィジェット側は既にその並び)。
    /// </summary>
    public void ApplyEntryOrderFromWidget(IReadOnlyList<BuffEntryKey> order)
    {
        Entries.ApplyOrder(order);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Load(BuffDebuffGroupWidgetSettingsConfig config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedBuffDebuffGroup(config);

        _isLoading = true;
        try
        {
            GaugeLengthIndex = normalized.GaugeLengthIndex;
            BuffGaugeColors.Load(normalized.BuffGaugeColors);
            DebuffGaugeColors.Load(normalized.DebuffGaugeColors);
            Entries.Load(normalized.Entries);
            NotificationTargets.Load(normalized.NotificationTargets);
        }
        finally
        {
            _isLoading = false;
        }
    }

    partial void OnGaugeLengthIndexChanged(int value)
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
        Entries.RefreshLocalizedText();
        NotificationTargets.RefreshLocalizedText();
    }

    private static bool SettingsEqual(
        BuffDebuffGroupWidgetSettingsConfig left,
        BuffDebuffGroupWidgetSettingsConfig right)
    {
        return left.GaugeLengthIndex == right.GaugeLengthIndex
            && left.BuffGaugeColors is not null
            && right.BuffGaugeColors is not null
            && left.DebuffGaugeColors is not null
            && right.DebuffGaugeColors is not null
            && BuffGaugeColorSettingsViewModel.ConfigEquals(left.BuffGaugeColors, right.BuffGaugeColors)
            && BuffGaugeColorSettingsViewModel.ConfigEquals(left.DebuffGaugeColors, right.DebuffGaugeColors)
            && BuffEntryListSettingsViewModel.EntriesEqual(left.Entries, right.Entries)
            && BuffEntryListSettingsViewModel.EntriesEqual(left.NotificationTargets, right.NotificationTargets);
    }
}
