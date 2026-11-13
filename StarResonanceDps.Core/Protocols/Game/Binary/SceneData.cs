// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary
{
    public class SceneData(BlobReader blob) : BlobType(ref blob)
    {
        public uint? MapId;
        public uint? ChannelId;
        public Position? Pos;
        public long? LevelUuid;
        public Position? LevelPos;
        public uint? LevelMapId;
        public uint? LevelReviveId;
        public Dictionary<uint, uint>? RecordId;
        public uint? PlaneId;
        public bool? CanSwitchLayer;
        public Position? BeforeFallPos;
        public string SceneGUID = null!;
        public string DungeonGUID = null!;
        public uint? LineId;
        public uint? VisualLayerConfigId;
        public int? SceneAreaId;
        public int? LevelAreaId;
        public int? BeforeFallSceneAreaId;

        public override bool ParseField(int index, ref BlobReader blob)
        {
            switch (index)
            {
                case Zproto.SceneData.MapIdFieldNumber:
                    MapId = blob.ReadUInt();
                    return true;
                case Zproto.SceneData.ChannelIdFieldNumber:
                    ChannelId = blob.ReadUInt();
                    return true;
                case Zproto.SceneData.PosFieldNumber:
                    Pos = new(blob);
                    return true;
                case Zproto.SceneData.LevelUuidFieldNumber:
                    LevelUuid = blob.ReadLong();
                    return true;
                case Zproto.SceneData.LevelPosFieldNumber:
                    LevelPos = new(blob);
                    return true;
                case Zproto.SceneData.LevelMapIdFieldNumber:
                    LevelMapId = blob.ReadUInt();
                    return true;
                case Zproto.SceneData.LevelReviveIdFieldNumber:
                    LevelReviveId = blob.ReadUInt();
                    return true;
                case Zproto.SceneData.RecordIdFieldNumber:
                    RecordId = blob.ReadHashMap<uint, uint>();
                    return true;
                case Zproto.SceneData.PlaneIdFieldNumber:
                    PlaneId = blob.ReadUInt();
                    return true;
                case Zproto.SceneData.CanSwitchLayerFieldNumber:
                    return false;
                case Zproto.SceneData.BeforeFallPosFieldNumber:
                    BeforeFallPos = new(blob);
                    return true;
                case Zproto.SceneData.LineIdFieldNumber:
                    LineId = blob.ReadUInt();
                    return true;
                case Zproto.SceneData.VisualLayerConfigIdFieldNumber:
                    VisualLayerConfigId = blob.ReadUInt();
                    return true;
                case Zproto.SceneData.SceneAreaIdFieldNumber:
                    SceneAreaId = blob.ReadInt();
                    return true;
                case Zproto.SceneData.LevelAreaIdFieldNumber:
                    LevelAreaId = blob.ReadInt();
                    return true;
                case Zproto.SceneData.BeforeFallSceneAreaIdFieldNumber:
                    BeforeFallSceneAreaId = blob.ReadInt();
                    return true;
                default:
                    return false;
            }
        }
    }
}
