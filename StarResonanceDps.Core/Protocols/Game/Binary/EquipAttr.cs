// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>
/// 装備の値。区分ごとの「属性庫の行ID → r」の map と、突破の段階など。更新では変わった項目だけが届く。
/// 使わない項目は読み進めるためだけに読む。
/// </summary>
public class EquipAttr : BlobType
{
    public override string? DebugName => "EquipAttr";

    public int? PerfectionValue;
    public int? BreakThroughTime;
    public BlobHashMapDelta<int, int>? BasicAttrChanges;
    public BlobHashMapDelta<int, int>? AdvanceAttrChanges;
    public BlobHashMapDelta<int, int>? RecastAttrChanges;
    public BlobHashMapDelta<int, int>? RareQualityAttrChanges;

    /// <summary>特化ごとの行(型2 の属性庫)。サーバーが今の特化の行に書き換える。</summary>
    public EquipAttrSet? EquipAttrSet;

    public EquipAttr()
    {
    }

    public EquipAttr(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.EquipAttr.BaseAttrsFieldNumber:
                blob.ReadHashMapDelta<uint, uint>();
                return true;
            case Zproto.EquipAttr.PerfectionValueFieldNumber:
                PerfectionValue = blob.ReadInt();
                return true;
            case Zproto.EquipAttr.RecastCountFieldNumber:
            case Zproto.EquipAttr.TotalRecastCountFieldNumber:
            case Zproto.EquipAttr.PerfectionLevelFieldNumber:
            case Zproto.EquipAttr.MaxPerfectionValueFieldNumber:
                blob.ReadInt();
                return true;
            case Zproto.EquipAttr.BasicAttrFieldNumber:
                BasicAttrChanges = blob.ReadHashMapDelta<int, int>();
                return true;
            case Zproto.EquipAttr.AdvanceAttrFieldNumber:
                AdvanceAttrChanges = blob.ReadHashMapDelta<int, int>();
                return true;
            case Zproto.EquipAttr.RecastAttrFieldNumber:
                RecastAttrChanges = blob.ReadHashMapDelta<int, int>();
                return true;
            case Zproto.EquipAttr.RareQualityAttrFieldNumber:
                RareQualityAttrChanges = blob.ReadHashMapDelta<int, int>();
                return true;
            case Zproto.EquipAttr.EquipAttrSetFieldNumber:
                EquipAttrSet = new(blob);
                return true;
            case Zproto.EquipAttr.BreakThroughTimeFieldNumber:
                BreakThroughTime = blob.ReadInt();
                return true;
            case Zproto.EquipAttr.CustomTransformAttrFieldNumber:
                blob.ReadHashMapDelta<int, SkippedBlob>();
                return true;
            default:
                return false;
        }
    }
}
