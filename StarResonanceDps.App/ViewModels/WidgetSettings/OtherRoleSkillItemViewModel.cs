// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// 「他人のロールスキル」設定の1行。左に枠つきアイコン、中央にスキル名、右にスイッチ。
/// </summary>
public sealed partial class OtherRoleSkillItemViewModel : ObservableObject
{
    private readonly Action _onVisibilityChanged;
    private bool _isLoading;

    public OtherRoleSkillItemViewModel(int skillId, bool isLast, Action onVisibilityChanged)
    {
        SkillId = skillId;
        IsLast = isLast;
        _onVisibilityChanged = onVisibilityChanged;
        RefreshMetadata();
    }

    public int SkillId { get; }

    /// <summary>最終行。区切り線を消して二重にならないようにする。</summary>
    public bool IsLast { get; }

    public string Key { get; private set; } = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string? _iconPath;

    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>スイッチの右に出す ON / OFF。</summary>
    public string StateText => Localization.LocalizationManager.Instance.GetString(
        IsVisible ? "Settings_Switch_On" : "Settings_Switch_Off");

    /// <summary>枠の絵をイマジン用に切り替えないためのフラグ。ロールスキルなので常に false。</summary>
    public bool IsImagine => false;

    /// <summary>
    /// アイコンの位置・サイズをイマジン側の調整値にするか。アイコンがイマジンの絵か(<c>CombatDataCatalog.IsImagineArtIcon</c>)で決める。
    /// プレイヤーリスト側(<c>PlayerImagineRoleSkillEntry</c>)と同じ判定。
    /// </summary>
    [ObservableProperty]
    private bool _usesImagineAsset;

    public void RefreshMetadata()
    {
        Key = SkillId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var iconName = CombatDataCatalog.GetSkillIconName(SkillId, string.Empty);
        DisplayName = CombatDataCatalog.GetSkillName(SkillId);
        UsesImagineAsset = CombatDataCatalog.IsImagineArtIcon(iconName);
        IconPath = CombatIconResolver.ResolveSkillIcon(iconName);
        OnPropertyChanged(nameof(StateText));
    }

    /// <summary>読み込み時はプレビュー反映を鳴らさずに値だけ入れる。</summary>
    public void LoadVisibility(bool isVisible)
    {
        _isLoading = true;
        try
        {
            IsVisible = isVisible;
        }
        finally
        {
            _isLoading = false;
        }
    }

    partial void OnIsVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(StateText));
        if (_isLoading)
        {
            return;
        }

        _onVisibilityChanged();
    }
}
