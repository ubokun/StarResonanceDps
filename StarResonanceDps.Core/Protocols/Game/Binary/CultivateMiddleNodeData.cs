// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>心相晶の因子の枠1つ(<c>CultivateMiddleNodeData</c>)の差分。</summary>
public class CultivateMiddleNodeData : BlobType
{
    /// <summary>付けている因子のアイテムの uuid。中国サーバーの定義にだけある項目で、使い先は無い(読み進めるためだけに読む)。</summary>
    private const int FactorItemUuidFieldNumber = 2;

    public override string? DebugName => "CultivateMiddleNodeData";

    /// <summary>付けている因子のアイテムの番号。0 は空き。</summary>
    public int? ItemId;

    public CultivateMiddleNodeData()
    {
    }

    public CultivateMiddleNodeData(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.CultivateMiddleNodeData.ItemIdFieldNumber:
                ItemId = blob.ReadInt();
                return true;
            case FactorItemUuidFieldNumber:
                blob.ReadLong();
                return true;
            default:
                return false;
        }
    }
}
