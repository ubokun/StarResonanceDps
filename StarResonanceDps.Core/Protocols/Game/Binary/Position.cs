// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary
{
    public class Position(BlobReader blob) : BlobType(ref blob)
    {
        public float? X;
        public float? Y;
        public float? Z;
        public float? Dir;

        public override bool ParseField(int index, ref BlobReader blob)
        {
            switch (index)
            {
                case Zproto.Position.XFieldNumber:
                    X = blob.ReadFloat();
                    return true;
                case Zproto.Position.YFieldNumber:
                    Y = blob.ReadFloat();
                    return true;
                case Zproto.Position.ZFieldNumber:
                    Z = blob.ReadFloat();
                    return true;
                case Zproto.Position.DirFieldNumber:
                    Dir = blob.ReadFloat();
                    return true;
                default:
                    return false;
            }
        }
    }
}
