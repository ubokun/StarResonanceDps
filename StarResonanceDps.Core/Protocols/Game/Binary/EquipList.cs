// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>装備スロット(<c>CharSerialize.equip</c>)の差分。使うのは部位ごとの変化で、ほかの項目は読み進めるためだけに読む。</summary>
public class EquipList : BlobType
{
    public override string? DebugName => "EquipList";

    /// <summary>部位 → 部位の差分。</summary>
    public BlobHashMapDelta<int, EquipInfo>? EquipInfoChanges;

    public EquipList()
    {
    }

    public EquipList(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.EquipList.EquipList_FieldNumber:
                EquipInfoChanges = blob.ReadHashMapDelta<int, EquipInfo>();
                return true;
            case Zproto.EquipList.EquipRecastInfoFieldNumber:
                blob.ReadHashMapDelta<ulong, SkippedBlob>();
                return true;
            case Zproto.EquipList.EquipEnchantFieldNumber:
                blob.ReadHashMapDelta<long, SkippedBlob>();
                return true;
            case Zproto.EquipList.SuitInfoDictFieldNumber:
                blob.ReadHashMapDelta<int, SkippedBlob>();
                return true;
            default:
                return false;
        }
    }
}
