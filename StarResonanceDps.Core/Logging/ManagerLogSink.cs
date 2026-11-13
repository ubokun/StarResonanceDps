// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace StarResonanceDps.Core.Logging;

public sealed class ManagerLogSink : ILogEventSink
{
    private static readonly MessageTemplateTextFormatter Formatter = new(
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        CultureInfo.InvariantCulture);

    private readonly PacketDiagnosticLogStore _logStore;

    public ManagerLogSink(PacketDiagnosticLogStore logStore)
    {
        _logStore = logStore;
    }

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        Formatter.Format(logEvent, writer);
        _logStore.AppendDisplayText(writer.ToString());
    }
}
