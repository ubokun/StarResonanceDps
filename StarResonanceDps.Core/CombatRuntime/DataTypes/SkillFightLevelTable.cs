// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class SkillFightLevelTable
    {
        public Dictionary<string, SkillFightLevel> Data = [];
    }

    public class SkillFightLevel
    {
        public string Id { get; set; } = null!;
        public int SkillId { get; set; }
        public int Level { get; set; }
        public int SkillEffectId { get; set; }
        public string Name { get; set; } = null!;
        public List<List<int>> SkillCost { get; set; } = null!;
        public List<List<int>> SkillResCheck { get; set; } = null!;
        public float PVECoolTime { get; set; }
        public List<List<string>> FloatParameter { get; set; } = null!;
        public List<int> ShowParameter { get; set; } = null!;
        public bool ShowSkillCountCD { get; set; }
        public int FightValue { get; set; }
        public List<int> CoolTimeOnBattleStart { get; set; } = null!;
        public List<float> RandomPVECoolTime { get; set; } = null!;
        public List<List<int>> SkillConditionCheck { get; set; } = null!;
        public float AbortSingTimeCD { get; set; }
    }
}
