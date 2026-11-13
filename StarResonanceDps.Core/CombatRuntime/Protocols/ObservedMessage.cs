// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime.Protocols;

public enum ObservedMessageKind
{
    /// <summary>サーバーからの通知(下り)。</summary>
    Notify,

    /// <summary>クライアントの呼び出し(上り)。</summary>
    Call,

    /// <summary>呼び出しへのサーバーの応答(下り)。番号は呼んだメソッドのもの。</summary>
    Return,
}

/// <summary>
/// 届いたメッセージ1件(<see cref="NetCap.MessageObserved"/> に渡す)。中身は解凍した後のもので、
/// 本体の解凍用の使い回しの領域を指すことがあるので、受け手の中でだけ有効。
/// </summary>
public readonly ref struct ObservedMessage
{
    public ObservedMessage(
        ObservedMessageKind kind,
        ulong serviceId,
        uint methodId,
        uint callId,
        ReadOnlySpan<byte> payload,
        DateTime arrivalTimeUtc)
    {
        Kind = kind;
        ServiceId = serviceId;
        MethodId = methodId;
        CallId = callId;
        Payload = payload;
        ArrivalTimeUtc = arrivalTimeUtc;
    }

    public ObservedMessageKind Kind { get; }

    public ulong ServiceId { get; }

    public uint MethodId { get; }

    /// <summary>呼び出しと応答を結ぶ番号。通知は 0。</summary>
    public uint CallId { get; }

    public ReadOnlySpan<byte> Payload { get; }

    public DateTime ArrivalTimeUtc { get; }
}
