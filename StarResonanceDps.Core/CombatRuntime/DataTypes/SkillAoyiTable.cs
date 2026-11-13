// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    /// <summary>イマジンの表(<c>Data/Raw/SkillAoyiTable.json</c>)。行の番号がイマジンの技ID。</summary>
    public class SkillAoyiTable
    {
        public Dictionary<string, SkillAoyi> Data = [];
    }

    public class SkillAoyi
    {
        /// <summary>イマジンの技ID。</summary>
        public int Id { get; set; }

        /// <summary>付ける効果。<c>[3, バフID, 1]</c> はイマジンのパッシブの根のバフ。</summary>
        public List<List<int>> TransformationType { get; set; } = [];
    }

    /// <summary>イマジンの改造段階の表(<c>Data/Raw/SkillAoyiStarTable.json</c>)。</summary>
    public class SkillAoyiStarTable
    {
        public Dictionary<string, SkillAoyiStar> Data = [];
    }

    public class SkillAoyiStar
    {
        public int Id { get; set; }

        /// <summary>その段階のイマジンの技ID。</summary>
        public int SkillId { get; set; }

        /// <summary>付ける効果。<c>[3, バフID, 1]</c> はイマジンのパッシブの根のバフ。</summary>
        public List<List<int>> TransformationType { get; set; } = [];
    }
}
