// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class EquipNine : BlobType
{
    public int? Slot;
    public int? EquipId;

    public EquipNine()
    {
    }

    public EquipNine(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.EquipNine.SlotFieldNumber:
                Slot = blob.ReadInt();
                return true;
            case Zproto.EquipNine.EquipIDFieldNumber:
                EquipId = blob.ReadInt();
                return true;
            default:
                return false;
        }
    }
}
