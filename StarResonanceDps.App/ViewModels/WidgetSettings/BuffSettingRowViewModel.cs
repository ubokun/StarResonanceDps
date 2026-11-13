// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// バフの設定の一覧の1行。左にアイコンと名前、右にスイッチ。名前と絵は一覧の行と同じ引き方。
/// バフ・デバフ一覧の「非表示リスト」(オン = 非表示のまま、オフにして保存すると行が消える)と、
/// バフ・デバフグループの「バフ・デバフ」(オン = ウィジェットに出す)が使う。
/// </summary>
public sealed partial class BuffSettingRowViewModel : ObservableObject
{
    private readonly Action _onEnabledChanged;
    private bool _isLoading;

    /// <param name="isDefault">既定の行か(バフ・デバフグループの既定の行は、オフにしても消えない)。</param>
    /// <param name="onEnabledChanged">スイッチを触った瞬間に呼ぶ(読み込みでは呼ばない)。</param>
    public BuffSettingRowViewModel(BuffEntryKey key, bool isDefault, bool isEnabled, Action onEnabledChanged)
    {
        Key = key;
        IsDefault = isDefault;
        _isEnabled = isEnabled;
        _onEnabledChanged = onEnabledChanged;
        RefreshMetadata();
    }

    public BuffEntryKey Key { get; }

    public bool IsDefault { get; }

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string? _iconPath;

    /// <summary>絵が Icons.xaml の絵(装備効果)なら型抜き。一覧の行と同じ引き方。</summary>
    [ObservableProperty]
    private ImageBrush? _iconMask;

    /// <summary>絵がモジュール効果の絵か。一覧の行と同じく、絵の大きさを切り替える。</summary>
    [ObservableProperty]
    private bool _isModuleEffectIcon;

    /// <summary>絵が因子の絵か。一覧の行と同じく、絵の位置を切り替える。</summary>
    [ObservableProperty]
    private bool _isFactorIcon;

    /// <summary>絵のパスはあるのにファイルが見つからないか。一覧の行と同じく、はてなを出す。パスが空なら何も出さない。</summary>
    [ObservableProperty]
    private bool _showsUnknownIcon;

    /// <summary>最終行。区切り線を消して二重にならないようにする。行が増減・並べ替えで変わる。</summary>
    [ObservableProperty]
    private bool _isLast;

    /// <summary>スイッチの右に出す ON / OFF。</summary>
    public string StateText => LocalizationManager.Instance.GetString(
        IsEnabled ? "Settings_Switch_On" : "Settings_Switch_Off");

    /// <summary>読み込み時はプレビュー反映を鳴らさずに値だけ入れる。</summary>
    public void LoadEnabled(bool isEnabled)
    {
        _isLoading = true;
        try
        {
            IsEnabled = isEnabled;
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void RefreshMetadata()
    {
        // 名前と絵は番号ごと(表と手修正)。付与元の由来は分からないので無し。番号で名前の引けないバフは名前が空になる。
        // 料理・薬剤のまとまりはまとまりの名前と絵(カードのタイトルと同じ名前)。
        string iconName;
        if (Key.IsGroup)
        {
            DisplayName = BuffDebuffCardContent.GetGroupName(Key.Group);
            iconName = CombatDataCatalog.GetBuffGroupIconName(Key.Group);
        }
        else
        {
            DisplayName = CombatDataCatalog.GetBuffDisplayName(Key.BuffId, SourceLanding.None);
            iconName = CombatDataCatalog.GetBuffOwnIconName(Key.BuffId);
        }

        IconPath = CombatIconResolver.ResolveBuffIcon(iconName);
        IconMask = CombatIconResolver.ResolveBuffIconMask(iconName);
        IsModuleEffectIcon = CombatDataCatalog.IsModEffectIcon(iconName);
        IsFactorIcon = CombatIconResolver.IsFactorIcon(iconName);
        ShowsUnknownIcon = !string.IsNullOrWhiteSpace(iconName) && IconPath is null && IconMask is null;
        OnPropertyChanged(nameof(StateText));
    }

    partial void OnIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(StateText));
        if (_isLoading)
        {
            return;
        }

        _onEnabledChanged();
    }
}
