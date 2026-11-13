// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class TargetTable
    {
        public Dictionary<string, TargetRow> Data = [];
    }

    public class TargetRow
    {
        public int Id { get; set; }
        public int TargetType { get; set; }
    }
}
