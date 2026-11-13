// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Buffers;

namespace StarResonanceDps.Core.CombatRuntime.Protocols;

/// <summary>切り出したメッセージ1つ。バッファは <see cref="ArrayPool{T}.Shared"/> から借りたもの。</summary>
public class RawPacket
{
    public byte[] Data { get; set; } = null!;
    public int Len { get; set; }

    /// <summary>そのメッセージが揃ったパケットのキャプチャ時刻(UTC)。</summary>
    public DateTime ArrivalTime { get; set; } = DateTime.MinValue;

    /// <summary>切り出しが借りたバッファを、写さずに受け取る。返すのは <see cref="Return"/>。</summary>
    public void Adopt(byte[] rentedBuffer, int len, DateTime arrivalTime)
    {
        Data = rentedBuffer;
        Len = len;
        ArrivalTime = arrivalTime;
    }

    public void Return()
    {
        ArrayPool<byte>.Shared.Return(Data);
    }
}
