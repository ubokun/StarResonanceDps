// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class SkillTable
    {
        public Dictionary<string, Skill> Data = new();
    }

    public class Skill
    {
        public int Id { get; set; }
        public string NameDesign { get; set; } = null!;
        public string Desc { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int SkillLevelGroup { get; set; }
        public List<int> SkillPreloadGroup { get; set; } = null!;
        public List<int> EffectIDs { get; set; } = null!;
        public int SkillType { get; set; }
        public int SlotPassiveType { get; set; }
        public bool FaceTarget { get; set; }
        public bool IsPreview { get; set; }
        public int TargetType { get; set; }
        public int SkillTargetRangeType { get; set; }
        public int SkillRangeType { get; set; }
        public int SkillSelectPointType { get; set; }
        public int SkillHatedType { get; set; }
        public int SkillDamType { get; set; }
        public int SwitchSkillId { get; set; }
        public bool IsAoe { get; set; }
        public string Icon { get; set; } = null!;
        public int NextSkillId { get; set; }
        public int SlotType { get; set; }
        public bool LongPressOpen { get; set; }
        public float LongPressTime { get; set; }
        public float ComboTakeEffectTime { get; set; }
        public bool CanPlayInSky { get; set; }
        public int SkySkillId { get; set; }
        public float PlayInSkyHeight { get; set; }
        public bool IsArmor { get; set; }
        public bool CanBeSilence { get; set; }
        public bool CantStiff { get; set; }
        public bool CantStiffBack { get; set; }
        public bool CantStiffDown { get; set; }
        public bool CantStiffAir { get; set; }
        public int UnbreakSkillPriority { get; set; }
        public int BreakSkillPriority { get; set; }
        public bool UseTotalDamageHud { get; set; }
        public bool WeaponReturn { get; set; }
        public float SkillRootShift { get; set; }
        public int CoolTimeType { get; set; }
        public List<int> NecessaryParts { get; set; } = null!;
        public List<int> ExcludeParts { get; set; } = null!;
        public List<string> SkillAreaArray { get; set; } = null!;
        public List<List<float>> SwitchSkillInfo { get; set; } = null!;
        public int EnergyChargeTime { get; set; }
        public int MaxEnergyChargeNum { get; set; }
        public float ContinuesSkillDelayTime { get; set; }
        public int UseAddResValue { get; set; }
        public bool AtkSpeedSwitch { get; set; }
        public int SkillLookAtAngle { get; set; }
        public int DefocusLookAtangle { get; set; }
        public bool IsFractureSkill { get; set; }
        public bool IsDangerSkill { get; set; }
        public bool IsPassiveDesc { get; set; }
        public bool IsSearchEnemie { get; set; }
        public string SearchEnemieFilterName { get; set; } = null!;
        public bool ExtendedSightRange { get; set; }
        public bool DontPlaySelectTargetEffect { get; set; }
        public bool RockerDir { get; set; }
        public int SkillUnbreakLevelAdditional { get; set; }
        public bool IsHideReplaceEffect { get; set; }
        public bool ChangeWpInSkill { get; set; }
        public bool IsTanlentContinuedBegin { get; set; }
        public List<float> UIWarningParam { get; set; } = null!;
        public bool InheritMotionSpeed { get; set; }
        public List<int> SlotPositionId { get; set; } = null!;
        public List<List<int>> UnlockCondition { get; set; } = null!;
        public string SkillTalk { get; set; } = null!;
        public float SkillTalkTime { get; set; }
        public int SkillLabel { get; set; }
        public bool DeathToward { get; set; }
        public List<List<float>> SingOrGuideTime { get; set; } = null!;
        public int VehicleSkillType { get; set; }
        public bool SyncStageFlag { get; set; }
        public bool NotInterruptDashing { get; set; }
        public int PCBgColour { get; set; }
        public float CancelLockDis { get; set; }
        public bool IsInheritMoveSpeed { get; set; }
        public List<float> IndicatorParam { get; set; } = null!;
        public bool CheckGB { get; set; }
        public int SkillLogicCheck { get; set; }
        public bool IsIgnoreDel { get; set; }

        public string GetIconName()
        {
            if (Icon != null && Icon.Length > 0)
            {
                int lastSeparator = Icon.LastIndexOf('/');
                if (lastSeparator != -1)
                {
                    return Icon.Substring(lastSeparator + 1);
                }
            }

            return Icon;
        }

        private float SkillFightLevel_PVECoolTime = -1.0f;
        public float Get_SkillFightLevel_PVECoolTime()
        {
            if (SkillFightLevel_PVECoolTime != -1.0f)
            {
                return SkillFightLevel_PVECoolTime;
            }

            if (EffectIDs != null && EffectIDs.Count > 0)
            {
                if (HelperMethods.DataTables.SkillFightLevels.Data.TryGetValue(EffectIDs.First().ToString(), out var skillFightLevel))
                {
                    SkillFightLevel_PVECoolTime = skillFightLevel.PVECoolTime;
                    return skillFightLevel.PVECoolTime;
                }
                else
                {
                    return 0.0f;
                }
            }

            return 0.0f;
        }

        public bool IsImagineSlot()
        {
            if (SlotPositionId == null)
            {
                return false;
            }

            if (SlotPositionId.Contains(7) || SlotPositionId.Contains(8))
            {
                return true;
            }

            return false;
        }

        public bool IsRoleSlot()
        {
            if (SlotPositionId == null)
            {
                return false;
            }

            if (SlotPositionId.Contains(21) || SlotPositionId.Contains(22) || SlotPositionId.Contains(23) || SlotPositionId.Contains(24))
            {
                return true;
            }

            return false;
        }

        /// <summary>究極スキル(6)の枠の技か。</summary>
        public bool IsUltimateSlot()
        {
            if (SlotPositionId == null)
            {
                return false;
            }

            return SlotPositionId.Contains(6);
        }

        /// <summary>
        /// 枠の番号を持つ技か(0 以外の値がある)。通常攻撃の2段目以降や特化で置き換わった技は持たない(0 だけ)。
        /// </summary>
        public bool HasSlotPosition()
        {
            if (SlotPositionId == null)
            {
                return false;
            }

            return SlotPositionId.Exists(slotPositionId => slotPositionId != 0);
        }
    }
}
