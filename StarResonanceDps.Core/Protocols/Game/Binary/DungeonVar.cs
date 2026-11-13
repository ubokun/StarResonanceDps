// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Zproto;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class DungeonVar : BlobType
{
    public List<DungeonVarData> Data = null!;

    public DungeonVar()
    {
    }

    public DungeonVar(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.DungeonVar.DungeonVarDataFieldNumber:
                Data = blob.ReadList<DungeonVarData>();
                return true;
            default:
                return false;
        }
    }

    public static implicit operator Zproto.DungeonVar(DungeonVar var)
    {
        var data = new Zproto.DungeonVar();
        foreach (DungeonVarData varData in var.Data)
        {
            data.DungeonVarData.Add(varData);
        }
        return data;
    }
}