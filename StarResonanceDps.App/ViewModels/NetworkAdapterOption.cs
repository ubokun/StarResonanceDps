// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class NetworkAdapterOption : ViewModelBase
{
    private NetworkAdapterOption(NetworkAdapterInfo? adapter)
    {
        Adapter = adapter;
    }

    public NetworkAdapterInfo? Adapter { get; }

    public bool IsAutomatic => Adapter is null;

    public string DeviceName => Adapter?.DeviceName ?? string.Empty;

    public string DisplayName => IsAutomatic
        ? LocalizationManager.Instance.GetString("Settings_NetworkAdapter_Auto")
        : Adapter?.DisplayName ?? string.Empty;

    public static NetworkAdapterOption CreateAutomatic()
    {
        return new NetworkAdapterOption(null);
    }

    public static NetworkAdapterOption Create(NetworkAdapterInfo adapter)
    {
        return new NetworkAdapterOption(adapter);
    }

    public void RefreshDisplayName()
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
