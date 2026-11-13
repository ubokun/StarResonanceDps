// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime.Protocols;

public class NotifyId(ulong serviceId, uint methoidId)
{
    public ulong ServiceId { get; set; } = serviceId;
    public uint MethodId { get; set; } = methoidId;

    public override bool Equals(object? obj)
    {
        return obj is NotifyId other && ServiceId == other.ServiceId && MethodId == other.MethodId;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ServiceId, MethodId);
    }
}
