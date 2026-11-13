// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>心相晶の育成データ(<c>CharSerialize.seasonCultivateLineData</c>)の差分。</summary>
public class SeasonCultivateLineData : BlobType
{
    public override string? DebugName => "SeasonCultivateLineData";

    /// <summary>シーズンの番号 → そのシーズンの育成データ。</summary>
    public BlobHashMapDelta<int, CultivateLineData>? SeasonChanges;

    public SeasonCultivateLineData()
    {
    }

    public SeasonCultivateLineData(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.SeasonCultivateLineData.SeasonCultivateLineMapFieldNumber:
                SeasonChanges = blob.ReadHashMapDelta<int, CultivateLineData>();
                return true;
            default:
                return false;
        }
    }
}
