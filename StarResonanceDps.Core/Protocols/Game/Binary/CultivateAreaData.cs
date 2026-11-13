// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>1つの型の心相晶の育成データ(<c>CultivateAreaData</c>)の差分。届いた項目だけが値を持つ。</summary>
public class CultivateAreaData : BlobType
{
    public override string? DebugName => "CultivateAreaData";

    /// <summary>木の節(ノード)の番号 → ノードの段階。</summary>
    public BlobHashMapDelta<int, CultivateNormalNodeData>? NormalNodeChanges;

    /// <summary>木の節(因子の枠)の番号 → 付けている因子。</summary>
    public BlobHashMapDelta<int, CultivateMiddleNodeData>? MiddleNodeChanges;

    /// <summary>絆スロットの番号 → 入れているもの。</summary>
    public BlobHashMapDelta<int, CultivateBigNodeData>? BigNodeChanges;

    public int? ActivateEffectScore;

    public bool? IsActive;

    public CultivateAreaData()
    {
    }

    public CultivateAreaData(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.CultivateAreaData.CultivateNormalNodeMapFieldNumber:
                NormalNodeChanges = blob.ReadHashMapDelta<int, CultivateNormalNodeData>();
                return true;
            case Zproto.CultivateAreaData.CultivateMiddleNodeMapFieldNumber:
                MiddleNodeChanges = blob.ReadHashMapDelta<int, CultivateMiddleNodeData>();
                return true;
            case Zproto.CultivateAreaData.CultivateBigNodeMapFieldNumber:
                BigNodeChanges = blob.ReadHashMapDelta<int, CultivateBigNodeData>();
                return true;
            case Zproto.CultivateAreaData.ActivateEffectScoreFieldNumber:
                ActivateEffectScore = blob.ReadInt();
                return true;
            case Zproto.CultivateAreaData.IsActiveFieldNumber:
                IsActive = blob.ReadByte() != 0;
                return true;
            default:
                return false;
        }
    }
}
