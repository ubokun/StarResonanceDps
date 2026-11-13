// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>
/// 自分のコンテナ(<c>CharSerialize.buffInfo</c>)が運ぶバフ1件。
///
/// <para>
/// AOI側の <c>BuffInfo</c> と違い、自分のコンテナにしか現れない。
/// 特化マーカーバフの判定に要るのは <see cref="BaseId"/> と <see cref="BuffUuid"/> の組。
/// </para>
///
/// <para>
/// field 12/13(customParamsKey / customParams)は解析しない。
/// スカラーの繰り返しは読み飛ばせずその時点で解析が打ち切られるが、
/// 必要な値は手前の field 4 までに揃っているため実害が無い。
/// </para>
/// </summary>
public sealed class BuffDBData : BlobType
{
    public override string? DebugName => "BuffDBData";

    public long? BuffUuid;
    public long? FirerId;
    public uint? BuffConfigId;
    public uint? BaseId;
    public uint? Level;
    public uint? Layer;
    public int? Duration;
    public int? Count;
    public long? CreateTime;
    public int? PartId;
    public int? CreateSceneId;

    public BuffDBData()
    {
    }

    public BuffDBData(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.BuffDBData.BuffUuidFieldNumber:
                BuffUuid = blob.ReadLong();
                return true;
            case Zproto.BuffDBData.FirerIdFieldNumber:
                FirerId = blob.ReadLong();
                return true;
            case Zproto.BuffDBData.BuffConfigIdFieldNumber:
                BuffConfigId = blob.ReadUInt();
                return true;
            case Zproto.BuffDBData.BaseIdFieldNumber:
                BaseId = blob.ReadUInt();
                return true;
            case Zproto.BuffDBData.LevelFieldNumber:
                Level = blob.ReadUInt();
                return true;
            case Zproto.BuffDBData.LayerFieldNumber:
                Layer = blob.ReadUInt();
                return true;
            case Zproto.BuffDBData.DurationFieldNumber:
                Duration = blob.ReadInt();
                return true;
            case Zproto.BuffDBData.CountFieldNumber:
                Count = blob.ReadInt();
                return true;
            case Zproto.BuffDBData.CreateTimeFieldNumber:
                CreateTime = blob.ReadLong();
                return true;
            case Zproto.BuffDBData.PartIdFieldNumber:
                PartId = blob.ReadInt();
                return true;
            case Zproto.BuffDBData.CreateSceneIdFieldNumber:
                CreateSceneId = blob.ReadInt();
                return true;
            default:
                return false;
        }
    }
}
