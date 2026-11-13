// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Logging;

public enum PacketDiagnosticLogLevel
{
    Information,
    Warning,
    Error
}

public sealed record PacketDiagnosticLogEntry(
    DateTimeOffset Timestamp,
    PacketDiagnosticLogLevel Level,
    string Source,
    string Message,
    string? DisplayTextOverride = null)
{
    public string DisplayText => DisplayTextOverride ?? $"{Timestamp:HH:mm:ss.fff} [{GetLevelText(Level)}] [{Source}] {Message}";

    private static string GetLevelText(PacketDiagnosticLogLevel level)
    {
        return level switch
        {
            PacketDiagnosticLogLevel.Warning => "WARN",
            PacketDiagnosticLogLevel.Error => "ERROR",
            _ => "INFO"
        };
    }
}

public sealed class PacketDiagnosticLogStore
{
    private const int MaximumEntryCount = 500;

    private static readonly Lazy<PacketDiagnosticLogStore> LazyInstance = new(() => new PacketDiagnosticLogStore());

    private readonly object _sync = new();
    private readonly Queue<PacketDiagnosticLogEntry> _entries = new();

    private PacketDiagnosticLogStore()
    {
    }

    public static PacketDiagnosticLogStore Instance => LazyInstance.Value;

    public event EventHandler? EntriesChanged;

    public IReadOnlyList<PacketDiagnosticLogEntry> Snapshot
    {
        get
        {
            lock (_sync)
            {
                return _entries.ToArray();
            }
        }
    }

    public void Information(string source, string message)
    {
        Write(PacketDiagnosticLogLevel.Information, source, message, null);
    }

    public void Warning(string source, string message, Exception? exception = null)
    {
        Write(PacketDiagnosticLogLevel.Warning, source, message, exception);
    }

    public void Error(string source, string message, Exception? exception = null)
    {
        Write(PacketDiagnosticLogLevel.Error, source, message, exception);
    }

    public void AppendDisplayText(string displayText)
    {
        ArgumentNullException.ThrowIfNull(displayText);

        var normalizedDisplayText = displayText.TrimEnd('\r', '\n');
        Enqueue(new PacketDiagnosticLogEntry(
            DateTimeOffset.Now,
            PacketDiagnosticLogLevel.Information,
            string.Empty,
            string.Empty,
            normalizedDisplayText));
    }

    private void Write(
        PacketDiagnosticLogLevel level,
        string source,
        string message,
        Exception? exception)
    {
        var resolvedSource = string.IsNullOrWhiteSpace(source) ? "Packet" : source.Trim();
        var resolvedMessage = string.IsNullOrWhiteSpace(message) ? "No message." : message.Trim();
        if (exception is not null)
        {
            resolvedMessage = $"{resolvedMessage}{Environment.NewLine}{exception}";
        }

        Enqueue(new PacketDiagnosticLogEntry(
            DateTimeOffset.Now,
            level,
            resolvedSource,
            resolvedMessage));
    }

    private void Enqueue(PacketDiagnosticLogEntry entry)
    {
        lock (_sync)
        {
            while (_entries.Count >= MaximumEntryCount)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(entry);
        }

        EntriesChanged?.Invoke(this, EventArgs.Empty);
    }
}
