// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public sealed class ProfessionTalentInfo : BlobType
{
    public uint? UsedTalentPoints;
    public List<uint>? TalentNodeIds;
    public int? TalentStageCfgId;
    public int? TalentIlegalResetCount;
    public int? UsedAttackMark;
    public int? UsedGuardMark;
    public int? UsedHealMark;

    public ProfessionTalentInfo()
    {
    }

    public ProfessionTalentInfo(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.ProfessionTalentInfo.UsedTalentPointsFieldNumber:
                UsedTalentPoints = blob.ReadUInt();
                return true;
            case Zproto.ProfessionTalentInfo.TalentNodeIdsFieldNumber:
                TalentNodeIds = blob.ReadList<uint>();
                return true;
            case Zproto.ProfessionTalentInfo.TalentStageCfgIdFieldNumber:
                TalentStageCfgId = blob.ReadInt();
                return true;
            case Zproto.ProfessionTalentInfo.TalentIlegalResetCountFieldNumber:
                TalentIlegalResetCount = blob.ReadInt();
                return true;
            case Zproto.ProfessionTalentInfo.UsedAttackMarkFieldNumber:
                UsedAttackMark = blob.ReadInt();
                return true;
            case Zproto.ProfessionTalentInfo.UsedGuardMarkFieldNumber:
                UsedGuardMark = blob.ReadInt();
                return true;
            case Zproto.ProfessionTalentInfo.UsedHealMarkFieldNumber:
                UsedHealMark = blob.ReadInt();
                return true;
            default:
                return false;
        }
    }
}
