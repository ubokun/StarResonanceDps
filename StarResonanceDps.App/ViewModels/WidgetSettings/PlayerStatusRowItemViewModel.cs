// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// 「ステータス」設定の1行。左にアイコン、中央に項目名、右にスイッチ。
/// 作りは <see cref="OtherRoleSkillItemViewModel"/> と同じ。
/// </summary>
public sealed partial class PlayerStatusRowItemViewModel : ObservableObject
{
    private readonly Action _onVisibilityChanged;
    private bool _isLoading;

    public PlayerStatusRowItemViewModel(int attrId, bool isLast, Action onVisibilityChanged)
    {
        AttrId = attrId;
        OrderUnitAttrId = PlayerStatusEntry.GetOrderUnitAttrId(attrId);
        _isLast = isLast;
        Key = attrId.ToString(CultureInfo.InvariantCulture);
        _onVisibilityChanged = onVisibilityChanged;
        RefreshMetadata();
    }

    public int AttrId { get; }

    /// <summary>並べ替えの単位(<see cref="PlayerStatusEntry.GetOrderUnitAttrId"/>)。同じ単位の行(組)は並んでいて、一緒に動く。</summary>
    public int OrderUnitAttrId { get; }

    /// <summary>最終行。区切り線を消して二重にならないようにする。並びが変わったら付け直す。</summary>
    [ObservableProperty]
    private bool _isLast;
    public string Key { get; }

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private Brush? _iconMask;

    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>スイッチの右に出す ON / OFF。</summary>
    public string StateText => LocalizationManager.Instance.GetString(
        IsVisible ? "Settings_Switch_On" : "Settings_Switch_Off");

    public void RefreshMetadata()
    {
        DisplayName = LocalizationManager.Instance.GetString(PlayerStatusEntry.GetRowNameKey(AttrId));
        IconMask = PlayerStatusEntry.GetRowIconMask(AttrId);
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
