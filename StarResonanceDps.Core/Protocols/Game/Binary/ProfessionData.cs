// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class ProfessionData : BlobType
{
    public int? ProfessionId;
    public int? WeaponSkin;

    public ProfessionData()
    {
    }

    public ProfessionData(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.ProfessionData.ProfessionIdFieldNumber:
                ProfessionId = blob.ReadInt();
                return true;
            case Zproto.ProfessionData.WeaponSkinFieldNumber:
                WeaponSkin = blob.ReadInt();
                return true;
            default:
                return false;
        }
    }
}
