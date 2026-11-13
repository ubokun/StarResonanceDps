// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime.Protocols
{
    public class ExtraPacketData(DateTime arrivalTime)
    {
        public DateTime ArrivalTime { get; set; } = arrivalTime;
    }
}
