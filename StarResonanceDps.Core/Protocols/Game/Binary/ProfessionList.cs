// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class ProfessionList : BlobType
{
    public int? CurProfessionId;
    public List<int>? CurAssistProfessions;
    public BlobHashMapDelta<int, ProfessionInfo>? ProfessionInfoChanges;
    public BlobHashMapDelta<int, ProfessionSkillInfo>? AoyiSkillInfoChanges;
    public BlobHashMapDelta<int, ProfessionTalentInfo>? TalentInfoChanges;
    public uint? TotalTalentPoints;
    public uint? TotalTalentResetCount;

    public ProfessionList()
    {
    }

    public ProfessionList(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.ProfessionList.CurProfessionIdFieldNumber:
                CurProfessionId = blob.ReadInt();
                return true;
            case Zproto.ProfessionList.CurAssistProfessionsFieldNumber:
                CurAssistProfessions = blob.ReadList<int>();
                return true;
            case Zproto.ProfessionList.ProfessionList_FieldNumber:
                ProfessionInfoChanges = blob.ReadHashMapDelta<int, ProfessionInfo>();
                return true;
            case Zproto.ProfessionList.AoyiSkillInfoMapFieldNumber:
                AoyiSkillInfoChanges = blob.ReadHashMapDelta<int, ProfessionSkillInfo>();
                return true;
            case Zproto.ProfessionList.TotalTalentPointsFieldNumber:
                TotalTalentPoints = blob.ReadUInt();
                return true;
            case Zproto.ProfessionList.TotalTalentResetCountFieldNumber:
                TotalTalentResetCount = blob.ReadUInt();
                return true;
            case Zproto.ProfessionList.TalentListFieldNumber:
                TalentInfoChanges = blob.ReadHashMapDelta<int, ProfessionTalentInfo>();
                return true;
            default:
                return false;
        }
    }
}
