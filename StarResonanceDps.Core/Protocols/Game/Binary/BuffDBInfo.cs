// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>
/// 自分のコンテナが運ぶバフ集合(<c>CharSerialize.buffInfo</c>, field 6)。
/// 自分は AOI の <c>Appear</c> に出てこないため、BaseId 付きの全バフを受け取れる唯一の候補。
/// </summary>
public sealed class BuffDBInfo : BlobType
{
    public override string? DebugName => "BuffDBInfo";

    public uint? MaxId;
    public BlobHashMapDelta<uint, BuffDBData>? BuffChanges;

    public BuffDBInfo()
    {
    }

    public BuffDBInfo(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.BuffDBInfo.MaxIDFieldNumber:
                MaxId = blob.ReadUInt();
                return true;
            case Zproto.BuffDBInfo.AllBuffDbDataFieldNumber:
                BuffChanges = blob.ReadHashMapDelta<uint, BuffDBData>();
                return true;
            default:
                return false;
        }
    }
}
