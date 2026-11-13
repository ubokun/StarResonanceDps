// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>1つのシーズンの心相晶の育成データ(<c>CultivateLineData</c>)の差分。</summary>
public class CultivateLineData : BlobType
{
    /// <summary>今の育成の組み合わせの番号。中国サーバーの定義にだけある項目で、使い先は無い(読み進めるためだけに読む)。</summary>
    private const int CurrentCultivateLineSchemeIdFieldNumber = 2;

    public override string? DebugName => "CultivateLineData";

    /// <summary>機能の番号(800522 潜在心相晶 / 800523 滅妄心相晶)→ その機能の育成データ。</summary>
    public BlobHashMapDelta<int, CultivateLineSubTypeData>? FunctionChanges;

    public CultivateLineData()
    {
    }

    public CultivateLineData(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.CultivateLineData.CultivateLineMapFieldNumber:
                FunctionChanges = blob.ReadHashMapDelta<int, CultivateLineSubTypeData>();
                return true;
            case CurrentCultivateLineSchemeIdFieldNumber:
                blob.ReadInt();
                return true;
            default:
                return false;
        }
    }
}
