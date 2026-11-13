// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class DutyList : BlobType
{
    public int? CurProfessionDutyId;
    public BlobHashMapDelta<int, DutyInfo>? DutyInfoChanges;

    public DutyList()
    {
    }

    public DutyList(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.DutyList.CurProfessionDutyIdFieldNumber:
                CurProfessionDutyId = blob.ReadInt();
                return true;
            case Zproto.DutyList.DutyInfoMapFieldNumber:
                DutyInfoChanges = blob.ReadHashMapDelta<int, DutyInfo>();
                return true;
            default:
                return false;
        }
    }
}
