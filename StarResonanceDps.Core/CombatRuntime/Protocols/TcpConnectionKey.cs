// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Buffers.Binary;
using System.Net;

namespace StarResonanceDps.Core.CombatRuntime.Protocols;

/// <summary>TCP の端点(IPv4 のアドレスとポート)。</summary>
public readonly record struct TcpEndpoint(uint Address, ushort Port)
{
    public static TcpEndpoint From(IPAddress address, ushort port)
    {
        Span<byte> bytes = stackalloc byte[4];
        if (!address.TryWriteBytes(bytes, out var written) || written != 4)
        {
            throw new ArgumentException($"Only IPv4 addresses are supported (got {address}).", nameof(address));
        }

        return new TcpEndpoint(BinaryPrimitives.ReadUInt32BigEndian(bytes), port);
    }

    public override string ToString()
    {
        return $"{Address >> 24}.{(Address >> 16) & 0xFF}.{(Address >> 8) & 0xFF}.{Address & 0xFF}:{Port}";
    }
}

/// <summary>
/// 1つの TCP 接続の鍵。両方向のパケットが同じ鍵になる(端点を (Address, Port) の順に並べて持つ)。
/// 向きは「送信元が <see cref="Low"/> か」で決める。
/// </summary>
public readonly record struct TcpConnectionKey(TcpEndpoint Low, TcpEndpoint High)
{
    public static TcpConnectionKey From(TcpEndpoint a, TcpEndpoint b)
    {
        return Compare(a, b) <= 0
            ? new TcpConnectionKey(a, b)
            : new TcpConnectionKey(b, a);
    }

    public override string ToString()
    {
        return $"{Low} <-> {High}";
    }

    private static int Compare(TcpEndpoint a, TcpEndpoint b)
    {
        var byAddress = a.Address.CompareTo(b.Address);
        return byAddress != 0 ? byAddress : a.Port.CompareTo(b.Port);
    }
}
