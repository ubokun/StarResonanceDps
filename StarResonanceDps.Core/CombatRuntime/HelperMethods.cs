// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime.DataTypes;

namespace StarResonanceDps.Core.CombatRuntime;

public class HelperMethods
{
    public static class DataTables
    {
        public static MonsterTable Monsters = new MonsterTable();
        public static SkillTable Skills = new SkillTable();
        public static BuffTable Buffs = new BuffTable();
        public static SkillFightLevelTable SkillFightLevels = new SkillFightLevelTable();
        public static SceneEventDungeonConfigTable SceneEventDungeonConfigs = new SceneEventDungeonConfigTable();
        public static TargetTable Targets = new TargetTable();
        public static TempAttrTable TempAttrs = new TempAttrTable();
        public static ProfessionSystemTable ProfessionSystems = new ProfessionSystemTable();
        public static CookCuisineTable CookCuisines = new CookCuisineTable();
        public static FightAttrTable FightAttrs = new FightAttrTable();
        public static SkillAoyiTable SkillAoyis = new SkillAoyiTable();
        public static SkillAoyiStarTable SkillAoyiStars = new SkillAoyiStarTable();
        public static ChatStickerTable ChatStickers = new ChatStickerTable();
        public static ProfileImageTable ProfileImages = new ProfileImageTable();
        public static DamageAttrTable DamageAttrs = new DamageAttrTable();
    }
}
