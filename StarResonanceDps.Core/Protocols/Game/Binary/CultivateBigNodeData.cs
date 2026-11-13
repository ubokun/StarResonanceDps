// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>心相晶の絆スロット1つ(<c>CultivateBigNodeData</c>)の差分。使い先は無く、読み進めるためだけに読む。</summary>
public class CultivateBigNodeData : BlobType
{
    public override string? DebugName => "CultivateBigNodeData";

    public int? FantasyId;

    public CultivateBigNodeData()
    {
    }

    public CultivateBigNodeData(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.CultivateBigNodeData.FantasyIdFieldNumber:
                FantasyId = blob.ReadInt();
                return true;
            default:
                return false;
        }
    }
}
