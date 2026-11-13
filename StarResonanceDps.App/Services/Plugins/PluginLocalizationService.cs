// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using StarResonanceDps.App.Localization;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグイン1つに渡す言語の口。言語の知らせはプラグインごとに受け止め(<see cref="PluginCallGuard"/>)の中で出し、
/// 1つのプラグインの例外がほかのプラグインへの知らせを止めないようにする。
/// </summary>
internal sealed class PluginLocalizationService : IPluginLocalizationService
{
    private readonly PluginCallGuard _guard;
    private bool _isDetached;

    public PluginLocalizationService(PluginCallGuard guard)
    {
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    public CultureInfo CurrentCulture => LocalizationManager.Instance.CurrentCulture;

    public event EventHandler? CultureChanged;

    /// <summary>プラグインを止めた・終えたときに、本体の知らせから外す。</summary>
    public void Detach()
    {
        if (_isDetached)
        {
            return;
        }

        _isDetached = true;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        _guard.TryInvoke("Culture change notification", () => CultureChanged?.Invoke(this, EventArgs.Empty));
    }
}
