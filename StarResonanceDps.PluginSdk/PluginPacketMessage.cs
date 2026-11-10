namespace StarResonanceDps.PluginSdk;

/// <summary>The kind of a captured game message.</summary>
public enum PluginPacketKind
{
    /// <summary>A notification the server sends to the client.</summary>
    Notify,

    /// <summary>A method call the client sends to the server.</summary>
    Call,

    /// <summary>The server's reply to a <see cref="Call"/>.</summary>
    Return,
}

/// <summary>The direction of a captured game message.</summary>
public enum PluginPacketDirection
{
    /// <summary>From the server to the client.</summary>
    Down,

    /// <summary>From the client to the server.</summary>
    Up,
}

/// <summary>
/// A type of game message to read: the kind, the service ID and the method ID.
/// For <see cref="PluginPacketKind.Return"/>, the IDs are those of the call it replies to.
/// </summary>
/// <param name="Kind">The kind of message.</param>
/// <param name="ServiceId">The service ID.</param>
/// <param name="MethodId">The method ID.</param>
public readonly record struct PluginPacketType(PluginPacketKind Kind, ulong ServiceId, uint MethodId);

/// <summary>
/// One captured game message. The payload is the message body as the game sent it (protobuf bytes): notifications and replies are decompressed,
/// calls are not. A reply is the reply wrapper of the called method (<c>X_Ret</c>).
/// </summary>
public sealed class PluginPacketMessage
{
    /// <summary>
    /// Creates a message. The host creates the messages it delivers; a plugin needs this only to create messages itself, for example in tests.
    /// </summary>
    public PluginPacketMessage(
        long sequenceNumber,
        PluginPacketKind kind,
        ulong serviceId,
        uint methodId,
        uint callId,
        ReadOnlyMemory<byte> payload,
        DateTime arrivalTimeUtc)
    {
        SequenceNumber = sequenceNumber;
        Kind = kind;
        ServiceId = serviceId;
        MethodId = methodId;
        CallId = callId;
        Payload = payload;
        ArrivalTimeUtc = arrivalTimeUtc;
    }

    /// <summary>
    /// The capture order. Numbered across all messages delivered to any plugin, so a plugin sees gaps
    /// for messages that only other plugins read.
    /// </summary>
    public long SequenceNumber { get; }

    /// <summary>The kind of message.</summary>
    public PluginPacketKind Kind { get; }

    /// <summary><see cref="PluginPacketDirection.Up"/> for a call, otherwise <see cref="PluginPacketDirection.Down"/>.</summary>
    public PluginPacketDirection Direction => Kind == PluginPacketKind.Call ? PluginPacketDirection.Up : PluginPacketDirection.Down;

    /// <summary>The service ID. For a reply, the service of the call it replies to.</summary>
    public ulong ServiceId { get; }

    /// <summary>The method ID. For a reply, the method of the call it replies to.</summary>
    public uint MethodId { get; }

    /// <summary>The number that links a <see cref="PluginPacketKind.Return"/> to its <see cref="PluginPacketKind.Call"/>. 0 for notifications.</summary>
    public uint CallId { get; }

    /// <summary>The message body (protobuf bytes). The host copies it for the plugins, so a plugin may keep it as long as it needs.</summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>The capture time (UTC) of the packet that completed the message.</summary>
    public DateTime ArrivalTimeUtc { get; }

    /// <summary>The kind, service ID and method ID of the message.</summary>
    public PluginPacketType Type => new(Kind, ServiceId, MethodId);
}
