// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// 全体設定「ホットキー」の1行。左に操作の名前、右に入力欄。
/// 入力欄は通常は割り当てたキー(割り当てなしは空欄)、受付中は「キーを入力」を出す。
/// </summary>
public sealed partial class HotkeyItemViewModel : ObservableObject
{
    public HotkeyItemViewModel(HotkeyAction action, bool isLast)
    {
        Action = action;
        IsLast = isLast;
    }

    public HotkeyAction Action { get; }

    /// <summary>最終行。区切り線を消して二重にならないようにする。</summary>
    public bool IsLast { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private HotkeyBindingConfig _binding = new();

    /// <summary>キーを受け付けている間。欄に「キーを入力」を出す。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private bool _isCapturing;

    public string Label => HotkeyText.GetLabel(Action);

    public string DisplayText => IsCapturing
        ? LocalizationManager.Instance.GetString("Settings_Hotkey_Listening")
        : HotkeyText.Format(Binding);

    public void RefreshText()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(DisplayText));
    }
}
