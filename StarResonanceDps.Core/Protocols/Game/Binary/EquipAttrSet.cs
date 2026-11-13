// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>装備の特化ごとの行(型2 の属性庫)。区分ごとの「行ID → r」の map の差分。</summary>
public class EquipAttrSet : BlobType
{
    public override string? DebugName => "EquipAttrSet";

    public BlobHashMapDelta<int, int>? BasicAttrChanges;
    public BlobHashMapDelta<int, int>? AdvanceAttrChanges;
    public BlobHashMapDelta<int, int>? RecastAttrChanges;
    public BlobHashMapDelta<int, int>? RareQualityAttrChanges;

    public EquipAttrSet()
    {
    }

    public EquipAttrSet(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.EquipAttrSet.BasicAttrFieldNumber:
                BasicAttrChanges = blob.ReadHashMapDelta<int, int>();
                return true;
            case Zproto.EquipAttrSet.AdvanceAttrFieldNumber:
                AdvanceAttrChanges = blob.ReadHashMapDelta<int, int>();
                return true;
            case Zproto.EquipAttrSet.RecastAttrFieldNumber:
                RecastAttrChanges = blob.ReadHashMapDelta<int, int>();
                return true;
            case Zproto.EquipAttrSet.RareQualityAttrFieldNumber:
                RareQualityAttrChanges = blob.ReadHashMapDelta<int, int>();
                return true;
            default:
                return false;
        }
    }
}
