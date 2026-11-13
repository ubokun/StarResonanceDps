// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class EquipData : BlobType
{
    public List<EquipNine>? EquipInfos;

    public EquipData()
    {
    }

    public EquipData(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.EquipData.EquipInfosFieldNumber:
                EquipInfos = blob.ReadList<EquipNine>();
                return true;
            default:
                return false;
        }
    }
}
