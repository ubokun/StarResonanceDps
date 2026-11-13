// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime
{
    public class EntityBlobTable
    {
        public ulong EncounterId { get; set; }
        public byte[] Data { get; set; } = null!;
    }
}
