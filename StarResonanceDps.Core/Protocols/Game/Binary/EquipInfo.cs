// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>装備スロット1つ。更新では変わった項目だけが届く。使うのは着けている装備の uuid(外すと 0)。</summary>
public class EquipInfo : BlobType
{
    public override string? DebugName => "EquipInfo";

    public ulong? ItemUuid;

    public EquipInfo()
    {
    }

    public EquipInfo(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.EquipInfo.EquipSlotFieldNumber:
                blob.ReadInt();
                return true;
            case Zproto.EquipInfo.ItemUuidFieldNumber:
                ItemUuid = blob.ReadULong();
                return true;
            case Zproto.EquipInfo.EquipSlotRefineLevelFieldNumber:
            case Zproto.EquipInfo.EquipSlotRefineFailedCountFieldNumber:
                blob.ReadUInt();
                return true;
            default:
                return false;
        }
    }
}
