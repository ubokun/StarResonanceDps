// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime
{
    public class Battle
    {
        public int BattleId { get; set; }
        public uint SceneId { get; set; }
        public string SceneName { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        private int DungeonDifficulty { get; set; } = -1;

        public int GetDungeonDifficulty()
        {
            if (DungeonDifficulty == -1)
            {
                var exData = DB.GetEncounterExDataForBattle(BattleId);
                if (exData != null)
                {
                    DungeonDifficulty = exData.DungeonDifficulty;
                }
            }

            return DungeonDifficulty;
        }
    }
}
