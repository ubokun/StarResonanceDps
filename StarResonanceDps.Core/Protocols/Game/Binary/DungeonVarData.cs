// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Text;
using Zproto;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class DungeonVarData : BlobType
{
    public string Name = "";
    public int Value;

    public DungeonVarData()
    {
    }

    public DungeonVarData(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.DungeonVarData.NameFieldNumber:
                Name = blob.ReadString();

                return true;
            case Zproto.DungeonVarData.ValueFieldNumber:

                Value = blob.ReadInt();

                return true;
            default:
                return false;
        }
    }

    public static implicit operator Zproto.DungeonVarData(DungeonVarData varData)
    {
        var data = new Zproto.DungeonVarData()
        {
            Name = varData.Name,
            Value = varData.Value
        };
        return data;
    }
}
