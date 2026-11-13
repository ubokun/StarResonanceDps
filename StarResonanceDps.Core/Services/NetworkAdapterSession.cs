// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using SharpPcap;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using StarResonanceDps.Core.Logging;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.Core.Services;

public sealed class NetworkAdapterSession
{
    private static readonly Lazy<NetworkAdapterSession> LazyInstance = new(() => new NetworkAdapterSession());

    private readonly PacketDiagnosticLogStore _diagnosticLog = PacketDiagnosticLogStore.Instance;
    private IReadOnlyList<NetworkAdapterInfo> _availableAdapters = [];
    private string _activeNetCaptureDeviceName = CombatRuntimeSettings.AutomaticNetCaptureDeviceName;
    private EGameCapturePreference _activeGameCapturePreference = EGameCapturePreference.Auto;
    private string _activeGameCaptureCustomExeName = string.Empty;

    private NetworkAdapterSession()
    {
    }

    public static NetworkAdapterSession Instance => LazyInstance.Value;

    public IReadOnlyList<NetworkAdapterInfo> AvailableAdapters => _availableAdapters;

    public NetworkAdapterInfo? SelectedAdapter { get; private set; }

    public bool IsInitialized { get; private set; }

    public event EventHandler? SelectedAdapterChanged;

    /// <summary>
    /// キャプチャ設定が変わったので保存してほしい、という合図。**Core は自分で保存しない。**
    /// App がこれを購読して <c>AppSettings.json</c> へ書く。
    /// </summary>
    public event EventHandler? CaptureSettingsPersistRequested;

    public void Initialize()
    {
        _availableAdapters = GetNetworkAdapters();
        NormalizePersistedAdapterSelection();
        _activeNetCaptureDeviceName = CombatRuntimeSettings.NetCaptureDeviceName;
        _activeGameCapturePreference = CombatRuntimeSettings.GameCapturePreference;
        _activeGameCaptureCustomExeName = NormalizeGameCaptureCustomExeName(CombatRuntimeSettings.GameCaptureCustomExeName);
        SelectedAdapter = FindSelectedAdapter(_activeNetCaptureDeviceName);
        ApplyRuntimeCaptureSettings();
        IsInitialized = true;
    }

    public bool SelectAdapter(NetworkAdapterInfo? adapter)
    {
        EnsureInitialized();

        if (adapter is null)
        {
            _diagnosticLog.Warning("Adapter", "Ignoring an empty capture-adapter selection.");
            return false;
        }

        var selected = _availableAdapters.FirstOrDefault(candidate =>
            string.Equals(candidate.DeviceName, adapter.DeviceName, StringComparison.Ordinal));

        if (selected is null)
        {
            _diagnosticLog.Warning(
                "Adapter",
                $"Ignoring a capture-adapter selection that is not in the current device list: {adapter.DisplayName}.");
            return false;
        }

        ApplyCaptureSettings(selected.DeviceName, _activeGameCapturePreference, _activeGameCaptureCustomExeName);
        return true;
    }

    public void ApplyGameCaptureSettings(EGameCapturePreference preference, string customExeName)
    {
        ApplyCaptureSettings(_activeNetCaptureDeviceName, preference, customExeName);
    }

    public void PreviewCaptureSettings(
        string networkAdapterDeviceName,
        EGameCapturePreference preference,
        string customExeName)
    {
        ApplyCaptureSettingsCore(networkAdapterDeviceName, preference, customExeName, persist: false);
    }

    public void ApplyCaptureSettings(
        string networkAdapterDeviceName,
        EGameCapturePreference preference,
        string customExeName)
    {
        ApplyCaptureSettingsCore(networkAdapterDeviceName, preference, customExeName, persist: true);
    }

    private void ApplyCaptureSettingsCore(
        string networkAdapterDeviceName,
        EGameCapturePreference preference,
        string customExeName,
        bool persist)
    {
        EnsureInitialized();

        var normalizedDeviceName = NormalizePersistedDeviceName(networkAdapterDeviceName);
        if (!CombatRuntimeSettings.IsAutomaticNetCaptureDeviceName(normalizedDeviceName)
            && FindPersistedSelection(_availableAdapters, normalizedDeviceName) is null)
        {
            _diagnosticLog.Warning(
                "Adapter",
                $"Falling back to automatic capture-adapter selection because the selected device is not in the current device list: {normalizedDeviceName}.");
            normalizedDeviceName = CombatRuntimeSettings.AutomaticNetCaptureDeviceName;
        }

        var normalizedCustomExeName = NormalizeGameCaptureCustomExeName(customExeName);
        var adapterChanged = !string.Equals(_activeNetCaptureDeviceName, normalizedDeviceName, StringComparison.Ordinal);
        var gameCaptureChanged = _activeGameCapturePreference != preference
            || !string.Equals(_activeGameCaptureCustomExeName, normalizedCustomExeName, StringComparison.Ordinal);

        if (!adapterChanged && !gameCaptureChanged)
        {
            if (persist)
            {
                PersistActiveCaptureSettings();
            }

            return;
        }

        _activeNetCaptureDeviceName = normalizedDeviceName;
        _activeGameCapturePreference = preference;
        _activeGameCaptureCustomExeName = normalizedCustomExeName;
        SelectedAdapter = FindSelectedAdapter(normalizedDeviceName);

        if (persist)
        {
            PersistActiveCaptureSettings();
        }

        RestartCapture();

        if (adapterChanged)
        {
            _diagnosticLog.Information("Adapter", $"Selected capture adapter: {SelectedAdapter?.DisplayName ?? "Auto"}.");
            SelectedAdapterChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void EnsureInitialized()
    {
        if (!IsInitialized)
        {
            Initialize();
        }
    }

    private void NormalizePersistedAdapterSelection()
    {
        var deviceName = NormalizePersistedDeviceName(CombatRuntimeSettings.NetCaptureDeviceName);
        if (CombatRuntimeSettings.IsAutomaticNetCaptureDeviceName(deviceName))
        {
            SetCaptureDeviceName(CombatRuntimeSettings.AutomaticNetCaptureDeviceName);
            return;
        }

        if (FindPersistedSelection(_availableAdapters, deviceName) is not null)
        {
            SetCaptureDeviceName(deviceName);
            return;
        }

        SetCaptureDeviceName(CombatRuntimeSettings.AutomaticNetCaptureDeviceName);
        CaptureSettingsPersistRequested?.Invoke(this, EventArgs.Empty);
        _diagnosticLog.Warning(
            "Adapter",
            $"Falling back to automatic capture-adapter selection because the persisted device is not in the current device list: {deviceName}.");
    }

    private NetworkAdapterInfo? FindSelectedAdapter(string persistedDeviceName)
    {
        var selected = FindPersistedSelection(_availableAdapters, persistedDeviceName);
        if (selected is not null)
        {
            return selected;
        }

        var runtimeDeviceName = ResolveRuntimeCaptureDeviceName(string.Empty);
        return FindPersistedSelection(_availableAdapters, runtimeDeviceName)
            ?? FindFirstAdapter(_availableAdapters);
    }

    private static NetworkAdapterInfo? FindPersistedSelection(
        IReadOnlyList<NetworkAdapterInfo> captureAdapters,
        string deviceName)
    {
        if (CombatRuntimeSettings.IsAutomaticNetCaptureDeviceName(deviceName))
        {
            return null;
        }

        return captureAdapters.FirstOrDefault(adapter =>
            string.Equals(adapter.DeviceName, deviceName, StringComparison.Ordinal));
    }

    private static NetworkAdapterInfo? FindFirstAdapter(IReadOnlyList<NetworkAdapterInfo> captureAdapters)
    {
        return captureAdapters.Count == 0
            ? null
            : captureAdapters[0];
    }

    private static string NormalizePersistedDeviceName(string? deviceName)
    {
        return CombatRuntimeSettings.IsAutomaticNetCaptureDeviceName(deviceName)
            ? CombatRuntimeSettings.AutomaticNetCaptureDeviceName
            : deviceName!.Trim();
    }

    private static string NormalizeGameCaptureCustomExeName(string? customExeName)
    {
        return Path.GetFileNameWithoutExtension(customExeName ?? string.Empty);
    }

    /// <summary>
    /// いま選んでいるキャプチャ設定を Core へ移し、<b>保存を App に依頼する</b>。
    ///
    /// <para>
    /// <b>Core はファイルへ書かない。</b>
    /// 保存先は App の <c>AppSettings.json</c> で、購読側が
    /// <see cref="CombatRuntimeSettings"/> から現在値を読んで書き出す。
    /// </para>
    /// </summary>
    private void PersistActiveCaptureSettings()
    {
        CombatRuntimeSettings.ApplyCaptureSettings(
            _activeNetCaptureDeviceName,
            _activeGameCapturePreference,
            _activeGameCaptureCustomExeName);
        CaptureSettingsPersistRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>アダプター名だけを差し替える。種別とカスタム名は今の値を保つ。</summary>
    private static void SetCaptureDeviceName(string deviceName)
    {
        CombatRuntimeSettings.ApplyCaptureSettings(
            deviceName,
            CombatRuntimeSettings.GameCapturePreference,
            CombatRuntimeSettings.GameCaptureCustomExeName);
    }

    private void ApplyRuntimeCaptureSettings()
    {
        MessageManager.NetCaptureDeviceName = ResolveRuntimeCaptureDeviceName(_activeNetCaptureDeviceName);
        MessageManager.GameCapturePreference = _activeGameCapturePreference;
        MessageManager.GameCaptureCustomExeName = _activeGameCaptureCustomExeName;
    }

    private static string ResolveRuntimeCaptureDeviceName(string persistedDeviceName)
    {
        if (!CombatRuntimeSettings.IsAutomaticNetCaptureDeviceName(persistedDeviceName))
        {
            return persistedDeviceName;
        }

        return MessageManager.TryFindBestNetworkDevice()?.Name ?? string.Empty;
    }

    private IReadOnlyList<NetworkAdapterInfo> GetNetworkAdapters()
    {
        try
        {
            return CaptureDeviceList.Instance
                .Select(device => new NetworkAdapterInfo(
                    device.Name,
                    device.Description ?? device.Name))
                .ToList();
        }
        catch (Exception exception)
        {
            _diagnosticLog.Error("Adapter", "Capture-device enumeration failed.", exception);
            return [];
        }
    }

    private void RestartCapture()
    {
        MessageManager.StopCapturing();
        ApplyRuntimeCaptureSettings();

        if (!string.IsNullOrWhiteSpace(MessageManager.NetCaptureDeviceName))
        {
            MessageManager.InitializeCapturing();
        }
    }
}
