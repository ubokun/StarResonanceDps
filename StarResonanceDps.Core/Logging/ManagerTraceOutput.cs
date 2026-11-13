// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Diagnostics;
using System.Text;

namespace StarResonanceDps.Core.Logging;

public static class ManagerTraceOutput
{
    private static readonly object Sync = new();
    private static readonly ManagerTraceListener Listener = new(PacketDiagnosticLogStore.Instance);
    private static bool _isConfigured;

    public static void Configure()
    {
        lock (Sync)
        {
            if (_isConfigured)
            {
                return;
            }

            foreach (var listener in Trace.Listeners.OfType<DefaultTraceListener>().ToArray())
            {
                Trace.Listeners.Remove(listener);
            }

            Trace.Listeners.Add(Listener);
            _isConfigured = true;
        }
    }
}

internal sealed class ManagerTraceListener : TraceListener
{
    private readonly object _sync = new();
    private readonly PacketDiagnosticLogStore _logStore;
    private readonly StringBuilder _pendingText = new();

    public ManagerTraceListener(PacketDiagnosticLogStore logStore)
    {
        _logStore = logStore;
    }

    public override void Write(string? message)
    {
        lock (_sync)
        {
            _pendingText.Append(message);
        }
    }

    public override void WriteLine(string? message)
    {
        string displayText;
        lock (_sync)
        {
            _pendingText.Append(message);
            displayText = _pendingText.ToString();
            _pendingText.Clear();
        }

        _logStore.AppendDisplayText(displayText);
    }

    public override void Write(string? message, string? category)
    {
        Write(Format(message, category));
    }

    public override void WriteLine(string? message, string? category)
    {
        WriteLine(Format(message, category));
    }

    private static string? Format(string? message, string? category)
    {
        return string.IsNullOrEmpty(category)
            ? message
            : string.Concat(category, ": ", message);
    }
}
