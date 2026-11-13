// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class DutyInfo : BlobType
{
    public BlobHashMapDelta<int, ProfessionSkillInfo>? DutySkillInfoChanges;
    public BlobHashMapDelta<int, int>? DutySkillSlotInfoChanges;

    public DutyInfo()
    {
    }

    public DutyInfo(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.DutyInfo.DutySkillInfoMapFieldNumber:
                DutySkillInfoChanges = blob.ReadHashMapDelta<int, ProfessionSkillInfo>();
                return true;
            case Zproto.DutyInfo.DutySkillSlotInfoMapFieldNumber:
                DutySkillSlotInfoChanges = blob.ReadHashMapDelta<int, int>();
                return true;
            default:
                return false;
        }
    }
}
