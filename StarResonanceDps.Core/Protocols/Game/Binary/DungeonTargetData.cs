// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Zproto;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class DungeonTargetData : BlobType
{
    public int? TargetId;
    public int? Nums;
    public int? Complete;
    
    public DungeonTargetData()
    {
    }

    public DungeonTargetData(BlobReader blob) : base(ref blob)
    {
    }
    
    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index) {
            case Zproto.DungeonTargetData.TargetIdFieldNumber:
                TargetId = blob.ReadInt();
                return true;
            case Zproto.DungeonTargetData.NumsFieldNumber:
                Nums = blob.ReadInt();
                return true;
            case Zproto.DungeonTargetData.CompleteFieldNumber:
                Complete = blob.ReadInt();
                return true;
            default:
                return false;
        }
    }

    public static implicit operator Zproto.DungeonTargetData(DungeonTargetData targetData)
    {
        var data = new Zproto.DungeonTargetData()
        {
            Complete = targetData.Complete ?? 0,
            Nums = targetData.Nums ?? 0,
            TargetId = targetData.TargetId ?? 0
        };
        return data;
    }
}