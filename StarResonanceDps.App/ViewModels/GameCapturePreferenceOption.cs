// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.CombatRuntime.DataTypes;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class GameCapturePreferenceOption : ViewModelBase
{
    public GameCapturePreferenceOption(EGameCapturePreference preference)
    {
        Preference = preference;
    }

    public EGameCapturePreference Preference { get; }

    public string DisplayName => Preference switch
    {
        EGameCapturePreference.Auto => LocalizationManager.Instance.GetString("Settings_GameCapturePreference_Auto"),
        EGameCapturePreference.Custom => LocalizationManager.Instance.GetString("Settings_GameCapturePreference_Custom"),
        _ => Utils.GameCapturePreferenceToName(Preference)
    };

    public void RefreshDisplayName()
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
