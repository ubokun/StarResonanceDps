// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>心相晶のノード1つ(<c>CultivateNormalNodeData</c>)の差分。</summary>
public class CultivateNormalNodeData : BlobType
{
    public override string? DebugName => "CultivateNormalNodeData";

    public int? ActiveLevel;

    public CultivateNormalNodeData()
    {
    }

    public CultivateNormalNodeData(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.CultivateNormalNodeData.ActiveLevelFieldNumber:
                ActiveLevel = blob.ReadInt();
                return true;
            default:
                return false;
        }
    }
}
