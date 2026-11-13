// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Serilog;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

internal sealed class PluginLogger : IPluginLogger
{
    private readonly string _pluginKey;

    public PluginLogger(string pluginKey)
    {
        _pluginKey = pluginKey;
    }

    public void Info(string message)
    {
        Log.Information("[Plugin:{Plugin}] {Message}", _pluginKey, message);
    }

    public void Warning(string message)
    {
        Log.Warning("[Plugin:{Plugin}] {Message}", _pluginKey, message);
    }

    public void Error(string message, Exception? exception = null)
    {
        Log.Error(exception, "[Plugin:{Plugin}] {Message}", _pluginKey, message);
    }
}
