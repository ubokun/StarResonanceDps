// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class UserFightAttr : BlobType
{
    public long? CurHp;
    public long? MaxHp;
    public float? OriginEnergy;
    public List<uint>? ResourceIds;
    public List<uint>? Resources;
    public int? IsDead;
    public long? DeadTime;
    public int? ReviveId;

    public UserFightAttr()
    {
    }

    public UserFightAttr(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.UserFightAttr.CurHpFieldNumber:
                CurHp = blob.ReadLong();
                return true;
            case Zproto.UserFightAttr.MaxHpFieldNumber:
                MaxHp = blob.ReadLong();
                return true;
            case Zproto.UserFightAttr.OriginEnergyFieldNumber:
                OriginEnergy = blob.ReadFloat();
                return true;
            case Zproto.UserFightAttr.ResourceIdsFieldNumber:
                ResourceIds = blob.ReadList<uint>();
                return true;
            case Zproto.UserFightAttr.ResourcesFieldNumber:
                Resources = blob.ReadList<uint>();
                return true;
            case Zproto.UserFightAttr.IsDeadFieldNumber:
                IsDead = blob.ReadInt();
                return true;
            case Zproto.UserFightAttr.DeadTimeFieldNumber:
                DeadTime = blob.ReadLong();
                return true;
            case Zproto.UserFightAttr.ReviveIdFieldNumber:
                ReviveId = blob.ReadInt();
                return true;
            case Zproto.UserFightAttr.CdInfoFieldNumber:
                // 使い先は無いが、一覧は読み飛ばせないので読んで捨てる(読まないと、この項目で読み取りを打ち切る)。
                blob.ReadList<SkillCDInfo>();
                return true;
            default:
                return false;
        }
    }
}
