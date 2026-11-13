// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.Views.Plugins;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

public sealed class PluginWindowManager
{
    private readonly Dictionary<string, OpenWindow> _openWindows = new(StringComparer.OrdinalIgnoreCase);
    private Window? _managerWindow;

    private PluginWindowManager()
    {
    }

    public static PluginWindowManager Instance { get; } = new();

    public bool ActivateExisting(string pluginKey)
    {
        if (string.IsNullOrWhiteSpace(pluginKey)
            || !_openWindows.TryGetValue(pluginKey, out var existingWindow))
        {
            return false;
        }

        if (existingWindow.Window.WindowState == WindowState.Minimized)
        {
            existingWindow.Window.WindowState = WindowState.Normal;
        }

        existingWindow.Window.Activate();
        return true;
    }

    /// <param name="contentClosed">窓を閉じた後に、中身を渡して呼ぶ(中身の後始末をする)。</param>
    public void Open(
        PluginListItemViewModel plugin,
        FrameworkElement content,
        PluginWindowOptions? windowOptions,
        Action<FrameworkElement> contentClosed)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(contentClosed);

        if (ActivateExisting(plugin.PluginKey))
        {
            return;
        }

        var managerWindow = Application.Current?.MainWindow;
        TrackManagerWindow(managerWindow);

        var window = new PluginWindow(plugin, content, managerWindow, windowOptions);
        window.Closed += PluginWindow_Closed;

        _openWindows.Add(plugin.PluginKey, new OpenWindow(window, contentClosed));
        window.Show();
    }

    /// <summary>プラグインの窓が開いていれば閉じる(本体がプラグインを止めるとき)。</summary>
    public void Close(string pluginKey)
    {
        if (_openWindows.TryGetValue(pluginKey, out var openWindow))
        {
            openWindow.Window.Close();
        }
    }

    private void TrackManagerWindow(Window? managerWindow)
    {
        if (managerWindow is null || ReferenceEquals(_managerWindow, managerWindow))
        {
            return;
        }

        if (_managerWindow is not null)
        {
            _managerWindow.Closed -= ManagerWindow_Closed;
        }

        _managerWindow = managerWindow;
        _managerWindow.Closed += ManagerWindow_Closed;
    }

    private void ManagerWindow_Closed(object? sender, EventArgs eventArgs)
    {
        foreach (var openWindow in _openWindows.Values.ToArray())
        {
            openWindow.Window.Close();
        }

        _managerWindow = null;
    }

    private void PluginWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is not PluginWindow window)
        {
            return;
        }

        window.Closed -= PluginWindow_Closed;

        if (!_openWindows.Remove(window.Plugin.PluginKey, out var openWindow))
        {
            return;
        }

        openWindow.ContentClosed(window.PluginContent);
    }

    private sealed record OpenWindow(PluginWindow Window, Action<FrameworkElement> ContentClosed);
}
