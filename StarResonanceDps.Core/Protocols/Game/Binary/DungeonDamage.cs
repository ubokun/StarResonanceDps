// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class DungeonDamage : BlobType
{
    public Dictionary<long, long>? Damages;

    public DungeonDamage()
    {
    }

    public DungeonDamage(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.DungeonDamage.DamagesFieldNumber:
                Damages = blob.ReadHashMap<long, long>();
                return true;
            default:
                return false;
        }
    }
}