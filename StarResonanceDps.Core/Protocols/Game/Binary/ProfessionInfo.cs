// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public sealed class ProfessionInfo : BlobType
{
    public int? ProfessionId;
    public int? Level;
    public long? Experience;
    public BlobHashMapDelta<int, ProfessionSkillInfo>? SkillInfoChanges;
    public List<int>? ActiveSkillIds;
    public BlobHashMapDelta<int, int>? SlotSkillInfoChanges;
    public int? UseSkinId;

    public ProfessionInfo()
    {
    }

    public ProfessionInfo(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.ProfessionInfo.ProfessionIdFieldNumber:
                ProfessionId = blob.ReadInt();
                return true;
            case Zproto.ProfessionInfo.LevelFieldNumber:
                Level = blob.ReadInt();
                return true;
            case Zproto.ProfessionInfo.ExperienceFieldNumber:
                Experience = blob.ReadLong();
                return true;
            case Zproto.ProfessionInfo.SkillInfoMapFieldNumber:
                SkillInfoChanges = blob.ReadHashMapDelta<int, ProfessionSkillInfo>();
                return true;
            case Zproto.ProfessionInfo.ActiveSkillIdsFieldNumber:
                ActiveSkillIds = blob.ReadList<int>();
                return true;
            case Zproto.ProfessionInfo.SlotSkillInfoMapFieldNumber:
                SlotSkillInfoChanges = blob.ReadHashMapDelta<int, int>();
                return true;
            case Zproto.ProfessionInfo.UseSkinIdFieldNumber:
                UseSkinId = blob.ReadInt();
                return true;
            default:
                return false;
        }
    }
}
