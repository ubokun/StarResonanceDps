// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// バフ稼働率の設定。表示設定の「常駐バフの表示」「料理・薬剤の表示」は被ダメログの「最新の被ダメのみ表示」と同じスイッチ、
/// 「1秒の幅」は推移グラフの「横軸の長さ」(<see cref="MetricGraphWidgetSettingsViewModel"/>)と同じスライダーの作り。
/// 優先表示リスト・非表示リストはバフ・デバフ一覧(<see cref="BuffListWidgetSettingsViewModel"/>)と同じ作り、
/// バーの色は推移グラフと共有の部品(<see cref="GraphColorSettingsViewModel"/>)。
/// 非表示・優先表示はウィジェットの窓でも変わり、そのときは
/// <see cref="AddHiddenBuffFromWidget"/>・<see cref="SetPriorityBuffEnabledFromWidget"/> で受ける。
/// </summary>
public sealed partial class BuffUptimeWidgetSettingsViewModel : ObservableObject, IDisposable
{
    private readonly ObservableCollection<BuffSettingRowViewModel> _hiddenBuffItems = [];
    private BuffUptimeWidgetSettingsConfig _lastSaved;
    private bool _isLoading;

    /// <summary>設定「常駐バフの表示」。</summary>
    [ObservableProperty]
    private bool _showsPermanentBuffs = WidgetConfigDefaults.DefaultBuffUptimeShowsPermanentBuffs;

    /// <summary>設定「料理・薬剤の表示」。</summary>
    [ObservableProperty]
    private bool _showsCuisinePotion = WidgetConfigDefaults.DefaultBuffUptimeShowsCuisinePotion;

    /// <summary>1秒の幅のスライダーの値。保存する値は <see cref="CreateConfig"/> で整数に丸めて範囲に収める。</summary>
    [ObservableProperty]
    private double _pixelsPerSecond = WidgetConfigDefaults.DefaultBuffUptimePixelsPerSecond;

    public BuffUptimeWidgetSettingsViewModel(BuffUptimeWidgetSettingsConfig? config)
    {
        PriorityBuffs = new BuffEntryListSettingsViewModel(
            WidgetConfigDefaults.DefaultBuffUptimePriorityBuffEntries,
            "Settings_Section_PriorityBuffs_Title",
            "Settings_Section_PriorityBuffs_Description",
            "Settings_ListReorderNote",
            NotifyChanged);
        HiddenBuffItems = new ReadOnlyObservableCollection<BuffSettingRowViewModel>(_hiddenBuffItems);

        GraphColors = new GraphColorSettingsViewModel(WidgetConfigDefaults.GetMetricGraphColorDefaultSource(WidgetKind.BuffUptime), usesBarSwatch: true);
        GraphColors.Changed += NotifyChanged;

        _lastSaved = WidgetConfigDefaults.CloneNormalizedBuffUptime(config);
        Load(_lastSaved);

        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    public event Action<BuffUptimeWidgetSettingsConfig>? PreviewChanged;

    /// <summary>1秒の幅のスライダーの最小。値の束縛より先に決まるよう、画面からは <c>x:Static</c> で読む。</summary>
    public const double PixelsPerSecondSliderMinimum = WidgetConfigDefaults.MinBuffUptimePixelsPerSecond;

    /// <summary>1秒の幅のスライダーの最大。値の束縛より先に決まるよう、画面からは <c>x:Static</c> で読む。</summary>
    public const double PixelsPerSecondSliderMaximum = WidgetConfigDefaults.MaxBuffUptimePixelsPerSecond;

    /// <summary>スライダーの右に出す今の1秒の幅。</summary>
    public string PixelsPerSecondText => FormatPixelsPerSecond(RoundPixelsPerSecond(PixelsPerSecond));

    /// <summary>
    /// いちばん長い1秒の幅の文字。スライダーの右の欄に見えない文字として重ね、欄の幅をこの文字に合わせる
    /// (値が変わっても溝の長さが変わらない)。
    /// </summary>
    public string PixelsPerSecondMaxText => FormatPixelsPerSecond(WidgetConfigDefaults.MaxBuffUptimePixelsPerSecond);

    /// <summary>「常駐バフの表示」のスイッチの右に出す ON / OFF。</summary>
    public string ShowsPermanentBuffsStateText => GetSwitchStateText(ShowsPermanentBuffs);

    /// <summary>「料理・薬剤の表示」のスイッチの右に出す ON / OFF。</summary>
    public string ShowsCuisinePotionStateText => GetSwitchStateText(ShowsCuisinePotion);

    /// <summary>「優先表示リスト」の節。並びは一覧の先頭からの並び(並べ替えはこの一覧だけ)。</summary>
    public BuffEntryListSettingsViewModel PriorityBuffs { get; }

    /// <summary>
    /// 非表示のバフの行。並びは非表示にした順。スイッチがオンなら非表示のまま、オフにすると保存前のプレビューで一覧に出て、
    /// 保存すると行が消える(バフ・デバフ一覧と同じ)。
    /// </summary>
    public ReadOnlyObservableCollection<BuffSettingRowViewModel> HiddenBuffItems { get; }

    /// <summary>非表示のバフが1つでもあるか。無いときは枠ごと出さない。</summary>
    public bool HasHiddenBuffs => _hiddenBuffItems.Count > 0;

    public string HiddenBuffSectionTitle => LocalizationManager.Instance.GetString("Settings_Section_HiddenBuffs_Title");

    /// <summary>見出しの下の説明。行を右クリックして非表示にできることを書く。</summary>
    public string HiddenBuffSectionDescription => LocalizationManager.Instance.GetString("Settings_Section_HiddenBuffs_Description");

    /// <summary>グラフカラーの節(バーの色)。</summary>
    public GraphColorSettingsViewModel GraphColors { get; }

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
        GraphColors.Changed -= NotifyChanged;
        GraphColors.Dispose();
    }

    /// <summary>非表示リストは、スイッチがオンの行だけを残す。優先表示リストは足した行のうちスイッチがオフの行を落とす(<see cref="BuffEntryListSettingsViewModel.CreateEntries"/>)。</summary>
    public BuffUptimeWidgetSettingsConfig CreateConfig()
    {
        var config = new BuffUptimeWidgetSettingsConfig
        {
            PixelsPerSecond = RoundPixelsPerSecond(PixelsPerSecond),
            ShowsPermanentBuffs = ShowsPermanentBuffs,
            ShowsCuisinePotion = ShowsCuisinePotion,
            PriorityBuffs = PriorityBuffs.CreateEntries(),
            HiddenBuffIds = [.. _hiddenBuffItems.Where(item => item.IsEnabled).Select(item => item.Key.BuffId)],
            ClassColorIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            ClassColorPalettes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        GraphColors.WriteTo(config.ClassColorIndexes, config.ClassColorPalettes);
        return WidgetConfigDefaults.CloneNormalizedBuffUptime(config);
    }

    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreateBuffUptimeSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    public void MarkSaved(BuffUptimeWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedBuffUptime(config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// 開いている間に、窓の行の右クリックでバフが非表示になった。その分は既に保存されているので、
    /// 行と保存済みの値の両方に足す(バフ・デバフ一覧の <see cref="BuffListWidgetSettingsViewModel.AddHiddenBuffFromWidget"/> と同じ)。
    /// </summary>
    public void AddHiddenBuffFromWidget(int baseId)
    {
        if (baseId <= 0)
        {
            return;
        }

        if (!_lastSaved.HiddenBuffIds.Contains(baseId))
        {
            _lastSaved.HiddenBuffIds.Add(baseId);
        }

        var existing = _hiddenBuffItems.FirstOrDefault(item => item.Key.BuffId == baseId);
        if (existing is not null)
        {
            existing.LoadEnabled(true);
        }
        else
        {
            _hiddenBuffItems.Add(CreateHiddenBuffItem(baseId));
            OnHiddenBuffItemsChanged();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// 開いている間に、窓の行の右クリックで優先表示リストの行がオン/オフになった。
    /// 扱いはバフ・デバフ一覧の <see cref="BuffListWidgetSettingsViewModel.SetPriorityBuffEnabledFromWidget"/> と同じ。
    /// </summary>
    public void SetPriorityBuffEnabledFromWidget(BuffEntryKey key, bool isEnabled)
    {
        if (!key.IsValid)
        {
            return;
        }

        WidgetConfigDefaults.SetBuffEntryEnabled(
            _lastSaved.PriorityBuffs ??= [],
            key,
            isEnabled,
            WidgetConfigDefaults.DefaultBuffUptimePriorityBuffEntries);
        PriorityBuffs.SetEntryEnabled(key, isEnabled);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Load(BuffUptimeWidgetSettingsConfig? config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedBuffUptime(config);

        _isLoading = true;
        try
        {
            PixelsPerSecond = normalized.PixelsPerSecond;
            ShowsPermanentBuffs = normalized.ShowsPermanentBuffs;
            ShowsCuisinePotion = normalized.ShowsCuisinePotion;
            PriorityBuffs.Load(normalized.PriorityBuffs);

            _hiddenBuffItems.Clear();
            foreach (var baseId in normalized.HiddenBuffIds)
            {
                _hiddenBuffItems.Add(CreateHiddenBuffItem(baseId));
            }

            OnHiddenBuffItemsChanged();
            GraphColors.Load(normalized.ClassColorIndexes, normalized.ClassColorPalettes);
        }
        finally
        {
            _isLoading = false;
        }
    }

    partial void OnShowsPermanentBuffsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowsPermanentBuffsStateText));
        NotifyChanged();
    }

    partial void OnShowsCuisinePotionChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowsCuisinePotionStateText));
        NotifyChanged();
    }

    partial void OnPixelsPerSecondChanged(double value)
    {
        OnPropertyChanged(nameof(PixelsPerSecondText));
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

    private BuffSettingRowViewModel CreateHiddenBuffItem(int baseId)
    {
        return new BuffSettingRowViewModel(BuffEntryKey.ForBuff(baseId), isDefault: false, isEnabled: true, NotifyChanged);
    }

    /// <summary>行が増減したら、最終行の印と枠の有無を当て直す。</summary>
    private void OnHiddenBuffItemsChanged()
    {
        for (var index = 0; index < _hiddenBuffItems.Count; index++)
        {
            _hiddenBuffItems[index].IsLast = index == _hiddenBuffItems.Count - 1;
        }

        OnPropertyChanged(nameof(HasHiddenBuffs));
    }

    private static int RoundPixelsPerSecond(double pixelsPerSecond)
    {
        return WidgetConfigDefaults.ClampBuffUptimePixelsPerSecond(
            (int)Math.Round(pixelsPerSecond, MidpointRounding.AwayFromZero));
    }

    private static string FormatPixelsPerSecond(int pixelsPerSecond)
    {
        return LocalizationManager.Instance.Format("Settings_BuffUptime_PixelsPerSecond_Option", pixelsPerSecond);
    }

    private static string GetSwitchStateText(bool isOn)
    {
        return LocalizationManager.Instance.GetString(isOn ? "Settings_Switch_On" : "Settings_Switch_Off");
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var item in _hiddenBuffItems)
        {
            item.RefreshMetadata();
        }

        PriorityBuffs.RefreshLocalizedText();

        OnPropertyChanged(nameof(HiddenBuffSectionTitle));
        OnPropertyChanged(nameof(HiddenBuffSectionDescription));
        OnPropertyChanged(nameof(ShowsPermanentBuffsStateText));
        OnPropertyChanged(nameof(ShowsCuisinePotionStateText));
        OnPropertyChanged(nameof(PixelsPerSecondText));
        OnPropertyChanged(nameof(PixelsPerSecondMaxText));
    }

    private bool SettingsEqual(BuffUptimeWidgetSettingsConfig left, BuffUptimeWidgetSettingsConfig right)
    {
        return left.PixelsPerSecond == right.PixelsPerSecond
            && left.ShowsPermanentBuffs == right.ShowsPermanentBuffs
            && left.ShowsCuisinePotion == right.ShowsCuisinePotion
            && BuffEntryListSettingsViewModel.EntriesEqual(left.PriorityBuffs, right.PriorityBuffs)
            && left.HiddenBuffIds.SequenceEqual(right.HiddenBuffIds)
            && GraphColors.ColorsEqual(
                left.ClassColorIndexes,
                left.ClassColorPalettes,
                right.ClassColorIndexes,
                right.ClassColorPalettes);
    }
}
