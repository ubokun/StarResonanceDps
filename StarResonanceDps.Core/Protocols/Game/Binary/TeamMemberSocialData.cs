// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class TeamMemberSocialData : BlobType
{
    public ProfessionData? ProfessionData;
    public EquipData? EquipData;

    public TeamMemberSocialData()
    {
    }

    public TeamMemberSocialData(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.TeamMemberSocialData.ProfessionDataFieldNumber:
                ProfessionData = new(blob);
                return true;
            case Zproto.TeamMemberSocialData.EquipDataFieldNumber:
                EquipData = new(blob);
                return true;
            default:
                return false;
        }
    }
}
