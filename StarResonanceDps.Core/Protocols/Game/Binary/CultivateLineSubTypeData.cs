// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>1つの機能の心相晶の育成データ(<c>CultivateLineSubTypeData</c>)の差分。</summary>
public class CultivateLineSubTypeData : BlobType
{
    public override string? DebugName => "CultivateLineSubTypeData";

    /// <summary>型の番号 → その型の育成データ。</summary>
    public BlobHashMapDelta<int, CultivateAreaData>? TemplateChanges;

    /// <summary>有効な型の番号の並び。届いたときは並び全体。</summary>
    public List<int>? ActiveTemplateIds;

    public CultivateLineSubTypeData()
    {
    }

    public CultivateLineSubTypeData(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.CultivateLineSubTypeData.CultivateLineDataMapFieldNumber:
                TemplateChanges = blob.ReadHashMapDelta<int, CultivateAreaData>();
                return true;
            case Zproto.CultivateLineSubTypeData.CultivateLineAreaListFieldNumber:
                ActiveTemplateIds = blob.ReadList<int>();
                return true;
            default:
                return false;
        }
    }
}
