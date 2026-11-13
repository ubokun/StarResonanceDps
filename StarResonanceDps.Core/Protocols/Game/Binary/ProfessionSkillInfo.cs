// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class ProfessionSkillInfo : BlobType
{
    public int? SkillId;
    public int? Level;
    public List<int>? ReplaceSkillIds;
    public int? RemodelLevel;
    public int? CurSkillSkin;

    public ProfessionSkillInfo()
    {
    }

    public ProfessionSkillInfo(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.ProfessionSkillInfo.SkillIdFieldNumber:
                SkillId = blob.ReadInt();
                return true;
            case Zproto.ProfessionSkillInfo.LevelFieldNumber:
                Level = blob.ReadInt();
                return true;
            case Zproto.ProfessionSkillInfo.ReplaceSkillIdsFieldNumber:
                ReplaceSkillIds = blob.ReadList<int>();
                return true;
            case Zproto.ProfessionSkillInfo.RemodelLevelFieldNumber:
                RemodelLevel = blob.ReadInt();
                return true;
            case Zproto.ProfessionSkillInfo.CurSkillSkinFieldNumber:
                CurSkillSkin = blob.ReadInt();
                return true;
            default:
                return false;
        }
    }
}
